using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Identity
{
    public sealed class SqlIdentityStore : IUserStore, IPermissionStore
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public SqlIdentityStore(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<UserAccount?> FindByNormalizedUsernameAsync(
            string normalizedUsername,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(normalizedUsername))
            {
                throw new ArgumentException("A normalized username is required.", nameof(normalizedUsername));
            }

            using var connection = await _connectionFactory
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            var row = await connection.QuerySingleOrDefaultAsync<UserAccountRow>(
                new CommandDefinition(
                    "SELECT TOP (1) UserId, Username, NormalizedUsername, DisplayName, IsActive, " +
                    "PasswordHash, PasswordSalt, PasswordAlgorithm " +
                    "FROM dbo.Users WHERE NormalizedUsername = @normalizedUsername;",
                    new { normalizedUsername },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            return row == null
                ? null
                : new UserAccount(
                    row.UserId,
                    row.Username,
                    row.NormalizedUsername,
                    row.DisplayName,
                    row.IsActive,
                    row.PasswordHash,
                    row.PasswordSalt,
                    row.PasswordAlgorithm);
        }

        public async Task<bool> TryReplacePasswordAsync(
            PasswordReplacement replacement,
            CancellationToken cancellationToken)
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            using var connection = await _connectionFactory
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            var affected = await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE dbo.Users SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt, " +
                    "PasswordAlgorithm = @PasswordAlgorithm, UpdatedAtUtc = @UpdatedAtUtc, " +
                    "UpdatedByUserId = @UserId " +
                    "WHERE UserId = @UserId AND PasswordHash = @ExpectedPasswordHash " +
                    "AND PasswordAlgorithm = @ExpectedPasswordAlgorithm AND IsActive = 1;",
                    replacement,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            return affected == 1;
        }

        public async Task<bool?> GetDirectDecisionAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            using var connection = await _connectionFactory
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            return await connection.QuerySingleOrDefaultAsync<bool?>(
                new CommandDefinition(
                    "SELECT up.Granted FROM dbo.UserPermissions AS up " +
                    "INNER JOIN dbo.Users AS u ON u.UserId = up.UserId AND u.IsActive = 1 " +
                    "WHERE up.UserId = @userId AND up.PermissionKey = @permissionKey;",
                    new { userId, permissionKey },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        public async Task<bool> HasRoleGrantAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            using var connection = await _connectionFactory
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            return await connection.QuerySingleAsync<bool>(
                new CommandDefinition(
                    "SELECT CAST(CASE WHEN EXISTS (" +
                    "SELECT 1 FROM dbo.UserRoles AS ur " +
                    "INNER JOIN dbo.Users AS u ON u.UserId = ur.UserId AND u.IsActive = 1 " +
                    "INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId AND r.IsActive = 1 " +
                    "INNER JOIN dbo.RolePermissions AS rp ON rp.RoleId = ur.RoleId " +
                    "WHERE ur.UserId = @userId AND rp.PermissionKey = @permissionKey" +
                    ") THEN 1 ELSE 0 END AS bit);",
                    new { userId, permissionKey },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        private sealed class UserAccountRow
        {
            public int UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string NormalizedUsername { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public string PasswordHash { get; set; } = string.Empty;
            public string PasswordSalt { get; set; } = string.Empty;
            public string PasswordAlgorithm { get; set; } = string.Empty;
        }
    }
}
