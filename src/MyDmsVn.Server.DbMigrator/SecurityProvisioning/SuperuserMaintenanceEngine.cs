using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning;

public sealed class SuperuserMaintenanceEngine
{
    private const string LockResource = "MyDmsVn.SuperuserMaintenance";
    private readonly IPasswordHasher _passwordHasher;

    public SuperuserMaintenanceEngine(IPasswordHasher passwordHasher)
    {
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task RecoverAsync(
        string maintenanceConnectionString,
        string configuredUsername,
        int expectedUserId,
        string password,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var configuredNormalizedUsername = ValidateCommon(
            maintenanceConnectionString,
            configuredUsername,
            expectedUserId,
            utcNow);
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("A recovery password is required.", nameof(password));
        }

        var passwordHash = _passwordHasher.Hash(password);
        using var connection = new SqlConnection(maintenanceConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        DatabaseMigrationPrincipalValidator.Validate(connection);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            await AcquireLockAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await RequireBoundAccountAsync(
                    connection,
                    transaction,
                    configuredNormalizedUsername,
                    expectedUserId,
                    cancellationToken)
                .ConfigureAwait(false);

            using var updatedCommand = MaintenanceSql.Command(connection, transaction,
                "UPDATE dbo.Users SET PasswordHash = @passwordHash, PasswordSalt = N'', " +
                "PasswordAlgorithm = @algorithm, IsActive = 1, UpdatedAtUtc = @utcNow, " +
                "UpdatedByUserId = NULL WHERE UserId = @expectedUserId;",
                MaintenanceSql.Text("@passwordHash", passwordHash), MaintenanceSql.Text("@algorithm", _passwordHasher.Algorithm),
                MaintenanceSql.P("@utcNow", SqlDbType.DateTime2, utcNow), MaintenanceSql.P("@expectedUserId", SqlDbType.Int, expectedUserId));
            var updated = await updatedCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (updated != 1)
            {
                throw new InvalidOperationException("The bound superuser could not be recovered.");
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task RenameAsync(
        string maintenanceConnectionString,
        string configuredUsername,
        int expectedUserId,
        string newUsername,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var configuredNormalizedUsername = ValidateCommon(
            maintenanceConnectionString,
            configuredUsername,
            expectedUserId,
            utcNow);
        if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Trim().Length > 100)
        {
            throw new ArgumentException("A valid new username is required.", nameof(newUsername));
        }

        var normalizedNewUsername = UsernameNormalizer.Normalize(newUsername);
        var username = newUsername.Trim();
        if (string.Equals(
                configuredNormalizedUsername,
                normalizedNewUsername,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("The new username must differ from the current username.", nameof(newUsername));
        }

        using var connection = new SqlConnection(maintenanceConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        DatabaseMigrationPrincipalValidator.Validate(connection);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            await AcquireLockAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await RequireBoundAccountAsync(
                    connection,
                    transaction,
                    configuredNormalizedUsername,
                    expectedUserId,
                    cancellationToken)
                .ConfigureAwait(false);

            using var updatedAccountCommand = MaintenanceSql.Command(connection, transaction,
                "UPDATE dbo.Users SET Username = @username, NormalizedUsername = @normalizedNewUsername, " +
                "UpdatedAtUtc = @utcNow, UpdatedByUserId = NULL WHERE UserId = @expectedUserId;",
                MaintenanceSql.Text("@username", username), MaintenanceSql.Text("@normalizedNewUsername", normalizedNewUsername),
                MaintenanceSql.P("@utcNow", SqlDbType.DateTime2, utcNow), MaintenanceSql.P("@expectedUserId", SqlDbType.Int, expectedUserId));
            var updatedAccount = await updatedAccountCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            using var updatedBindingCommand = MaintenanceSql.Command(connection, transaction,
                "UPDATE dbo.SystemSecuritySettings SET BoundNormalizedUsername = @normalizedNewUsername " +
                "WHERE Id = 1 AND SuperuserUserId = @expectedUserId " +
                "AND BoundNormalizedUsername = @configuredNormalizedUsername;",
                MaintenanceSql.Text("@normalizedNewUsername", normalizedNewUsername), MaintenanceSql.P("@expectedUserId", SqlDbType.Int, expectedUserId),
                MaintenanceSql.Text("@configuredNormalizedUsername", configuredNormalizedUsername));
            var updatedBinding = await updatedBindingCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (updatedAccount != 1 || updatedBinding != 1)
            {
                throw new InvalidOperationException("The bound superuser identity could not be renamed.");
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static string ValidateCommon(
        string maintenanceConnectionString,
        string configuredUsername,
        int expectedUserId,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(maintenanceConnectionString))
        {
            throw new ArgumentException("A maintenance connection is required.", nameof(maintenanceConnectionString));
        }

        if (string.IsNullOrWhiteSpace(configuredUsername) ||
            configuredUsername.Trim().Length > 100 ||
            expectedUserId <= 0 ||
            utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Valid bound superuser maintenance values are required.");
        }

        return UsernameNormalizer.Normalize(configuredUsername);
    }

    private static async Task AcquireLockAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var lockResultCommand = MaintenanceSql.Command(connection, transaction,
            "DECLARE @result int; EXEC @result = sys.sp_getapplock " +
            "@Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', " +
            "@LockTimeout = 10000; SELECT @result;",
            MaintenanceSql.Text("@resource", LockResource));
        var lockResult = await MaintenanceSql.SingleAsync<int>(lockResultCommand, cancellationToken).ConfigureAwait(false);
        if (lockResult < 0)
        {
            throw new InvalidOperationException("The superuser maintenance lock could not be acquired.");
        }
    }

    private static async Task RequireBoundAccountAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string configuredNormalizedUsername,
        int expectedUserId,
        CancellationToken cancellationToken)
    {
        using var matchesCommand = MaintenanceSql.Command(connection, transaction,
            "SELECT CAST(CASE WHEN EXISTS (" +
            "SELECT 1 FROM dbo.SystemSecuritySettings AS settings WITH (UPDLOCK, HOLDLOCK) " +
            "INNER JOIN dbo.Users AS account WITH (UPDLOCK, HOLDLOCK) " +
            "ON account.UserId = settings.SuperuserUserId " +
            "WHERE settings.Id = 1 AND settings.SuperuserUserId = @expectedUserId " +
            "AND settings.BoundNormalizedUsername = @configuredNormalizedUsername " +
            "AND account.NormalizedUsername = settings.BoundNormalizedUsername) " +
            "THEN 1 ELSE 0 END AS bit);",
            MaintenanceSql.P("@expectedUserId", SqlDbType.Int, expectedUserId), MaintenanceSql.Text("@configuredNormalizedUsername", configuredNormalizedUsername));
        var matches = await MaintenanceSql.SingleAsync<bool>(matchesCommand, cancellationToken).ConfigureAwait(false);
        if (!matches)
        {
            throw new InvalidOperationException(
                "The expected user ID, configured username and database binding do not identify the same superuser.");
        }
    }
}
