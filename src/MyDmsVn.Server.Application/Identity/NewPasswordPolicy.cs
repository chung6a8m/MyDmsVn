using System;
using System.Collections.Generic;

namespace MyDmsVn.Server.Application.Identity
{
    public static class NewPasswordPolicy
    {
        public const int MinimumLength = 12;

        private static readonly HashSet<string> KnownDefaults = new HashSet<string>(
            new[]
            {
                "Password123!",
                "Admin123456!",
                "ChangeMe123!",
            },
            StringComparer.OrdinalIgnoreCase);

        public static bool IsAcceptable(string password, string username)
        {
            if (string.IsNullOrWhiteSpace(password) ||
                password.Length < MinimumLength ||
                !PasswordInputLimits.FitsBcrypt(password) ||
                KnownDefaults.Contains(password))
            {
                return false;
            }

            var trimmedUsername = username?.Trim();
            if (trimmedUsername != null &&
                trimmedUsername.Length >= 3 &&
                password.IndexOf(trimmedUsername, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            var categories = 0;
            var hasUpper = false;
            var hasLower = false;
            var hasDigit = false;
            var hasOther = false;
            foreach (var character in password)
            {
                hasUpper |= char.IsUpper(character);
                hasLower |= char.IsLower(character);
                hasDigit |= char.IsDigit(character);
                hasOther |= !char.IsLetterOrDigit(character);
            }

            categories += hasUpper ? 1 : 0;
            categories += hasLower ? 1 : 0;
            categories += hasDigit ? 1 : 0;
            categories += hasOther ? 1 : 0;
            return categories >= 3;
        }
    }
}
