using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private readonly string _connectionString;
    private readonly string _expectedDatabaseUser;
    private readonly string _expectedLogin;
    private readonly byte[] _expectedTargetIdentity;

    public MaintenanceSqlConnectionFactory(string? connectionString, string? expectedDatabaseUser, string? expectedLogin,
        string? expectedTargetIdentity)
    {
        _expectedTargetIdentity = ParseTargetIdentity(expectedTargetIdentity);
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
            await VerifyTargetAndPrincipalAsync(connection, options, deadline.Token).ConfigureAwait(false);
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

    private async Task VerifyTargetAndPrincipalAsync(SqlConnection connection, IOperatorTargetOptions options,
        CancellationToken cancellationToken)
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
    AND NOT EXISTS(SELECT 1 FROM sys.server_principals AS candidate WHERE candidate.name<>ORIGINAL_LOGIN() AND candidate.type<>N'R'
        AND (ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'LOGIN',N'IMPERSONATE'),0)=1
            OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'LOGIN',N'CONTROL'),0)=1
            OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'LOGIN',N'ALTER'),0)=1
            OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'LOGIN',N'TAKE OWNERSHIP'),0)=1))
    AND NOT EXISTS(SELECT 1 FROM sys.server_principals AS candidate WHERE candidate.name<>N'public' AND candidate.type=N'R'
        AND (ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'SERVER ROLE',N'CONTROL'),0)=1
            OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'SERVER ROLE',N'ALTER'),0)=1
            OR ISNULL(HAS_PERMS_BY_NAME(candidate.name,N'SERVER ROLE',N'TAKE OWNERSHIP'),0)=1))
    AND NOT EXISTS(SELECT 1 FROM sys.server_permissions AS permission
        JOIN sys.login_token AS token ON token.principal_id=permission.grantee_principal_id
        WHERE NOT(permission.state=N'G' AND
            ((permission.class=100 AND permission.major_id=0 AND permission.permission_name IN
                (N'CONNECT SQL',N'VIEW ANY DATABASE',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION'))
             OR (permission.class=105 AND permission.major_id IN (2,3,4,5) AND permission.permission_name=N'CONNECT'))))
THEN 1 ELSE 0 END,
CONVERT(nvarchar(128),SERVERPROPERTY(N'ServerName')), CONVERT(int,DB_ID()), allocation.create_date,
(SELECT CONVERT(nvarchar(max),value) FROM sys.extended_properties WHERE class=0 AND name=N'MyDmsVn.TestOwnership')
FROM sys.databases AS allocation WHERE allocation.database_id=DB_ID();";
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            !string.Equals(reader.GetString(0), options.DatabaseName, StringComparison.Ordinal) ||
            !string.Equals(reader.GetString(1), _expectedDatabaseUser, StringComparison.Ordinal) ||
            !string.Equals(reader.GetString(2), _expectedLogin, StringComparison.Ordinal) ||
            !string.Equals(reader.GetString(3), _expectedLogin, StringComparison.Ordinal) ||
            reader.GetInt32(4) != 1 || reader.IsDBNull(5) || reader.IsDBNull(8))
            throw new WebOperationException("Auth.Forbidden");

        var serverName = reader.GetString(5);
        var ownershipToken = reader.GetString(8);
        if (string.IsNullOrWhiteSpace(serverName) || string.IsNullOrWhiteSpace(ownershipToken))
            throw new WebOperationException("Auth.Forbidden");
        var actualIdentity = ComputeTargetIdentity(options.EnvironmentName, reader.GetString(0), serverName,
            reader.GetInt32(6), reader.GetDateTime(7), ownershipToken);
        if (!MatchesExpectedTargetIdentity(actualIdentity) || await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new WebOperationException("Auth.Forbidden");
    }

    internal static byte[] ComputeTargetIdentity(string environmentName, string databaseName, string serverName,
        int databaseId, DateTime databaseCreatedAt, string ownershipToken)
    {
        // Each UTF-8 field has a four-byte big-endian length. SQL create_date has no offset.
        var fields = new[]
        {
            "mydmsvn-maintenance-target-v1", environmentName, databaseName, serverName,
            databaseId.ToString(CultureInfo.InvariantCulture),
            databaseCreatedAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture), ownershipToken
        };
        using var stream = new MemoryStream();
        foreach (var field in fields)
        {
            var bytes = StrictUtf8.GetBytes(field);
            var length = bytes.Length;
            stream.WriteByte((byte)(length >> 24));
            stream.WriteByte((byte)(length >> 16));
            stream.WriteByte((byte)(length >> 8));
            stream.WriteByte((byte)length);
            stream.Write(bytes, 0, length);
        }
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(stream.ToArray());
    }

    private static byte[] ParseTargetIdentity(string? text)
    {
        if (text == null || text.Length != 64)
            throw new WebOperationException("Auth.Forbidden");
        var digest = new byte[32];
        for (var index = 0; index < digest.Length; index++)
            digest[index] = (byte)((HexDigit(text[index * 2]) << 4) | HexDigit(text[index * 2 + 1]));
        return digest;
    }

    private static int HexDigit(char value)
    {
        if (value >= '0' && value <= '9') return value - '0';
        if (value >= 'a' && value <= 'f') return value - 'a' + 10;
        throw new WebOperationException("Auth.Forbidden");
    }

    private bool MatchesExpectedTargetIdentity(byte[] actual)
    {
        // Both operands are SHA-256 digests. Process all bytes before testing the result.
        var difference = 0;
        for (var index = 0; index < _expectedTargetIdentity.Length; index++)
            difference |= _expectedTargetIdentity[index] ^ actual[index];
        return difference == 0;
    }
}
