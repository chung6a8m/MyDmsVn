using System;
using System.IO;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Operations;
using Newtonsoft.Json;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning
{
    public static class AdministratorGrantCommand
    {
        public static int Run(
            string[] arguments,
            SqlConnectionConfigurationLoader configurationLoader,
            TextWriter output,
            TextWriter error,
            DateTime utcNow)
        {
            try
            {
                var options = Parse(arguments);
                var configuration = configurationLoader.Load(SqlConnectionConfigurationPurpose.Migration);
                var inspection = new InitialAdministratorProvisioningEngine(new BcryptPasswordHasher())
                    .InspectAsync(configuration.ConnectionString, System.Threading.CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                if (inspection.DatabaseIdentity != options.ConfirmedDatabaseIdentity)
                {
                    throw new PermissionSynchronizationException(
                        "PermissionSync.TargetMismatch",
                        "The confirmed database identity does not match the target.");
                }

                var engine = new PermissionSynchronizationEngine();
                var operation = options.Restore ? "restore" : "revoke";
                var task = options.Restore
                    ? engine.RestoreAdministratorGrantAsync(
                        configuration.ConnectionString,
                        options.PermissionKey,
                        options.ReleaseId,
                        options.Actor,
                        options.Reason,
                        utcNow,
                        System.Threading.CancellationToken.None)
                    : engine.RevokeAdministratorGrantAsync(
                        configuration.ConnectionString,
                        options.PermissionKey,
                        options.ReleaseId,
                        options.Actor,
                        options.Reason,
                        utcNow,
                        System.Threading.CancellationToken.None);
                task.GetAwaiter().GetResult();
                output.WriteLine(JsonConvert.SerializeObject(new
                {
                    operation = "permissions administrator-grant --" + operation,
                    outcome = "Succeeded",
                    permissionKey = options.PermissionKey,
                    databaseIdentity = inspection.DatabaseIdentity,
                    targetDatabase = inspection.TargetDatabase,
                }));
                return 0;
            }
            catch (PermissionSynchronizationException exception)
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
                    "PermissionSync.GrantChangeFailed: The managed grant change failed. " +
                    "Review the protected administrator log.");
                return 1;
            }
        }

        private static Options Parse(string[] arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            var revoke = Array.IndexOf(arguments, "--revoke") >= 0;
            var restore = Array.IndexOf(arguments, "--restore") >= 0;
            if (revoke == restore)
            {
                throw Usage("Specify exactly one of --revoke or --restore.");
            }

            Guid databaseIdentity;
            if (!Guid.TryParse(ValueAfter(arguments, "--confirm-database"), out databaseIdentity) ||
                databaseIdentity == Guid.Empty)
            {
                throw Usage("Specify --confirm-database using the inspected database identity.");
            }

            return new Options(
                restore,
                Required(arguments, "--permission-key"),
                Required(arguments, "--release-id"),
                Required(arguments, "--actor"),
                Required(arguments, "--reason"),
                databaseIdentity);
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
            return new ProvisioningCommandException("PermissionSync.InvalidArguments", message);
        }

        private sealed class Options
        {
            public Options(
                bool restore,
                string permissionKey,
                string releaseId,
                string actor,
                string reason,
                Guid confirmedDatabaseIdentity)
            {
                Restore = restore;
                PermissionKey = permissionKey;
                ReleaseId = releaseId;
                Actor = actor;
                Reason = reason;
                ConfirmedDatabaseIdentity = confirmedDatabaseIdentity;
            }

            public bool Restore { get; }
            public string PermissionKey { get; }
            public string ReleaseId { get; }
            public string Actor { get; }
            public string Reason { get; }
            public Guid ConfirmedDatabaseIdentity { get; }
        }
    }
}
