CREATE TABLE dbo.P1TestProbe
(
    ProbeId int IDENTITY(1,1) NOT NULL
        CONSTRAINT PK_P1TestProbe PRIMARY KEY,
    ProbeValue nvarchar(128) NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_P1TestProbe_CreatedAtUtc DEFAULT SYSUTCDATETIME()
);
