using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class SecurityAdministrationTests
{
    [Fact]
    public void Mutation_uses_target_management_permission()
    {
        Assert.Equal(
            PermissionKeys.SecurityUsersManage,
            new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetActive).PermissionKey);
        Assert.Equal(
            PermissionKeys.SecurityRolesManage,
            new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.Role,
                SecurityAdministrationOperation.SetPermission).PermissionKey);
    }

    [Fact]
    public async Task Mutation_binds_actor_from_authenticated_session_not_request_data()
    {
        var store = new StubAdministrationStore();
        var services = CreateServices(store, permissionAllowed: true);
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<ISender>().Send(
            new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetActive,
                targetId: 31,
                isActive: false,
                expectedRevision: 4));

        Assert.False(result.IsError);
        Assert.NotNull(store.LastMutation);
        Assert.Equal(17, store.LastMutation!.ActorUserId);
        Assert.Equal(31, store.LastMutation.TargetId);
    }

    [Fact]
    public async Task Reserved_superuser_name_cannot_be_created_through_normal_administration()
    {
        var store = new StubAdministrationStore();
        var services = CreateServices(store, permissionAllowed: true);
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<ISender>().Send(
            new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.Create,
                name: " root.admin ",
                displayName: "Root",
                password: "safe-test-password"));

        Assert.True(result.IsError);
        Assert.Equal("Security.ReservedSuperuserName", result.FirstError.Code);
        Assert.Null(store.LastMutation);
    }

    [Fact]
    public async Task A_denied_administrator_command_never_reaches_the_store()
    {
        var store = new StubAdministrationStore();
        var services = CreateServices(store, permissionAllowed: false);
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<ISender>().Send(
            new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.Role,
                SecurityAdministrationOperation.Create,
                name: "Warehouse"));

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Null(store.LastMutation);
    }

    [Fact]
    public async Task Unknown_or_deprecated_permission_keys_are_not_assignable()
    {
        var store = new StubAdministrationStore();
        var services = CreateServices(store, permissionAllowed: true);
        using var provider = services.BuildServiceProvider();

        var unknown = await provider.GetRequiredService<ISender>().Send(
            new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.Role,
                SecurityAdministrationOperation.SetPermission,
                targetId: 8,
                permissionKey: "Catalog.Unknown.Read",
                decision: true,
                expectedRevision: 0));
        var nonCanonical = await provider.GetRequiredService<ISender>().Send(
            new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.Role,
                SecurityAdministrationOperation.SetPermission,
                targetId: 8,
                permissionKey: PermissionKeys.CatalogProductsRead + " ",
                decision: true,
                expectedRevision: 0));

        Assert.True(unknown.IsError);
        Assert.True(nonCanonical.IsError);
        Assert.Null(store.LastMutation);
    }

    private static ServiceCollection CreateServices(
        StubAdministrationStore store,
        bool permissionAllowed)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserAccessor>(new StubCurrentUserAccessor());
        services.AddSingleton<IPermissionStore>(new StubPermissionStore(permissionAllowed));
        services.AddSingleton<ISecurityAdministrationStore>(store);
        services.AddSingleton<IPasswordHasher, StubPasswordHasher>();
        services.AddServerApplication("root.admin");
        return services;
    }

    private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser Current => CurrentUser.Authenticated(17, "operator", "Operator", true);
    }

    private sealed class StubPermissionStore : IPermissionStore
    {
        private readonly bool _allowed;

        public StubPermissionStore(bool allowed) => _allowed = allowed;

        public Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PermissionSnapshot(_allowed, false));
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Algorithm => "Test";
        public string Hash(string password) => "hashed";
        public bool Verify(string password, string passwordHash) => false;
        public void VerifyForTiming(string password) { }
        public bool NeedsRehash(string passwordHash) => false;
    }

    private sealed class StubAdministrationStore : ISecurityAdministrationStore
    {
        public SecurityAdministrationMutation? LastMutation { get; private set; }

        public Task<IReadOnlyList<SecurityUserDto>> ListUsersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SecurityUserDto>>(new List<SecurityUserDto>());

        public Task<IReadOnlyList<SecurityRoleDto>> ListRolesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SecurityRoleDto>>(new List<SecurityRoleDto>());

        public Task<ErrorOr<SecurityAdministrationResultDto>> ApplyAsync(
            SecurityAdministrationMutation mutation,
            CancellationToken cancellationToken)
        {
            LastMutation = mutation;
            return Task.FromResult<ErrorOr<SecurityAdministrationResultDto>>(
                new SecurityAdministrationResultDto(5, true));
        }
    }
}
