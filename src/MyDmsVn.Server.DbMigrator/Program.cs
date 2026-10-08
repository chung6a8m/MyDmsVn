using System;
using System.Linq;

namespace MyDmsVn.Server.DbMigrator;

internal static class Program
{
    private static int Main()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            DatabaseMigrationRunner.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine(
                $"Set {DatabaseMigrationRunner.ConnectionStringEnvironmentVariable} before running the migrator.");
            return 2;
        }

        var result = new DatabaseMigrationRunner().Migrate(connectionString);
        if (!result.Successful)
        {
            Console.Error.WriteLine(result.Error?.Message ?? "Database migration failed.");
            return 1;
        }

        Console.WriteLine($"Database migration succeeded; applied {result.Scripts.Count()} script(s).");
        return 0;
    }
}
