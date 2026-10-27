using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Application.Security;
using Xunit;
namespace MyDmsVn.Server.Application.Tests;
public sealed class SalesPermissionTests
{
    [Fact]
    public void Exactly_three_distinct_active_sales_keys_are_composed()
    {
        var sales = PermissionRegistryComposition.CreateDefault().Definitions.Where(d => d.Module == "Sales").ToArray();
        Assert.Equal(new[] { "Sales.Receipts.Post", "Sales.Receipts.Read", "Sales.Receipts.Write" }, sales.Select(d => d.Key).OrderBy(x => x));
        Assert.All(sales, d => Assert.Equal(PermissionLifecycleStatus.Active, d.LifecycleStatus));
    }
    [Theory]
    [InlineData(SalesPermissionKeys.ReceiptsRead)]
    [InlineData(SalesPermissionKeys.ReceiptsWrite)]
    [InlineData(SalesPermissionKeys.ReceiptsPost)]
    public async Task Separate_grants_active_identity_and_direct_deny_are_preserved(string key)
    {
        var user = new User(); var store = new Store(); var service = new PermissionAuthorizationService(user, store, PermissionRegistryComposition.CreateDefault());
        Assert.False(await service.IsAllowedAsync(key, CancellationToken.None));
        store.Grant = key; Assert.True(await service.IsAllowedAsync(key, CancellationToken.None));
        foreach (var other in SalesPermissionDefinitions.All.Where(d => d.Key != key)) Assert.False(await service.IsAllowedAsync(other.Key, CancellationToken.None));
        store.Deny = true; Assert.False(await service.IsAllowedAsync(key, CancellationToken.None));
        store.Deny = false; store.Active = false; Assert.False(await service.IsAllowedAsync(key, CancellationToken.None));
        store.Active = true; user.Current = CurrentUser.Authenticated(42, "operator", "Operator", false); Assert.False(await service.IsAllowedAsync(key, CancellationToken.None));
    }
    [Fact]
    public async Task Unknown_and_deprecated_deny_before_superuser_or_store()
    {
        var store = new Store();
        var registry = PermissionRegistry.Create(new[] { new PermissionDefinition(SalesPermissionKeys.ReceiptsRead, "Sales", "Retired", PermissionLifecycleStatus.Deprecated) });
        var service = new PermissionAuthorizationService(new User(), store, registry, new SuperuserIdentityPolicy("operator"));
        Assert.False(await service.IsAllowedAsync(SalesPermissionKeys.ReceiptsRead, CancellationToken.None));
        Assert.False(await service.IsAllowedAsync("Sales.Unknown", CancellationToken.None)); Assert.Equal(0, store.Queries);
    }
    private sealed class User : ICurrentUserAccessor { public CurrentUser Current { get; set; } = CurrentUser.Authenticated(42, "operator", "Operator", true); }
    private sealed class Store : IPermissionStore
    {
        public string? Grant; public bool Deny; public bool Active = true; public int Queries;
        public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string key, CancellationToken token)
        { Queries++; return Task.FromResult(new PermissionSnapshot(Deny ? false : (bool?)null, key == Grant, Active)); }
    }
}
