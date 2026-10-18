using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class SqlTestDatabase : IDisposable
{
    public const string ConnectionStringEnvironmentVariable =
        "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING";

    private const int CommandTimeoutSeconds = 30;
    private static readonly TimeSpan LifecycleTimeout = TimeSpan.FromMinutes(2);

    private readonly string _adminConnectionString;
    private readonly Func<string, SqlConnection> _connectionFactory;
    private readonly string _serverName;
    private readonly string _ownershipToken;
    private readonly (int Id, DateTime CreateDate) _identity;
    private int _disposeState;

    private SqlTestDatabase(
        string adminConnectionString,
        Func<string, SqlConnection> connectionFactory,
        string databaseName,
        string serverName,
        string ownershipToken,
        (int Id, DateTime CreateDate) identity)
    {
        _adminConnectionString = adminConnectionString;
        _connectionFactory = connectionFactory;
        _serverName = serverName;
        _ownershipToken = ownershipToken;
        _identity = identity;
        DatabaseName = databaseName;
        ConnectionString = new SqlConnectionStringBuilder(adminConnectionString)
        {
            InitialCatalog = databaseName,
            Pooling = false,
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    public string DatabaseName { get; }

    public static Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        CancellationToken cancellationToken = default) =>
        CreateAsync(
            adminConnectionString,
            cancellationToken,
            value => new SqlConnection(value),
            _ => { });

    internal static async Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        CancellationToken cancellationToken,
        Func<string, SqlConnection> connectionFactory,
        Action<string> afterCreate)
    {
        var normalizedConnectionString = NormalizeAdminConnectionString(adminConnectionString);
        if (connectionFactory is null)
        {
            throw new ArgumentNullException(nameof(connectionFactory));
        }

        if (afterCreate is null)
        {
            throw new ArgumentNullException(nameof(afterCreate));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LifecycleTimeout);
        var token = timeout.Token;
        var databaseName = SqlTestDatabaseName.Create();
        SqlTestDatabaseName.EnsureDisposable(databaseName);
        var ownershipToken = Guid.NewGuid().ToString("N");
        SqlTestDatabase? database = null;
        var createAttempted = false;
        try
        {
            using var connection = connectionFactory(normalizedConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            var serverName = await ReadInstanceAsync(connection, token).ConfigureAwait(false);

            createAttempted = true;
            using (var create = CreateCommand(connection, $"CREATE DATABASE [{databaseName}];"))
            {
                await create.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            using (var mark = CreateCommand(
                connection,
                $"EXEC [{databaseName}].sys.sp_addextendedproperty " +
                "@name = N'MyDmsVn.TestOwnership', @value = @ownershipToken;"))
            {
                mark.Parameters.Add("@ownershipToken", SqlDbType.NVarChar, 32).Value = ownershipToken;
                await mark.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            var identity = await ReadDatabaseIdentityAsync(connection, databaseName, token)
                .ConfigureAwait(false);
            var marker = await ReadOwnershipMarkerAsync(connection, databaseName, token)
                .ConfigureAwait(false);
            if (identity is null || !string.Equals(marker, ownershipToken, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The created database ownership could not be confirmed.");
            }

            database = new SqlTestDatabase(
                normalizedConnectionString,
                connectionFactory,
                databaseName,
                serverName,
                ownershipToken,
                identity.Value);
            afterCreate(databaseName);
            return database;
        }
        catch (Exception provisioningError)
        {
            if (database is not null)
            {
                try
                {
                    await database.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException(
                        $"Provisioning and guarded cleanup both failed for SQL test database '{databaseName}'.",
                        provisioningError,
                        cleanupError);
                }
            }
            else if (createAttempted)
            {
                throw new InvalidOperationException(
                    $"Provisioning did not confirm ownership of SQL test database '{databaseName}'. " +
                    "Inspect the allocation manually; no unverified database was dropped.",
                    provisioningError);
            }

            throw;
        }
    }

    // Validate the actual reset connection, not just a name or connection-string target.
    internal async Task VerifyOwnershipAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new InvalidOperationException("The SQL test allocation is disposing or disposed.");
        }

        SqlTestDatabaseName.EnsureDisposable(DatabaseName);
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Ownership verification requires an open connection.");
        }

        using var command = CreateCommand(connection,
            "SELECT DB_NAME(), CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), " +
            "database_id, create_date, " +
            "(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties " +
            "WHERE class=0 AND name=N'MyDmsVn.TestOwnership') " +
            "FROM sys.databases WHERE database_id=DB_ID();");
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(4) ||
            !string.Equals(reader.GetString(0), DatabaseName, StringComparison.Ordinal) ||
            !string.Equals(reader.GetString(1), _serverName, StringComparison.OrdinalIgnoreCase) ||
            reader.GetInt32(2) != _identity.Id || reader.GetDateTime(3) != _identity.CreateDate ||
            !string.Equals(reader.GetString(4), _ownershipToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing reset: the live SQL allocation ownership does not match.");
        }
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    public async Task DisposeAsync()
    {
        var previousState = Interlocked.CompareExchange(ref _disposeState, 1, 0);
        if (previousState == 2)
        {
            return;
        }

        if (previousState == 1)
        {
            throw new InvalidOperationException("SQL test database cleanup is already in progress.");
        }

        try
        {
            SqlTestDatabaseName.EnsureDisposable(DatabaseName);
            using var timeout = new CancellationTokenSource(LifecycleTimeout);
            var token = timeout.Token;
            using var connection = _connectionFactory(_adminConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            var serverName = await ReadInstanceAsync(connection, token).ConfigureAwait(false);
            if (!string.Equals(serverName, _serverName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Refusing cleanup of '{DatabaseName}' on a different SQL Server instance.");
            }

            var identity = await ReadDatabaseIdentityAsync(connection, DatabaseName, token)
                .ConfigureAwait(false);
            if (identity is null)
            {
                Volatile.Write(ref _disposeState, 2);
                return;
            }

            if (identity.Value.Id != _identity.Id || identity.Value.CreateDate != _identity.CreateDate)
            {
                throw new InvalidOperationException(
                    $"Refusing cleanup of '{DatabaseName}': the database identity has changed.");
            }

            var marker = await ReadOwnershipMarkerAsync(connection, DatabaseName, token)
                .ConfigureAwait(false);
            if (!string.Equals(marker, _ownershipToken, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing cleanup of '{DatabaseName}': the database ownership marker has changed or is missing.");
            }

            using var drop = CreateCommand(
                connection,
                $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{DatabaseName}];");
            await drop.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            Volatile.Write(ref _disposeState, 2);
        }
        catch
        {
            Volatile.Write(ref _disposeState, 0);
            throw;
        }
    }

    private static string NormalizeAdminConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("A SQL Server connection string targeting master is required.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            !string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(builder.AttachDBFilename) ||
            builder.UserInstance)
        {
            throw new InvalidOperationException(
                "SQL test administration requires an explicit SQL Server instance and master database without attachment.");
        }

        builder.InitialCatalog = "master";
        builder.Pooling = false;
        builder.ConnectRetryCount = 0;
        builder.ConnectTimeout = builder.ConnectTimeout == 0
            ? CommandTimeoutSeconds
            : Math.Min(builder.ConnectTimeout, CommandTimeoutSeconds);
        return builder.ConnectionString;
    }

    private static async Task<string> ReadInstanceAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(
            connection,
            "SELECT DB_NAME(), CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'));");
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            reader.IsDBNull(0) || reader.IsDBNull(1) ||
            !string.Equals(reader.GetString(0), "master", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(reader.GetString(1)))
        {
            throw new InvalidOperationException(
                "SQL test administration could not verify the live master database and server identity.");
        }

        return reader.GetString(1);
    }

    private static async Task<(int Id, DateTime CreateDate)?> ReadDatabaseIdentityAsync(
        SqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(
            connection,
            "SELECT database_id, create_date FROM sys.databases WHERE name = @databaseName;");
        command.Parameters.Add("@databaseName", SqlDbType.NVarChar, 128).Value = databaseName;
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (reader.GetInt32(0), reader.GetDateTime(1));
    }

    private static async Task<string?> ReadOwnershipMarkerAsync(
        SqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(
            connection,
            $"SELECT CONVERT(nvarchar(128), value) FROM [{databaseName}].sys.extended_properties " +
            "WHERE class = 0 AND name = N'MyDmsVn.TestOwnership';");
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string commandText) =>
        new SqlCommand(commandText, connection) { CommandTimeout = CommandTimeoutSeconds };
}
