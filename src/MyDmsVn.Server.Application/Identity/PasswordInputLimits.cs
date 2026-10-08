using System.Text;

namespace MyDmsVn.Server.Application.Identity
{
    public static class PasswordInputLimits
    {
        public const int BcryptMaximumUtf8Bytes = 72;

        public static bool FitsBcrypt(string password)
        {
            return password != null &&
                Encoding.UTF8.GetByteCount(password) <= BcryptMaximumUtf8Bytes;
        }
    }
}
