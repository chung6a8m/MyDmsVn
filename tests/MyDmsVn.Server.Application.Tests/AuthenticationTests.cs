using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using Xunit;

namespace MyDmsVn.Server.Application.Tests
{
    public sealed class AuthenticationTests
    {
        [Fact]
        public async Task Valid_current_hash_returns_user_and_records_safe_utc_audit()
        {
            var user = StoredUser(isActive: true, algorithm: "BCrypt");
            var store = new FakeUserStore { User = user };
            var audit = new CapturingSecurityAuditSink();
            var sender = CreateProvider(store, new FakePasswordHasher(), new FakeLegacyVerifier(), audit)
                .GetRequiredService<MediatR.ISender>();

            var result = await sender.Send(
                new LoginCommand(" operator ", "correct-password"),
                CancellationToken.None);

            Assert.False(result.IsError);
            Assert.Equal(42, result.Value.UserId);
            Assert.Null(store.Replacement);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal(SecurityAuditAction.Authentication, entry.Action);
            Assert.Equal(SecurityAuditOutcome.Succeeded, entry.Outcome);
            Assert.Equal(42, entry.UserId);
            Assert.Equal(DateTimeKind.Utc, entry.OccurredAtUtc.Kind);
        }

        [Theory]
        [InlineData(false, "BCrypt", "wrong-password")]
        [InlineData(true, "UnknownLegacy", "correct-password")]
        public async Task Wrong_disabled_or_unsupported_credentials_return_same_public_error(
            bool isActive,
            string algorithm,
            string password)
        {
            var store = new FakeUserStore { User = StoredUser(isActive, algorithm) };
            using var provider = CreateProvider(
                store,
                new FakePasswordHasher(),
                new FakeLegacyVerifier(),
                new CapturingSecurityAuditSink());

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new LoginCommand("operator", password),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal("Auth.InvalidCredentials", result.FirstError.Code);
            Assert.Equal("The username or password is invalid.", result.FirstError.Description);
            Assert.Null(store.Replacement);
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("inactive")]
        [InlineData("unsupported")]
        public async Task Non_current_login_paths_still_apply_one_current_hash_verification_cost(
            string accountState)
        {
            var store = new FakeUserStore
            {
                User = accountState == "missing"
                    ? null
                    : StoredUser(
                        isActive: accountState != "inactive",
                        algorithm: accountState == "unsupported" ? "UnknownLegacy" : "BCrypt"),
            };
            var hasher = new FakePasswordHasher();
            using var provider = CreateProvider(
                store,
                hasher,
                new FakeLegacyVerifier(),
                new CapturingSecurityAuditSink());

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new LoginCommand("operator", "wrong-password"),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal(1, hasher.VerificationCount);
        }

        [Fact]
        public async Task Approved_legacy_verification_rehashes_with_bcrypt_and_empty_legacy_salt()
        {
            var store = new FakeUserStore
            {
                User = StoredUser(isActive: true, algorithm: "ApprovedLegacy"),
                ReplaceSucceeds = true,
            };
            var legacyVerifier = new FakeLegacyVerifier
            {
                SupportedAlgorithm = "ApprovedLegacy",
                VerificationResult = true,
            };
            using var provider = CreateProvider(
                store,
                new FakePasswordHasher(),
                legacyVerifier,
                new CapturingSecurityAuditSink());

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new LoginCommand("operator", "correct-password"),
                CancellationToken.None);

            Assert.False(result.IsError);
            Assert.NotNull(store.Replacement);
            Assert.Equal("new-bcrypt-hash", store.Replacement!.PasswordHash);
            Assert.Equal(string.Empty, store.Replacement.PasswordSalt);
            Assert.Equal("BCrypt", store.Replacement.PasswordAlgorithm);
            Assert.Equal("legacy-hash", store.Replacement.ExpectedPasswordHash);
        }

        [Fact]
        public async Task Concurrent_rehash_with_the_same_password_allows_the_losing_login()
        {
            var store = new FakeUserStore
            {
                User = StoredUser(isActive: true, algorithm: "BCrypt"),
                RefreshedUser = StoredUser(
                    isActive: true,
                    algorithm: "BCrypt",
                    passwordHash: "concurrent-bcrypt-hash"),
                ReplaceSucceeds = false,
            };
            var hasher = new FakePasswordHasher { NeedsRehashResult = true };
            using var provider = CreateProvider(
                store,
                hasher,
                new FakeLegacyVerifier(),
                new CapturingSecurityAuditSink());

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new LoginCommand("operator", "correct-password"),
                CancellationToken.None);

            Assert.False(result.IsError);
            Assert.Equal(2, store.LookupCount);
        }

        [Theory]
        [InlineData(true, "reset-bcrypt-hash")]
        [InlineData(false, "concurrent-bcrypt-hash")]
        public async Task Failed_atomic_rehash_rejects_a_password_reset_or_deactivated_account(
            bool refreshedIsActive,
            string refreshedPasswordHash)
        {
            var store = new FakeUserStore
            {
                User = StoredUser(isActive: true, algorithm: "BCrypt"),
                RefreshedUser = StoredUser(
                    refreshedIsActive,
                    "BCrypt",
                    refreshedPasswordHash),
                ReplaceSucceeds = false,
            };
            var hasher = new FakePasswordHasher { NeedsRehashResult = true };
            using var provider = CreateProvider(
                store,
                hasher,
                new FakeLegacyVerifier(),
                new CapturingSecurityAuditSink());

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new LoginCommand("operator", "correct-password"),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal("Auth.InvalidCredentials", result.FirstError.Code);
            Assert.Equal(2, store.LookupCount);
        }

        [Fact]
        public async Task Current_user_query_returns_only_active_authenticated_identity()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ICurrentUserAccessor>(
                new StubCurrentUserAccessor(
                    CurrentUser.Authenticated(42, "operator", "Operator", true)));
            services.AddServerApplication();
            using var provider = services.BuildServiceProvider();

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new GetCurrentUserQuery(),
                CancellationToken.None);

            Assert.False(result.IsError);
            Assert.Equal(42, result.Value.UserId);
        }

        [Fact]
        public async Task Anonymous_current_user_query_returns_unauthorized()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            using var provider = services.BuildServiceProvider();

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new GetCurrentUserQuery(),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal(ErrorOr.ErrorType.Unauthorized, result.FirstError.Type);
        }

        [Fact]
        public void Audit_entry_rejects_non_utc_timestamp()
        {
            Assert.Throws<ArgumentException>(() => new SecurityAuditEntry(
                SecurityAuditAction.PermissionChanged,
                SecurityAuditOutcome.Succeeded,
                42,
                new DateTime(2026, 10, 9, 13, 0, 0, DateTimeKind.Local)));
        }

        private static ServiceProvider CreateProvider(
            IUserStore store,
            IPasswordHasher passwordHasher,
            ILegacyPasswordVerifier legacyVerifier,
            ISecurityAuditSink auditSink)
        {
            var services = new ServiceCollection();
            services.AddSingleton(store);
            services.AddSingleton(passwordHasher);
            services.AddSingleton(legacyVerifier);
            services.AddSingleton(auditSink);
            services.AddSingleton<IUtcClock>(
                new StubUtcClock(new DateTime(2026, 10, 9, 6, 0, 0, DateTimeKind.Utc)));
            services.AddServerApplication();
            return services.BuildServiceProvider();
        }

        private static UserAccount StoredUser(
            bool isActive,
            string algorithm,
            string passwordHash = "legacy-hash")
        {
            return new UserAccount(
                42,
                "operator",
                "OPERATOR",
                "Operator",
                isActive,
                passwordHash,
                "legacy-salt",
                algorithm);
        }

        private sealed class FakeUserStore : IUserStore
        {
            public UserAccount? User { get; set; }
            public UserAccount? RefreshedUser { get; set; }
            public bool ReplaceSucceeds { get; set; } = true;
            public PasswordReplacement? Replacement { get; private set; }
            public int LookupCount { get; private set; }

            public Task<UserAccount?> FindByNormalizedUsernameAsync(
                string normalizedUsername,
                CancellationToken cancellationToken)
            {
                Assert.Equal("OPERATOR", normalizedUsername);
                LookupCount++;
                return Task.FromResult(LookupCount == 1 || RefreshedUser == null
                    ? User
                    : RefreshedUser);
            }

            public Task<bool> TryReplacePasswordAsync(
                PasswordReplacement replacement,
                CancellationToken cancellationToken)
            {
                Replacement = replacement;
                return Task.FromResult(ReplaceSucceeds);
            }
        }

        private sealed class FakePasswordHasher : IPasswordHasher
        {
            public string Algorithm => "BCrypt";
            public bool NeedsRehashResult { get; set; }
            public int VerificationCount { get; private set; }

            public string Hash(string password)
            {
                return "new-bcrypt-hash";
            }

            public bool Verify(string password, string passwordHash)
            {
                VerificationCount++;
                return password == "correct-password" &&
                    (passwordHash == "legacy-hash" || passwordHash == "concurrent-bcrypt-hash");
            }

            public void VerifyForTiming(string password)
            {
                VerificationCount++;
            }

            public bool NeedsRehash(string passwordHash)
            {
                return NeedsRehashResult;
            }
        }

        private sealed class FakeLegacyVerifier : ILegacyPasswordVerifier
        {
            public string? SupportedAlgorithm { get; set; }
            public bool VerificationResult { get; set; }

            public bool Supports(string algorithm)
            {
                return algorithm == SupportedAlgorithm;
            }

            public bool Verify(
                string algorithm,
                string password,
                string passwordHash,
                string passwordSalt)
            {
                return VerificationResult;
            }
        }

        private sealed class CapturingSecurityAuditSink : ISecurityAuditSink
        {
            public List<SecurityAuditEntry> Entries { get; } = new List<SecurityAuditEntry>();

            public Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken)
            {
                Entries.Add(entry);
                return Task.CompletedTask;
            }
        }

        private sealed class StubUtcClock : IUtcClock
        {
            public StubUtcClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; }
        }

        private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
        {
            public StubCurrentUserAccessor(CurrentUser current)
            {
                Current = current;
            }

            public CurrentUser Current { get; }
        }
    }
}
