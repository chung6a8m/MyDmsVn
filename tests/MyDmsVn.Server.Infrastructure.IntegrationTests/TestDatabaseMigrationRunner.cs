using System;
using DbUp;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal static class TestDatabaseMigrationRunner
{
    public static void Migrate(string connectionString)
    {
        var result = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(TestDatabaseMigrationRunner).Assembly,
                resourceName => resourceName.Contains(".TestScripts."))
            .JournalToSqlTable("dbo", "TestSchemaVersions")
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw result.Error ?? new InvalidOperationException("Test database migration failed.");
        }
    }
}
