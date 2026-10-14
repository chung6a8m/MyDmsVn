using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Operations;
using Newtonsoft.Json;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning
{
    public static class PermissionSyncCommand
    {
        public static int Run(
            string[] arguments,
            SqlConnectionConfigurationLoader configurationLoader,
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

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            try
            {
                var options = Parse(arguments);
                var configuration = configurationLoader.Load(SqlConnectionConfigurationPurpose.Migration);
                var registry = PermissionRegistryComposition.CreateDefault();
                var approvals = LoadApprovals(options.ApprovalPath);
                var engine = new PermissionSynchronizationEngine();
                var plan = engine.CreatePlanAsync(
                        configuration.ConnectionString,
                        registry,
                        approvals,
                        utcNow,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                if (!options.Apply)
                {
                    output.WriteLine(JsonConvert.SerializeObject(
                        new
                        {
                            operation = "permissions sync --dry-run",
                            planId = plan.PlanId,
                            releaseId = plan.ReleaseId,
                            databaseIdentity = plan.DatabaseIdentity,
                            targetDatabase = plan.TargetDatabase,
                            changes = plan.Changes.Select(
                                change => new { kind = change.Kind.ToString(), change.PermissionKey }),
                            conflicts = plan.Conflicts.Select(
                                conflict => new { conflict.Code, conflict.PermissionKey }),
                        },
                        Formatting.Indented));
                    return plan.Conflicts.Count == 0 ? 0 : 3;
                }

                var approvedPlan = new PermissionSynchronizationPlan(
                    options.PlanId!,
                    plan.ReleaseId,
                    plan.DatabaseIdentity,
                    plan.TargetDatabase,
                    plan.StateFingerprint,
                    plan.ManifestFingerprint,
                    plan.ApprovalFingerprint,
                    plan.Changes,
                    plan.Conflicts);
                var result = engine.ApplyAsync(
                        configuration.ConnectionString,
                        registry,
                        approvals,
                        approvedPlan,
                        utcNow,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                output.WriteLine(JsonConvert.SerializeObject(new
                {
                    operation = "permissions sync --apply",
                    planId = result.PlanId,
                    outcome = "Succeeded",
                    targetDatabase = plan.TargetDatabase,
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
            catch (PermissionSyncCommandException exception)
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
                    "PermissionSync.Failed: Permission synchronization failed. " +
                    "Review the protected administrator log.");
                return 1;
            }
        }

        private static CommandOptions Parse(string[] arguments)
        {
            var dryRun = arguments.Contains("--dry-run", StringComparer.Ordinal);
            var apply = arguments.Contains("--apply", StringComparer.Ordinal);
            if (dryRun == apply)
            {
                throw Usage("Specify exactly one of --dry-run or --apply.");
            }

            var approvalPath = ValueAfter(arguments, "--approvals");
            if (string.IsNullOrWhiteSpace(approvalPath))
            {
                throw Usage("Specify a reviewed approval artifact with --approvals.");
            }

            var planId = ValueAfter(arguments, "--plan-id");
            if (apply && (planId == null || planId.Length != 64 || !planId.All(IsHex)))
            {
                throw Usage("Apply requires the exact 64-character --plan-id from dry-run.");
            }

            return new CommandOptions(apply, approvalPath!, planId);
        }

        private static PermissionSyncApprovalSet LoadApprovals(string path)
        {
            try
            {
                var document = JsonConvert.DeserializeObject<ApprovalDocument>(File.ReadAllText(path));
                if (document == null || document.Permissions == null)
                {
                    throw Usage("The approval artifact is invalid.");
                }

                return new PermissionSyncApprovalSet(
                    document.ReleaseId ?? string.Empty,
                    document.ApprovedBy ?? string.Empty,
                    document.ExpiresAtUtc,
                    document.Permissions.Select(item => new PermissionSyncApproval(
                        item.PermissionKey ?? string.Empty,
                        ParseCatalogDecision(item.CatalogDecision),
                        ParseAdministratorDecision(item.AdministratorGrantDecision))));
            }
            catch (PermissionSyncCommandException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is JsonException ||
                exception is ArgumentException)
            {
                throw Usage("The approval artifact could not be read or validated.");
            }
        }

        private static PermissionCatalogDecision ParseCatalogDecision(string? value)
        {
            PermissionCatalogDecision decision;
            if (!Enum.TryParse(value, false, out decision) ||
                !Enum.IsDefined(typeof(PermissionCatalogDecision), decision))
            {
                throw Usage("The approval artifact contains an invalid catalog decision.");
            }

            return decision;
        }

        private static AdministratorGrantDecision ParseAdministratorDecision(string? value)
        {
            AdministratorGrantDecision decision;
            if (!Enum.TryParse(value, false, out decision) ||
                !Enum.IsDefined(typeof(AdministratorGrantDecision), decision))
            {
                throw Usage("The approval artifact contains an invalid Administrator decision.");
            }

            return decision;
        }

        private static string? ValueAfter(string[] arguments, string option)
        {
            var index = Array.IndexOf(arguments, option);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        private static bool IsHex(char value)
        {
            return (value >= '0' && value <= '9') || (value >= 'A' && value <= 'F');
        }

        private static PermissionSyncCommandException Usage(string message)
        {
            return new PermissionSyncCommandException("PermissionSync.InvalidArguments", message);
        }

        private sealed class CommandOptions
        {
            public CommandOptions(bool apply, string approvalPath, string? planId)
            {
                Apply = apply;
                ApprovalPath = approvalPath;
                PlanId = planId;
            }

            public bool Apply { get; }
            public string ApprovalPath { get; }
            public string? PlanId { get; }
        }

        private sealed class ApprovalDocument
        {
            public string? ReleaseId { get; set; }
            public string? ApprovedBy { get; set; }
            public DateTime ExpiresAtUtc { get; set; }
            public List<ApprovalItem>? Permissions { get; set; }
        }

        private sealed class ApprovalItem
        {
            public string? PermissionKey { get; set; }
            public string? CatalogDecision { get; set; }
            public string? AdministratorGrantDecision { get; set; }
        }
    }

    public sealed class PermissionSyncCommandException : Exception
    {
        internal PermissionSyncCommandException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
