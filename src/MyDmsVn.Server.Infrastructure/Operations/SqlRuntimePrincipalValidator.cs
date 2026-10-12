using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.Operations;

public interface IRuntimeSqlPrincipalValidator
{
    Task ValidateAsync(string connectionString, CancellationToken cancellationToken);
}

public sealed class SqlRuntimePrincipalValidator : IRuntimeSqlPrincipalValidator
{
    private const string ElevatedPermissionSql =
        "SELECT CASE WHEN " +
        "ISNULL(IS_SRVROLEMEMBER(N'sysadmin'), 0) = 1 OR " +
        "ISNULL(IS_SRVROLEMEMBER(N'securityadmin'), 0) = 1 OR " +
        "ISNULL(IS_SRVROLEMEMBER(N'serveradmin'), 0) = 1 OR " +
        "ISNULL(IS_SRVROLEMEMBER(N'setupadmin'), 0) = 1 OR " +
        "ISNULL(IS_SRVROLEMEMBER(N'processadmin'), 0) = 1 OR " +
        "ISNULL(IS_SRVROLEMEMBER(N'diskadmin'), 0) = 1 OR " +
        "ISNULL(IS_SRVROLEMEMBER(N'dbcreator'), 0) = 1 OR " +
        "ISNULL(IS_SRVROLEMEMBER(N'bulkadmin'), 0) = 1 OR " +
        "ISNULL(IS_ROLEMEMBER(N'db_owner'), 0) = 1 OR " +
        "ISNULL(IS_ROLEMEMBER(N'db_ddladmin'), 0) = 1 OR " +
        "ISNULL(IS_ROLEMEMBER(N'db_securityadmin'), 0) = 1 OR " +
        "ISNULL(IS_ROLEMEMBER(N'db_accessadmin'), 0) = 1 OR " +
        "ISNULL(IS_ROLEMEMBER(N'db_backupoperator'), 0) = 1 OR " +
        "ISNULL(IS_ROLEMEMBER(N'db_datareader'), 0) = 1 OR " +
        "ISNULL(IS_ROLEMEMBER(N'db_datawriter'), 0) = 1 OR " +
        "ISNULL(IS_ROLEMEMBER(N'db_denydatareader'), 0) = 1 OR " +
        "ISNULL(IS_ROLEMEMBER(N'db_denydatawriter'), 0) = 1 OR " +
        "ISNULL(HAS_PERMS_BY_NAME(NULL, NULL, N'CONTROL SERVER'), 0) = 1 OR " +
        "ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL'), 0) = 1 OR " +
        "ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER'), 0) = 1 OR " +
        "ISNULL(HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'CONTROL'), 0) = 1 OR " +
        "ISNULL(HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'ALTER'), 0) = 1 OR " +
        "EXISTS (SELECT 1 FROM sys.fn_my_permissions(NULL, N'DATABASE') " +
        "WHERE permission_name NOT IN (N'CONNECT', " +
        "N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION', " +
        "N'VIEW ANY COLUMN MASTER KEY DEFINITION')) " +
        "THEN 1 ELSE 0 END;";

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

        var hasElevatedPermissions = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                ElevatedPermissionSql,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (hasElevatedPermissions == 1)
        {
            throw new DesktopStartupException(
                "Security.RuntimePrincipalOverprivileged",
                "Tài khoản kết nối runtime có quyền quản trị không được phép. Hãy cấu hình đúng tài khoản runtime giới hạn quyền.");
        }
    }
}
