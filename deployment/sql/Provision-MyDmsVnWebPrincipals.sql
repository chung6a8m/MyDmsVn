SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb') THROW 51100, 'Web principals require an application database.', 1;
IF USER_ID(N'$(WebRuntimeUser)') IS NULL OR USER_ID(N'$(MigrationUser)') IS NULL THROW 51101, 'Create both database users before provisioning.', 1;
IF USER_ID(N'$(WebRuntimeUser)')=USER_ID(N'$(MigrationUser)') THROW 51102, 'Web and migration users must be distinct.', 1;
IF OBJECT_ID(N'dbo.WebAuthSessions',N'U') IS NULL OR OBJECT_ID(N'dbo.WebRefreshTokens',N'U') IS NULL THROW 51103, 'Explicit schema11 migration is required.', 1;
DECLARE @elevated int;
EXECUTE AS USER = N'$(WebRuntimeUser)';
SELECT @elevated = CASE WHEN ISNULL(IS_SRVROLEMEMBER(N'sysadmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'securityadmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'serveradmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'setupadmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'processadmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'diskadmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'dbcreator'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'bulkadmin'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_owner'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_ddladmin'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_securityadmin'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_accessadmin'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_backupoperator'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_datareader'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_datawriter'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_denydatareader'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_denydatawriter'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(NULL, NULL, N'CONTROL SERVER'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'CONTROL'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'ALTER'), 0) = 1
OR EXISTS (SELECT 1 FROM sys.schemas AS candidate
CROSS APPLY sys.fn_my_permissions(QUOTENAME(candidate.name), N'SCHEMA') AS schemaPermission WHERE schemaPermission.permission_name IN (N'SELECT', N'INSERT', N'UPDATE', N'DELETE'))
OR EXISTS (SELECT 1 FROM sys.objects AS candidate
INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = candidate.schema_id
CROSS APPLY sys.fn_my_permissions(QUOTENAME(ownerSchema.name) + N'.' + QUOTENAME(candidate.name), N'OBJECT') AS objectPermission WHERE candidate.is_ms_shipped = 0 AND candidate.parent_object_id = 0 AND objectPermission.permission_name IN (N'SELECT', N'INSERT', N'UPDATE', N'DELETE')
AND NOT (ownerSchema.name = N'dbo' AND ((objectPermission.permission_name = N'SELECT' AND candidate.name IN (N'Users', N'Roles', N'UserRoles', N'RolePermissions', N'UserPermissions', N'Products', N'Warehouses', N'Employees', N'Customers', N'GoodsReceipts', N'GoodsReceiptLines', N'SalesReceipts', N'SalesReceiptLines', N'StockValuationEntries', N'StockLedger', N'StockBalances', N'SchemaVersions', N'SystemSecuritySettings', N'WebAuthSessions', N'WebRefreshTokens', N'WebDeploymentState', N'WebOperationJournal')) OR (objectPermission.permission_name = N'INSERT' AND candidate.name IN (N'GoodsReceipts', N'GoodsReceiptLines', N'SalesReceipts', N'SalesReceiptLines', N'StockValuationEntries', N'StockLedger', N'StockBalances', N'WebAuthSessions', N'WebRefreshTokens', N'WebOperationJournal')) OR (objectPermission.permission_name = N'UPDATE' AND candidate.name IN (N'Users', N'GoodsReceipts', N'GoodsReceiptNumberSequence', N'SalesReceipts', N'SalesReceiptLines', N'SalesReceiptNumberSequence', N'StockBalances', N'WebAuthSessions', N'WebRefreshTokens')) OR (objectPermission.permission_name = N'DELETE' AND candidate.name IN (N'GoodsReceiptLines', N'SalesReceiptLines')))))
OR EXISTS (SELECT 1 FROM sys.tables AS ownerTable
INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id
INNER JOIN sys.columns AS candidate ON candidate.object_id = ownerTable.object_id
CROSS APPLY (VALUES (N'SELECT'), (N'UPDATE')) AS checkedPermission(permission_name) WHERE ownerTable.is_ms_shipped = 0 AND ISNULL(HAS_PERMS_BY_NAME(QUOTENAME(ownerSchema.name) + N'.' + QUOTENAME(ownerTable.name), N'OBJECT', checkedPermission.permission_name, candidate.name, N'COLUMN'), 0) = 1
AND NOT (ownerSchema.name = N'dbo' AND ((checkedPermission.permission_name = N'SELECT' AND ownerTable.name IN (N'Users', N'Roles', N'UserRoles', N'RolePermissions', N'UserPermissions', N'Products', N'Warehouses', N'Employees', N'Customers', N'GoodsReceipts', N'GoodsReceiptLines', N'SalesReceipts', N'SalesReceiptLines', N'StockValuationEntries', N'StockLedger', N'StockBalances', N'SchemaVersions', N'SystemSecuritySettings', N'WebAuthSessions', N'WebRefreshTokens', N'WebDeploymentState', N'WebOperationJournal')) OR (checkedPermission.permission_name = N'UPDATE' AND (ownerTable.name IN (N'GoodsReceipts', N'SalesReceipts', N'SalesReceiptLines', N'StockBalances', N'WebAuthSessions', N'WebRefreshTokens') OR (ownerTable.name = N'Users' AND candidate.name IN (N'PasswordHash', N'PasswordSalt', N'PasswordAlgorithm', N'UpdatedAtUtc', N'UpdatedByUserId')))))))
OR EXISTS (SELECT 1 FROM sys.objects AS candidate
INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = candidate.schema_id
CROSS APPLY sys.fn_my_permissions(QUOTENAME(ownerSchema.name) + N'.' + QUOTENAME(candidate.name), N'OBJECT') AS objectPermission WHERE candidate.is_ms_shipped = 0 AND objectPermission.permission_name IN (N'ALTER', N'CONTROL', N'TAKE OWNERSHIP'))
OR EXISTS (SELECT 1 FROM sys.database_principals AS candidate WHERE candidate.name <> USER_NAME() AND candidate.type <> N'R' AND (ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'USER', N'IMPERSONATE'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'USER', N'CONTROL'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'USER', N'ALTER'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'USER', N'TAKE OWNERSHIP'), 0) = 1))
OR EXISTS (SELECT 1 FROM sys.database_principals AS candidate WHERE candidate.name <> USER_NAME() AND candidate.type = N'R' AND (ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'ROLE', N'CONTROL'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'ROLE', N'ALTER'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'ROLE', N'TAKE OWNERSHIP'), 0) = 1))
OR EXISTS (SELECT 1 FROM sys.server_principals AS candidate WHERE candidate.name <> ORIGINAL_LOGIN() AND candidate.type <> N'R' AND (ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'LOGIN', N'IMPERSONATE'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'LOGIN', N'CONTROL'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'LOGIN', N'ALTER'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'LOGIN', N'TAKE OWNERSHIP'), 0) = 1))
OR EXISTS (SELECT 1 FROM sys.server_principals AS candidate WHERE candidate.name <> ORIGINAL_LOGIN() AND candidate.type = N'R' AND (ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'SERVER ROLE', N'CONTROL'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'SERVER ROLE', N'ALTER'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name, N'SERVER ROLE', N'TAKE OWNERSHIP'), 0) = 1))
OR EXISTS (SELECT 1 FROM sys.database_permissions p JOIN sys.user_token t ON t.principal_id=p.grantee_principal_id WHERE p.state='W')
OR EXISTS (SELECT 1 FROM sys.fn_my_permissions(NULL, N'DATABASE') WHERE permission_name NOT IN (N'CONNECT', N'VIEW DEFINITION', N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION', N'VIEW ANY COLUMN MASTER KEY DEFINITION'))
OR EXISTS (SELECT 1 FROM sys.objects o
CROSS APPLY sys.fn_my_permissions(QUOTENAME(OBJECT_SCHEMA_NAME(o.object_id))+N'.'+QUOTENAME(o.name),N'OBJECT') p WHERE o.is_ms_shipped=0 AND p.permission_name IN (N'EXECUTE',N'REFERENCES')) THEN 1 ELSE 0 END;
REVERT;
IF @elevated=1 THROW 51104, 'Web runtime has elevated or unlisted permissions.', 1;
IF DATABASE_PRINCIPAL_ID(N'MyDmsVnWebRuntimeRole') IS NULL EXEC(N'CREATE ROLE [MyDmsVnWebRuntimeRole] AUTHORIZATION dbo;');
IF DATABASE_PRINCIPAL_ID(N'MyDmsVnMigrationRole') IS NULL EXEC(N'CREATE ROLE [MyDmsVnMigrationRole] AUTHORIZATION dbo;');
IF IS_ROLEMEMBER(N'MyDmsVnWebRuntimeRole',N'$(WebRuntimeUser)')<>1 ALTER ROLE MyDmsVnWebRuntimeRole ADD MEMBER [$(WebRuntimeUser)];
IF IS_ROLEMEMBER(N'MyDmsVnMigrationRole',N'$(MigrationUser)')<>1 ALTER ROLE MyDmsVnMigrationRole ADD MEMBER [$(MigrationUser)];
GRANT VIEW DEFINITION TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.Users TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.Roles TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.UserRoles TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.RolePermissions TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.UserPermissions TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.Products TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.Warehouses TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.Employees TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.Customers TO MyDmsVnWebRuntimeRole;
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.GoodsReceipts TO MyDmsVnWebRuntimeRole;
GRANT SELECT, INSERT, DELETE ON OBJECT::dbo.GoodsReceiptLines TO MyDmsVnWebRuntimeRole;
GRANT UPDATE ON OBJECT::dbo.GoodsReceiptNumberSequence TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.SalesReceipts TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.SalesReceiptLines TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.StockValuationEntries TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.StockLedger TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.StockBalances TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.SchemaVersions TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.SystemSecuritySettings TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.WebAuthSessions TO MyDmsVnWebRuntimeRole;
GRANT SELECT ON OBJECT::dbo.WebRefreshTokens TO MyDmsVnWebRuntimeRole;
GRANT INSERT ON OBJECT::dbo.SalesReceipts TO MyDmsVnWebRuntimeRole;
GRANT INSERT ON OBJECT::dbo.SalesReceiptLines TO MyDmsVnWebRuntimeRole;
GRANT INSERT ON OBJECT::dbo.StockValuationEntries TO MyDmsVnWebRuntimeRole;
GRANT INSERT ON OBJECT::dbo.StockLedger TO MyDmsVnWebRuntimeRole;
GRANT INSERT ON OBJECT::dbo.StockBalances TO MyDmsVnWebRuntimeRole;
GRANT INSERT ON OBJECT::dbo.WebAuthSessions TO MyDmsVnWebRuntimeRole;
GRANT INSERT ON OBJECT::dbo.WebRefreshTokens TO MyDmsVnWebRuntimeRole;
GRANT UPDATE ON OBJECT::dbo.SalesReceipts TO MyDmsVnWebRuntimeRole;
GRANT UPDATE ON OBJECT::dbo.SalesReceiptLines TO MyDmsVnWebRuntimeRole;
GRANT UPDATE ON OBJECT::dbo.SalesReceiptNumberSequence TO MyDmsVnWebRuntimeRole;
GRANT UPDATE ON OBJECT::dbo.StockBalances TO MyDmsVnWebRuntimeRole;
GRANT UPDATE ON OBJECT::dbo.WebAuthSessions TO MyDmsVnWebRuntimeRole;
GRANT UPDATE ON OBJECT::dbo.WebRefreshTokens TO MyDmsVnWebRuntimeRole;
GRANT UPDATE (PasswordHash, PasswordSalt, PasswordAlgorithm, UpdatedAtUtc, UpdatedByUserId) ON OBJECT::dbo.Users TO MyDmsVnWebRuntimeRole;
GRANT DELETE ON OBJECT::dbo.SalesReceiptLines TO MyDmsVnWebRuntimeRole;
DENY INSERT,UPDATE,DELETE ON OBJECT::dbo.SchemaVersions TO MyDmsVnWebRuntimeRole;
GRANT CONTROL TO MyDmsVnMigrationRole;

-- Existing Web11 remains separately admitted by its frozen host. Schema12 grants are opt-in by installed artifacts.
IF OBJECT_ID(N'dbo.WebOperationJournal',N'U') IS NOT NULL
BEGIN
    GRANT SELECT ON OBJECT::dbo.WebDeploymentState TO MyDmsVnWebRuntimeRole;
    GRANT SELECT, INSERT ON OBJECT::dbo.WebOperationJournal TO MyDmsVnWebRuntimeRole;
END;

DECLARE @journal bit=CASE WHEN OBJECT_ID(N'dbo.WebOperationJournal',N'U') IS NULL THEN 0 ELSE 1 END;
DECLARE @missing int;
EXECUTE AS USER=N'$(WebRuntimeUser)';
SELECT @missing = CASE WHEN ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'VIEW DEFINITION'),0)<>1 OR EXISTS (
SELECT 1 FROM (VALUES (N'Users',N'SELECT'),
(N'Roles',N'SELECT'),
(N'UserRoles',N'SELECT'),
(N'RolePermissions',N'SELECT'),
(N'UserPermissions',N'SELECT'),
(N'Products',N'SELECT'),
(N'Warehouses',N'SELECT'),
(N'Employees',N'SELECT'),
(N'Customers',N'SELECT'),
(N'GoodsReceipts',N'SELECT'),
(N'GoodsReceipts',N'INSERT'),
(N'GoodsReceipts',N'UPDATE'),
(N'GoodsReceiptLines',N'SELECT'),
(N'GoodsReceiptLines',N'INSERT'),
(N'GoodsReceiptLines',N'DELETE'),
(N'GoodsReceiptNumberSequence',N'UPDATE'),
(N'SalesReceipts',N'SELECT'),
(N'SalesReceiptLines',N'SELECT'),
(N'StockValuationEntries',N'SELECT'),
(N'StockLedger',N'SELECT'),
(N'StockBalances',N'SELECT'),
(N'SchemaVersions',N'SELECT'),
(N'SystemSecuritySettings',N'SELECT'),
(N'WebAuthSessions',N'SELECT'),
(N'WebRefreshTokens',N'SELECT'),
(N'SalesReceipts',N'INSERT'),
(N'SalesReceiptLines',N'INSERT'),
(N'StockValuationEntries',N'INSERT'),
(N'StockLedger',N'INSERT'),
(N'StockBalances',N'INSERT'),
(N'WebAuthSessions',N'INSERT'),
(N'WebRefreshTokens',N'INSERT'),
(N'SalesReceipts',N'UPDATE'),
(N'SalesReceiptLines',N'UPDATE'),
(N'SalesReceiptNumberSequence',N'UPDATE'),
(N'StockBalances',N'UPDATE'),
(N'WebAuthSessions',N'UPDATE'),
(N'WebRefreshTokens',N'UPDATE'),
(N'SalesReceiptLines',N'DELETE'),
(N'WebDeploymentState',N'SELECT'),
(N'WebOperationJournal',N'SELECT'),
(N'WebOperationJournal',N'INSERT') ) required(object_name,permission_name)
WHERE (@journal=1 OR required.object_name NOT IN(N'WebDeploymentState',N'WebOperationJournal')) AND
(ISNULL(HAS_PERMS_BY_NAME(N'dbo.'+required.object_name,N'OBJECT',required.permission_name),0)<>1 OR
(required.permission_name IN(N'SELECT',N'UPDATE') AND EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.'+required.object_name)
AND ISNULL(HAS_PERMS_BY_NAME(N'dbo.'+required.object_name,N'OBJECT',required.permission_name,c.name,N'COLUMN'),0)<>1))))
OR EXISTS(SELECT 1 FROM (VALUES(N'PasswordHash'),(N'PasswordSalt'),(N'PasswordAlgorithm'),(N'UpdatedAtUtc'),(N'UpdatedByUserId')) required(column_name)
WHERE ISNULL(HAS_PERMS_BY_NAME(N'dbo.Users',N'OBJECT',N'UPDATE',required.column_name,N'COLUMN'),0)<>1) THEN 1 ELSE 0 END;
REVERT;
IF @missing=1 THROW 51105, 'Web runtime is missing required effective permissions.',1;
