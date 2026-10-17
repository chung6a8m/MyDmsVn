using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
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
            var lockResult = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "DECLARE @result int; EXEC @result = sys.sp_getapplock " +
                "@Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', " +
                "@LockTimeout = 10000; SELECT @result;",
                new { resource = LockResource },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (lockResult < 0)
            {
                throw new InvalidOperationException("The superuser bootstrap lock could not be acquired.");
            }

            var existingState = await connection.QuerySingleAsync<int>(new CommandDefinition(
                "SELECT (SELECT COUNT(*) FROM dbo.Users) + " +
                "(SELECT COUNT(*) FROM dbo.SystemSecuritySettings);",
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (existingState != 0)
            {
                throw new InvalidOperationException(
                    "Bootstrap requires an empty user store and an unbound security settings table.");
            }

            var passwordHash = _passwordHasher.Hash(password);
            var userId = await connection.QuerySingleAsync<int>(new CommandDefinition(
                "INSERT dbo.Users " +
                "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, " +
                "PasswordAlgorithm, IsActive, CreatedAtUtc) " +
                "OUTPUT INSERTED.UserId VALUES " +
                "(@username, @normalizedUsername, @displayName, N'Local', @passwordHash, N'', " +
                "@algorithm, 1, @utcNow);",
                new
                {
                    username,
                    normalizedUsername,
                    displayName = displayName.Trim(),
                    passwordHash,
                    algorithm = _passwordHasher.Algorithm,
                    utcNow,
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT dbo.SystemSecuritySettings (Id, SuperuserUserId, BoundNormalizedUsername) " +
                "VALUES (1, @userId, @normalizedUsername);",
                new { userId, normalizedUsername },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

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
