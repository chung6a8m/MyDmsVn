using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.Operations;

public sealed class SqlWebRuntimePrincipalValidator : IRuntimeSqlPrincipalValidator
{
    private readonly bool _requireJournal;
    public SqlWebRuntimePrincipalValidator(bool requireJournal = true) { _requireJournal=requireJournal; }
    private const string MissingPermissionSql = @"SELECT CASE WHEN ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'VIEW DEFINITION'),0)<>1 OR EXISTS (
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
WHERE ISNULL(HAS_PERMS_BY_NAME(N'dbo.Users',N'OBJECT',N'UPDATE',required.column_name,N'COLUMN'),0)<>1) THEN 1 ELSE 0 END;";

    internal const string ElevatedPermissionSql = @"SELECT CASE WHEN ISNULL(IS_SRVROLEMEMBER(N'sysadmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'securityadmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'serveradmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'setupadmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'processadmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'diskadmin'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'dbcreator'), 0) = 1 OR ISNULL(IS_SRVROLEMEMBER(N'bulkadmin'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_owner'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_ddladmin'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_securityadmin'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_accessadmin'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_backupoperator'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_datareader'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_datawriter'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_denydatareader'), 0) = 1 OR ISNULL(IS_ROLEMEMBER(N'db_denydatawriter'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(NULL, NULL, N'CONTROL SERVER'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'CONTROL'), 0) = 1 OR ISNULL(HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'ALTER'), 0) = 1
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
CROSS APPLY sys.fn_my_permissions(QUOTENAME(OBJECT_SCHEMA_NAME(o.object_id))+N'.'+QUOTENAME(o.name),N'OBJECT') p WHERE o.is_ms_shipped=0 AND p.permission_name IN (N'EXECUTE',N'REFERENCES')) THEN 1 ELSE 0 END;";

    public async Task ValidateAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "A SQL Server connection string is required.",
                nameof(connectionString));
        }

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ValidateConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task ValidateConnectionAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            throw new ArgumentException("The SQL connection must be open.", nameof(connection));
        }

        using var command = SqlRead.Command(connection, null, ElevatedPermissionSql);
        var hasElevatedPermissions = await SqlRead.SingleAsync(command, reader => reader.GetInt32(0), cancellationToken).ConfigureAwait(false);
        if (hasElevatedPermissions == 1)
        {
            throw new DesktopStartupException(
                "Security.WebRuntimePrincipalOverprivileged",
                "Tài khoản kết nối runtime có quyền quản trị không được phép. Hãy cấu hình đúng tài khoản runtime giới hạn quyền.");
        }
        using var minimum = SqlRead.Command(connection, null, MissingPermissionSql,
            SqlRead.P("@journal", System.Data.SqlDbType.Bit, _requireJournal));
        if (Convert.ToInt32(await minimum.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 0)
            throw new DesktopStartupException("Security.WebRuntimePrincipalUnderprivileged", "Web runtime is missing required effective permissions.");
    }
}
