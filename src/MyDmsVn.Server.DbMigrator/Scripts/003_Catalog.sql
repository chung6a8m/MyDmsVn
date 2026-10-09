CREATE TABLE dbo.Products
(
    ProductId int IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_Products PRIMARY KEY,
    Code nvarchar(32) COLLATE Latin1_General_100_CI_AI NOT NULL,
    Name nvarchar(256) NOT NULL,
    Unit nvarchar(32) NOT NULL,
    IsActive bit NOT NULL
        CONSTRAINT DF_Products_IsActive DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_Products_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    CONSTRAINT CK_Products_Code_NotBlank CHECK (LEN(LTRIM(RTRIM(Code))) > 0),
    CONSTRAINT CK_Products_Name_NotBlank CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
    CONSTRAINT CK_Products_Unit_NotBlank CHECK (LEN(LTRIM(RTRIM(Unit))) > 0)
);

CREATE UNIQUE INDEX UX_Products_Code
    ON dbo.Products (Code);

CREATE TABLE dbo.Warehouses
(
    WarehouseId int IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_Warehouses PRIMARY KEY,
    Code nvarchar(32) COLLATE Latin1_General_100_CI_AI NOT NULL,
    Name nvarchar(256) NOT NULL,
    Address nvarchar(500) NULL,
    IsActive bit NOT NULL
        CONSTRAINT DF_Warehouses_IsActive DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_Warehouses_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    CONSTRAINT CK_Warehouses_Code_NotBlank CHECK (LEN(LTRIM(RTRIM(Code))) > 0),
    CONSTRAINT CK_Warehouses_Name_NotBlank CHECK (LEN(LTRIM(RTRIM(Name))) > 0)
);

CREATE UNIQUE INDEX UX_Warehouses_Code
    ON dbo.Warehouses (Code);

CREATE TABLE dbo.Employees
(
    EmployeeId int IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_Employees PRIMARY KEY,
    Code nvarchar(32) COLLATE Latin1_General_100_CI_AI NOT NULL,
    Name nvarchar(256) NOT NULL,
    Phone nvarchar(64) NULL,
    UserId int NULL,
    IsActive bit NOT NULL
        CONSTRAINT DF_Employees_IsActive DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_Employees_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    CONSTRAINT CK_Employees_Code_NotBlank CHECK (LEN(LTRIM(RTRIM(Code))) > 0),
    CONSTRAINT CK_Employees_Name_NotBlank CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
    CONSTRAINT FK_Employees_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId)
);

CREATE UNIQUE INDEX UX_Employees_Code
    ON dbo.Employees (Code);

CREATE UNIQUE INDEX UX_Employees_UserId
    ON dbo.Employees (UserId)
    WHERE UserId IS NOT NULL;

CREATE TABLE dbo.Customers
(
    CustomerId int IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_Customers PRIMARY KEY,
    Code nvarchar(32) COLLATE Latin1_General_100_CI_AI NOT NULL,
    Name nvarchar(256) NOT NULL,
    Address nvarchar(500) NULL,
    Phone nvarchar(64) NULL,
    TaxCode nvarchar(32) NULL,
    IsActive bit NOT NULL
        CONSTRAINT DF_Customers_IsActive DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_Customers_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    CONSTRAINT CK_Customers_Code_NotBlank CHECK (LEN(LTRIM(RTRIM(Code))) > 0),
    CONSTRAINT CK_Customers_Name_NotBlank CHECK (LEN(LTRIM(RTRIM(Name))) > 0)
);

CREATE UNIQUE INDEX UX_Customers_Code
    ON dbo.Customers (Code);
