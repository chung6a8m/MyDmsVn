using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Identity;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests
{
    public sealed class LoginTimingMitigationTests
    {
        private const string OutdatedPasswordHash =
            "$2a$10$UUw6HvtdyUhWhI7SlbM4Vuz5qxprDwSaqDCVWqXY1e4a3TkCQNZ8a";

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Oversized_password_is_rejected_before_user_lookup_with_real_hasher(
            bool useMultibytePassword)
        {
            var store = new FakeUserStore { User = StoredUser(OutdatedPasswordHash) };
            using var provider = CreateProvider(store);
            var password = useMultibytePassword
                ? new string('\u00e9', 37)
                : new string('a', 73);

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new LoginCommand("operator", password),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal("Validation.MaximumLength", result.FirstError.Code);
            Assert.Equal(0, store.LookupCount);
        }

        [Fact]
        public async Task Wrong_password_for_cost_10_hash_has_same_work_budget_as_missing_user()
        {
            var store = new FakeUserStore { User = StoredUser(OutdatedPasswordHash) };
            using var provider = CreateProvider(store);
            var sender = provider.GetRequiredService<MediatR.ISender>();

            await SendInvalidLoginAsync(sender, "operator");
            await SendInvalidLoginAsync(sender, "missing");

            var existingTicks = await MeasureInvalidLoginsAsync(sender, "operator");
            var missingTicks = await MeasureInvalidLoginsAsync(sender, "missing");
            var ratio = (double)existingTicks / missingTicks;

            Assert.InRange(ratio, 0.60, 1.80);
        }

        private static ServiceProvider CreateProvider(IUserStore store)
        {
            var services = new ServiceCollection();
            services.AddSingleton(store);
            services.AddSingleton<IPasswordHasher>(new BcryptPasswordHasher());
            services.AddSingleton<ILegacyPasswordVerifier, UnsupportedLegacyPasswordVerifier>();
            services.AddServerApplication();
            return services.BuildServiceProvider();
        }

        private static async Task<long> MeasureInvalidLoginsAsync(
            MediatR.ISender sender,
            string username)
        {
            var stopwatch = Stopwatch.StartNew();
            await SendInvalidLoginAsync(sender, username);
            await SendInvalidLoginAsync(sender, username);
            stopwatch.Stop();
            return stopwatch.ElapsedTicks;
        }

        private static async Task SendInvalidLoginAsync(MediatR.ISender sender, string username)
        {
            var result = await sender.Send(
                new LoginCommand(username, "wrong-password"),
                CancellationToken.None);
            Assert.True(result.IsError);
            Assert.Equal("Auth.InvalidCredentials", result.FirstError.Code);
        }

        private static UserAccount StoredUser(string passwordHash)
        {
            return new UserAccount(
                42,
                "operator",
                "OPERATOR",
                "Operator",
                true,
                passwordHash,
                string.Empty,
                BcryptPasswordHasher.AlgorithmName);
        }

        private sealed class FakeUserStore : IUserStore
        {
            public UserAccount? User { get; set; }

            public int LookupCount { get; private set; }

            public Task<UserAccount?> FindByNormalizedUsernameAsync(
                string normalizedUsername,
                CancellationToken cancellationToken)
            {
                LookupCount++;
                return Task.FromResult(
                    string.Equals(normalizedUsername, "OPERATOR", StringComparison.Ordinal)
                        ? User
                        : null);
            }

            public Task<bool> TryReplacePasswordAsync(
                PasswordReplacement replacement,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(false);
            }
        }
    }
}
