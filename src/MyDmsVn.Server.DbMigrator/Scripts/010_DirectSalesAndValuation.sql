-- DbUp owns the transaction, including the journal entry. Never run outside maintenance.
-- Existing schema9 data is proved in both directions before any valuation is assigned.
DECLARE @SourceRows bigint;
SELECT @SourceRows = COUNT_BIG(*) FROM dbo.StockLedger WITH (TABLOCKX, HOLDLOCK);
SELECT @SourceRows = COUNT_BIG(*) FROM dbo.StockBalances WITH (TABLOCKX, HOLDLOCK);
SELECT @SourceRows = COUNT_BIG(*) FROM dbo.GoodsReceipts WITH (TABLOCKX, HOLDLOCK);
SELECT @SourceRows = COUNT_BIG(*) FROM dbo.GoodsReceiptLines WITH (TABLOCKX, HOLDLOCK);
IF ISNULL(TRY_CONVERT(int,SESSION_CONTEXT(N'MyDmsVn.FreshInstall')),0) <> 1 AND
    CONVERT(nvarchar(60), DATABASEPROPERTYEX(DB_NAME(), 'UserAccess')) <> N'RESTRICTED_USER'
    THROW 51010, 'Valuation bootstrap requires restricted maintenance access.', 1;
-- Diagnostics contain bounded numeric counts/identities only, never source text.
DECLARE @InvalidCount bigint, @SampleId bigint, @Diagnostic nvarchar(300),
    @SampleWarehouse int, @SampleProduct int;
SELECT e.LedgerId INTO #InvalidCorrespondence
FROM dbo.StockLedger AS e
LEFT JOIN dbo.GoodsReceiptLines AS l ON l.ReceiptLineId=e.DocumentLineId
LEFT JOIN dbo.GoodsReceipts AS h ON h.ReceiptId=l.ReceiptId
WHERE e.DocumentType <> 'GoodsReceipt' OR DATALENGTH(e.DocumentType) <> 12 OR e.QuantityChange <= 0
   OR l.ReceiptLineId IS NULL OR h.ReceiptId IS NULL OR h.Status <> 'Posted'
   OR e.DocumentId <> h.ReceiptId OR e.WarehouseId <> h.WarehouseId OR e.ProductId <> l.ProductId
   OR e.QuantityChange <> l.Quantity OR e.PostedAtUtc <> h.PostedAtUtc
   OR e.PostedByUserId <> h.PostedByUserId OR h.PostedAtUtc IS NULL OR h.PostedByUserId IS NULL
   OR l.Quantity <= 0 OR l.UnitCost < 0;
SELECT @InvalidCount=COUNT_BIG(*),@SampleId=MIN(LedgerId) FROM #InvalidCorrespondence;
IF @InvalidCount > 0
BEGIN
    SET @Diagnostic=CONCAT(N'Historical ledger/source correspondence is invalid; Count=',@InvalidCount,N'; LedgerId=',@SampleId,N'.');
    THROW 51011, @Diagnostic, 1;
END;
-- Existing GoodsReceiptValidation.MaximumLinesPerReceipt is 200, independently of Sales.
SELECT h.ReceiptId INTO #InvalidLineCount FROM dbo.GoodsReceipts AS h WHERE h.Status='Posted' AND
    (SELECT COUNT_BIG(*) FROM dbo.GoodsReceiptLines AS l WHERE l.ReceiptId=h.ReceiptId) NOT BETWEEN 1 AND 200;
SELECT @InvalidCount=COUNT_BIG(*),@SampleId=MIN(ReceiptId) FROM #InvalidLineCount;
IF @InvalidCount > 0
BEGIN
    SET @Diagnostic=CONCAT(N'Posted receipt line count is invalid; Count=',@InvalidCount,N'; ReceiptId=',@SampleId,N'.');
    THROW 51012, @Diagnostic, 1;
END;
SELECT l.ReceiptLineId INTO #InvalidCounterpart
FROM dbo.GoodsReceiptLines AS l JOIN dbo.GoodsReceipts AS h ON h.ReceiptId=l.ReceiptId
WHERE h.Status='Posted' AND (SELECT COUNT_BIG(*) FROM dbo.StockLedger AS e
    WHERE e.DocumentType='GoodsReceipt' AND DATALENGTH(e.DocumentType)=12 AND e.DocumentLineId=l.ReceiptLineId
    AND e.DocumentId=h.ReceiptId) <> 1;
SELECT @InvalidCount=COUNT_BIG(*),@SampleId=MIN(ReceiptLineId) FROM #InvalidCounterpart;
IF @InvalidCount > 0
BEGIN
    SET @Diagnostic=CONCAT(N'Posted source has no unique ledger counterpart; Count=',@InvalidCount,N'; ReceiptLineId=',@SampleId,N'.');
    THROW 51013, @Diagnostic, 1;
END;
-- decimal(18,4) quantities and at most bigint rows fit this widened sum.
SELECT WarehouseId,ProductId,SUM(CONVERT(decimal(38,4),QuantityChange)) AS Quantity
INTO #QuantityProof FROM dbo.StockLedger GROUP BY WarehouseId,ProductId;
SELECT COALESCE(p.WarehouseId,b.WarehouseId) AS WarehouseId,COALESCE(p.ProductId,b.ProductId) AS ProductId
INTO #InvalidBalance FROM #QuantityProof AS p FULL JOIN dbo.StockBalances AS b
    ON b.WarehouseId=p.WarehouseId AND b.ProductId=p.ProductId
WHERE p.WarehouseId IS NULL OR b.WarehouseId IS NULL OR b.Quantity < 0 OR p.Quantity <> b.Quantity;
SELECT @InvalidCount=COUNT_BIG(*) FROM #InvalidBalance;
IF @InvalidCount > 0
BEGIN
    SELECT TOP(1) @SampleWarehouse=WarehouseId,@SampleProduct=ProductId FROM #InvalidBalance ORDER BY WarehouseId,ProductId;
    SET @Diagnostic=CONCAT(N'Historical balance keys or quantities disagree; Count=',@InvalidCount,N'; WarehouseId=',@SampleWarehouse,N'; ProductId=',@SampleProduct,N'.');
    THROW 51014, @Diagnostic, 1;
END;
-- The exact product fits decimal(38,8). TRY_CONVERT detects only the target range;
-- an unrepresentable result remains a fatal refusal, never a substituted value.
SELECT e.LedgerId,e.WarehouseId,e.ProductId,TRY_CONVERT(decimal(28,4),ROUND(l.Quantity*l.UnitCost,4)) AS ValueChange
INTO #LineValues FROM dbo.StockLedger AS e JOIN dbo.GoodsReceiptLines AS l ON l.ReceiptLineId=e.DocumentLineId;
SELECT @InvalidCount=COUNT_BIG(*),@SampleId=MIN(LedgerId) FROM #LineValues WHERE ValueChange IS NULL;
IF @InvalidCount > 0
BEGIN
    SET @Diagnostic=CONCAT(N'Historical line amount overflow; Count=',@InvalidCount,N'; LedgerId=',@SampleId,N'.');
    THROW 51015, @Diagnostic, 1;
END;
CREATE CLUSTERED INDEX IX_LineValues_StockKey ON #LineValues(WarehouseId,ProductId,LedgerId);
CREATE TABLE #ValueProof(WarehouseId int NOT NULL,ProductId int NOT NULL,InventoryValue decimal(28,4) NULL);
-- Maintenance-only key iteration identifies the exact key even if the widened SUM
-- itself overflows. All values are nonnegative; no arithmetic is clamped or rounded here.
DECLARE @InventoryValue decimal(28,4);
DECLARE ValueKeys CURSOR LOCAL FAST_FORWARD FOR SELECT WarehouseId,ProductId FROM #QuantityProof ORDER BY WarehouseId,ProductId;
OPEN ValueKeys;
FETCH NEXT FROM ValueKeys INTO @SampleWarehouse,@SampleProduct;
WHILE @@FETCH_STATUS=0
BEGIN
    BEGIN TRY
        SELECT @InventoryValue=TRY_CONVERT(decimal(28,4),SUM(CONVERT(decimal(38,4),ValueChange)))
        FROM #LineValues WHERE WarehouseId=@SampleWarehouse AND ProductId=@SampleProduct;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 8115 THROW;
        CLOSE ValueKeys;
        DEALLOCATE ValueKeys;
        SET @Diagnostic=CONCAT(N'Historical aggregate value overflow; Count>=1; WarehouseId=',@SampleWarehouse,N'; ProductId=',@SampleProduct,N'.');
        THROW 51016, @Diagnostic, 1;
    END CATCH;
    INSERT #ValueProof(WarehouseId,ProductId,InventoryValue) VALUES(@SampleWarehouse,@SampleProduct,@InventoryValue);
    FETCH NEXT FROM ValueKeys INTO @SampleWarehouse,@SampleProduct;
END;
CLOSE ValueKeys;
DEALLOCATE ValueKeys;
SELECT @InvalidCount=COUNT_BIG(*) FROM #ValueProof WHERE InventoryValue IS NULL;
IF @InvalidCount > 0
BEGIN
    SELECT TOP(1) @SampleWarehouse=WarehouseId,@SampleProduct=ProductId FROM #ValueProof WHERE InventoryValue IS NULL ORDER BY WarehouseId,ProductId;
    SET @Diagnostic=CONCAT(N'Historical aggregate value overflow; Count=',@InvalidCount,N'; WarehouseId=',@SampleWarehouse,N'; ProductId=',@SampleProduct,N'.');
    THROW 51016, @Diagnostic, 1;
END;
CREATE TABLE dbo.StockValuationEntries
(
    LedgerId bigint NOT NULL CONSTRAINT PK_StockValuationEntries PRIMARY KEY,
    ValueChange decimal(28,4) NOT NULL,
    CONSTRAINT FK_StockValuationEntries_StockLedger FOREIGN KEY (LedgerId) REFERENCES dbo.StockLedger(LedgerId)
);
INSERT dbo.StockValuationEntries(LedgerId,ValueChange) SELECT LedgerId,ValueChange FROM #LineValues;
ALTER TABLE dbo.StockBalances ADD InventoryValue decimal(28,4) NULL;
GO
UPDATE b SET InventoryValue=p.InventoryValue
FROM dbo.StockBalances AS b JOIN #ValueProof AS p ON p.WarehouseId=b.WarehouseId AND p.ProductId=b.ProductId;
ALTER TABLE dbo.StockBalances ALTER COLUMN InventoryValue decimal(28,4) NOT NULL;
GO
ALTER TABLE dbo.StockBalances ADD CONSTRAINT CK_StockBalances_Quantity CHECK (Quantity >= 0),
    CONSTRAINT CK_StockBalances_InventoryValue CHECK (InventoryValue >= 0),
    CONSTRAINT CK_StockBalances_EmptyValue CHECK (Quantity <> 0 OR InventoryValue = 0);
EXEC(N'CREATE TRIGGER dbo.TR_StockValuationEntries_AppendOnly
ON dbo.StockValuationEntries
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, ''StockValuationEntries is append-only.'', 1;
END;');
CREATE TABLE dbo.SalesReceipts
(
    SaleId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_SalesReceipts PRIMARY KEY,
    SaleNo nvarchar(32) COLLATE Latin1_General_100_CI_AI NOT NULL,
    SaleDate date NOT NULL,
    WarehouseId int NOT NULL,
    EmployeeId int NOT NULL,
    CustomerId int NOT NULL,
    Status varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL
        CONSTRAINT DF_SalesReceipts_Status DEFAULT ('Draft'),
    Note nvarchar(1000) NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_SalesReceipts_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    PostedAtUtc datetime2(7) NULL,
    PostedByUserId int NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_SalesReceipts_SaleNo_NotBlank CHECK
        (LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            SaleNo, N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(11), N''),
            NCHAR(12), N''), NCHAR(13), N''), NCHAR(160), N'')) > 0),
    CONSTRAINT CK_SalesReceipts_SaleNo_NoNul CHECK
        (dbo.CatalogTextContainsNul(SaleNo) = 0),
    CONSTRAINT CK_SalesReceipts_Status CHECK
        ((Status = 'Draft' AND DATALENGTH(Status) = 5) OR
         (Status = 'Posted' AND DATALENGTH(Status) = 6)),
    CONSTRAINT CK_SalesReceipts_PostingAudit CHECK
        ((Status = 'Draft' AND PostedAtUtc IS NULL AND PostedByUserId IS NULL) OR
         (Status = 'Posted' AND PostedAtUtc IS NOT NULL AND PostedByUserId IS NOT NULL)),
    CONSTRAINT FK_SalesReceipts_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
    CONSTRAINT FK_SalesReceipts_Warehouses FOREIGN KEY (WarehouseId)
        REFERENCES dbo.Warehouses (WarehouseId),
    CONSTRAINT FK_SalesReceipts_Employees FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees (EmployeeId),
    CONSTRAINT FK_SalesReceipts_CreatedByUsers FOREIGN KEY (CreatedByUserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_SalesReceipts_UpdatedByUsers FOREIGN KEY (UpdatedByUserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_SalesReceipts_PostedByUsers FOREIGN KEY (PostedByUserId)
        REFERENCES dbo.Users (UserId)
);

CREATE UNIQUE INDEX UX_SalesReceipts_SaleNo
    ON dbo.SalesReceipts (SaleNo);

CREATE INDEX IX_SalesReceipts_SaleDate_SaleId
    ON dbo.SalesReceipts (SaleDate, SaleId);

CREATE INDEX IX_SalesReceipts_WarehouseId_SaleDate
    ON dbo.SalesReceipts (WarehouseId, SaleDate);

CREATE INDEX IX_SalesReceipts_EmployeeId
    ON dbo.SalesReceipts (EmployeeId);

CREATE TABLE dbo.SalesReceiptLines
(
    SaleLineId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_SalesReceiptLines PRIMARY KEY,
    SaleId bigint NOT NULL,
    [LineNo] int NOT NULL,
    ProductId int NOT NULL,
    Qty decimal(18, 4) NOT NULL,
    Unit nvarchar(32) NOT NULL,
    IsGift bit NOT NULL,
    LineAmount decimal(28,4) NOT NULL,
    PostedUnitCost decimal(28,8) NULL,
    CostAmount decimal(28,4) NULL,
    UnitPrice decimal(19, 4) NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_SalesReceiptLines_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    CONSTRAINT CK_SalesReceiptLines_Unit CHECK (LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
        Unit, N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(11), N''),
        NCHAR(12), N''), NCHAR(13), N''), NCHAR(160), N'')) > 0 AND dbo.CatalogTextContainsNul(Unit)=0),
    CONSTRAINT CK_SalesReceiptLines_LineAmount CHECK (LineAmount >= 0 AND (IsGift=0 OR LineAmount=0)),
    CONSTRAINT CK_SalesReceiptLines_Cost CHECK ((PostedUnitCost IS NULL AND CostAmount IS NULL) OR (PostedUnitCost IS NOT NULL AND CostAmount IS NOT NULL AND PostedUnitCost >= 0 AND CostAmount >= 0)),
    CONSTRAINT CK_SalesReceiptLines_LineNo CHECK ([LineNo] > 0),
    CONSTRAINT CK_SalesReceiptLines_Quantity CHECK (Qty > 0),
    CONSTRAINT CK_SalesReceiptLines_UnitPrice CHECK (UnitPrice >= 0),
    CONSTRAINT FK_SalesReceiptLines_SalesReceipts FOREIGN KEY (SaleId)
        REFERENCES dbo.SalesReceipts (SaleId),
    CONSTRAINT FK_SalesReceiptLines_Products FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId),
    CONSTRAINT FK_SalesReceiptLines_CreatedByUsers FOREIGN KEY (CreatedByUserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_SalesReceiptLines_UpdatedByUsers FOREIGN KEY (UpdatedByUserId)
        REFERENCES dbo.Users (UserId)
);

CREATE UNIQUE INDEX UX_SalesReceiptLines_SaleId_LineNo
    ON dbo.SalesReceiptLines (SaleId, [LineNo]);

CREATE INDEX IX_SalesReceiptLines_ProductId_SaleId
    ON dbo.SalesReceiptLines (ProductId, SaleId);

CREATE SEQUENCE dbo.SalesReceiptNumberSequence AS bigint START WITH 1 INCREMENT BY 1 MINVALUE 1 MAXVALUE 9999999999 NO CYCLE;
