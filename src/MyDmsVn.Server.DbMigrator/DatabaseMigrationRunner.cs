using System;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.DbMigrator;

public sealed class DatabaseMigrationRunner
{
    private static readonly string[] SystemDatabaseNames =
    {
        "master",
        "model",
        "msdb",
        "tempdb",
    };

    public const string ConnectionStringEnvironmentVariable =
        "MYDMSVN_SQLSERVER_CONNECTION_STRING";

    public DatabaseUpgradeResult Migrate(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));
        }

        ValidateTargetDatabase(connectionString);

        var assembly = typeof(DatabaseMigrationRunner).Assembly;
        var engine = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                assembly,
                resourceName => resourceName.Contains(".Scripts."))
            .JournalToSqlTable("dbo", "SchemaVersions")
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build();

        return engine.PerformUpgrade();
    }

    private static void ValidateTargetDatabase(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog?.Trim();
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException(
                "The migration connection string must specify an application database.",
                nameof(connectionString));
        }

        if (Array.Exists(
                SystemDatabaseNames,
                systemDatabase => string.Equals(systemDatabase, databaseName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                $"Migrations cannot target the SQL Server system database '{databaseName}'.",
                nameof(connectionString));
        }
    }
}
