using System;
using System.IO;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning;

public static class SuperuserMaintenanceCommand
{
    public static int RunRecovery(
        string[] arguments,
        SqlConnectionConfigurationLoader configurationLoader,
        IProvisioningSecretReader secretReader,
        TextReader input,
        TextWriter output,
        TextWriter error,
        DateTime utcNow)
    {
        return Run(
            arguments,
            configurationLoader,
            secretReader,
            input,
            output,
            error,
            utcNow,
            isRename: false);
    }

    public static int RunRename(
        string[] arguments,
        SqlConnectionConfigurationLoader configurationLoader,
        IProvisioningSecretReader secretReader,
        TextReader input,
        TextWriter output,
        TextWriter error,
        DateTime utcNow)
    {
        return Run(
            arguments,
            configurationLoader,
            secretReader,
            input,
            output,
            error,
            utcNow,
            isRename: true);
    }

    private static int Run(
        string[] arguments,
        SqlConnectionConfigurationLoader configurationLoader,
        IProvisioningSecretReader secretReader,
        TextReader input,
        TextWriter output,
        TextWriter error,
        DateTime utcNow,
        bool isRename)
    {
        try
        {
            var parsed = Parse(arguments, isRename);
            var configuredUsername = new SuperuserConfigurationLoader().Load();
            var configuration = configurationLoader.Load(SqlConnectionConfigurationPurpose.Migration);
            var builder = new SqlConnectionStringBuilder(configuration.ConnectionString);
            if (!string.Equals(builder.InitialCatalog, parsed.TargetDatabase, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The confirmed target does not match the maintenance connection.");
            }

            output.WriteLine("Target database: " + parsed.TargetDatabase);
            output.WriteLine("Configured superuser: " + configuredUsername);
            output.WriteLine("Expected bound user ID: " + parsed.ExpectedUserId);
            if (isRename)
            {
                output.WriteLine("New username: " + parsed.NewUsername);
            }

            output.Write("Type the database name to confirm: ");
            if (!string.Equals(input.ReadLine(), parsed.TargetDatabase, StringComparison.Ordinal))
            {
                output.WriteLine("Operation cancelled; no changes were made.");
                return 1;
            }

            output.Write("Type the expected user ID to confirm: ");
            if (!string.Equals(input.ReadLine(), parsed.ExpectedUserId.ToString(), StringComparison.Ordinal))
            {
                output.WriteLine("Operation cancelled; no changes were made.");
                return 1;
            }

            var engine = new SuperuserMaintenanceEngine(new BcryptPasswordHasher());
            if (isRename)
            {
                engine.RenameAsync(
                        configuration.ConnectionString,
                        configuredUsername,
                        parsed.ExpectedUserId,
                        parsed.NewUsername!,
                        utcNow,
                        System.Threading.CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                output.WriteLine(
                    "Superuser renamed. Update Security__SuperuserUsername to the new value and restart every host before login.");
                return 0;
            }

            var password = secretReader.ReadSecret("New superuser password: ", output);
            engine.RecoverAsync(
                    configuration.ConnectionString,
                    configuredUsername,
                    parsed.ExpectedUserId,
                    password,
                    utcNow,
                    System.Threading.CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            output.WriteLine(
                "Superuser recovery completed for the expected account. Existing sessions must sign in again.");
            return 0;
        }
        catch (SqlException)
        {
            error.WriteLine("SecurityMaintenance.Failed: database operation failed; details are omitted.");
            return 2;
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is OperationalConfigurationException)
        {
            error.WriteLine("SecurityMaintenance.Failed: " + exception.Message);
            return 2;
        }
    }

    private static ParsedArguments Parse(string[] arguments, bool isRename)
    {
        var expectedArgumentCount = isRename ? 6 : 4;
        if (arguments == null || arguments.Length != expectedArgumentCount ||
            !string.Equals(arguments[0], "--target", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(arguments[1]) ||
            !string.Equals(arguments[2], "--expected-user-id", StringComparison.Ordinal) ||
            !int.TryParse(arguments[3], out var expectedUserId) || expectedUserId <= 0 ||
            (isRename && !string.Equals(arguments[4], "--new-username", StringComparison.Ordinal)) ||
            (isRename && string.IsNullOrWhiteSpace(arguments[5])))
        {
            throw new ArgumentException(isRename
                ? "Usage: security rename-superuser --target <database-name> --expected-user-id <id> --new-username <name>"
                : "Usage: security recover-superuser --target <database-name> --expected-user-id <id>");
        }

        return new ParsedArguments(
            arguments[1].Trim(),
            expectedUserId,
            isRename ? arguments[5].Trim() : null);
    }

    private sealed class ParsedArguments
    {
        public ParsedArguments(string targetDatabase, int expectedUserId, string? newUsername)
        {
            TargetDatabase = targetDatabase;
            ExpectedUserId = expectedUserId;
            NewUsername = newUsername;
        }

        public string TargetDatabase { get; }
        public int ExpectedUserId { get; }
        public string? NewUsername { get; }
    }
}
