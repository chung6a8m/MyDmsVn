CREATE TABLE dbo.StockLedger
(
    LedgerId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_StockLedger PRIMARY KEY,
    WarehouseId int NOT NULL,
    ProductId int NOT NULL,
    DocumentType varchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    DocumentId bigint NOT NULL,
    DocumentLineId bigint NOT NULL,
    QuantityChange decimal(18, 4) NOT NULL,
    PostedAtUtc datetime2(7) NOT NULL,
    PostedByUserId int NOT NULL,
    CONSTRAINT CK_StockLedger_DocumentType_NotBlank CHECK
        (LEN(LTRIM(RTRIM(DocumentType))) > 0),
    CONSTRAINT CK_StockLedger_DocumentType_NoNul CHECK
        (CHARINDEX(CHAR(0), DocumentType COLLATE Latin1_General_100_BIN2) = 0),
    CONSTRAINT CK_StockLedger_QuantityChange_NonZero CHECK
        (QuantityChange <> 0),
    CONSTRAINT FK_StockLedger_Warehouses FOREIGN KEY (WarehouseId)
        REFERENCES dbo.Warehouses (WarehouseId),
    CONSTRAINT FK_StockLedger_Products FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId),
    CONSTRAINT FK_StockLedger_PostedByUsers FOREIGN KEY (PostedByUserId)
        REFERENCES dbo.Users (UserId)
);

CREATE UNIQUE INDEX UX_StockLedger_DocumentType_DocumentLineId
    ON dbo.StockLedger (DocumentType, DocumentLineId);

CREATE INDEX IX_StockLedger_WarehouseId_ProductId_PostedAtUtc_LedgerId
    ON dbo.StockLedger (WarehouseId, ProductId, PostedAtUtc, LedgerId)
    INCLUDE (DocumentType, DocumentId, DocumentLineId, QuantityChange, PostedByUserId);

CREATE INDEX IX_StockLedger_ProductId
    ON dbo.StockLedger (ProductId);

CREATE TABLE dbo.StockBalances
(
    WarehouseId int NOT NULL,
    ProductId int NOT NULL,
    Quantity decimal(18, 4) NOT NULL
        CONSTRAINT DF_StockBalances_Quantity DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_StockBalances PRIMARY KEY (WarehouseId, ProductId),
    CONSTRAINT FK_StockBalances_Warehouses FOREIGN KEY (WarehouseId)
        REFERENCES dbo.Warehouses (WarehouseId),
    CONSTRAINT FK_StockBalances_Products FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId)
);

CREATE INDEX IX_StockBalances_ProductId
    ON dbo.StockBalances (ProductId);

EXEC(N'CREATE TRIGGER dbo.TR_StockLedger_AppendOnly
ON dbo.StockLedger
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, ''StockLedger is append-only.'', 1;
END;');
