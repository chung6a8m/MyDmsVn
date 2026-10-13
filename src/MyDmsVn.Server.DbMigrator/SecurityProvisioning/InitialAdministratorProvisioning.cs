using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning
{
    public enum InitialAdministratorProvisioningStatus
    {
        Ready = 0,
        AlreadyCompleted = 1,
        SynchronizationRequired = 2,
        RecoveryRequired = 3,
    }

    public enum InitialAdministratorProvisioningOutcome
    {
        Created = 0,
        AlreadyCompleted = 1,
    }

    public sealed class InitialAdministratorProvisioningInspection
    {
        internal InitialAdministratorProvisioningInspection(
            Guid databaseIdentity,
            string targetDatabase,
            InitialAdministratorProvisioningStatus status,
            string reasonCode)
        {
            DatabaseIdentity = databaseIdentity;
            TargetDatabase = targetDatabase;
            Status = status;
            ReasonCode = reasonCode;
        }

        public Guid DatabaseIdentity { get; }
        public string TargetDatabase { get; }
        public InitialAdministratorProvisioningStatus Status { get; }
        public string ReasonCode { get; }
    }

    public sealed class InitialAdministratorProvisioningRequest
    {
        public InitialAdministratorProvisioningRequest(
            Guid expectedDatabaseIdentity,
            string username,
            string displayName,
            string password,
            string actor,
            string releaseId)
        {
            ExpectedDatabaseIdentity = expectedDatabaseIdentity;
            Username = username;
            DisplayName = displayName;
            Password = password;
            Actor = actor;
            ReleaseId = releaseId;
        }

        public Guid ExpectedDatabaseIdentity { get; }
        public string Username { get; }
        public string DisplayName { get; }
        public string Password { get; }
        public string Actor { get; }
        public string ReleaseId { get; }
    }

    public sealed class InitialAdministratorProvisioningResult
    {
        internal InitialAdministratorProvisioningResult(
            InitialAdministratorProvisioningOutcome outcome,
            int? administratorUserId)
        {
            Outcome = outcome;
            AdministratorUserId = administratorUserId;
        }

        public InitialAdministratorProvisioningOutcome Outcome { get; }
        public int? AdministratorUserId { get; }
    }

    public sealed class InitialAdministratorProvisioningException : Exception
    {
        internal InitialAdministratorProvisioningException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }

    public sealed class InitialAdministratorProvisioningEngine
    {
        private const string BootstrapKey = "InitialAdministrator";
        private const string LockResource = "MyDmsVn.PermissionSync";
        private readonly IPasswordHasher _passwordHasher;

        public InitialAdministratorProvisioningEngine(IPasswordHasher passwordHasher)
        {
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        }

        public async Task<InitialAdministratorProvisioningInspection> InspectAsync(
            string connectionString,
            CancellationToken cancellationToken)
        {
            ValidateConnectionString(connectionString);
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                DatabaseMigrationPrincipalValidator.Validate(connection);
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    await AcquireLockAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                    var state = await ReadStateAsync(connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                    transaction.Commit();
                    return CreateInspection(state);
                }
            }
        }

        public async Task<InitialAdministratorProvisioningResult> ProvisionAsync(
            string connectionString,
            InitialAdministratorProvisioningRequest request,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            ValidateConnectionString(connectionString);
            ValidateRequest(request, utcNow);
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                DatabaseMigrationPrincipalValidator.Validate(connection);
                Guid databaseIdentity = request.ExpectedDatabaseIdentity;
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        await AcquireLockAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                        var state = await ReadStateAsync(connection, transaction, cancellationToken)
                            .ConfigureAwait(false);
                        databaseIdentity = state.DatabaseIdentity;
                        if (state.DatabaseIdentity != request.ExpectedDatabaseIdentity)
                        {
                            throw new InitialAdministratorProvisioningException(
                                "SecurityBootstrap.TargetMismatch",
                                "The confirmed database identity does not match the target.");
                        }

                        var inspection = CreateInspection(state);
                        if (inspection.Status == InitialAdministratorProvisioningStatus.AlreadyCompleted)
                        {
                            transaction.Commit();
                            return new InitialAdministratorProvisioningResult(
                                InitialAdministratorProvisioningOutcome.AlreadyCompleted,
                                state.AdministratorUserId);
                        }

                        if (inspection.Status == InitialAdministratorProvisioningStatus.RecoveryRequired)
                        {
                            throw new InitialAdministratorProvisioningException(
                                "SecurityBootstrap.RecoveryRequired",
                                "Unexpected identity data exists without a bootstrap marker. Use the reviewed recovery procedure.");
                        }

                        if (inspection.Status != InitialAdministratorProvisioningStatus.Ready)
                        {
                            throw new InitialAdministratorProvisioningException(
                                "SecurityBootstrap.PermissionSyncRequired",
                                "Apply an approved permission synchronization plan before enrollment.");
                        }

                        var invalidGrants = await connection.QuerySingleAsync<int>(new CommandDefinition(
                            "SELECT COUNT(*) FROM dbo.ManagedRolePermissionGrants AS managedGrant " +
                            "JOIN dbo.SystemManagedRoles AS managedRole " +
                            "ON managedRole.ManagedRoleId = managedGrant.ManagedRoleId " +
                            "LEFT JOIN dbo.RolePermissions AS assignment " +
                            "ON assignment.RoleId = managedRole.RoleId " +
                            "AND assignment.PermissionKey = managedGrant.PermissionKey " +
                            "LEFT JOIN dbo.Permissions AS permission " +
                            "ON permission.PermissionKey = managedGrant.PermissionKey " +
                            "LEFT JOIN dbo.PermissionGrantApprovals AS approval " +
                            "ON approval.PermissionKey = managedGrant.PermissionKey " +
                            "WHERE managedRole.SystemRoleKey = 'Administrator' " +
                            "AND managedGrant.GrantState = 'Granted' " +
                            "AND (assignment.RolePermissionId IS NULL " +
                            "OR permission.CatalogStatus <> 'Active' " +
                            "OR approval.AdministratorGrantDecision <> 'Approved' " +
                            "OR approval.ExpiresAtUtc IS NULL OR approval.ExpiresAtUtc <= @UtcNow);",
                            new { UtcNow = utcNow },
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                        if (invalidGrants != 0)
                        {
                            throw new InitialAdministratorProvisioningException(
                                "SecurityBootstrap.PermissionSyncRequired",
                                "Managed Administrator grants are stale or unapproved.");
                        }

                        var username = request.Username.Trim();
                        var normalizedUsername = UsernameNormalizer.Normalize(username);
                        var displayName = request.DisplayName.Trim();
                        var passwordHash = _passwordHasher.Hash(request.Password);
                        const string insertUserSql =
                            "INSERT dbo.Users " +
                            "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, " +
                            "PasswordSalt, PasswordAlgorithm, IsActive, CreatedAtUtc) " +
                            "OUTPUT INSERTED.UserId VALUES " +
                            "(@Username, @NormalizedUsername, @DisplayName, N'Local', @PasswordHash, " +
                            "N'', @PasswordAlgorithm, 1, @UtcNow);";
                        var userId = await connection.QuerySingleAsync<int>(new CommandDefinition(
                            insertUserSql,
                            new
                            {
                                Username = username,
                                NormalizedUsername = normalizedUsername,
                                DisplayName = displayName,
                                PasswordHash = passwordHash,
                                PasswordAlgorithm = _passwordHasher.Algorithm,
                                UtcNow = utcNow,
                            },
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                        await connection.ExecuteAsync(new CommandDefinition(
                            "INSERT dbo.UserRoles (UserId, RoleId, CreatedAtUtc) " +
                            "VALUES (@UserId, @RoleId, @UtcNow); " +
                            "INSERT dbo.SecurityBootstrapState " +
                            "(BootstrapKey, CompletedAtUtc, CompletedBy, AdministratorUserId, ReleaseId) " +
                            "VALUES (@BootstrapKey, @UtcNow, @Actor, @UserId, @ReleaseId);",
                            new
                            {
                                UserId = userId,
                                RoleId = state.AdministratorRoleId,
                                UtcNow = utcNow,
                                BootstrapKey,
                                Actor = request.Actor,
                                request.ReleaseId,
                            },
                            transaction,
                            cancellationToken: cancellationToken)).ConfigureAwait(false);
                        await WriteAuditAsync(
                            connection,
                            transaction,
                            databaseIdentity,
                            request,
                            "Succeeded",
                            "SecurityBootstrap.Created",
                            utcNow,
                            cancellationToken).ConfigureAwait(false);
                        transaction.Commit();
                        return new InitialAdministratorProvisioningResult(
                            InitialAdministratorProvisioningOutcome.Created,
                            userId);
                    }
                    catch (Exception exception)
                    {
                        transaction.Rollback();
                        var known = exception as InitialAdministratorProvisioningException;
                        await WriteAuditAsync(
                            connection,
                            null,
                            databaseIdentity,
                            request,
                            known == null ? "Failed" : "Rejected",
                            known?.Code ?? "SecurityBootstrap.Failed",
                            utcNow,
                            CancellationToken.None).ConfigureAwait(false);
                        if (known != null)
                        {
                            throw;
                        }

                        throw new InitialAdministratorProvisioningException(
                            "SecurityBootstrap.Failed",
                            "Initial Administrator enrollment failed without committing.");
                    }
                }
            }
        }

        private static InitialAdministratorProvisioningInspection CreateInspection(BootstrapState state)
        {
            InitialAdministratorProvisioningStatus status;
            string reasonCode;
            if (state.BootstrapCompleted)
            {
                status = InitialAdministratorProvisioningStatus.AlreadyCompleted;
                reasonCode = "SecurityBootstrap.AlreadyCompleted";
            }
            else if (state.UserCount != 0 || state.UserRoleCount != 0 || state.UnexpectedRoleCount != 0)
            {
                status = InitialAdministratorProvisioningStatus.RecoveryRequired;
                reasonCode = "SecurityBootstrap.RecoveryRequired";
            }
            else if (!state.SyncApplied || !state.AdministratorRoleValid || state.GrantedPermissionCount == 0)
            {
                status = InitialAdministratorProvisioningStatus.SynchronizationRequired;
                reasonCode = "SecurityBootstrap.PermissionSyncRequired";
            }
            else
            {
                status = InitialAdministratorProvisioningStatus.Ready;
                reasonCode = "SecurityBootstrap.Ready";
            }

            return new InitialAdministratorProvisioningInspection(
                state.DatabaseIdentity,
                state.ServerName + "/" + state.DatabaseName,
                status,
                reasonCode);
        }

        private static async Task<BootstrapState> ReadStateAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken)
        {
            const string sql =
                "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ServerName, " +
                "DB_NAME() AS DatabaseName, sync.DatabaseIdentity, " +
                "CASE WHEN sync.LastAppliedPlanId IS NULL THEN CONVERT(bit, 0) ELSE CONVERT(bit, 1) END AS SyncApplied, " +
                "CASE WHEN bootstrap.BootstrapKey IS NULL THEN CONVERT(bit, 0) ELSE CONVERT(bit, 1) END AS BootstrapCompleted, " +
                "bootstrap.AdministratorUserId, " +
                "managed.RoleId AS AdministratorRoleId, " +
                "CASE WHEN managed.SystemRoleKey = 'Administrator' AND role.IsActive = 1 " +
                "AND role.NormalizedRoleName = N'ADMINISTRATOR' " +
                "AND managed.ExpectedNormalizedRoleName = N'ADMINISTRATOR' " +
                "THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END AS AdministratorRoleValid, " +
                "(SELECT COUNT(*) FROM dbo.Users) AS UserCount, " +
                "(SELECT COUNT(*) FROM dbo.UserRoles) AS UserRoleCount, " +
                "(SELECT COUNT(*) FROM dbo.Roles AS candidate " +
                "WHERE managed.RoleId IS NULL OR candidate.RoleId <> managed.RoleId) AS UnexpectedRoleCount, " +
                "(SELECT COUNT(*) FROM dbo.ManagedRolePermissionGrants AS managedGrant " +
                "WHERE managedGrant.ManagedRoleId = managed.ManagedRoleId " +
                "AND managedGrant.GrantState = 'Granted') AS GrantedPermissionCount " +
                "FROM dbo.PermissionSynchronizationState AS sync " +
                "LEFT JOIN dbo.SecurityBootstrapState AS bootstrap " +
                "ON bootstrap.BootstrapKey = 'InitialAdministrator' " +
                "LEFT JOIN dbo.SystemManagedRoles AS managed ON managed.SystemRoleKey = 'Administrator' " +
                "LEFT JOIN dbo.Roles AS role ON role.RoleId = managed.RoleId " +
                "WHERE sync.StateId = 1;";
            return await connection.QuerySingleAsync<BootstrapState>(new CommandDefinition(
                sql,
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        private static async Task AcquireLockAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken)
        {
            const string sql =
                "DECLARE @Result int; EXEC @Result = sys.sp_getapplock " +
                "@Resource = @Resource, @LockMode = N'Exclusive', " +
                "@LockOwner = N'Transaction', @LockTimeout = 0; SELECT @Result;";
            var result = await connection.QuerySingleAsync<int>(new CommandDefinition(
                sql,
                new { Resource = LockResource },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (result < 0)
            {
                throw new InitialAdministratorProvisioningException(
                    "SecurityBootstrap.Busy",
                    "Another security provisioning operation is in progress.");
            }
        }

        private static Task<int> WriteAuditAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            Guid databaseIdentity,
            InitialAdministratorProvisioningRequest request,
            string outcome,
            string reasonCode,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            const string sql =
                "INSERT dbo.PermissionSecurityAudit " +
                "(AttemptId, Operation, ReleaseId, DatabaseIdentity, Actor, Outcome, " +
                "ReasonCode, ChangeSummary, OccurredAtUtc) VALUES " +
                "(@AttemptId, 'SecurityBootstrap.Enroll', @ReleaseId, @DatabaseIdentity, " +
                "@Actor, @Outcome, @ReasonCode, N'Initial Administrator enrollment.', @UtcNow);";
            return connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    AttemptId = Guid.NewGuid(),
                    request.ReleaseId,
                    DatabaseIdentity = databaseIdentity,
                    request.Actor,
                    Outcome = outcome,
                    ReasonCode = reasonCode,
                    UtcNow = utcNow,
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        private static void ValidateConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("A database connection string is required.", nameof(connectionString));
            }
        }

        private static void ValidateRequest(
            InitialAdministratorProvisioningRequest request,
            DateTime utcNow)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.ExpectedDatabaseIdentity == Guid.Empty)
            {
                throw new ArgumentException("A confirmed database identity is required.", nameof(request));
            }

            var username = request.Username?.Trim();
            if (username == null || string.IsNullOrWhiteSpace(username) || username.Length > 100)
            {
                throw new ArgumentException("A username of at most 100 characters is required.", nameof(request));
            }

            var displayName = request.DisplayName?.Trim();
            if (displayName == null || string.IsNullOrWhiteSpace(displayName) || displayName.Length > 100)
            {
                throw new ArgumentException("A display name of at most 100 characters is required.", nameof(request));
            }

            if (!NewPasswordPolicy.IsAcceptable(request.Password, username))
            {
                throw new InitialAdministratorProvisioningException(
                    "SecurityBootstrap.PasswordPolicy",
                    "The supplied password does not meet the new-password policy.");
            }

            if (string.IsNullOrWhiteSpace(request.Actor) || request.Actor.Length > 128 ||
                string.IsNullOrWhiteSpace(request.ReleaseId) || request.ReleaseId.Length > 64)
            {
                throw new ArgumentException("Valid actor and release identifiers are required.", nameof(request));
            }

            if (utcNow.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("The enrollment time must be UTC.", nameof(utcNow));
            }
        }

        private sealed class BootstrapState
        {
            public string ServerName { get; set; } = string.Empty;
            public string DatabaseName { get; set; } = string.Empty;
            public Guid DatabaseIdentity { get; set; }
            public bool SyncApplied { get; set; }
            public bool BootstrapCompleted { get; set; }
            public int? AdministratorUserId { get; set; }
            public int AdministratorRoleId { get; set; }
            public bool AdministratorRoleValid { get; set; }
            public int UserCount { get; set; }
            public int UserRoleCount { get; set; }
            public int UnexpectedRoleCount { get; set; }
            public int GrantedPermissionCount { get; set; }
        }
    }
}
