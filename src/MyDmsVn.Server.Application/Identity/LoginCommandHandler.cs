using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application.Identity
{
    internal sealed class LoginCommandHandler
        : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
    {
        private readonly IUserStore _userStore;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILegacyPasswordVerifier _legacyPasswordVerifier;
        private readonly ISecurityAuditSink _auditSink;
        private readonly IUtcClock _clock;

        public LoginCommandHandler(
            IUserStore userStore,
            IPasswordHasher passwordHasher,
            ILegacyPasswordVerifier legacyPasswordVerifier,
            ISecurityAuditSink auditSink,
            IUtcClock clock)
        {
            _userStore = userStore;
            _passwordHasher = passwordHasher;
            _legacyPasswordVerifier = legacyPasswordVerifier;
            _auditSink = auditSink;
            _clock = clock;
        }

        public async Task<ErrorOr<CurrentUserDto>> Handle(
            LoginCommand request,
            CancellationToken cancellationToken)
        {
            var normalizedUsername = UsernameNormalizer.Normalize(request.Username);
            var user = await _userStore
                .FindByNormalizedUsernameAsync(normalizedUsername, cancellationToken)
                .ConfigureAwait(false);

            if (user == null)
            {
                await AuditAsync(null, SecurityAuditOutcome.Failed, cancellationToken)
                    .ConfigureAwait(false);
                return InvalidCredentials();
            }

            if (!user.IsActive)
            {
                await AuditAsync(user.UserId, SecurityAuditOutcome.Disabled, cancellationToken)
                    .ConfigureAwait(false);
                return InvalidCredentials();
            }

            var isCurrentAlgorithm = string.Equals(
                user.PasswordAlgorithm,
                _passwordHasher.Algorithm,
                StringComparison.Ordinal);
            var verified = isCurrentAlgorithm
                ? _passwordHasher.Verify(request.Password, user.PasswordHash)
                : _legacyPasswordVerifier.Supports(user.PasswordAlgorithm) &&
                    _legacyPasswordVerifier.Verify(
                        user.PasswordAlgorithm,
                        request.Password,
                        user.PasswordHash,
                        user.PasswordSalt);

            if (!verified)
            {
                await AuditAsync(user.UserId, SecurityAuditOutcome.Failed, cancellationToken)
                    .ConfigureAwait(false);
                return InvalidCredentials();
            }

            if (!isCurrentAlgorithm || _passwordHasher.NeedsRehash(user.PasswordHash))
            {
                var replacement = new PasswordReplacement(
                    user.UserId,
                    user.PasswordHash,
                    user.PasswordAlgorithm,
                    _passwordHasher.Hash(request.Password),
                    string.Empty,
                    _passwordHasher.Algorithm,
                    _clock.UtcNow);
                var replaced = await _userStore
                    .TryReplacePasswordAsync(replacement, cancellationToken)
                    .ConfigureAwait(false);
                if (!replaced)
                {
                    await AuditAsync(user.UserId, SecurityAuditOutcome.RehashFailed, cancellationToken)
                        .ConfigureAwait(false);
                    return InvalidCredentials();
                }
            }

            await AuditAsync(user.UserId, SecurityAuditOutcome.Succeeded, cancellationToken)
                .ConfigureAwait(false);
            return new CurrentUserDto(user.UserId, user.Username, user.DisplayName);
        }

        private Task AuditAsync(
            int? userId,
            SecurityAuditOutcome outcome,
            CancellationToken cancellationToken)
        {
            return _auditSink.WriteAsync(
                new SecurityAuditEntry(
                    SecurityAuditAction.Authentication,
                    outcome,
                    userId,
                    _clock.UtcNow),
                cancellationToken);
        }

        private static Error InvalidCredentials()
        {
            return Error.Unauthorized(
                "Auth.InvalidCredentials",
                "The username or password is invalid.");
        }
    }
}
