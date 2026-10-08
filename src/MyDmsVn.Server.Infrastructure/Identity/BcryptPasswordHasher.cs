using System;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.Infrastructure.Identity
{
    public sealed class BcryptPasswordHasher : IPasswordHasher
    {
        public const string AlgorithmName = "BCrypt";
        public const int DefaultWorkFactor = 12;

        private const string TimingVerificationHash =
            "$2a$12$0PHck83ZN3JHVvlAEViX.ui/HKwJGyLrxp56jbAodCpsZZdQYFYH2";

        private readonly int _workFactor;

        public BcryptPasswordHasher(int workFactor = DefaultWorkFactor)
        {
            if (workFactor < 10 || workFactor > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(workFactor));
            }

            _workFactor = workFactor;
        }

        public string Algorithm => AlgorithmName;

        public string Hash(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("A password is required.", nameof(password));
            }

            if (!PasswordInputLimits.FitsBcrypt(password))
            {
                throw new ArgumentException(
                    $"A password cannot exceed {PasswordInputLimits.BcryptMaximumUtf8Bytes} UTF-8 bytes for BCrypt.",
                    nameof(password));
            }

            return BCrypt.Net.BCrypt.HashPassword(password, _workFactor);
        }

        public bool Verify(string password, string passwordHash)
        {
            if (string.IsNullOrEmpty(password) ||
                !PasswordInputLimits.FitsBcrypt(password) ||
                string.IsNullOrWhiteSpace(passwordHash))
            {
                return false;
            }

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, passwordHash);
            }
            catch (Exception exception) when (
                exception is BCrypt.Net.SaltParseException ||
                exception is ArgumentException ||
                exception is FormatException)
            {
                return false;
            }
        }

        public void VerifyForTiming(string password)
        {
            BCrypt.Net.BCrypt.Verify(password ?? string.Empty, TimingVerificationHash);
        }

        public bool NeedsRehash(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                return true;
            }

            try
            {
                return BCrypt.Net.BCrypt.PasswordNeedsRehash(passwordHash, _workFactor);
            }
            catch (Exception exception) when (
                exception is BCrypt.Net.SaltParseException ||
                exception is ArgumentException ||
                exception is FormatException)
            {
                return true;
            }
        }
    }
}
