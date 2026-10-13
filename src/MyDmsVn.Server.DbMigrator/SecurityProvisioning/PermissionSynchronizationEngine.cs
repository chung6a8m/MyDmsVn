using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning
{
    public sealed class PermissionSynchronizationEngine
    {
        private const string Operation = "PermissionSync.Apply";
        private const string LockResource = "MyDmsVn.PermissionSync";

        public async Task<PermissionSynchronizationPlan> CreatePlanAsync(
            string connectionString,
            IPermissionRegistry registry,
            PermissionSyncApprovalSet approvals,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            ValidateInputs(connectionString, registry, approvals, utcNow);

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                DatabaseMigrationPrincipalValidator.Validate(connection);
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    await AcquireLockAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                    var state = await ReadStateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                    var plan = BuildPlan(registry, approvals, state);
                    transaction.Commit();
                    return plan;
                }
            }
        }

        public async Task<PermissionSynchronizationResult> ApplyAsync(
            string connectionString,
            IPermissionRegistry registry,
            PermissionSyncApprovalSet approvals,
            PermissionSynchronizationPlan plan,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            ValidateInputs(connectionString, registry, approvals, utcNow);
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                DatabaseMigrationPrincipalValidator.Validate(connection);
                Guid databaseIdentity = plan.DatabaseIdentity;
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        if (approvals.ExpiresAtUtc <= utcNow)
                        {
                            throw new PermissionSynchronizationException(
                                "PermissionSync.ApprovalExpired",
                                "The reviewed approval set has expired.");
                        }

                        await AcquireLockAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                        var state = await ReadStateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                        databaseIdentity = state.DatabaseIdentity;
                        var currentPlan = BuildPlan(registry, approvals, state);
                        EnsurePlanIsCurrent(plan, currentPlan);

                        if (currentPlan.Conflicts.Count > 0)
                        {
                            throw new PermissionSynchronizationException(
                                "PermissionSync.PlanConflict",
                                "The synchronization plan contains unresolved conflicts.");
                        }

                        await ApplyDefinitionsAsync(
                            connection,
                            transaction,
                            registry,
                            approvals,
                            currentPlan,
                            state,
                            utcNow,
                            cancellationToken).ConfigureAwait(false);
                        await ApplyApprovalsAsync(
                            connection,
                            transaction,
                            registry,
                            approvals,
                            currentPlan,
                            utcNow,
                            cancellationToken).ConfigureAwait(false);
                        await ApplyAdministratorPolicyAsync(
                            connection,
                            transaction,
                            currentPlan,
                            registry,
                            approvals,
                            state,
                            utcNow,
                            cancellationToken).ConfigureAwait(false);
                        await UpdateSynchronizationStateAsync(
                            connection,
                            transaction,
                            currentPlan,
                            approvals,
                            utcNow,
                            cancellationToken).ConfigureAwait(false);
                        await WriteAuditAsync(
                            connection,
                            transaction,
                            Guid.NewGuid(),
                            currentPlan,
                            approvals,
                            databaseIdentity,
                            "Succeeded",
                            "PermissionSync.Applied",
                            Summarize(currentPlan),
                            utcNow,
                            cancellationToken).ConfigureAwait(false);
                        transaction.Commit();
                        return new PermissionSynchronizationResult(currentPlan.PlanId);
                    }
                    catch (Exception exception)
                    {
                        transaction.Rollback();
                        var synchronizationException = exception as PermissionSynchronizationException;
                        var outcome = synchronizationException == null ? "Failed" : "Rejected";
                        var reasonCode = synchronizationException?.Code ?? "PermissionSync.Failed";
                        await WriteAuditAsync(
                            connection,
                            null,
                            Guid.NewGuid(),
                            plan,
                            approvals,
                            databaseIdentity,
                            outcome,
                            reasonCode,
                            null,
                            utcNow,
                            CancellationToken.None).ConfigureAwait(false);

                        if (synchronizationException != null)
                        {
                            throw;
                        }

                        throw new PermissionSynchronizationException(
                            "PermissionSync.Failed",
                            "Permission synchronization failed without committing changes.");
                    }
                }
            }
        }

        public Task RevokeAdministratorGrantAsync(
            string connectionString,
            string permissionKey,
            string releaseId,
            string actor,
            string reason,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            return ChangeAdministratorGrantStateAsync(
                connectionString,
                permissionKey,
                releaseId,
                actor,
                reason,
                utcNow,
                restore: false,
                cancellationToken);
        }

        public Task RestoreAdministratorGrantAsync(
            string connectionString,
            string permissionKey,
            string releaseId,
            string actor,
            string reason,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            return ChangeAdministratorGrantStateAsync(
                connectionString,
                permissionKey,
                releaseId,
                actor,
                reason,
                utcNow,
                restore: true,
                cancellationToken);
        }

        private static async Task ChangeAdministratorGrantStateAsync(
            string connectionString,
            string permissionKey,
            string releaseId,
            string actor,
            string reason,
            DateTime utcNow,
            bool restore,
            CancellationToken cancellationToken)
        {
            ValidateGrantChangeInputs(
                connectionString,
                permissionKey,
                releaseId,
                actor,
                reason,
                utcNow);
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                DatabaseMigrationPrincipalValidator.Validate(connection);
                Guid databaseIdentity = Guid.Empty;
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        await AcquireLockAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                        databaseIdentity = await connection.QuerySingleAsync<Guid>(new CommandDefinition(
                            "SELECT DatabaseIdentity FROM dbo.PermissionSynchronizationState WHERE StateId = 1;",
                            transaction: transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                        var grant = await connection.QuerySingleOrDefaultAsync<AdministratorGrantRow>(
                            new CommandDefinition(
                                "SELECT managed.ManagedRoleId, managed.RoleId, managedGrant.GrantState, " +
                                "permission.CatalogStatus, permission.AdministratorGrantPolicy, " +
                                "approval.AdministratorGrantDecision, approval.DecisionReleaseId, " +
                                "approval.ExpiresAtUtc " +
                                "FROM dbo.SystemManagedRoles AS managed " +
                                "JOIN dbo.ManagedRolePermissionGrants AS managedGrant " +
                                "ON managedGrant.ManagedRoleId = managed.ManagedRoleId " +
                                "JOIN dbo.Permissions AS permission " +
                                "ON permission.PermissionKey = managedGrant.PermissionKey " +
                                "LEFT JOIN dbo.PermissionGrantApprovals AS approval " +
                                "ON approval.PermissionKey = managedGrant.PermissionKey " +
                                "WHERE managed.SystemRoleKey = 'Administrator' " +
                                "AND managedGrant.PermissionKey = @PermissionKey;",
                                new { PermissionKey = permissionKey },
                                transaction,
                                cancellationToken: cancellationToken)).ConfigureAwait(false);
                        if (grant == null)
                        {
                            throw new PermissionSynchronizationException(
                                "PermissionSync.ManagedGrantMissing",
                                "The requested managed Administrator grant does not exist.");
                        }

                        if (restore)
                        {
                            ValidateRestore(grant, releaseId, utcNow);
                            await connection.ExecuteAsync(new CommandDefinition(
                                "IF NOT EXISTS (SELECT 1 FROM dbo.RolePermissions " +
                                "WHERE RoleId = @RoleId AND PermissionKey = @PermissionKey) " +
                                "INSERT dbo.RolePermissions (RoleId, PermissionKey) " +
                                "VALUES (@RoleId, @PermissionKey);",
                                new { grant.RoleId, PermissionKey = permissionKey },
                                transaction,
                                cancellationToken: cancellationToken)).ConfigureAwait(false);
                        }
                        else
                        {
                            if (!string.Equals(grant.GrantState, "Granted", StringComparison.Ordinal))
                            {
                                throw new PermissionSynchronizationException(
                                    "PermissionSync.ManagedGrantNotGranted",
                                    "Only a currently granted managed permission can be revoked.");
                            }

                            await connection.ExecuteAsync(new CommandDefinition(
                                "DELETE dbo.RolePermissions " +
                                "WHERE RoleId = @RoleId AND PermissionKey = @PermissionKey;",
                                new { grant.RoleId, PermissionKey = permissionKey },
                                transaction,
                                cancellationToken: cancellationToken)).ConfigureAwait(false);
                        }

                        var expectedState = restore ? "Revoked" : "Granted";
                        var targetState = restore ? "Granted" : "Revoked";
                        var affected = await connection.ExecuteAsync(new CommandDefinition(
                            "UPDATE dbo.ManagedRolePermissionGrants SET GrantState = @TargetState, " +
                            "StateReleaseId = @ReleaseId, StateChangedAtUtc = @UtcNow, " +
                            "StateChangedBy = @Actor, Reason = @Reason " +
                            "WHERE ManagedRoleId = @ManagedRoleId AND PermissionKey = @PermissionKey " +
                            "AND GrantState = @ExpectedState;",
                            new
                            {
                                TargetState = targetState,
                                ReleaseId = releaseId,
                                UtcNow = utcNow,
                                Actor = actor,
                                Reason = reason,
                                grant.ManagedRoleId,
                                PermissionKey = permissionKey,
                                ExpectedState = expectedState,
                            },
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                        if (affected != 1)
                        {
                            throw new PermissionSynchronizationException(
                                "PermissionSync.ManagedGrantStateChanged",
                                "The managed grant state no longer matches the reviewed operation.");
                        }

                        await WriteGrantChangeAuditAsync(
                            connection,
                            transaction,
                            databaseIdentity,
                            releaseId,
                            actor,
                            restore,
                            "Succeeded",
                            restore ? "PermissionSync.GrantRestored" : "PermissionSync.GrantRevoked",
                            permissionKey,
                            utcNow,
                            cancellationToken).ConfigureAwait(false);
                        transaction.Commit();
                    }
                    catch (Exception exception)
                    {
                        transaction.Rollback();
                        var known = exception as PermissionSynchronizationException;
                        await WriteGrantChangeAuditAsync(
                            connection,
                            null,
                            databaseIdentity,
                            releaseId,
                            actor,
                            restore,
                            known == null ? "Failed" : "Rejected",
                            known?.Code ?? "PermissionSync.GrantChangeFailed",
                            permissionKey,
                            utcNow,
                            CancellationToken.None).ConfigureAwait(false);
                        if (known != null)
                        {
                            throw;
                        }

                        throw new PermissionSynchronizationException(
                            "PermissionSync.GrantChangeFailed",
                            "The managed Administrator grant change failed without committing.");
                    }
                }
            }
        }

        private static void ValidateGrantChangeInputs(
            string connectionString,
            string permissionKey,
            string releaseId,
            string actor,
            string reason,
            DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("A database connection string is required.", nameof(connectionString));
            }

            if (string.IsNullOrWhiteSpace(permissionKey) || permissionKey.Length > 100)
            {
                throw new ArgumentException("A valid permission key is required.", nameof(permissionKey));
            }

            if (string.IsNullOrWhiteSpace(releaseId) || releaseId.Length > 64)
            {
                throw new ArgumentException("A valid release identifier is required.", nameof(releaseId));
            }

            if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
            {
                throw new ArgumentException("A valid operator identity is required.", nameof(actor));
            }

            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            {
                throw new ArgumentException("A review reason of at most 500 characters is required.", nameof(reason));
            }

            if (utcNow.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("The grant change time must be UTC.", nameof(utcNow));
            }
        }

        private static void ValidateRestore(
            AdministratorGrantRow grant,
            string releaseId,
            DateTime utcNow)
        {
            if (!string.Equals(grant.GrantState, "Revoked", StringComparison.Ordinal))
            {
                throw new PermissionSynchronizationException(
                    "PermissionSync.ManagedGrantNotRevoked",
                    "Only a revoked managed permission can be restored.");
            }

            if (!string.Equals(grant.CatalogStatus, "Active", StringComparison.Ordinal) ||
                string.Equals(grant.AdministratorGrantPolicy, "NotEligible", StringComparison.Ordinal) ||
                !string.Equals(grant.AdministratorGrantDecision, "Approved", StringComparison.Ordinal) ||
                !string.Equals(grant.DecisionReleaseId, releaseId, StringComparison.Ordinal) ||
                !grant.ExpiresAtUtc.HasValue ||
                grant.ExpiresAtUtc.Value <= utcNow)
            {
                throw new PermissionSynchronizationException(
                    "PermissionSync.RestoreNotApproved",
                    "Restore requires a current, unexpired Administrator grant approval for an active key.");
            }
        }

        private static Task<int> WriteGrantChangeAuditAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            Guid databaseIdentity,
            string releaseId,
            string actor,
            bool restore,
            string outcome,
            string reasonCode,
            string permissionKey,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            const string sql =
                "INSERT dbo.PermissionSecurityAudit " +
                "(AttemptId, Operation, ReleaseId, DatabaseIdentity, Actor, Outcome, " +
                "ReasonCode, ChangeSummary, OccurredAtUtc) VALUES " +
                "(@AttemptId, @Operation, @ReleaseId, @DatabaseIdentity, @Actor, @Outcome, " +
                "@ReasonCode, @ChangeSummary, @UtcNow);";
            return connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    AttemptId = Guid.NewGuid(),
                    Operation = restore ? "PermissionSync.Restore" : "PermissionSync.Revoke",
                    ReleaseId = releaseId,
                    DatabaseIdentity = databaseIdentity,
                    Actor = actor,
                    Outcome = outcome,
                    ReasonCode = reasonCode,
                    ChangeSummary = "PermissionKey=" + permissionKey,
                    UtcNow = utcNow,
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        private static void ValidateInputs(
            string connectionString,
            IPermissionRegistry registry,
            PermissionSyncApprovalSet approvals,
            DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("A database connection string is required.", nameof(connectionString));
            }

            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (approvals == null)
            {
                throw new ArgumentNullException(nameof(approvals));
            }

            if (utcNow.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("The synchronization time must be UTC.", nameof(utcNow));
            }

            if (!string.Equals(registry.ReleaseId, approvals.ReleaseId, StringComparison.Ordinal))
            {
                throw new PermissionSynchronizationException(
                    "PermissionSync.ReleaseMismatch",
                    "The approval release does not match the permission manifest release.");
            }

            var manifestKeys = new HashSet<string>(
                registry.Definitions.Select(definition => definition.Key),
                StringComparer.Ordinal);
            var approvalKeys = new HashSet<string>(
                approvals.Approvals.Select(approval => approval.PermissionKey),
                StringComparer.Ordinal);
            if (!manifestKeys.SetEquals(approvalKeys))
            {
                throw new PermissionSynchronizationException(
                    "PermissionSync.ApprovalIncomplete",
                    "The approval set must contain exactly one decision for every manifest permission.");
            }
        }

        private static async Task AcquireLockAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken)
        {
            const string sql =
                "DECLARE @Result int; " +
                "EXEC @Result = sys.sp_getapplock " +
                "@Resource = @Resource, @LockMode = N'Exclusive', " +
                "@LockOwner = N'Transaction', @LockTimeout = 0; " +
                "SELECT @Result;";
            var result = await connection.QuerySingleAsync<int>(new CommandDefinition(
                sql,
                new { Resource = LockResource },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (result < 0)
            {
                throw new PermissionSynchronizationException(
                    "PermissionSync.Busy",
                    "Another permission synchronization operation is in progress.");
            }
        }

        private static async Task<DatabaseState> ReadStateAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken)
        {
            const string sql =
                "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ServerName, " +
                "DB_NAME() AS DatabaseName, DatabaseIdentity, LastAppliedPlanId, " +
                "LastAppliedReleaseId, LastAppliedAtUtc, LastAppliedBy, RowVersion " +
                "FROM dbo.PermissionSynchronizationState WHERE StateId = 1; " +
                "SELECT PermissionKey, Module, Description, CatalogStatus, " +
                "AdministratorGrantPolicy, IntroducedReleaseId, DeprecatedReleaseId, " +
                "ActivatedAtUtc, ActivatedBy, UpdatedAtUtc, UpdatedBy, RowVersion " +
                "FROM dbo.Permissions ORDER BY PermissionKey; " +
                "SELECT PermissionKey, CatalogDecision, AdministratorGrantDecision, " +
                "DecisionReleaseId, DecisionBy, DecisionAtUtc, ExpiresAtUtc, RowVersion " +
                "FROM dbo.PermissionGrantApprovals ORDER BY PermissionKey; " +
                "SELECT ManagedRoleId, SystemRoleKey, RoleId, ExpectedNormalizedRoleName, " +
                "CreatedAtUtc, CreatedBy FROM dbo.SystemManagedRoles ORDER BY ManagedRoleId; " +
                "SELECT RoleId, RoleName, NormalizedRoleName, IsActive, RowVersion " +
                "FROM dbo.Roles ORDER BY RoleId; " +
                "SELECT ManagedRolePermissionGrantId, ManagedRoleId, PermissionKey, GrantState, " +
                "StateReleaseId, StateChangedAtUtc, StateChangedBy, Reason, RowVersion " +
                "FROM dbo.ManagedRolePermissionGrants " +
                "ORDER BY ManagedRoleId, PermissionKey; " +
                "SELECT RoleId, PermissionKey FROM dbo.RolePermissions " +
                "ORDER BY RoleId, PermissionKey; " +
                "SELECT UserId, PermissionKey, Granted, RowVersion FROM dbo.UserPermissions " +
                "ORDER BY UserId, PermissionKey; " +
                "SELECT PermissionKey, RoleGrantCount, UserGrantCount, ReviewStatus, " +
                "FirstObservedAtUtc, ReviewedAtUtc, ReviewedBy " +
                "FROM dbo.PermissionLegacyAssignmentDrift ORDER BY PermissionKey;";

            using (var grid = await connection.QueryMultipleAsync(new CommandDefinition(
                sql,
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false))
            {
                var synchronizationState = await grid.ReadSingleAsync<SynchronizationStateRow>()
                    .ConfigureAwait(false);
                var permissions = (await grid.ReadAsync<PermissionRow>().ConfigureAwait(false)).ToArray();
                var approvalRows = (await grid.ReadAsync<ApprovalRow>().ConfigureAwait(false)).ToArray();
                var managedRoles = (await grid.ReadAsync<ManagedRoleRow>().ConfigureAwait(false)).ToArray();
                var roles = (await grid.ReadAsync<RoleRow>().ConfigureAwait(false)).ToArray();
                var managedGrants = (await grid.ReadAsync<ManagedGrantRow>().ConfigureAwait(false)).ToArray();
                var roleAssignments = (await grid.ReadAsync<RoleAssignmentRow>().ConfigureAwait(false)).ToArray();
                var userAssignments = (await grid.ReadAsync<UserAssignmentRow>().ConfigureAwait(false)).ToArray();
                var legacyDrift = (await grid.ReadAsync<LegacyDriftRow>().ConfigureAwait(false)).ToArray();
                return new DatabaseState(
                    synchronizationState,
                    permissions,
                    approvalRows,
                    managedRoles,
                    roles,
                    managedGrants,
                    roleAssignments,
                    userAssignments,
                    legacyDrift);
            }
        }

        private static PermissionSynchronizationPlan BuildPlan(
            IPermissionRegistry registry,
            PermissionSyncApprovalSet approvals,
            DatabaseState state)
        {
            var changes = new List<PermissionSyncChange>();
            var conflicts = new List<PermissionSyncConflict>();
            var databaseByKey = state.Permissions.ToDictionary(row => row.PermissionKey, StringComparer.Ordinal);
            var manifestKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var definition in registry.Definitions)
            {
                manifestKeys.Add(definition.Key);
                var approval = GetApproval(approvals, definition.Key);
                PermissionRow row;
                if (!databaseByKey.TryGetValue(definition.Key, out row!))
                {
                    changes.Add(new PermissionSyncChange(PermissionSyncChangeKind.AddDefinition, definition.Key));
                }
                else if (!DefinitionMatches(row, definition))
                {
                    changes.Add(new PermissionSyncChange(PermissionSyncChangeKind.UpdateDefinition, definition.Key));
                }

                var currentStatus = row?.CatalogStatus;
                var desiredStatus = DesiredStatus(definition, approval, currentStatus);
                if (!string.Equals(currentStatus, desiredStatus, StringComparison.Ordinal))
                {
                    var changeKind = desiredStatus == "Active"
                        ? PermissionSyncChangeKind.ActivateDefinition
                        : desiredStatus == "Deprecated"
                            ? PermissionSyncChangeKind.DeprecateDefinition
                            : PermissionSyncChangeKind.StageDefinition;
                    changes.Add(new PermissionSyncChange(changeKind, definition.Key));
                }

                var currentApproval = state.Approvals.FirstOrDefault(
                    item => string.Equals(item.PermissionKey, definition.Key, StringComparison.Ordinal));
                if (!ApprovalMatches(currentApproval, approval, approvals))
                {
                    changes.Add(new PermissionSyncChange(
                        PermissionSyncChangeKind.UpdateApproval,
                        definition.Key));
                }
            }

            foreach (var row in state.Permissions)
            {
                if (!manifestKeys.Contains(row.PermissionKey) && row.CatalogStatus == "Unregistered")
                {
                    changes.Add(new PermissionSyncChange(
                        PermissionSyncChangeKind.ReportUnregisteredDefinition,
                        row.PermissionKey));
                }
                else if (!manifestKeys.Contains(row.PermissionKey))
                {
                    conflicts.Add(new PermissionSyncConflict("PermissionSync.ManifestOmission", row.PermissionKey));
                }
            }

            AddAdministratorPolicyChanges(
                registry,
                approvals,
                state,
                changes,
                conflicts);

            var stateFingerprint = FingerprintState(state);
            var manifestFingerprint = FingerprintManifest(registry);
            var approvalFingerprint = FingerprintApprovals(approvals);
            var targetDatabase = state.SynchronizationState.ServerName + "/" +
                                 state.SynchronizationState.DatabaseName;
            var planId = Hash(
                registry.ReleaseId,
                state.DatabaseIdentity.ToString("D"),
                targetDatabase,
                stateFingerprint,
                manifestFingerprint,
                approvalFingerprint);
            return new PermissionSynchronizationPlan(
                planId,
                registry.ReleaseId,
                state.DatabaseIdentity,
                targetDatabase,
                stateFingerprint,
                manifestFingerprint,
                approvalFingerprint,
                changes.OrderBy(change => change.PermissionKey, StringComparer.Ordinal)
                    .ThenBy(change => change.Kind),
                conflicts.OrderBy(conflict => conflict.PermissionKey, StringComparer.Ordinal));
        }

        private static void AddAdministratorPolicyChanges(
            IPermissionRegistry registry,
            PermissionSyncApprovalSet approvals,
            DatabaseState state,
            ICollection<PermissionSyncChange> changes,
            ICollection<PermissionSyncConflict> conflicts)
        {
            var managedRole = state.ManagedRoles.FirstOrDefault(role =>
                string.Equals(role.SystemRoleKey, "Administrator", StringComparison.Ordinal));
            if (managedRole == null)
            {
                var collision = state.Roles.FirstOrDefault(role =>
                    string.Equals(
                        role.NormalizedRoleName,
                        "ADMINISTRATOR",
                        StringComparison.OrdinalIgnoreCase));
                if (collision != null)
                {
                    conflicts.Add(new PermissionSyncConflict(
                        "PermissionSync.AdministratorRoleOwnershipCollision",
                        "Administrator"));
                    return;
                }

                changes.Add(new PermissionSyncChange(
                    PermissionSyncChangeKind.CreateAdministratorRole,
                    "Administrator"));
                foreach (var definition in registry.Definitions)
                {
                    if (ShouldGrantToAdministrator(definition, GetApproval(approvals, definition.Key)))
                    {
                        changes.Add(new PermissionSyncChange(
                            PermissionSyncChangeKind.GrantAdministratorPermission,
                            definition.Key));
                    }
                }

                return;
            }

            var roleRow = state.Roles.FirstOrDefault(role => role.RoleId == managedRole.RoleId);
            if (roleRow == null ||
                !roleRow.IsActive ||
                !string.Equals(roleRow.NormalizedRoleName, "ADMINISTRATOR", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    managedRole.ExpectedNormalizedRoleName,
                    "ADMINISTRATOR",
                    StringComparison.OrdinalIgnoreCase))
            {
                conflicts.Add(new PermissionSyncConflict(
                    "PermissionSync.AdministratorRoleOwnershipInvalid",
                    "Administrator"));
                return;
            }

            foreach (var definition in registry.Definitions)
            {
                var assignmentExists = state.RoleAssignments.Any(assignment =>
                    assignment.RoleId == managedRole.RoleId &&
                    string.Equals(assignment.PermissionKey, definition.Key, StringComparison.Ordinal));
                var managedGrant = state.ManagedGrants.FirstOrDefault(grant =>
                    grant.ManagedRoleId == managedRole.ManagedRoleId &&
                    string.Equals(grant.PermissionKey, definition.Key, StringComparison.Ordinal));
                if (managedGrant == null)
                {
                    if (assignmentExists)
                    {
                        changes.Add(new PermissionSyncChange(
                            PermissionSyncChangeKind.ReportManualAdministratorGrant,
                            definition.Key));
                    }
                    else if (ShouldGrantToAdministrator(
                        definition,
                        GetApproval(approvals, definition.Key)))
                    {
                        changes.Add(new PermissionSyncChange(
                            PermissionSyncChangeKind.GrantAdministratorPermission,
                            definition.Key));
                    }

                    continue;
                }

                if (string.Equals(managedGrant.GrantState, "Granted", StringComparison.Ordinal) &&
                    !assignmentExists)
                {
                    changes.Add(new PermissionSyncChange(
                        PermissionSyncChangeKind.RecordAdministratorRevocation,
                        definition.Key));
                }
                else if (!string.Equals(managedGrant.GrantState, "Granted", StringComparison.Ordinal) &&
                         assignmentExists)
                {
                    conflicts.Add(new PermissionSyncConflict(
                        "PermissionSync.ManagedGrantAssignmentConflict",
                        definition.Key));
                }
                else if (string.Equals(managedGrant.GrantState, "NotGranted", StringComparison.Ordinal) &&
                         !assignmentExists &&
                         ShouldGrantToAdministrator(
                             definition,
                             GetApproval(approvals, definition.Key)))
                {
                    changes.Add(new PermissionSyncChange(
                        PermissionSyncChangeKind.GrantAdministratorPermission,
                        definition.Key));
                }
            }
        }

        private static bool ShouldGrantToAdministrator(
            PermissionDefinition definition,
            PermissionSyncApproval approval)
        {
            return definition.LifecycleStatus == PermissionLifecycleStatus.Active &&
                   definition.AdministratorGrantPolicy != AdministratorGrantPolicy.NotEligible &&
                   approval.CatalogDecision == PermissionCatalogDecision.Approved &&
                   approval.AdministratorGrantDecision == AdministratorGrantDecision.Approved;
        }

        private static void EnsurePlanIsCurrent(
            PermissionSynchronizationPlan supplied,
            PermissionSynchronizationPlan current)
        {
            if (supplied.DatabaseIdentity != current.DatabaseIdentity ||
                !string.Equals(supplied.TargetDatabase, current.TargetDatabase, StringComparison.Ordinal) ||
                !string.Equals(supplied.StateFingerprint, current.StateFingerprint, StringComparison.Ordinal) ||
                !string.Equals(supplied.ManifestFingerprint, current.ManifestFingerprint, StringComparison.Ordinal) ||
                !string.Equals(supplied.ApprovalFingerprint, current.ApprovalFingerprint, StringComparison.Ordinal) ||
                !string.Equals(supplied.PlanId, current.PlanId, StringComparison.Ordinal))
            {
                throw new PermissionSynchronizationException(
                    "PermissionSync.StalePlan",
                    "The reviewed plan no longer matches the target database or inputs.");
            }
        }

        private static async Task ApplyDefinitionsAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            IPermissionRegistry registry,
            PermissionSyncApprovalSet approvals,
            PermissionSynchronizationPlan plan,
            DatabaseState state,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var existingKeys = new HashSet<string>(
                state.Permissions.Select(row => row.PermissionKey),
                StringComparer.Ordinal);
            foreach (var definition in registry.Definitions)
            {
                if (!plan.Changes.Any(change =>
                    string.Equals(change.PermissionKey, definition.Key, StringComparison.Ordinal) &&
                    (change.Kind == PermissionSyncChangeKind.AddDefinition ||
                     change.Kind == PermissionSyncChangeKind.UpdateDefinition ||
                     change.Kind == PermissionSyncChangeKind.ActivateDefinition ||
                     change.Kind == PermissionSyncChangeKind.DeprecateDefinition ||
                     change.Kind == PermissionSyncChangeKind.StageDefinition)))
                {
                    continue;
                }

                var approval = GetApproval(approvals, definition.Key);
                var existing = state.Permissions.FirstOrDefault(
                    row => string.Equals(row.PermissionKey, definition.Key, StringComparison.Ordinal));
                var desiredStatus = DesiredStatus(definition, approval, existing?.CatalogStatus);
                var activatedAt = desiredStatus == "Active" ? (DateTime?)utcNow : null;
                var activatedBy = desiredStatus == "Active" ? approvals.ApprovedBy : null;
                var policy = PolicyValue(definition.AdministratorGrantPolicy);

                if (!existingKeys.Contains(definition.Key))
                {
                    const string insertSql =
                        "INSERT dbo.Permissions " +
                        "(PermissionKey, Module, Description, CatalogStatus, AdministratorGrantPolicy, " +
                        "IntroducedReleaseId, DeprecatedReleaseId, ActivatedAtUtc, ActivatedBy, " +
                        "CreatedAtUtc, UpdatedAtUtc, UpdatedBy) " +
                        "VALUES (@PermissionKey, @Module, @Description, @CatalogStatus, @Policy, " +
                        "@ReleaseId, @DeprecatedReleaseId, @ActivatedAtUtc, @ActivatedBy, " +
                        "@UtcNow, @UtcNow, @Actor);";
                    await connection.ExecuteAsync(new CommandDefinition(
                        insertSql,
                        new
                        {
                            PermissionKey = definition.Key,
                            definition.Module,
                            definition.Description,
                            CatalogStatus = desiredStatus,
                            Policy = policy,
                            ReleaseId = registry.ReleaseId,
                            DeprecatedReleaseId = desiredStatus == "Deprecated" ? registry.ReleaseId : null,
                            ActivatedAtUtc = activatedAt,
                            ActivatedBy = activatedBy,
                            UtcNow = utcNow,
                            Actor = approvals.ApprovedBy,
                        },
                        transaction,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);

                }
                else
                {
                    const string updateSql =
                        "UPDATE dbo.Permissions SET Module = @Module, Description = @Description, " +
                        "CatalogStatus = @CatalogStatus, AdministratorGrantPolicy = @Policy, " +
                        "IntroducedReleaseId = COALESCE(IntroducedReleaseId, @ReleaseId), " +
                        "DeprecatedReleaseId = CASE WHEN @CatalogStatus = 'Deprecated' " +
                        "THEN COALESCE(DeprecatedReleaseId, @ReleaseId) ELSE NULL END, " +
                        "ActivatedAtUtc = CASE WHEN @CatalogStatus = 'Active' " +
                        "THEN COALESCE(ActivatedAtUtc, @UtcNow) ELSE NULL END, " +
                        "ActivatedBy = CASE WHEN @CatalogStatus = 'Active' " +
                        "THEN COALESCE(ActivatedBy, @Actor) ELSE NULL END, " +
                        "UpdatedAtUtc = @UtcNow, UpdatedBy = @Actor " +
                        "WHERE PermissionKey = @PermissionKey;";
                    await connection.ExecuteAsync(new CommandDefinition(
                        updateSql,
                        new
                        {
                            PermissionKey = definition.Key,
                            definition.Module,
                            definition.Description,
                            CatalogStatus = desiredStatus,
                            Policy = policy,
                            ReleaseId = registry.ReleaseId,
                            UtcNow = utcNow,
                            Actor = approvals.ApprovedBy,
                        },
                        transaction,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);

                    if (desiredStatus == "Active")
                    {
                        await connection.ExecuteAsync(new CommandDefinition(
                            "UPDATE dbo.PermissionLegacyAssignmentDrift " +
                            "SET ReviewStatus = 'Adopted', ReviewedAtUtc = @UtcNow, ReviewedBy = @Actor " +
                            "WHERE PermissionKey = @PermissionKey AND ReviewStatus = 'Unreviewed';",
                            new
                            {
                                PermissionKey = definition.Key,
                                UtcNow = utcNow,
                                Actor = approvals.ApprovedBy,
                            },
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                    }
                }
            }
        }

        private static async Task ApplyApprovalsAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            IPermissionRegistry registry,
            PermissionSyncApprovalSet approvals,
            PermissionSynchronizationPlan plan,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            const string sql =
                "UPDATE dbo.PermissionGrantApprovals SET CatalogDecision = @CatalogDecision, " +
                "AdministratorGrantDecision = @AdministratorGrantDecision, " +
                "DecisionReleaseId = @ReleaseId, DecisionBy = @Actor, " +
                "DecisionAtUtc = @UtcNow, ExpiresAtUtc = @ExpiresAtUtc " +
                "WHERE PermissionKey = @PermissionKey; " +
                "IF @@ROWCOUNT = 0 INSERT dbo.PermissionGrantApprovals " +
                "(PermissionKey, CatalogDecision, AdministratorGrantDecision, DecisionReleaseId, " +
                "DecisionBy, DecisionAtUtc, ExpiresAtUtc) VALUES " +
                "(@PermissionKey, @CatalogDecision, @AdministratorGrantDecision, @ReleaseId, " +
                "@Actor, @UtcNow, @ExpiresAtUtc);";
            foreach (var definition in registry.Definitions)
            {
                if (!plan.Changes.Any(change =>
                    change.Kind == PermissionSyncChangeKind.UpdateApproval &&
                    string.Equals(change.PermissionKey, definition.Key, StringComparison.Ordinal)))
                {
                    continue;
                }

                var approval = GetApproval(approvals, definition.Key);
                await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        PermissionKey = definition.Key,
                        CatalogDecision = DecisionValue(approval.CatalogDecision),
                        AdministratorGrantDecision = DecisionValue(approval.AdministratorGrantDecision),
                        ReleaseId = approvals.ReleaseId,
                        Actor = approvals.ApprovedBy,
                        UtcNow = utcNow,
                        approvals.ExpiresAtUtc,
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
        }

        private static async Task ApplyAdministratorPolicyAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            PermissionSynchronizationPlan plan,
            IPermissionRegistry registry,
            PermissionSyncApprovalSet approvals,
            DatabaseState state,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var managedRole = state.ManagedRoles.FirstOrDefault(role =>
                string.Equals(role.SystemRoleKey, "Administrator", StringComparison.Ordinal));
            int managedRoleId;
            int roleId;
            if (plan.Changes.Any(change =>
                change.Kind == PermissionSyncChangeKind.CreateAdministratorRole))
            {
                const string createRoleSql =
                    "INSERT dbo.Roles (RoleName, NormalizedRoleName, IsActive, CreatedAtUtc) " +
                    "OUTPUT INSERTED.RoleId VALUES (N'Administrator', N'ADMINISTRATOR', 1, @UtcNow);";
                roleId = await connection.QuerySingleAsync<int>(new CommandDefinition(
                    createRoleSql,
                    new { UtcNow = utcNow },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
                const string ownRoleSql =
                    "INSERT dbo.SystemManagedRoles " +
                    "(SystemRoleKey, RoleId, ExpectedNormalizedRoleName, CreatedAtUtc, CreatedBy) " +
                    "OUTPUT INSERTED.ManagedRoleId " +
                    "VALUES ('Administrator', @RoleId, N'ADMINISTRATOR', @UtcNow, @Actor);";
                managedRoleId = await connection.QuerySingleAsync<int>(new CommandDefinition(
                    ownRoleSql,
                    new { RoleId = roleId, UtcNow = utcNow, Actor = approvals.ApprovedBy },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            else
            {
                if (managedRole == null)
                {
                    throw new PermissionSynchronizationException(
                        "PermissionSync.AdministratorRoleMissing",
                        "The managed Administrator role is missing from the reviewed plan state.");
                }

                managedRoleId = managedRole.ManagedRoleId;
                roleId = managedRole.RoleId;
            }

            foreach (var change in plan.Changes.Where(item =>
                item.Kind == PermissionSyncChangeKind.RecordAdministratorRevocation))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.ManagedRolePermissionGrants SET GrantState = 'Revoked', " +
                    "StateReleaseId = @ReleaseId, StateChangedAtUtc = @UtcNow, " +
                    "StateChangedBy = @Actor, Reason = N'Assignment missing during reviewed sync.' " +
                    "WHERE ManagedRoleId = @ManagedRoleId AND PermissionKey = @PermissionKey " +
                    "AND GrantState = 'Granted';",
                    new
                    {
                        ManagedRoleId = managedRoleId,
                        change.PermissionKey,
                        ReleaseId = registry.ReleaseId,
                        UtcNow = utcNow,
                        Actor = approvals.ApprovedBy,
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            foreach (var change in plan.Changes.Where(item =>
                item.Kind == PermissionSyncChangeKind.GrantAdministratorPermission))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "IF NOT EXISTS (SELECT 1 FROM dbo.RolePermissions " +
                    "WHERE RoleId = @RoleId AND PermissionKey = @PermissionKey) " +
                    "INSERT dbo.RolePermissions (RoleId, PermissionKey) " +
                    "VALUES (@RoleId, @PermissionKey);",
                    new { RoleId = roleId, change.PermissionKey },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.ManagedRolePermissionGrants SET GrantState = 'Granted', " +
                    "StateReleaseId = @ReleaseId, StateChangedAtUtc = @UtcNow, " +
                    "StateChangedBy = @Actor, Reason = N'Reviewed release grant.' " +
                    "WHERE ManagedRoleId = @ManagedRoleId AND PermissionKey = @PermissionKey " +
                    "AND GrantState = 'NotGranted'; " +
                    "IF @@ROWCOUNT = 0 AND NOT EXISTS " +
                    "(SELECT 1 FROM dbo.ManagedRolePermissionGrants " +
                    "WHERE ManagedRoleId = @ManagedRoleId AND PermissionKey = @PermissionKey) " +
                    "INSERT dbo.ManagedRolePermissionGrants " +
                    "(ManagedRoleId, PermissionKey, GrantState, StateReleaseId, " +
                    "StateChangedAtUtc, StateChangedBy, Reason) VALUES " +
                    "(@ManagedRoleId, @PermissionKey, 'Granted', @ReleaseId, @UtcNow, @Actor, " +
                    "N'Reviewed release grant.');",
                    new
                    {
                        ManagedRoleId = managedRoleId,
                        change.PermissionKey,
                        ReleaseId = registry.ReleaseId,
                        UtcNow = utcNow,
                        Actor = approvals.ApprovedBy,
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
        }

        private static Task<int> UpdateSynchronizationStateAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            PermissionSynchronizationPlan plan,
            PermissionSyncApprovalSet approvals,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            const string sql =
                "UPDATE dbo.PermissionSynchronizationState SET LastAppliedPlanId = @PlanId, " +
                "LastAppliedReleaseId = @ReleaseId, LastAppliedAtUtc = @UtcNow, " +
                "LastAppliedBy = @Actor WHERE StateId = 1;";
            return connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    plan.PlanId,
                    ReleaseId = approvals.ReleaseId,
                    UtcNow = utcNow,
                    Actor = approvals.ApprovedBy,
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        private static Task<int> WriteAuditAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            Guid attemptId,
            PermissionSynchronizationPlan plan,
            PermissionSyncApprovalSet approvals,
            Guid databaseIdentity,
            string outcome,
            string reasonCode,
            string? summary,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            const string sql =
                "INSERT dbo.PermissionSecurityAudit " +
                "(AttemptId, Operation, PlanId, ReleaseId, DatabaseIdentity, Actor, Outcome, " +
                "ReasonCode, ChangeSummary, OccurredAtUtc) VALUES " +
                "(@AttemptId, @Operation, @PlanId, @ReleaseId, @DatabaseIdentity, @Actor, " +
                "@Outcome, @ReasonCode, @Summary, @UtcNow);";
            return connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    AttemptId = attemptId,
                    Operation,
                    plan.PlanId,
                    ReleaseId = approvals.ReleaseId,
                    DatabaseIdentity = databaseIdentity,
                    Actor = approvals.ApprovedBy,
                    Outcome = outcome,
                    ReasonCode = reasonCode,
                    Summary = summary,
                    UtcNow = utcNow,
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        private static PermissionSyncApproval GetApproval(
            PermissionSyncApprovalSet approvals,
            string permissionKey)
        {
            PermissionSyncApproval approval;
            if (!approvals.TryGet(permissionKey, out approval!))
            {
                throw new PermissionSynchronizationException(
                    "PermissionSync.ApprovalIncomplete",
                    "The approval set is incomplete.");
            }

            return approval;
        }

        private static string DesiredStatus(
            PermissionDefinition definition,
            PermissionSyncApproval approval,
            string? currentStatus)
        {
            if (definition.LifecycleStatus == PermissionLifecycleStatus.Deprecated)
            {
                return "Deprecated";
            }

            if (definition.LifecycleStatus == PermissionLifecycleStatus.Active &&
                approval.CatalogDecision == PermissionCatalogDecision.Approved)
            {
                return "Active";
            }

            return currentStatus == "Active" ? "Active" : "PendingActivation";
        }

        private static bool DefinitionMatches(PermissionRow row, PermissionDefinition definition)
        {
            return string.Equals(row.Module, definition.Module, StringComparison.Ordinal) &&
                   string.Equals(row.Description, definition.Description, StringComparison.Ordinal) &&
                   string.Equals(
                       row.AdministratorGrantPolicy,
                       PolicyValue(definition.AdministratorGrantPolicy),
                       StringComparison.Ordinal);
        }

        private static bool ApprovalMatches(
            ApprovalRow? row,
            PermissionSyncApproval approval,
            PermissionSyncApprovalSet set)
        {
            return row != null &&
                   string.Equals(row.CatalogDecision, DecisionValue(approval.CatalogDecision), StringComparison.Ordinal) &&
                   string.Equals(row.AdministratorGrantDecision, DecisionValue(approval.AdministratorGrantDecision), StringComparison.Ordinal) &&
                   string.Equals(row.DecisionReleaseId, set.ReleaseId, StringComparison.Ordinal) &&
                   string.Equals(row.DecisionBy, set.ApprovedBy, StringComparison.Ordinal) &&
                   row.ExpiresAtUtc == set.ExpiresAtUtc;
        }

        private static string PolicyValue(AdministratorGrantPolicy value)
        {
            return value.ToString();
        }

        private static string DecisionValue(PermissionCatalogDecision value)
        {
            return value.ToString();
        }

        private static string DecisionValue(AdministratorGrantDecision value)
        {
            return value.ToString();
        }

        private static string FingerprintManifest(IPermissionRegistry registry)
        {
            var values = new List<string> { registry.ReleaseId };
            foreach (var definition in registry.Definitions)
            {
                values.Add(definition.Key);
                values.Add(definition.Module);
                values.Add(definition.Description);
                values.Add(definition.LifecycleStatus.ToString());
                values.Add(definition.AdministratorGrantPolicy.ToString());
            }

            return Hash(values.ToArray());
        }

        private static string FingerprintApprovals(PermissionSyncApprovalSet approvals)
        {
            var values = new List<string?>
            {
                approvals.ReleaseId,
                approvals.ApprovedBy,
                approvals.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture),
            };
            foreach (var approval in approvals.Approvals.OrderBy(item => item.PermissionKey, StringComparer.Ordinal))
            {
                values.Add(approval.PermissionKey);
                values.Add(approval.CatalogDecision.ToString());
                values.Add(approval.AdministratorGrantDecision.ToString());
            }

            return Hash(values.ToArray());
        }

        private static string FingerprintState(DatabaseState state)
        {
            var values = new List<string?>
            {
                state.DatabaseIdentity.ToString("D"),
                state.SynchronizationState.LastAppliedPlanId,
                state.SynchronizationState.LastAppliedReleaseId,
                Format(state.SynchronizationState.LastAppliedAtUtc),
                state.SynchronizationState.LastAppliedBy,
                Convert.ToBase64String(state.SynchronizationState.RowVersion),
            };
            foreach (var row in state.Permissions)
            {
                values.Add(row.PermissionKey);
                values.Add(row.Module);
                values.Add(row.Description);
                values.Add(row.CatalogStatus);
                values.Add(row.AdministratorGrantPolicy);
                values.Add(row.IntroducedReleaseId);
                values.Add(row.DeprecatedReleaseId);
                values.Add(Format(row.ActivatedAtUtc));
                values.Add(row.ActivatedBy);
                values.Add(Format(row.UpdatedAtUtc));
                values.Add(row.UpdatedBy);
                values.Add(Convert.ToBase64String(row.RowVersion));
            }

            foreach (var row in state.Approvals)
            {
                values.Add(row.PermissionKey);
                values.Add(row.CatalogDecision);
                values.Add(row.AdministratorGrantDecision);
                values.Add(row.DecisionReleaseId);
                values.Add(row.DecisionBy);
                values.Add(Format(row.DecisionAtUtc));
                values.Add(Format(row.ExpiresAtUtc));
                values.Add(Convert.ToBase64String(row.RowVersion));
            }

            foreach (var row in state.ManagedRoles)
            {
                values.Add("ManagedRole");
                values.Add(row.ManagedRoleId.ToString(CultureInfo.InvariantCulture));
                values.Add(row.SystemRoleKey);
                values.Add(row.RoleId.ToString(CultureInfo.InvariantCulture));
                values.Add(row.ExpectedNormalizedRoleName);
                values.Add(Format(row.CreatedAtUtc));
                values.Add(row.CreatedBy);
            }

            foreach (var row in state.Roles)
            {
                values.Add("Role");
                values.Add(row.RoleId.ToString(CultureInfo.InvariantCulture));
                values.Add(row.RoleName);
                values.Add(row.NormalizedRoleName);
                values.Add(row.IsActive ? "1" : "0");
                values.Add(Convert.ToBase64String(row.RowVersion));
            }

            foreach (var row in state.ManagedGrants)
            {
                values.Add("ManagedGrant");
                values.Add(row.ManagedRolePermissionGrantId.ToString(CultureInfo.InvariantCulture));
                values.Add(row.ManagedRoleId.ToString(CultureInfo.InvariantCulture));
                values.Add(row.PermissionKey);
                values.Add(row.GrantState);
                values.Add(row.StateReleaseId);
                values.Add(Format(row.StateChangedAtUtc));
                values.Add(row.StateChangedBy);
                values.Add(row.Reason);
                values.Add(Convert.ToBase64String(row.RowVersion));
            }

            foreach (var row in state.RoleAssignments)
            {
                values.Add("RoleAssignment");
                values.Add(row.RoleId.ToString(CultureInfo.InvariantCulture));
                values.Add(row.PermissionKey);
            }

            foreach (var row in state.UserAssignments)
            {
                values.Add("UserAssignment");
                values.Add(row.UserId.ToString(CultureInfo.InvariantCulture));
                values.Add(row.PermissionKey);
                values.Add(row.Granted ? "1" : "0");
                values.Add(Convert.ToBase64String(row.RowVersion));
            }

            foreach (var row in state.LegacyDrift)
            {
                values.Add("LegacyDrift");
                values.Add(row.PermissionKey);
                values.Add(row.RoleGrantCount.ToString(CultureInfo.InvariantCulture));
                values.Add(row.UserGrantCount.ToString(CultureInfo.InvariantCulture));
                values.Add(row.ReviewStatus);
                values.Add(Format(row.FirstObservedAtUtc));
                values.Add(Format(row.ReviewedAtUtc));
                values.Add(row.ReviewedBy);
            }

            return Hash(values.ToArray());
        }

        private static string Hash(params string?[] values)
        {
            var canonical = new StringBuilder();
            foreach (var value in values)
            {
                var safe = value ?? string.Empty;
                canonical.Append(safe.Length.ToString(CultureInfo.InvariantCulture));
                canonical.Append(':');
                canonical.Append(safe);
                canonical.Append('|');
            }

            using (var sha256 = SHA256.Create())
            {
                var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                return BitConverter.ToString(digest).Replace("-", string.Empty);
            }
        }

        private static string Format(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : null!;
        }

        private static string Summarize(PermissionSynchronizationPlan plan)
        {
            return string.Join(
                ";",
                plan.Changes.GroupBy(change => change.Kind)
                    .OrderBy(group => group.Key)
                    .Select(group => group.Key + "=" + group.Count().ToString(CultureInfo.InvariantCulture)));
        }

        private sealed class DatabaseState
        {
            public DatabaseState(
                SynchronizationStateRow synchronizationState,
                PermissionRow[] permissions,
                ApprovalRow[] approvals,
                ManagedRoleRow[] managedRoles,
                RoleRow[] roles,
                ManagedGrantRow[] managedGrants,
                RoleAssignmentRow[] roleAssignments,
                UserAssignmentRow[] userAssignments,
                LegacyDriftRow[] legacyDrift)
            {
                SynchronizationState = synchronizationState;
                Permissions = permissions;
                Approvals = approvals;
                ManagedRoles = managedRoles;
                Roles = roles;
                ManagedGrants = managedGrants;
                RoleAssignments = roleAssignments;
                UserAssignments = userAssignments;
                LegacyDrift = legacyDrift;
            }

            public SynchronizationStateRow SynchronizationState { get; }
            public PermissionRow[] Permissions { get; }
            public ApprovalRow[] Approvals { get; }
            public ManagedRoleRow[] ManagedRoles { get; }
            public RoleRow[] Roles { get; }
            public ManagedGrantRow[] ManagedGrants { get; }
            public RoleAssignmentRow[] RoleAssignments { get; }
            public UserAssignmentRow[] UserAssignments { get; }
            public LegacyDriftRow[] LegacyDrift { get; }
            public Guid DatabaseIdentity => SynchronizationState.DatabaseIdentity;
        }

        private sealed class SynchronizationStateRow
        {
            public string ServerName { get; set; } = string.Empty;
            public string DatabaseName { get; set; } = string.Empty;
            public Guid DatabaseIdentity { get; set; }
            public string? LastAppliedPlanId { get; set; }
            public string? LastAppliedReleaseId { get; set; }
            public DateTime? LastAppliedAtUtc { get; set; }
            public string? LastAppliedBy { get; set; }
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        }

        private sealed class PermissionRow
        {
            public string PermissionKey { get; set; } = string.Empty;
            public string Module { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string CatalogStatus { get; set; } = string.Empty;
            public string AdministratorGrantPolicy { get; set; } = string.Empty;
            public string? IntroducedReleaseId { get; set; }
            public string? DeprecatedReleaseId { get; set; }
            public DateTime? ActivatedAtUtc { get; set; }
            public string? ActivatedBy { get; set; }
            public DateTime? UpdatedAtUtc { get; set; }
            public string? UpdatedBy { get; set; }
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        }

        private sealed class ApprovalRow
        {
            public string PermissionKey { get; set; } = string.Empty;
            public string CatalogDecision { get; set; } = string.Empty;
            public string AdministratorGrantDecision { get; set; } = string.Empty;
            public string DecisionReleaseId { get; set; } = string.Empty;
            public string DecisionBy { get; set; } = string.Empty;
            public DateTime DecisionAtUtc { get; set; }
            public DateTime? ExpiresAtUtc { get; set; }
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        }

        private sealed class ManagedRoleRow
        {
            public int ManagedRoleId { get; set; }
            public string SystemRoleKey { get; set; } = string.Empty;
            public int RoleId { get; set; }
            public string ExpectedNormalizedRoleName { get; set; } = string.Empty;
            public DateTime CreatedAtUtc { get; set; }
            public string CreatedBy { get; set; } = string.Empty;
        }

        private sealed class RoleRow
        {
            public int RoleId { get; set; }
            public string RoleName { get; set; } = string.Empty;
            public string NormalizedRoleName { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        }

        private sealed class ManagedGrantRow
        {
            public long ManagedRolePermissionGrantId { get; set; }
            public int ManagedRoleId { get; set; }
            public string PermissionKey { get; set; } = string.Empty;
            public string GrantState { get; set; } = string.Empty;
            public string StateReleaseId { get; set; } = string.Empty;
            public DateTime StateChangedAtUtc { get; set; }
            public string StateChangedBy { get; set; } = string.Empty;
            public string? Reason { get; set; }
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        }

        private sealed class RoleAssignmentRow
        {
            public int RoleId { get; set; }
            public string PermissionKey { get; set; } = string.Empty;
        }

        private sealed class UserAssignmentRow
        {
            public int UserId { get; set; }
            public string PermissionKey { get; set; } = string.Empty;
            public bool Granted { get; set; }
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        }

        private sealed class LegacyDriftRow
        {
            public string PermissionKey { get; set; } = string.Empty;
            public int RoleGrantCount { get; set; }
            public int UserGrantCount { get; set; }
            public string ReviewStatus { get; set; } = string.Empty;
            public DateTime FirstObservedAtUtc { get; set; }
            public DateTime? ReviewedAtUtc { get; set; }
            public string? ReviewedBy { get; set; }
        }

        private sealed class AdministratorGrantRow
        {
            public int ManagedRoleId { get; set; }
            public int RoleId { get; set; }
            public string GrantState { get; set; } = string.Empty;
            public string CatalogStatus { get; set; } = string.Empty;
            public string AdministratorGrantPolicy { get; set; } = string.Empty;
            public string? AdministratorGrantDecision { get; set; }
            public string? DecisionReleaseId { get; set; }
            public DateTime? ExpiresAtUtc { get; set; }
        }
    }
}
