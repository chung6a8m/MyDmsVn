using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.WebOperation.Operator;

public interface IOperatorSqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(InspectOptions options, CancellationToken cancellationToken);
}

public sealed class OperatorSqlConnectionFactory : IOperatorSqlConnectionFactory
{
    private readonly string _connectionString;
    private readonly string _expectedDatabaseUser;
    private readonly string _expectedLogin;

    public OperatorSqlConnectionFactory(string? connectionString, string? expectedDatabaseUser, string? expectedLogin)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The dedicated operator connection is not configured.");
        if (string.IsNullOrWhiteSpace(expectedDatabaseUser) || string.IsNullOrWhiteSpace(expectedLogin))
            throw new InvalidOperationException("The expected restricted operator identity is not configured.");
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog) || builder.InitialCatalog.Equals("master", StringComparison.OrdinalIgnoreCase) ||
            builder.InitialCatalog.Equals("model", StringComparison.OrdinalIgnoreCase) || builder.InitialCatalog.Equals("msdb", StringComparison.OrdinalIgnoreCase) ||
            builder.InitialCatalog.Equals("tempdb", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(builder.AttachDBFilename) || builder.UserInstance)
            throw new InvalidOperationException("A fixed application database operator connection is required.");
        builder.Pooling = false;
        _connectionString = builder.ConnectionString;
        _expectedDatabaseUser = expectedDatabaseUser!;
        _expectedLogin = expectedLogin!;
    }

    public async Task<SqlConnection> OpenAsync(InspectOptions options, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        if (!builder.InitialCatalog.Equals(options.DatabaseName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The configured operator database does not match the explicit target.");
        builder.ConnectTimeout = builder.ConnectTimeout <= 0
            ? options.TimeoutSeconds
            : Math.Min(builder.ConnectTimeout, options.TimeoutSeconds);
        var connection = new SqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await VerifyPrincipalAsync(connection, options, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private async Task VerifyPrincipalAsync(SqlConnection connection, InspectOptions options, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = options.TimeoutSeconds;
        command.CommandText = @"SELECT DB_NAME(),USER_NAME(),ORIGINAL_LOGIN(),
ISNULL(IS_ROLEMEMBER(N'MyDmsVnWebOperationOperatorRole'),0),
(SELECT COUNT(*) FROM sys.database_role_members AS drm
 JOIN sys.database_principals AS rolePrincipal ON rolePrincipal.principal_id=drm.role_principal_id
 JOIN sys.database_principals AS memberPrincipal ON memberPrincipal.principal_id=drm.member_principal_id
 WHERE memberPrincipal.name=USER_NAME() AND rolePrincipal.name<>N'MyDmsVnWebOperationOperatorRole'),
(SELECT COUNT(*) FROM sys.database_permissions AS permission
 JOIN sys.database_principals AS grantee ON grantee.principal_id=permission.grantee_principal_id
 WHERE grantee.name=USER_NAME()
 AND NOT(permission.class=0 AND permission.permission_name=N'CONNECT' AND permission.state=N'G')),
ISNULL(IS_ROLEMEMBER(N'db_owner'),0),ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONTROL'),0),
ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,N'CONTROL SERVER'),0),
ISNULL(IS_SRVROLEMEMBER(N'sysadmin'),0)+ISNULL(IS_SRVROLEMEMBER(N'securityadmin'),0)+
ISNULL(IS_SRVROLEMEMBER(N'serveradmin'),0)+ISNULL(IS_SRVROLEMEMBER(N'setupadmin'),0)+
ISNULL(IS_SRVROLEMEMBER(N'processadmin'),0)+ISNULL(IS_SRVROLEMEMBER(N'diskadmin'),0)+
ISNULL(IS_SRVROLEMEMBER(N'dbcreator'),0)+ISNULL(IS_SRVROLEMEMBER(N'bulkadmin'),0);";
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) || reader.FieldCount != 10 ||
            !string.Equals(reader.GetString(0), options.DatabaseName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The connected database does not match the explicit target.");
        if (!string.Equals(reader.GetString(1), _expectedDatabaseUser, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(reader.GetString(2), _expectedLogin, StringComparison.OrdinalIgnoreCase) ||
            reader.GetInt32(3) != 1 || reader.GetInt32(4) != 0 || reader.GetInt32(5) != 0 ||
            reader.GetInt32(6) != 0 || reader.GetInt32(7) != 0 || reader.GetInt32(8) != 0 || reader.GetInt32(9) != 0 ||
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new WebOperationException("Auth.Forbidden");
        reader.Close();
        await VerifyEffectivePermissionsAsync(connection, options, cancellationToken).ConfigureAwait(false);
    }

    private static async Task VerifyEffectivePermissionsAsync(SqlConnection connection, InspectOptions options,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = options.TimeoutSeconds;
        command.CommandText = @"SELECT CASE WHEN
ISNULL(IS_ROLEMEMBER(N'MyDmsVnWebRuntimeRole'),0)=1 OR ISNULL(IS_ROLEMEMBER(N'MyDmsVnMigrationRole'),0)=1 OR
ISNULL(IS_ROLEMEMBER(N'db_owner'),0)=1 OR ISNULL(IS_ROLEMEMBER(N'db_ddladmin'),0)=1 OR
ISNULL(IS_ROLEMEMBER(N'db_securityadmin'),0)=1 OR ISNULL(IS_ROLEMEMBER(N'db_accessadmin'),0)=1 OR
ISNULL(IS_ROLEMEMBER(N'db_backupoperator'),0)=1 OR ISNULL(IS_ROLEMEMBER(N'db_datareader'),0)=1 OR
ISNULL(IS_ROLEMEMBER(N'db_datawriter'),0)=1 OR ISNULL(IS_ROLEMEMBER(N'db_denydatareader'),0)=1 OR
ISNULL(IS_ROLEMEMBER(N'db_denydatawriter'),0)=1 OR
ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,N'CONTROL SERVER'),0)=1 OR
ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONTROL'),0)=1 OR
ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'ALTER'),0)=1 OR
ISNULL(HAS_PERMS_BY_NAME(N'dbo',N'SCHEMA',N'CONTROL'),0)=1 OR
ISNULL(HAS_PERMS_BY_NAME(N'dbo',N'SCHEMA',N'ALTER'),0)=1 OR
EXISTS(SELECT 1 FROM sys.fn_my_permissions(NULL,N'SERVER')
 WHERE permission_name NOT IN(N'CONNECT SQL',N'VIEW ANY DATABASE',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION')) OR
EXISTS(SELECT 1 FROM sys.fn_my_permissions(NULL,N'DATABASE')
 WHERE permission_name NOT IN(N'CONNECT',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION')) OR
EXISTS(SELECT 1 FROM sys.schemas AS candidate
 CROSS APPLY sys.fn_my_permissions(QUOTENAME(candidate.name),N'SCHEMA') AS permission
 WHERE permission.permission_name IN(N'SELECT',N'INSERT',N'UPDATE',N'DELETE',N'ALTER',N'CONTROL',N'TAKE OWNERSHIP')) OR
EXISTS(SELECT 1 FROM sys.objects AS candidate
 JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id=candidate.schema_id
 CROSS APPLY sys.fn_my_permissions(QUOTENAME(ownerSchema.name)+N'.'+QUOTENAME(candidate.name),N'OBJECT') AS permission
 WHERE candidate.is_ms_shipped=0 AND candidate.parent_object_id=0 AND
 (permission.permission_name IN(N'INSERT',N'UPDATE',N'DELETE',N'ALTER',N'CONTROL',N'TAKE OWNERSHIP',N'REFERENCES') OR
  (permission.permission_name=N'SELECT' AND NOT(ownerSchema.name=N'dbo' AND candidate.name=N'WebOperationTerminalAudit')) OR
  (permission.permission_name=N'EXECUTE' AND NOT(ownerSchema.name=N'dbo' AND candidate.name IN(N'InspectWebOperationResolution',N'RecordWebOperationNotExecuted'))))) OR
EXISTS(SELECT 1 FROM sys.tables AS ownerTable
 JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id=ownerTable.schema_id
 JOIN sys.columns AS candidate ON candidate.object_id=ownerTable.object_id
 CROSS APPLY(VALUES(N'SELECT'),(N'UPDATE')) AS checked(permission_name)
 WHERE ownerTable.is_ms_shipped=0 AND
 ISNULL(HAS_PERMS_BY_NAME(QUOTENAME(ownerSchema.name)+N'.'+QUOTENAME(ownerTable.name),N'OBJECT',checked.permission_name,candidate.name,N'COLUMN'),0)=1 AND
 NOT(ownerSchema.name=N'dbo' AND ownerTable.name=N'WebOperationTerminalAudit' AND checked.permission_name=N'SELECT')) OR
EXISTS(SELECT 1 FROM sys.database_principals AS candidate WHERE candidate.name<>USER_NAME() AND candidate.type<>N'R' AND
 (ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'USER',N'IMPERSONATE'),0)=1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'USER',N'CONTROL'),0)=1 OR
  ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'USER',N'ALTER'),0)=1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'USER',N'TAKE OWNERSHIP'),0)=1)) OR
EXISTS(SELECT 1 FROM sys.database_principals AS candidate WHERE candidate.name<>N'public' AND candidate.type=N'R' AND
 (ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'ROLE',N'CONTROL'),0)=1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'ROLE',N'ALTER'),0)=1 OR
  ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'ROLE',N'TAKE OWNERSHIP'),0)=1)) OR
EXISTS(SELECT 1 FROM sys.server_principals AS candidate WHERE candidate.name<>ORIGINAL_LOGIN() AND candidate.type<>N'R' AND
 (ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'LOGIN',N'IMPERSONATE'),0)=1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'LOGIN',N'CONTROL'),0)=1 OR
  ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'LOGIN',N'ALTER'),0)=1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'LOGIN',N'TAKE OWNERSHIP'),0)=1)) OR
EXISTS(SELECT 1 FROM sys.server_principals AS candidate WHERE candidate.name<>N'public' AND candidate.type=N'R' AND
 (ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'SERVER ROLE',N'CONTROL'),0)=1 OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'SERVER ROLE',N'ALTER'),0)=1 OR
  ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'SERVER ROLE',N'TAKE OWNERSHIP'),0)=1)) OR
EXISTS(SELECT 1 FROM sys.database_permissions AS permission
 JOIN sys.user_token AS token ON token.principal_id=permission.grantee_principal_id
 WHERE NOT(permission.state=N'G' AND permission.minor_id=0 AND
  ((permission.class=0 AND permission.major_id=0 AND permission.permission_name IN(N'CONNECT',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION')) OR
   (permission.class=1 AND permission.major_id<0 AND permission.permission_name=N'SELECT') OR
   (permission.class=1 AND permission.major_id IN(OBJECT_ID(N'dbo.InspectWebOperationResolution'),OBJECT_ID(N'dbo.RecordWebOperationNotExecuted')) AND permission.permission_name=N'EXECUTE')))) OR
EXISTS(SELECT 1 FROM sys.server_permissions AS permission
 JOIN sys.login_token AS token ON token.principal_id=permission.grantee_principal_id
 WHERE NOT(permission.state=N'G' AND
  ((permission.class=100 AND permission.major_id=0 AND permission.permission_name IN(N'CONNECT SQL',N'VIEW ANY DATABASE',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION')) OR
   (permission.class=105 AND permission.major_id IN(2,3,4,5) AND permission.permission_name=N'CONNECT'))))
THEN 1 ELSE 0 END;";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 0)
            throw new WebOperationException("Auth.Forbidden");
    }
}

public sealed class SqlOperatorInspectionClient : IOperatorInspectionClient
{
    private readonly IOperatorSqlConnectionFactory _connections;
    public SqlOperatorInspectionClient(IOperatorSqlConnectionFactory connections) =>
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));

    public async Task<WebOperationInspection> InspectAsync(InspectOptions options, CancellationToken cancellationToken)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        using var connection = await _connections.OpenAsync(options, cancellationToken).ConfigureAwait(false);
        using (var target = connection.CreateCommand())
        {
            target.CommandText = "SELECT DB_NAME();";
            target.CommandTimeout = options.TimeoutSeconds;
            var actual = Convert.ToString(await target.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            if (!string.Equals(actual, options.DatabaseName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The connected database does not match the explicit target.");
        }

        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var context = new InspectionContext(connection, transaction);
        var rollbackStarted = false;
        try
        {
            WebOperationInspection result;
            try
            {
                result = await new SqlWebOperationResolutionMaintenance(context).InspectAsync(options.DeploymentEpoch,
                    options.ActorUserId, options.OperationId, options.FingerprintBytes(), cancellationToken).ConfigureAwait(false);
            }
            catch (WebOperationException exception) when (exception.Code == "Operation.Unavailable" && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The bounded SQL inspection was cancelled.", exception, cancellationToken);
            }
            rollbackStarted = true;
            transaction.Rollback();
            return result;
        }
        catch (OperationCanceledException)
        {
            connection.Close();
            throw;
        }
        catch
        {
            if (!rollbackStarted && transaction.Connection != null)
                transaction.Rollback();
            throw;
        }
    }

    private sealed class InspectionContext : ISqlExecutionContext
    {
        public InspectionContext(SqlConnection connection, SqlTransaction transaction)
        {
            Connection = connection;
            Transaction = transaction;
        }
        public IDbConnection Connection { get; }
        public IDbTransaction? Transaction { get; }
    }
}
