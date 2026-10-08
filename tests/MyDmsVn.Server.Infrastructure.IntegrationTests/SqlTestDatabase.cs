using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class SqlTestDatabase : IDisposable
{
    public const string ConnectionStringEnvironmentVariable =
        "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly string _adminConnectionString;
    private readonly Func<string, SqlConnection> _connectionFactory;
    private int _disposeState;

    private SqlTestDatabase(
        string adminConnectionString,
        string databaseName,
        Func<string, SqlConnection> connectionFactory)
    {
        _adminConnectionString = adminConnectionString;
        _connectionFactory = connectionFactory;
        DatabaseName = databaseName;

        var builder = new SqlConnectionStringBuilder(adminConnectionString)
        {
            InitialCatalog = databaseName,
            Pooling = false,
        };
        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    public string DatabaseName { get; }

    public static async Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(
            adminConnectionString,
            cancellationToken,
            connectionString => new SqlConnection(connectionString),
            _ => { }).ConfigureAwait(false);
    }

    internal static async Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        CancellationToken cancellationToken,
        Func<string, SqlConnection> connectionFactory,
        Action<string> afterCreate)
    {
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            throw new ArgumentException("A SQL Server connection string is required.", nameof(adminConnectionString));
        }

        var builder = new SqlConnectionStringBuilder(adminConnectionString);
        if (!string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The SQL test connection must explicitly target the master database.");
        }

        if (connectionFactory is null)
        {
            throw new ArgumentNullException(nameof(connectionFactory));
        }

        if (afterCreate is null)
        {
            throw new ArgumentNullException(nameof(afterCreate));
        }

        var databaseName = SqlTestDatabaseName.Create();
        SqlTestDatabaseName.EnsureDisposable(databaseName);
        var database = new SqlTestDatabase(
            builder.ConnectionString,
            databaseName,
            connectionFactory);

        try
        {
            using (var connection = connectionFactory(builder.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        $"CREATE DATABASE [{databaseName}];",
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            afterCreate(databaseName);
            return database;
        }
        catch (Exception provisioningError)
        {
            try
            {
                await database.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    "SQL test database provisioning failed and guarded cleanup also failed.",
                    provisioningError,
                    cleanupError);
            }

            throw;
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

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
            SqlConnection.ClearAllPools();

            using var connection = _connectionFactory(_adminConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await connection.ExecuteAsync(
                $"IF DB_ID(N'{DatabaseName}') IS NOT NULL " +
                "BEGIN " +
                $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{DatabaseName}]; " +
                "END;").ConfigureAwait(false);
            Volatile.Write(ref _disposeState, 2);
        }
        catch
        {
            Volatile.Write(ref _disposeState, 0);
            throw;
        }
    }
}
