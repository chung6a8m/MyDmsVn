using System;
using System.Linq;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;

namespace MyDmsVn.Server.DbMigrator;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length >= 2 &&
            string.Equals(args[0], "permissions", StringComparison.Ordinal) &&
            string.Equals(args[1], "sync", StringComparison.Ordinal))
        {
            return PermissionSyncCommand.Run(
                args.Skip(2).ToArray(),
                new SqlConnectionConfigurationLoader(),
                Console.Out,
                Console.Error,
                DateTime.UtcNow);
        }

        if (args.Length >= 2 &&
            string.Equals(args[0], "security", StringComparison.Ordinal) &&
            string.Equals(args[1], "bootstrap-admin", StringComparison.Ordinal))
        {
            return InitialAdministratorProvisioningCommand.Run(
                args.Skip(2).ToArray(),
                new SqlConnectionConfigurationLoader(),
                new ConsoleProvisioningSecretReader(),
                Console.Out,
                Console.Error,
                DateTime.UtcNow);
        }

        if (args.Length >= 2 &&
            string.Equals(args[0], "permissions", StringComparison.Ordinal) &&
            string.Equals(args[1], "administrator-grant", StringComparison.Ordinal))
        {
            return AdministratorGrantCommand.Run(
                args.Skip(2).ToArray(),
                new SqlConnectionConfigurationLoader(),
                Console.Out,
                Console.Error,
                DateTime.UtcNow);
        }

        return DatabaseMigratorCommand.Run(
            new SqlConnectionConfigurationLoader(),
            Console.Out,
            Console.Error);
    }
}
