using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Identity
{
    public sealed class SqlIdentityStore : IUserStore, IPermissionStore, IUserAccountValidator
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
            using var command = SqlRead.Command(connection, null,
                "SELECT TOP (1) UserId, Username, NormalizedUsername, DisplayName, IsActive, " +
                "PasswordHash, PasswordSalt, PasswordAlgorithm " +
                "FROM dbo.Users WHERE NormalizedUsername = @normalizedUsername;",
                SqlRead.Text("@normalizedUsername", normalizedUsername));
            var row = await SqlRead.SingleOrDefaultAsync(command, reader => new UserAccountRow
            {
                UserId = reader.GetInt32(0), Username = reader.GetString(1), NormalizedUsername = reader.GetString(2),
                DisplayName = reader.GetString(3), IsActive = reader.GetBoolean(4), PasswordHash = reader.GetString(5),
                PasswordSalt = reader.GetString(6), PasswordAlgorithm = reader.GetString(7),
            }, cancellationToken).ConfigureAwait(false);
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
            using var command = SqlRead.Command(connection, null,
                "UPDATE dbo.Users SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt, " +
                "PasswordAlgorithm = @PasswordAlgorithm, UpdatedAtUtc = @UpdatedAtUtc, " +
                "UpdatedByUserId = @UserId " +
                "WHERE UserId = @UserId AND PasswordHash = @ExpectedPasswordHash " +
                "AND PasswordAlgorithm = @ExpectedPasswordAlgorithm AND IsActive = 1;",
                SqlRead.Text("@PasswordHash", replacement.PasswordHash), SqlRead.Text("@PasswordSalt", replacement.PasswordSalt),
                SqlRead.Text("@PasswordAlgorithm", replacement.PasswordAlgorithm), SqlRead.P("@UpdatedAtUtc", SqlDbType.DateTime2, replacement.UpdatedAtUtc),
                SqlRead.P("@UserId", SqlDbType.Int, replacement.UserId), SqlRead.Text("@ExpectedPasswordHash", replacement.ExpectedPasswordHash),
                SqlRead.Text("@ExpectedPasswordAlgorithm", replacement.ExpectedPasswordAlgorithm));
            var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return affected == 1;
        }

        public async Task<bool> IsActiveAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            if (userId <= 0)
            {
                return false;
            }

            using var connection = await _connectionFactory
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            using var command = SqlRead.Command(connection, null,
                "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Users " +
                "WHERE UserId = @userId AND IsActive = 1) THEN 1 ELSE 0 END AS bit);",
                SqlRead.P("@userId", SqlDbType.Int, userId));
            return await SqlRead.SingleAsync(command, reader => reader.GetBoolean(0), cancellationToken).ConfigureAwait(false);
        }

        public async Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            using var connection = await _connectionFactory
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                using var command = SqlRead.Command(connection, transaction,
                        "SELECT " +
                        "CAST(CASE WHEN u.IsActive = 1 THEN 1 ELSE 0 END AS bit) AS IsAccountActive, " +
                        "u.NormalizedUsername AS DatabaseUsername, " +
                        "settings.SuperuserUserId AS BoundSuperuserUserId, " +
                        "settings.BoundNormalizedUsername AS BoundNormalizedUsername, " +
                        "CASE WHEN u.IsActive = 1 THEN (" +
                        "SELECT TOP (1) up.Granted FROM dbo.UserPermissions AS up " +
                        "WHERE up.UserId = u.UserId AND up.PermissionKey = @permissionKey " +
                        "AND NOT EXISTS (SELECT 1 FROM dbo.SystemSecuritySettings AS bound " +
                        "WHERE bound.Id = 1 AND bound.SuperuserUserId = u.UserId " +
                        "AND bound.BoundNormalizedUsername = u.NormalizedUsername)" +
                        ") ELSE NULL END AS DirectDecision, " +
                        "CAST(CASE WHEN u.IsActive = 1 AND EXISTS (" +
                        "SELECT 1 FROM dbo.UserRoles AS ur " +
                        "INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId AND r.IsActive = 1 " +
                        "INNER JOIN dbo.RolePermissions AS rp ON rp.RoleId = ur.RoleId " +
                        "WHERE ur.UserId = u.UserId AND rp.PermissionKey = @permissionKey" +
                        " AND NOT EXISTS (SELECT 1 FROM dbo.SystemSecuritySettings AS bound " +
                        "WHERE bound.Id = 1 AND bound.SuperuserUserId = u.UserId " +
                        "AND bound.BoundNormalizedUsername = u.NormalizedUsername)" +
                        ") THEN 1 ELSE 0 END AS bit) AS HasRoleGrant " +
                        "FROM dbo.Users AS u " +
                        "LEFT JOIN dbo.SystemSecuritySettings AS settings ON settings.Id = 1 " +
                        "WHERE u.UserId = @userId;",
                    SqlRead.P("@userId", SqlDbType.Int, userId), SqlRead.Text("@permissionKey", permissionKey));
                var row = await SqlRead.SingleOrDefaultAsync(command, reader => new PermissionSnapshotRow
                {
                    IsAccountActive = reader.GetBoolean(0), DatabaseUsername = reader.IsDBNull(1) ? null : reader.GetString(1),
                    BoundSuperuserUserId = reader.IsDBNull(2) ? null : reader.GetInt32(2), BoundNormalizedUsername = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DirectDecision = reader.IsDBNull(4) ? null : reader.GetBoolean(4), HasRoleGrant = reader.GetBoolean(5),
                }, cancellationToken).ConfigureAwait(false);
                transaction.Commit();
                return row == null
                    ? new PermissionSnapshot(null, false, false)
                    : new PermissionSnapshot(
                        row.DirectDecision,
                        row.HasRoleGrant,
                        row.IsAccountActive,
                        row.DatabaseUsername,
                        row.BoundSuperuserUserId,
                        row.BoundNormalizedUsername);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
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

        private sealed class PermissionSnapshotRow
        {
            public bool IsAccountActive { get; set; }

            public string? DatabaseUsername { get; set; }

            public int? BoundSuperuserUserId { get; set; }

            public string? BoundNormalizedUsername { get; set; }

            public bool? DirectDecision { get; set; }

            public bool HasRoleGrant { get; set; }
        }
    }
}
