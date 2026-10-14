using System;
using DbUp;
using MyDmsVn.SqlTestHarness.Timing;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal static class TestDatabaseMigrationRunner
{
    public static void Migrate(
        SqlTestMeasurementContext context,
        string invocationId,
        string connectionString)
    {
        var result = MeasuredDatabaseMigrationRunner.Execute(
            context,
            invocationId,
            () => DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(
                    typeof(TestDatabaseMigrationRunner).Assembly,
                    resourceName => resourceName.Contains(".TestScripts."))
                .JournalToSqlTable("dbo", "TestSchemaVersions")
                .WithTransactionPerScript()
                .LogToNowhere()
                .Build()
                .PerformUpgrade());

        if (!result.Successful)
        {
            throw result.Error ?? new InvalidOperationException("Test database migration failed.");
        }
    }
}
