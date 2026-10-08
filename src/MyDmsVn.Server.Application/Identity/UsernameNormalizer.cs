using System;

namespace MyDmsVn.Server.Application.Identity
{
    public static class UsernameNormalizer
    {
        public static string Normalize(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("A username is required.", nameof(username));
            }

            return username.Trim().ToUpperInvariant();
        }
    }
}
