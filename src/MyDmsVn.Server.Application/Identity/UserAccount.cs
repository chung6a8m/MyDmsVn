using System;

namespace MyDmsVn.Server.Application.Identity
{
    public sealed class UserAccount
    {
        public UserAccount(
            int userId,
            string username,
            string normalizedUsername,
            string displayName,
            bool isActive,
            string passwordHash,
            string passwordSalt,
            string passwordAlgorithm)
        {
            UserId = userId;
            Username = username;
            NormalizedUsername = normalizedUsername;
            DisplayName = displayName;
            IsActive = isActive;
            PasswordHash = passwordHash;
            PasswordSalt = passwordSalt;
            PasswordAlgorithm = passwordAlgorithm;
        }

        public int UserId { get; }
        public string Username { get; }
        public string NormalizedUsername { get; }
        public string DisplayName { get; }
        public bool IsActive { get; }
        public string PasswordHash { get; }
        public string PasswordSalt { get; }
        public string PasswordAlgorithm { get; }
    }

    public sealed class PasswordReplacement
    {
        public PasswordReplacement(
            int userId,
            string expectedPasswordHash,
            string expectedPasswordAlgorithm,
            string passwordHash,
            string passwordSalt,
            string passwordAlgorithm,
            DateTime updatedAtUtc)
        {
            UserId = userId;
            ExpectedPasswordHash = expectedPasswordHash;
            ExpectedPasswordAlgorithm = expectedPasswordAlgorithm;
            PasswordHash = passwordHash;
            PasswordSalt = passwordSalt;
            PasswordAlgorithm = passwordAlgorithm;
            UpdatedAtUtc = updatedAtUtc;
        }

        public int UserId { get; }
        public string ExpectedPasswordHash { get; }
        public string ExpectedPasswordAlgorithm { get; }
        public string PasswordHash { get; }
        public string PasswordSalt { get; }
        public string PasswordAlgorithm { get; }
        public DateTime UpdatedAtUtc { get; }
    }
}
