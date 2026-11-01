using System;
using DbUp;
using DbUp.Engine;
using MyDmsVn.Server.DbMigrator;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
// Existing HTTP tests deliberately stay on the current unkeyed Web11 host until B04.
internal static class FrozenWeb11Migration
{
    public static DatabaseUpgradeResult Install(string connectionString)
    {
        var desktop = new DatabaseMigrationRunner().Migrate(connectionString);
        if (!desktop.Successful)
            return desktop;
        return DeployChanges.To.SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrationRunner).Assembly, n => n.EndsWith(".Scripts.011_WebAuthSessions.sql", StringComparison.Ordinal))
            .JournalToSqlTable("dbo", "SchemaVersions").WithTransactionPerScript().LogToNowhere().Build().PerformUpgrade();
    }
}
