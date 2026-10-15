using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Identity
{
    public interface ICurrentUserAccessor
    {
        CurrentUser Current { get; }
    }

    public interface IPasswordHasher
    {
        string Algorithm { get; }

        string Hash(string password);

        bool Verify(string password, string passwordHash);

        void VerifyForTiming(string password);

        bool NeedsRehash(string passwordHash);
    }

    public interface ILegacyPasswordVerifier
    {
        bool Supports(string algorithm);

        bool Verify(string algorithm, string password, string passwordHash, string passwordSalt);
    }

    public interface IUserStore
    {
        Task<UserAccount?> FindByNormalizedUsernameAsync(
            string normalizedUsername,
            CancellationToken cancellationToken);

        Task<bool> TryReplacePasswordAsync(
            PasswordReplacement replacement,
            CancellationToken cancellationToken);
    }

    public interface IUserAccountValidator
    {
        Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken);
    }

    internal sealed class RejectingUserAccountValidator : IUserAccountValidator
    {
        public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }
    }
}
