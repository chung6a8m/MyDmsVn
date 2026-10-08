using System;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using Xunit;

namespace MyDmsVn.Server.Application.Tests
{
    public sealed class IdentityAbstractionTests
    {
        [Fact]
        public void Anonymous_identity_is_unauthenticated_and_inactive()
        {
            var currentUser = CurrentUser.Anonymous;

            Assert.False(currentUser.IsAuthenticated);
            Assert.False(currentUser.IsActive);
            Assert.Null(currentUser.UserId);
        }

        [Theory]
        [InlineData("operator", "OPERATOR")]
        [InlineData("  Mixed.Case  ", "MIXED.CASE")]
        public void Username_normalization_is_trimmed_and_invariant(string username, string expected)
        {
            Assert.Equal(expected, UsernameNormalizer.Normalize(username));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Blank_username_cannot_be_normalized(string username)
        {
            Assert.Throws<ArgumentException>(() => UsernameNormalizer.Normalize(username));
        }

        [Fact]
        public void Application_registration_defaults_to_anonymous_current_user()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();

            using var provider = services.BuildServiceProvider();

            Assert.Same(CurrentUser.Anonymous, provider.GetRequiredService<ICurrentUserAccessor>().Current);
        }
    }
}
