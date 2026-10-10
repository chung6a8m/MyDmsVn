CREATE TABLE dbo.GoodsReceipts
(
    ReceiptId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_GoodsReceipts PRIMARY KEY,
    ReceiptNo nvarchar(32) COLLATE Latin1_General_100_CI_AI NOT NULL,
    ReceiptDate date NOT NULL,
    WarehouseId int NOT NULL,
    EmployeeId int NOT NULL,
    Status varchar(16) NOT NULL
        CONSTRAINT DF_GoodsReceipts_Status DEFAULT ('Draft'),
    Note nvarchar(1000) NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_GoodsReceipts_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    PostedAtUtc datetime2(7) NULL,
    PostedByUserId int NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_GoodsReceipts_ReceiptNo_NotBlank CHECK
        (LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            ReceiptNo, N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(11), N''),
            NCHAR(12), N''), NCHAR(13), N''), NCHAR(160), N'')) > 0),
    CONSTRAINT CK_GoodsReceipts_Status CHECK (Status IN ('Draft', 'Posted')),
    CONSTRAINT CK_GoodsReceipts_PostingAudit CHECK
        ((Status = 'Draft' AND PostedAtUtc IS NULL AND PostedByUserId IS NULL) OR
         (Status = 'Posted' AND PostedAtUtc IS NOT NULL AND PostedByUserId IS NOT NULL)),
    CONSTRAINT FK_GoodsReceipts_Warehouses FOREIGN KEY (WarehouseId)
        REFERENCES dbo.Warehouses (WarehouseId),
    CONSTRAINT FK_GoodsReceipts_Employees FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees (EmployeeId),
    CONSTRAINT FK_GoodsReceipts_CreatedByUsers FOREIGN KEY (CreatedByUserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_GoodsReceipts_UpdatedByUsers FOREIGN KEY (UpdatedByUserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_GoodsReceipts_PostedByUsers FOREIGN KEY (PostedByUserId)
        REFERENCES dbo.Users (UserId)
);

CREATE UNIQUE INDEX UX_GoodsReceipts_ReceiptNo
    ON dbo.GoodsReceipts (ReceiptNo);

CREATE INDEX IX_GoodsReceipts_ReceiptDate_ReceiptId
    ON dbo.GoodsReceipts (ReceiptDate, ReceiptId);

CREATE INDEX IX_GoodsReceipts_WarehouseId_ReceiptDate
    ON dbo.GoodsReceipts (WarehouseId, ReceiptDate);

CREATE INDEX IX_GoodsReceipts_EmployeeId
    ON dbo.GoodsReceipts (EmployeeId);

CREATE TABLE dbo.GoodsReceiptLines
(
    ReceiptLineId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_GoodsReceiptLines PRIMARY KEY,
    ReceiptId bigint NOT NULL,
    [LineNo] int NOT NULL,
    ProductId int NOT NULL,
    Quantity decimal(18, 4) NOT NULL,
    UnitCost decimal(19, 4) NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_GoodsReceiptLines_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    CONSTRAINT CK_GoodsReceiptLines_LineNo CHECK ([LineNo] > 0),
    CONSTRAINT CK_GoodsReceiptLines_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_GoodsReceiptLines_UnitCost CHECK (UnitCost >= 0),
    CONSTRAINT FK_GoodsReceiptLines_GoodsReceipts FOREIGN KEY (ReceiptId)
        REFERENCES dbo.GoodsReceipts (ReceiptId),
    CONSTRAINT FK_GoodsReceiptLines_Products FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId),
    CONSTRAINT FK_GoodsReceiptLines_CreatedByUsers FOREIGN KEY (CreatedByUserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_GoodsReceiptLines_UpdatedByUsers FOREIGN KEY (UpdatedByUserId)
        REFERENCES dbo.Users (UserId)
);

CREATE UNIQUE INDEX UX_GoodsReceiptLines_ReceiptId_LineNo
    ON dbo.GoodsReceiptLines (ReceiptId, [LineNo]);

CREATE UNIQUE INDEX UX_GoodsReceiptLines_ReceiptId_ProductId
    ON dbo.GoodsReceiptLines (ReceiptId, ProductId);

CREATE INDEX IX_GoodsReceiptLines_ProductId
    ON dbo.GoodsReceiptLines (ProductId);
