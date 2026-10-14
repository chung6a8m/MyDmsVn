using System;
using System.Collections.Generic;
using System.IO;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Operations;
using Newtonsoft.Json;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning
{
    public interface IProvisioningSecretReader
    {
        bool IsInteractive { get; }

        string ReadSecret(string prompt, TextWriter output);
    }

    public sealed class ConsoleProvisioningSecretReader : IProvisioningSecretReader
    {
        public bool IsInteractive => !Console.IsInputRedirected;

        public string ReadSecret(string prompt, TextWriter output)
        {
            if (!IsInteractive)
            {
                throw new InitialAdministratorProvisioningException(
                    "SecurityBootstrap.InteractiveInputRequired",
                    "Interactive masked input is required for enrollment.");
            }

            output.Write(prompt);
            var characters = new List<char>();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    output.WriteLine();
                    return new string(characters.ToArray());
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (characters.Count > 0)
                    {
                        characters.RemoveAt(characters.Count - 1);
                    }

                    continue;
                }

                if (key.Key == ConsoleKey.C &&
                    (key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
                {
                    throw new OperationCanceledException("Secret entry was cancelled.");
                }

                if (!char.IsControl(key.KeyChar))
                {
                    characters.Add(key.KeyChar);
                }
            }
        }
    }

    public static class InitialAdministratorProvisioningCommand
    {
        public static int Run(
            string[] arguments,
            SqlConnectionConfigurationLoader configurationLoader,
            IProvisioningSecretReader secretReader,
            TextWriter output,
            TextWriter error,
            DateTime utcNow)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            if (configurationLoader == null)
            {
                throw new ArgumentNullException(nameof(configurationLoader));
            }

            if (secretReader == null)
            {
                throw new ArgumentNullException(nameof(secretReader));
            }

            if (output == null || error == null)
            {
                throw new ArgumentNullException(output == null ? nameof(output) : nameof(error));
            }

            try
            {
                var options = Parse(arguments);
                var connection = configurationLoader.Load(SqlConnectionConfigurationPurpose.Migration);
                var engine = new InitialAdministratorProvisioningEngine(new BcryptPasswordHasher());
                var inspection = engine.InspectAsync(
                        connection.ConnectionString,
                        System.Threading.CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                if (!options.Apply)
                {
                    WriteInspection(output, inspection);
                    return inspection.Status == InitialAdministratorProvisioningStatus.RecoveryRequired ? 3 : 0;
                }

                if (inspection.Status == InitialAdministratorProvisioningStatus.AlreadyCompleted)
                {
                    WriteInspection(output, inspection);
                    return 0;
                }

                if (inspection.Status != InitialAdministratorProvisioningStatus.Ready)
                {
                    throw new InitialAdministratorProvisioningException(
                        inspection.ReasonCode,
                        "The target is not ready for initial Administrator enrollment.");
                }

                if (!secretReader.IsInteractive)
                {
                    throw new InitialAdministratorProvisioningException(
                        "SecurityBootstrap.InteractiveInputRequired",
                        "Apply requires an interactive terminal for masked password entry.");
                }

                if (options.ConfirmedDatabaseIdentity != inspection.DatabaseIdentity)
                {
                    throw new InitialAdministratorProvisioningException(
                        "SecurityBootstrap.TargetMismatch",
                        "The --confirm-database identity does not match the inspected target.");
                }

                var password = secretReader.ReadSecret("Password: ", output);
                var confirmation = secretReader.ReadSecret("Confirm password: ", output);
                if (!string.Equals(password, confirmation, StringComparison.Ordinal))
                {
                    throw new InitialAdministratorProvisioningException(
                        "SecurityBootstrap.PasswordConfirmationMismatch",
                        "Password confirmation did not match.");
                }

                var result = engine.ProvisionAsync(
                        connection.ConnectionString,
                        new InitialAdministratorProvisioningRequest(
                            options.ConfirmedDatabaseIdentity,
                            options.Username!,
                            options.DisplayName!,
                            password,
                            options.Actor!,
                            options.ReleaseId!),
                        utcNow,
                        System.Threading.CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                output.WriteLine(JsonConvert.SerializeObject(new
                {
                    operation = "security bootstrap-admin --apply",
                    outcome = result.Outcome.ToString(),
                    databaseIdentity = inspection.DatabaseIdentity,
                    targetDatabase = inspection.TargetDatabase,
                }));
                return 0;
            }
            catch (InitialAdministratorProvisioningException exception)
            {
                error.WriteLine(exception.Code + ": " + exception.Message);
                return 3;
            }
            catch (OperationalConfigurationException exception)
            {
                error.WriteLine(exception.Code + ": " + exception.SafeMessage);
                return 2;
            }
            catch (ProvisioningCommandException exception)
            {
                error.WriteLine(exception.Code + ": " + exception.Message);
                return 2;
            }
            catch (DatabaseMigrationAuthorizationException exception)
            {
                error.WriteLine(exception.Code + ": " + exception.Message);
                return 4;
            }
            catch (Exception)
            {
                error.WriteLine(
                    "SecurityBootstrap.Failed: Enrollment failed. Review the protected administrator log.");
                return 1;
            }
        }

        private static CommandOptions Parse(string[] arguments)
        {
            if (Array.Exists(arguments, argument =>
                argument.StartsWith("--password", StringComparison.Ordinal)))
            {
                throw Usage("Passwords are never accepted as command-line arguments.");
            }

            var apply = Array.IndexOf(arguments, "--apply") >= 0;
            var dryRun = Array.IndexOf(arguments, "--dry-run") >= 0;
            var nonInteractive = Array.IndexOf(arguments, "--non-interactive") >= 0;
            if (apply && (dryRun || nonInteractive))
            {
                throw Usage("--non-interactive and --dry-run never create an account.");
            }

            if (!apply && !dryRun && !nonInteractive)
            {
                throw Usage("Specify --dry-run, --non-interactive, or --apply.");
            }

            if (!apply)
            {
                return new CommandOptions(false, Guid.Empty, null, null, null, null);
            }

            Guid confirmedDatabaseIdentity;
            if (!Guid.TryParse(ValueAfter(arguments, "--confirm-database"), out confirmedDatabaseIdentity) ||
                confirmedDatabaseIdentity == Guid.Empty)
            {
                throw Usage("Apply requires --confirm-database with the identity printed by dry-run.");
            }

            var username = Required(arguments, "--username");
            var displayName = Required(arguments, "--display-name");
            var actor = Required(arguments, "--actor");
            var releaseId = Required(arguments, "--release-id");
            return new CommandOptions(
                true,
                confirmedDatabaseIdentity,
                username,
                displayName,
                actor,
                releaseId);
        }

        private static string Required(string[] arguments, string option)
        {
            var value = ValueAfter(arguments, option);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Usage("Missing required option " + option + ".");
            }

            return value!;
        }

        private static string? ValueAfter(string[] arguments, string option)
        {
            var index = Array.IndexOf(arguments, option);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        private static ProvisioningCommandException Usage(string message)
        {
            return new ProvisioningCommandException("SecurityBootstrap.InvalidArguments", message);
        }

        private static void WriteInspection(
            TextWriter output,
            InitialAdministratorProvisioningInspection inspection)
        {
            output.WriteLine(JsonConvert.SerializeObject(new
            {
                operation = "security bootstrap-admin --dry-run",
                status = inspection.Status.ToString(),
                inspection.ReasonCode,
                databaseIdentity = inspection.DatabaseIdentity,
                targetDatabase = inspection.TargetDatabase,
            }, Formatting.Indented));
        }

        private sealed class CommandOptions
        {
            public CommandOptions(
                bool apply,
                Guid confirmedDatabaseIdentity,
                string? username,
                string? displayName,
                string? actor,
                string? releaseId)
            {
                Apply = apply;
                ConfirmedDatabaseIdentity = confirmedDatabaseIdentity;
                Username = username;
                DisplayName = displayName;
                Actor = actor;
                ReleaseId = releaseId;
            }

            public bool Apply { get; }
            public Guid ConfirmedDatabaseIdentity { get; }
            public string? Username { get; }
            public string? DisplayName { get; }
            public string? Actor { get; }
            public string? ReleaseId { get; }
        }
    }

    public sealed class ProvisioningCommandException : Exception
    {
        internal ProvisioningCommandException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
