using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.WebOperation.Operator;

public interface IMaintenanceSqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(IOperatorTargetOptions options, CancellationToken cancellationToken);
}

public sealed class MaintenanceSqlConnectionFactory : IMaintenanceSqlConnectionFactory
{
    private readonly string _connectionString;
    private readonly string _expectedDatabaseUser;
    private readonly string _expectedLogin;

    public MaintenanceSqlConnectionFactory(string? connectionString, string? expectedDatabaseUser, string? expectedLogin)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(expectedDatabaseUser) ||
            string.IsNullOrWhiteSpace(expectedLogin))
            throw new InvalidOperationException("The dedicated maintenance connection and identity are required.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog) || string.IsNullOrWhiteSpace(builder.DataSource) ||
            !string.IsNullOrEmpty(builder.AttachDBFilename) || builder.UserInstance || !string.IsNullOrEmpty(builder.FailoverPartner) ||
            builder.IntegratedSecurity || (builder.Authentication != SqlAuthenticationMethod.NotSpecified &&
                builder.Authentication != SqlAuthenticationMethod.SqlPassword))
            throw new InvalidOperationException("A fixed application database and dedicated SQL login are required.");
        if (!string.Equals(builder.UserID, expectedLogin, StringComparison.Ordinal))
            throw new WebOperationException("Auth.Forbidden");

        builder.Pooling = false;
        builder.Enlist = false;
        builder.ConnectRetryCount = 0;
        _connectionString = builder.ConnectionString;
        _expectedDatabaseUser = expectedDatabaseUser!;
        _expectedLogin = expectedLogin!;
    }

    public async Task<SqlConnection> OpenAsync(IOperatorTargetOptions options, CancellationToken cancellationToken)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        var builder = new SqlConnectionStringBuilder(_connectionString);
        if (!string.Equals(builder.InitialCatalog, options.DatabaseName, StringComparison.Ordinal) ||
            builder.InitialCatalog.Equals("master", StringComparison.OrdinalIgnoreCase) ||
            builder.InitialCatalog.Equals("model", StringComparison.OrdinalIgnoreCase) ||
            builder.InitialCatalog.Equals("msdb", StringComparison.OrdinalIgnoreCase) ||
            builder.InitialCatalog.Equals("tempdb", StringComparison.OrdinalIgnoreCase))
            throw new WebOperationException("Auth.Forbidden");
        builder.ConnectTimeout = builder.ConnectTimeout <= 0
            ? options.TimeoutSeconds
            : Math.Min(builder.ConnectTimeout, options.TimeoutSeconds);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var connection = new SqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(deadline.Token).ConfigureAwait(false);
            await VerifyPrincipalAsync(connection, options, deadline.Token).ConfigureAwait(false);
            var schema = await SqlWeb5SchemaCompatibilityChecker.CheckConnectionAsync(connection, deadline.Token).ConfigureAwait(false);
            if (!schema.IsCompatible)
                throw new WebOperationException("Auth.Forbidden");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private async Task VerifyPrincipalAsync(SqlConnection connection, IOperatorTargetOptions options, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = options.TimeoutSeconds;
        command.CommandText = @"
SELECT DB_NAME(),USER_NAME(),ORIGINAL_LOGIN(),SUSER_SNAME(),
CASE WHEN
    EXISTS(SELECT 1 FROM sys.database_principals WHERE principal_id=USER_ID() AND principal_id>4
        AND type=N'S' AND authentication_type=1 AND sid=SUSER_SID(ORIGINAL_LOGIN()))
    AND EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=USER_ID()
        AND class=0 AND major_id=0 AND minor_id=0 AND permission_name=N'CONTROL' AND state=N'G')
    AND HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONTROL')=1
    AND NOT EXISTS(SELECT 1 FROM sys.database_role_members WHERE member_principal_id=USER_ID())
    AND ISNULL(IS_ROLEMEMBER(N'MyDmsVnWebRuntimeRole'),0)=0
    AND ISNULL(IS_ROLEMEMBER(N'MyDmsVnWebOperationOperatorRole'),0)=0
    AND HAS_PERMS_BY_NAME(NULL,NULL,N'CONTROL SERVER')=0
    AND IS_SRVROLEMEMBER(N'sysadmin')=0 AND IS_SRVROLEMEMBER(N'securityadmin')=0
    AND IS_SRVROLEMEMBER(N'serveradmin')=0 AND IS_SRVROLEMEMBER(N'setupadmin')=0
    AND IS_SRVROLEMEMBER(N'processadmin')=0 AND IS_SRVROLEMEMBER(N'diskadmin')=0
    AND IS_SRVROLEMEMBER(N'dbcreator')=0 AND IS_SRVROLEMEMBER(N'bulkadmin')=0
    AND NOT EXISTS(SELECT 1 FROM sys.login_token WHERE type=N'SERVER ROLE' AND name<>N'public')
    AND NOT EXISTS(SELECT 1 FROM sys.fn_my_permissions(NULL,N'SERVER') WHERE permission_name NOT IN
        (N'CONNECT SQL',N'VIEW ANY DATABASE',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION'))
THEN 1 ELSE 0 END;";
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            !string.Equals(reader.GetString(0), options.DatabaseName, StringComparison.Ordinal) ||
            !string.Equals(reader.GetString(1), _expectedDatabaseUser, StringComparison.Ordinal) ||
            !string.Equals(reader.GetString(2), _expectedLogin, StringComparison.Ordinal) ||
            !string.Equals(reader.GetString(3), _expectedLogin, StringComparison.Ordinal) ||
            reader.GetInt32(4) != 1 || await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new WebOperationException("Auth.Forbidden");
    }
}
