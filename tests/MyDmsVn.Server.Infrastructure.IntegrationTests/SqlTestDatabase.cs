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
    private int _disposed;

    private SqlTestDatabase(string adminConnectionString, string databaseName)
    {
        _adminConnectionString = adminConnectionString;
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

        var databaseName = SqlTestDatabaseName.Create();
        SqlTestDatabaseName.EnsureDisposable(databaseName);

        using (var connection = new SqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(
                new CommandDefinition(
                    $"CREATE DATABASE [{databaseName}];",
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        return new SqlTestDatabase(builder.ConnectionString, databaseName);
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    public async Task DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        SqlTestDatabaseName.EnsureDisposable(DatabaseName);
        SqlConnection.ClearAllPools();

        using var connection = new SqlConnection(_adminConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await connection.ExecuteAsync(
            $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{DatabaseName}];").ConfigureAwait(false);
    }
}
