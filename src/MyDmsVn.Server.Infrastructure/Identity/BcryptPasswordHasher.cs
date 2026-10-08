using System;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.Infrastructure.Identity
{
    public sealed class BcryptPasswordHasher : IPasswordHasher
    {
        public const string AlgorithmName = "BCrypt";
        public const int DefaultWorkFactor = 12;

        private const string TimingHash10 =
            "$2a$10$JKhP2H4JWQ12flQWQAw5k.MBpGuTwLlXzKIdLlUaU5YIBCxKSh5Ei";
        private const string TimingHash11 =
            "$2a$11$lIEdP90XyykN0AvAj2VMRu5T3.G95gl4G3Zvts7eoqOLn3isicSGC";
        private const string TimingHash12 =
            "$2a$12$w2lxtU47YUD0G4wkUcS6zuRtwlw.FCLVoComRykoXRIKXrcNV2Y3a";
        private const string TimingHash13 =
            "$2a$13$SuqcTkD5x0AqeDg9ZExRfeF1XU7TDZNwQLQ51SOwWl0RxzoQ2Plnu";
        private const string TimingHash14 =
            "$2a$14$kIzHd3J9.7xIynj4kXKJy.1bw6TU6tTjI7T1O2k51o3WkLiJbrh3K";
        private const string TimingHash15 =
            "$2a$15$yGi9tGfbsyCA8GjKZRpeHeSXkywr11tx/X8OS7c6bI/wE/LgIK526";
        private const string TimingHash16 =
            "$2a$16$cKfORiYhy7OyR2y6HKYdb.BLpS9if0GSwit6Axy2KRrFIl6wOQvCa";

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
                var hashInformation = BCrypt.Net.BCrypt.InterrogateHash(passwordHash);
                if (!int.TryParse(hashInformation.WorkFactor, out var storedWorkFactor) ||
                    storedWorkFactor < 10 ||
                    storedWorkFactor > _workFactor)
                {
                    VerifyForTiming(password);
                    return false;
                }

                var verified = BCrypt.Net.BCrypt.Verify(password, passwordHash);
                if (!verified)
                {
                    EqualizeFailedVerification(password, storedWorkFactor);
                }

                return verified;
            }
            catch (Exception exception) when (
                exception is BCrypt.Net.HashInformationException ||
                exception is BCrypt.Net.SaltParseException ||
                exception is ArgumentException ||
                exception is FormatException)
            {
                VerifyForTiming(password);
                return false;
            }
        }

        public void VerifyForTiming(string password)
        {
            BCrypt.Net.BCrypt.Verify(password ?? string.Empty, GetTimingHash(_workFactor));
        }

        private void EqualizeFailedVerification(string password, int storedWorkFactor)
        {
            if (storedWorkFactor >= _workFactor)
            {
                return;
            }

            var additionalChecks = (1 << (_workFactor - storedWorkFactor)) - 1;
            var timingHash = GetTimingHash(storedWorkFactor);
            for (var check = 0; check < additionalChecks; check++)
            {
                BCrypt.Net.BCrypt.Verify(password, timingHash);
            }
        }

        private static string GetTimingHash(int workFactor)
        {
            switch (workFactor)
            {
                case 10:
                    return TimingHash10;
                case 11:
                    return TimingHash11;
                case 12:
                    return TimingHash12;
                case 13:
                    return TimingHash13;
                case 14:
                    return TimingHash14;
                case 15:
                    return TimingHash15;
                case 16:
                    return TimingHash16;
                default:
                    throw new ArgumentOutOfRangeException(nameof(workFactor));
            }
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
