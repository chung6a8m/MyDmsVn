using System;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.Application.Security
{
    public sealed class SuperuserIdentityPolicy
    {
        private readonly string _configuredNormalizedUsername;

        public SuperuserIdentityPolicy(string? configuredUsername)
        {
            if (string.IsNullOrWhiteSpace(configuredUsername))
            {
                throw new ArgumentException("A configured superuser username is required.", nameof(configuredUsername));
            }

            _configuredNormalizedUsername = UsernameNormalizer.Normalize(configuredUsername!);
        }

        public bool Matches(
            int currentUserId,
            string? currentNormalizedUsername,
            int? boundUserId,
            string? boundNormalizedUsername)
        {
            return currentUserId > 0 &&
                   boundUserId == currentUserId &&
                   string.Equals(
                       _configuredNormalizedUsername,
                       currentNormalizedUsername,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       _configuredNormalizedUsername,
                       boundNormalizedUsername,
                       StringComparison.Ordinal);
        }
    }
}
