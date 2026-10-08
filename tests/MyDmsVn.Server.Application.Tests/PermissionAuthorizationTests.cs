using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Server.Application.Tests
{
    public sealed class PermissionAuthorizationTests
    {
        [Fact]
        public async Task Direct_user_decision_overrides_role_union()
        {
            var store = new FakePermissionStore { DirectDecision = false, RoleGrant = true };
            var service = CreateService(ActiveUser(), store);

            Assert.False(await service.IsAllowedAsync(PermissionKeys.CatalogProductsWrite, CancellationToken.None));
            Assert.Equal(1, store.QueryCount);

            store.DirectDecision = true;
            store.RoleGrant = false;

            Assert.True(await service.IsAllowedAsync(PermissionKeys.CatalogProductsWrite, CancellationToken.None));
            Assert.Equal(2, store.QueryCount);
        }

        [Fact]
        public async Task Role_union_allows_only_when_no_direct_override_exists()
        {
            var store = new FakePermissionStore { DirectDecision = null, RoleGrant = true };
            var service = CreateService(ActiveUser(), store);

            Assert.True(await service.IsAllowedAsync(PermissionKeys.CatalogProductsRead, CancellationToken.None));

            store.RoleGrant = false;

            Assert.False(await service.IsAllowedAsync(PermissionKeys.CatalogProductsRead, CancellationToken.None));
        }

        [Fact]
        public async Task Unknown_permission_and_inactive_or_anonymous_user_fail_closed()
        {
            var store = new FakePermissionStore { DirectDecision = true, RoleGrant = true };

            Assert.False(await CreateService(ActiveUser(), store)
                .IsAllowedAsync("Unknown.Permission", CancellationToken.None));
            Assert.False(await CreateService(CurrentUser.Anonymous, store)
                .IsAllowedAsync(PermissionKeys.CatalogProductsRead, CancellationToken.None));
            Assert.False(await CreateService(
                    CurrentUser.Authenticated(42, "operator", "Operator", false),
                    store)
                .IsAllowedAsync(PermissionKeys.CatalogProductsRead, CancellationToken.None));
            Assert.Equal(0, store.QueryCount);
        }

        [Fact]
        public async Task Unauthorized_application_command_never_reaches_handler()
        {
            var services = new ServiceCollection();
            var tracker = new MutationTracker();
            services.AddSingleton(tracker);
            services.AddSingleton<ICurrentUserAccessor>(
                new StubCurrentUserAccessor(ActiveUser()));
            services.AddSingleton<IPermissionStore>(new FakePermissionStore());
            services.AddTransient<
                IRequestHandler<ProtectedMutationCommand, ErrorOr<string>>,
                ProtectedMutationHandler>();
            services.AddServerApplication();

            using var provider = services.BuildServiceProvider();
            var result = await provider.GetRequiredService<ISender>().Send(
                new ProtectedMutationCommand(),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
            Assert.Equal(0, tracker.Count);
        }

        [Fact]
        public async Task Anonymous_application_command_returns_unauthorized_without_query_or_mutation()
        {
            var services = new ServiceCollection();
            var tracker = new MutationTracker();
            var store = new FakePermissionStore { DirectDecision = true, RoleGrant = true };
            services.AddSingleton(tracker);
            services.AddSingleton<ICurrentUserAccessor>(
                new StubCurrentUserAccessor(CurrentUser.Anonymous));
            services.AddSingleton<IPermissionStore>(store);
            services.AddTransient<
                IRequestHandler<ProtectedMutationCommand, ErrorOr<string>>,
                ProtectedMutationHandler>();
            services.AddServerApplication();

            using var provider = services.BuildServiceProvider();
            var result = await provider.GetRequiredService<ISender>().Send(
                new ProtectedMutationCommand(),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
            Assert.Equal(0, store.QueryCount);
            Assert.Equal(0, tracker.Count);
        }

        private static PermissionAuthorizationService CreateService(
            CurrentUser currentUser,
            IPermissionStore store)
        {
            return new PermissionAuthorizationService(
                new StubCurrentUserAccessor(currentUser),
                store);
        }

        private static CurrentUser ActiveUser()
        {
            return CurrentUser.Authenticated(42, "operator", "Operator", true);
        }

        private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
        {
            public StubCurrentUserAccessor(CurrentUser current)
            {
                Current = current;
            }

            public CurrentUser Current { get; }
        }

        private sealed class FakePermissionStore : IPermissionStore
        {
            public bool? DirectDecision { get; set; }
            public bool RoleGrant { get; set; }
            public int QueryCount { get; private set; }

            public Task<PermissionSnapshot> GetSnapshotAsync(
                int userId,
                string permissionKey,
                CancellationToken cancellationToken)
            {
                QueryCount++;
                return Task.FromResult(new PermissionSnapshot(DirectDecision, RoleGrant));
            }
        }

        private sealed class ProtectedMutationCommand : ApplicationRequest<string>, IAuthorizedRequest
        {
            public string PermissionKey => PermissionKeys.CatalogProductsWrite;
        }

        private sealed class ProtectedMutationHandler
            : IRequestHandler<ProtectedMutationCommand, ErrorOr<string>>
        {
            private readonly MutationTracker _tracker;

            public ProtectedMutationHandler(MutationTracker tracker)
            {
                _tracker = tracker;
            }

            public Task<ErrorOr<string>> Handle(
                ProtectedMutationCommand request,
                CancellationToken cancellationToken)
            {
                _tracker.Count++;
                ErrorOr<string> result = "mutated";
                return Task.FromResult(result);
            }
        }

        private sealed class MutationTracker
        {
            public int Count { get; set; }
        }
    }
}
