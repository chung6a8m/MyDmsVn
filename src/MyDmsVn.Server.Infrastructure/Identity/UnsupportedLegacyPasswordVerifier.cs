using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.Infrastructure.Identity
{
    public sealed class UnsupportedLegacyPasswordVerifier : ILegacyPasswordVerifier
    {
        public bool Supports(string algorithm)
        {
            return false;
        }

        public bool Verify(
            string algorithm,
            string password,
            string passwordHash,
            string passwordSalt)
        {
            return false;
        }
    }
}
