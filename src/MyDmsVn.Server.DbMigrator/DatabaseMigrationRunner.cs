using System;
using DbUp;
using DbUp.Engine;

namespace MyDmsVn.Server.DbMigrator;

public sealed class DatabaseMigrationRunner
{
    public const string ConnectionStringEnvironmentVariable =
        "MYDMSVN_SQLSERVER_CONNECTION_STRING";

    public DatabaseUpgradeResult Migrate(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));
        }

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
}
