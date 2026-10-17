using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Server.Application.Tests
{
    public sealed class SuperuserAuthorizationTests
    {
        [Fact]
        public async Task Bound_active_superuser_bypasses_direct_deny_without_assignment_rows()
        {
            var registry = PermissionRegistryComposition.CreateDefault();
            var activeDefinitions = registry.Definitions
                .Where(definition => definition.LifecycleStatus == PermissionLifecycleStatus.Active)
                .ToArray();
            var store = new AuthorizationStore
            {
                Snapshot = new PermissionSnapshot(
                    directDecision: false,
                    hasRoleGrant: false,
                    isAccountActive: true,
                    databaseUsername: "ROOT.ADMIN",
                    boundSuperuserUserId: 7,
                    boundNormalizedUsername: "ROOT.ADMIN"),
            };
            var service = CreateService(CurrentUser.Authenticated(7, "stale-name", "Root", true), store);

            Assert.NotEmpty(activeDefinitions);
            foreach (var definition in activeDefinitions)
            {
                Assert.True(await service.IsAllowedAsync(definition.Key, CancellationToken.None));
            }

            Assert.Equal(activeDefinitions.Length, store.QueryCount);
        }

        [Fact]
        public async Task Ordinary_administrator_role_does_not_receive_superuser_bypass()
        {
            var store = new AuthorizationStore
            {
                Snapshot = new PermissionSnapshot(
                    directDecision: null,
                    hasRoleGrant: false,
                    isAccountActive: true,
                    databaseUsername: "ADMINISTRATOR",
                    boundSuperuserUserId: 7,
                    boundNormalizedUsername: "ROOT.ADMIN"),
            };
            var service = CreateService(CurrentUser.Authenticated(8, "Administrator", "Administrator", true), store);

            Assert.False(await service.IsAllowedAsync(
                PermissionKeys.CatalogProductsWrite,
                CancellationToken.None));
        }

        [Fact]
        public async Task Disabled_bound_account_cannot_bypass_even_with_matching_binding()
        {
            var store = new AuthorizationStore
            {
                Snapshot = new PermissionSnapshot(
                    directDecision: true,
                    hasRoleGrant: true,
                    isAccountActive: false,
                    databaseUsername: "ROOT.ADMIN",
                    boundSuperuserUserId: 7,
                    boundNormalizedUsername: "ROOT.ADMIN"),
            };
            var service = CreateService(CurrentUser.Authenticated(7, "root.admin", "Root", true), store);

            Assert.False(await service.IsAllowedAsync(
                PermissionKeys.CatalogProductsWrite,
                CancellationToken.None));
        }

        [Fact]
        public async Task User_id_overload_cannot_authorize_a_different_subject()
        {
            var store = new AuthorizationStore
            {
                Snapshot = new PermissionSnapshot(
                    directDecision: true,
                    hasRoleGrant: true,
                    isAccountActive: true,
                    databaseUsername: "ROOT.ADMIN",
                    boundSuperuserUserId: 7,
                    boundNormalizedUsername: "ROOT.ADMIN"),
            };
            var service = CreateService(CurrentUser.Authenticated(7, "root.admin", "Root", true), store);

            Assert.False(await service.IsAllowedAsync(
                8,
                PermissionKeys.CatalogProductsWrite,
                CancellationToken.None));
            Assert.Equal(0, store.QueryCount);
        }

        [Fact]
        public void Configured_username_matches_only_the_current_bound_database_identity()
        {
            var policy = new SuperuserIdentityPolicy(" root.admin ");

            Assert.True(policy.Matches(7, "ROOT.ADMIN", 7, "ROOT.ADMIN"));
            Assert.False(policy.Matches(8, "ROOT.ADMIN", 7, "ROOT.ADMIN"));
            Assert.False(policy.Matches(7, "OTHER", 7, "ROOT.ADMIN"));
            Assert.False(policy.Matches(7, "ROOT.ADMIN", 7, "OTHER"));
            Assert.False(policy.Matches(7, "ROOT.ADMIN", null, "ROOT.ADMIN"));
        }

        [Fact]
        public void Blank_configuration_is_rejected()
        {
            foreach (var configuredUsername in new string?[] { null, string.Empty, "   " })
            {
                Assert.Throws<ArgumentException>(
                    () => new SuperuserIdentityPolicy(configuredUsername));
            }
        }

        private static PermissionAuthorizationService CreateService(
            CurrentUser currentUser,
            AuthorizationStore store)
        {
            return new PermissionAuthorizationService(
                new StubCurrentUserAccessor(currentUser),
                store,
                PermissionRegistryComposition.CreateDefault(),
                new SuperuserIdentityPolicy("root.admin"));
        }

        private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
        {
            public StubCurrentUserAccessor(CurrentUser current)
            {
                Current = current;
            }

            public CurrentUser Current { get; }
        }

        private sealed class AuthorizationStore : IPermissionStore
        {
            public PermissionSnapshot Snapshot { get; set; } = new PermissionSnapshot(null, false);
            public int QueryCount { get; private set; }

            public Task<PermissionSnapshot> GetSnapshotAsync(
                int userId,
                string permissionKey,
                CancellationToken cancellationToken)
            {
                QueryCount++;
                return Task.FromResult(Snapshot);
            }
        }
    }
}
