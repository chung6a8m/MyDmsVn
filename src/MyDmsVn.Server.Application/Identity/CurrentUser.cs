using System;

namespace MyDmsVn.Server.Application.Identity
{
    public sealed class CurrentUser
    {
        private CurrentUser(
            int? userId,
            string? username,
            string? displayName,
            bool isAuthenticated,
            bool isActive)
        {
            UserId = userId;
            Username = username;
            DisplayName = displayName;
            IsAuthenticated = isAuthenticated;
            IsActive = isActive;
        }

        public static CurrentUser Anonymous { get; } =
            new CurrentUser(null, null, null, false, false);

        public int? UserId { get; }

        public string? Username { get; }

        public string? DisplayName { get; }

        public bool IsAuthenticated { get; }

        public bool IsActive { get; }

        public static CurrentUser Authenticated(
            int userId,
            string username,
            string displayName,
            bool isActive)
        {
            if (userId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("A username is required.", nameof(username));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A display name is required.", nameof(displayName));
            }

            return new CurrentUser(userId, username, displayName, true, isActive);
        }
    }
}
