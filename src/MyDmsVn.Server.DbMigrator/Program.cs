using System;
using System.Linq;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;

namespace MyDmsVn.Server.DbMigrator;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length >= 1 &&
            string.Equals(args[0], "permissions", StringComparison.Ordinal))
        {
            return PermissionCatalogCommand.Run(
                args.Skip(1).ToArray(),
                Console.Out,
                Console.Error);
        }

        if (args.Length == 1 && string.Equals(args[0], "--web", StringComparison.Ordinal))
        {
            return DatabaseMigratorCommand.Run(new SqlConnectionConfigurationLoader(), Console.Out, Console.Error, web: true);
        }

        if (args.Length == 0)
        {
            return DatabaseMigratorCommand.Run(
                new SqlConnectionConfigurationLoader(),
                Console.Out,
                Console.Error);
        }

        if (args.Length >= 2 &&
            string.Equals(args[0], "security", StringComparison.Ordinal) &&
            string.Equals(args[1], "bootstrap-superuser", StringComparison.Ordinal))
        {
            return SuperuserBootstrapCommand.Run(
                args.Skip(2).ToArray(),
                new SqlConnectionConfigurationLoader(),
                new ConsoleProvisioningSecretReader(),
                Console.Out,
                Console.Error,
                DateTime.UtcNow);
        }

        if (args.Length >= 2 &&
            string.Equals(args[0], "security", StringComparison.Ordinal) &&
            string.Equals(args[1], "recover-superuser", StringComparison.Ordinal))
        {
            return SuperuserMaintenanceCommand.RunRecovery(
                args.Skip(2).ToArray(),
                new SqlConnectionConfigurationLoader(),
                new ConsoleProvisioningSecretReader(),
                Console.In,
                Console.Out,
                Console.Error,
                DateTime.UtcNow);
        }

        if (args.Length >= 2 &&
            string.Equals(args[0], "security", StringComparison.Ordinal) &&
            string.Equals(args[1], "rename-superuser", StringComparison.Ordinal))
        {
            return SuperuserMaintenanceCommand.RunRename(
                args.Skip(2).ToArray(),
                new SqlConnectionConfigurationLoader(),
                new ConsoleProvisioningSecretReader(),
                Console.In,
                Console.Out,
                Console.Error,
                DateTime.UtcNow);
        }

        Console.Error.WriteLine(
            "DbMigrator.InvalidArguments: Unknown command. Use no arguments for desktop schema10, --web for explicit schema11, " +
            "or use security bootstrap-superuser, recover-superuser or rename-superuser.");
        return 2;
    }
}
