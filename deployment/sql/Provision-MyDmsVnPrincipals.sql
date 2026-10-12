SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 51000, 'MyDmsVn principals cannot be provisioned in a system database.', 1;

IF USER_ID(N'$(RuntimeUser)') IS NULL
    THROW 51001, 'Create the runtime database user before applying grants.', 1;

IF USER_ID(N'$(MigrationUser)') IS NULL
    THROW 51002, 'Create the migration database user before applying grants.', 1;

IF USER_ID(N'$(RuntimeUser)') = USER_ID(N'$(MigrationUser)')
    THROW 51003, 'Runtime and migration users must be distinct identities.', 1;

DECLARE @RuntimeHasElevatedPermissions bit = 0;
EXECUTE AS USER = N'$(RuntimeUser)';
SELECT @RuntimeHasElevatedPermissions = CASE WHEN
    ISNULL(IS_ROLEMEMBER(N'db_owner'), 0) = 1 OR
    ISNULL(IS_ROLEMEMBER(N'db_ddladmin'), 0) = 1 OR
    ISNULL(IS_ROLEMEMBER(N'db_securityadmin'), 0) = 1 OR
    ISNULL(IS_ROLEMEMBER(N'db_accessadmin'), 0) = 1 OR
    ISNULL(IS_ROLEMEMBER(N'db_backupoperator'), 0) = 1 OR
    ISNULL(IS_ROLEMEMBER(N'db_datareader'), 0) = 1 OR
    ISNULL(IS_ROLEMEMBER(N'db_datawriter'), 0) = 1 OR
    ISNULL(IS_ROLEMEMBER(N'db_denydatareader'), 0) = 1 OR
    ISNULL(IS_ROLEMEMBER(N'db_denydatawriter'), 0) = 1 OR
    ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL'), 0) = 1 OR
    ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER'), 0) = 1 OR
    ISNULL(HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'CONTROL'), 0) = 1 OR
    ISNULL(HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'ALTER'), 0) = 1 OR
    EXISTS
    (
        SELECT 1
        FROM sys.objects AS candidate
        INNER JOIN sys.schemas AS ownerSchema
            ON ownerSchema.schema_id = candidate.schema_id
        CROSS APPLY sys.fn_my_permissions
        (
            QUOTENAME(ownerSchema.name) + N'.' + QUOTENAME(candidate.name),
            N'OBJECT'
        ) AS objectPermission
        WHERE candidate.is_ms_shipped = 0
          AND objectPermission.permission_name IN (N'ALTER', N'CONTROL', N'TAKE OWNERSHIP')
    ) OR
    EXISTS
    (
        SELECT 1
        FROM sys.database_principals AS candidate
        WHERE candidate.name <> USER_NAME()
          AND candidate.type <> N'R'
          AND
          (
              ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'USER', N'IMPERSONATE'), 0) = 1 OR
              ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'USER', N'CONTROL'), 0) = 1 OR
              ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'USER', N'ALTER'), 0) = 1 OR
              ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'USER', N'TAKE OWNERSHIP'), 0) = 1
          )
    ) OR
    EXISTS
    (
        SELECT 1
        FROM sys.database_principals AS candidate
        WHERE candidate.name <> USER_NAME()
          AND candidate.type = N'R'
          AND
          (
              ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'ROLE', N'CONTROL'), 0) = 1 OR
              ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'ROLE', N'ALTER'), 0) = 1 OR
              ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'ROLE', N'TAKE OWNERSHIP'), 0) = 1
          )
    ) OR
    EXISTS
    (
        SELECT 1
        FROM sys.fn_my_permissions(NULL, N'DATABASE')
        WHERE permission_name NOT IN
        (
            N'CONNECT',
            N'VIEW DEFINITION',
            N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',
            N'VIEW ANY COLUMN MASTER KEY DEFINITION'
        )
    )
    THEN 1 ELSE 0 END;
REVERT;

IF @RuntimeHasElevatedPermissions = 1
    THROW 51004, 'Runtime user already has elevated or broad database permissions.', 1;

IF DATABASE_PRINCIPAL_ID(N'MyDmsVnRuntimeRole') IS NULL
    EXEC(N'CREATE ROLE [MyDmsVnRuntimeRole] AUTHORIZATION [dbo];');

IF DATABASE_PRINCIPAL_ID(N'MyDmsVnMigrationRole') IS NULL
    EXEC(N'CREATE ROLE [MyDmsVnMigrationRole] AUTHORIZATION [dbo];');

IF IS_ROLEMEMBER(N'MyDmsVnRuntimeRole', N'$(RuntimeUser)') <> 1
    ALTER ROLE [MyDmsVnRuntimeRole] ADD MEMBER [$(RuntimeUser)];

IF IS_ROLEMEMBER(N'MyDmsVnMigrationRole', N'$(MigrationUser)') <> 1
    ALTER ROLE [MyDmsVnMigrationRole] ADD MEMBER [$(MigrationUser)];

GRANT VIEW DEFINITION TO [MyDmsVnRuntimeRole];

REVOKE INSERT ON OBJECT::dbo.Users FROM [MyDmsVnRuntimeRole];
REVOKE UPDATE ON OBJECT::dbo.Users FROM [MyDmsVnRuntimeRole];
GRANT SELECT ON OBJECT::dbo.Users TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.Users TO [MyDmsVnRuntimeRole];
GRANT UPDATE
    (PasswordHash, PasswordSalt, PasswordAlgorithm, UpdatedAtUtc, UpdatedByUserId)
    ON OBJECT::dbo.Users TO [MyDmsVnRuntimeRole];
GRANT SELECT ON OBJECT::dbo.Roles TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.Roles TO [MyDmsVnRuntimeRole];
GRANT SELECT ON OBJECT::dbo.UserRoles TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.UserRoles TO [MyDmsVnRuntimeRole];
GRANT SELECT ON OBJECT::dbo.RolePermissions TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.RolePermissions TO [MyDmsVnRuntimeRole];
GRANT SELECT ON OBJECT::dbo.UserPermissions TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.UserPermissions TO [MyDmsVnRuntimeRole];

GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.Products TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.Products TO [MyDmsVnRuntimeRole];
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.Warehouses TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.Warehouses TO [MyDmsVnRuntimeRole];
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.Employees TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.Employees TO [MyDmsVnRuntimeRole];
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.Customers TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.Customers TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.CatalogTextContainsNul TO [MyDmsVnRuntimeRole];

GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.GoodsReceipts TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.GoodsReceipts TO [MyDmsVnRuntimeRole];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.GoodsReceiptLines TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.GoodsReceiptLines TO [MyDmsVnRuntimeRole];
GRANT UPDATE ON OBJECT::dbo.GoodsReceiptNumberSequence TO [MyDmsVnRuntimeRole];
GRANT SELECT, INSERT ON OBJECT::dbo.StockLedger TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.StockLedger TO [MyDmsVnRuntimeRole];
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.StockBalances TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.StockBalances TO [MyDmsVnRuntimeRole];

GRANT SELECT ON OBJECT::dbo.SchemaVersions TO [MyDmsVnRuntimeRole];
GRANT VIEW DEFINITION ON OBJECT::dbo.SchemaVersions TO [MyDmsVnRuntimeRole];
DENY INSERT, UPDATE, DELETE ON OBJECT::dbo.SchemaVersions TO [MyDmsVnRuntimeRole];

GRANT CONTROL TO [MyDmsVnMigrationRole];
