using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning;

public sealed class SuperuserBootstrapEngine
{
    private const string LockResource = "MyDmsVn.SuperuserBootstrap";
    private readonly IPasswordHasher _passwordHasher;

    public SuperuserBootstrapEngine(IPasswordHasher passwordHasher)
    {
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task<int> BootstrapAsync(
        string maintenanceConnectionString,
        string configuredUsername,
        string displayName,
        string password,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(maintenanceConnectionString))
        {
            throw new ArgumentException("A maintenance connection is required.", nameof(maintenanceConnectionString));
        }

        if (string.IsNullOrWhiteSpace(configuredUsername) ||
            string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(password) ||
            utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Valid superuser enrollment values are required.");
        }

        var username = configuredUsername.Trim();
        var normalizedUsername = UsernameNormalizer.Normalize(username);
        using var connection = new SqlConnection(maintenanceConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        DatabaseMigrationPrincipalValidator.Validate(connection);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            using var lockResultCommand = MaintenanceSql.Command(connection, transaction,
                "DECLARE @result int; EXEC @result = sys.sp_getapplock " +
                "@Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', " +
                "@LockTimeout = 10000; SELECT @result;",
                MaintenanceSql.Text("@resource", LockResource));
            var lockResult = await MaintenanceSql.SingleAsync<int>(lockResultCommand, cancellationToken).ConfigureAwait(false);
            if (lockResult < 0)
            {
                throw new InvalidOperationException("The superuser bootstrap lock could not be acquired.");
            }

            using var existingStateCommand = MaintenanceSql.Command(connection, transaction,
                "SELECT (SELECT COUNT(*) FROM dbo.Users) + " +
                "(SELECT COUNT(*) FROM dbo.SystemSecuritySettings);");
            var existingState = await MaintenanceSql.SingleAsync<int>(existingStateCommand, cancellationToken).ConfigureAwait(false);
            if (existingState != 0)
            {
                throw new InvalidOperationException(
                    "Bootstrap requires an empty user store and an unbound security settings table.");
            }

            var passwordHash = _passwordHasher.Hash(password);
            using var userIdCommand = MaintenanceSql.Command(connection, transaction,
                "INSERT dbo.Users " +
                "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, " +
                "PasswordAlgorithm, IsActive, CreatedAtUtc) " +
                "OUTPUT INSERTED.UserId VALUES " +
                "(@username, @normalizedUsername, @displayName, N'Local', @passwordHash, N'', " +
                "@algorithm, 1, @utcNow);",
                MaintenanceSql.Text("@username", username), MaintenanceSql.Text("@normalizedUsername", normalizedUsername),
                MaintenanceSql.Text("@displayName", displayName.Trim()), MaintenanceSql.Text("@passwordHash", passwordHash),
                MaintenanceSql.Text("@algorithm", _passwordHasher.Algorithm), MaintenanceSql.P("@utcNow", SqlDbType.DateTime2, utcNow));
            var userId = await MaintenanceSql.SingleAsync<int>(userIdCommand, cancellationToken).ConfigureAwait(false);

            using var bindingCommand = MaintenanceSql.Command(connection, transaction,
                "INSERT dbo.SystemSecuritySettings (Id, SuperuserUserId, BoundNormalizedUsername) " +
                "VALUES (1, @userId, @normalizedUsername);",
                MaintenanceSql.P("@userId", SqlDbType.Int, userId), MaintenanceSql.Text("@normalizedUsername", normalizedUsername));
            await bindingCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            transaction.Commit();
            return userId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
