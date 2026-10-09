EXEC(N'
CREATE FUNCTION dbo.CatalogTextContainsNul(@Value nvarchar(max))
RETURNS bit
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @Bytes varbinary(max) = CONVERT(varbinary(max), @Value);
    DECLARE @Offset int = 1;

    WHILE @Offset < DATALENGTH(@Bytes)
    BEGIN
        IF SUBSTRING(@Bytes, @Offset, 2) = 0x0000
            RETURN 1;
        SET @Offset += 2;
    END;

    RETURN 0;
END;');

ALTER TABLE dbo.Products ADD
    CONSTRAINT CK_Products_Code_NoNul CHECK (dbo.CatalogTextContainsNul(Code) = 0),
    CONSTRAINT CK_Products_Name_NoNul CHECK (dbo.CatalogTextContainsNul(Name) = 0),
    CONSTRAINT CK_Products_Unit_NoNul CHECK (dbo.CatalogTextContainsNul(Unit) = 0);

ALTER TABLE dbo.Warehouses ADD
    CONSTRAINT CK_Warehouses_Code_NoNul CHECK (dbo.CatalogTextContainsNul(Code) = 0),
    CONSTRAINT CK_Warehouses_Name_NoNul CHECK (dbo.CatalogTextContainsNul(Name) = 0);

ALTER TABLE dbo.Employees ADD
    CONSTRAINT CK_Employees_Code_NoNul CHECK (dbo.CatalogTextContainsNul(Code) = 0),
    CONSTRAINT CK_Employees_Name_NoNul CHECK (dbo.CatalogTextContainsNul(Name) = 0);

ALTER TABLE dbo.Customers ADD
    CONSTRAINT CK_Customers_Code_NoNul CHECK (dbo.CatalogTextContainsNul(Code) = 0),
    CONSTRAINT CK_Customers_Name_NoNul CHECK (dbo.CatalogTextContainsNul(Name) = 0);
