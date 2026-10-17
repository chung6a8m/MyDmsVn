using System;
using System.IO;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning;

public static class SuperuserBootstrapCommand
{
    public static int Run(
        string[] arguments,
        SqlConnectionConfigurationLoader configurationLoader,
        IProvisioningSecretReader secretReader,
        TextWriter output,
        TextWriter error,
        DateTime utcNow)
    {
        try
        {
            var targetDatabase = ParseTarget(arguments);
            var username = new SuperuserConfigurationLoader().Load();
            var configuration = configurationLoader.Load(SqlConnectionConfigurationPurpose.Migration);
            var builder = new SqlConnectionStringBuilder(configuration.ConnectionString);
            if (!string.Equals(builder.InitialCatalog, targetDatabase, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The confirmed target does not match the maintenance connection.");
            }

            output.WriteLine("Target database: " + targetDatabase);
            output.WriteLine("Configured superuser: " + username);
            output.Write("Type the database name to confirm: ");
            var confirmation = Console.ReadLine();
            if (!string.Equals(confirmation, targetDatabase, StringComparison.Ordinal))
            {
                output.WriteLine("Bootstrap cancelled; no changes were made.");
                return 1;
            }

            var password = secretReader.ReadSecret("New superuser password: ", output);
            var userId = new SuperuserBootstrapEngine(new BcryptPasswordHasher())
                .BootstrapAsync(
                    configuration.ConnectionString,
                    username,
                    username,
                    password,
                    utcNow,
                    System.Threading.CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            output.WriteLine("Superuser bootstrap completed for user ID " + userId + ".");
            return 0;
        }
        catch (SqlException)
        {
            error.WriteLine("SecurityBootstrap.Failed: database operation failed; details are omitted.");
            return 2;
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is OperationalConfigurationException)
        {
            error.WriteLine("SecurityBootstrap.Failed: " + exception.Message);
            return 2;
        }
    }

    private static string ParseTarget(string[] arguments)
    {
        if (arguments == null || arguments.Length != 2 ||
            !string.Equals(arguments[0], "--target", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(arguments[1]))
        {
            throw new ArgumentException(
                "Usage: security bootstrap-superuser --target <database-name>");
        }

        return arguments[1].Trim();
    }
}
