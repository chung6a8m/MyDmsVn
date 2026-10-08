using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Identity;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests
{
    public sealed class LegacyPasswordVerifierTests
    {
        [Fact]
        public void Unsupported_legacy_algorithm_fails_closed()
        {
            var verifier = new UnsupportedLegacyPasswordVerifier();

            Assert.False(verifier.Supports("UnknownLegacy"));
            Assert.False(verifier.Verify("UnknownLegacy", "password", "hash", "salt"));
        }

        [Fact]
        public void Sql_persistence_registers_fail_closed_legacy_verifier()
        {
            var services = new ServiceCollection();
            services.AddSqlPersistence(
                "Server=invalid.invalid;Database=MyDmsVn;User ID=test;Password=test;Encrypt=false");

            using var provider = services.BuildServiceProvider();

            Assert.IsType<UnsupportedLegacyPasswordVerifier>(
                provider.GetRequiredService<ILegacyPasswordVerifier>());
        }
    }
}
