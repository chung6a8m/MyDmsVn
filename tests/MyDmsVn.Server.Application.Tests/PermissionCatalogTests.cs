using System;
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

public sealed class PermissionCatalogTests
{
    [Fact]
    public void Catalog_contains_sorted_current_definitions_and_a_stable_digest()
    {
        var registry = PermissionRegistryComposition.CreateDefault();

        var catalog = PermissionCatalogFactory.Create(registry, "9.8.7");

        Assert.Equal("9.8.7", catalog.ApplicationVersion);
        Assert.Equal(
            registry.Definitions.Select(definition => definition.Key),
            catalog.Items.Select(item => item.Key));
        Assert.Equal(64, catalog.CatalogDigest.Length);
        Assert.All(catalog.Items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Module));
            Assert.False(string.IsNullOrWhiteSpace(item.Description));
            Assert.Contains(item.Status, new[] { "Active", "Deprecated" });
        });
        Assert.Contains(catalog.Items, item =>
            item.Key == SecurityPermissionKeys.PermissionsRead && item.Status == "Active");
        Assert.True(registry.TryGet(PermissionKeys.CatalogProductsRead, out _));
        Assert.False(registry.TryGet("catalog.products.read", out _));
        Assert.False(registry.TryGet("Catalog.Products.Read ", out _));
        Assert.Throws<ArgumentException>(() => new PermissionDefinition(
            "Catalog.Products.Read ",
            "Catalog",
            "Read products."));
        Assert.Throws<InvalidOperationException>(() => PermissionRegistry.Create(new[]
        {
            new PermissionDefinition("Catalog.Products.Read", "Catalog", "Read products."),
            new PermissionDefinition("catalog.products.read", "Catalog", "Read products."),
        }));
    }

    [Fact]
    public void Catalog_digest_depends_on_metadata_but_not_input_order()
    {
        var first = new PermissionDefinition("A.One.Read", "A", "Read one.");
        var second = new PermissionDefinition("B.Two.Read", "B", "Read two.");
        var forward = PermissionRegistry.Create(new[] { first, second });
        var reverse = PermissionRegistry.Create(new[] { second, first });
        var changed = PermissionRegistry.Create(new[]
        {
            first,
            new PermissionDefinition("B.Two.Read", "B", "Read another two."),
        });

        Assert.Equal(
            PermissionCatalogFactory.Create(forward, "1.0").CatalogDigest,
            PermissionCatalogFactory.Create(reverse, "1.0").CatalogDigest);
        Assert.NotEqual(
            PermissionCatalogFactory.Create(forward, "1.0").CatalogDigest,
            PermissionCatalogFactory.Create(changed, "1.0").CatalogDigest);
    }

    [Fact]
    public async Task Catalog_query_authorizes_through_the_registered_read_permission()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserAccessor>(new StubCurrentUserAccessor());
        services.AddSingleton<IPermissionStore>(new StubPermissionStore(isAllowed: true));
        services.AddServerApplication();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<ISender>().Send(
            new ListPermissionCatalogQuery(),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotEmpty(result.Value.Items);
        Assert.Equal(PermissionKeys.SecurityPermissionsRead,
            ((StubPermissionStore)provider.GetRequiredService<IPermissionStore>()).LastPermissionKey);
        Assert.Equal(PermissionKeys.SecurityPermissionsRead,
            new ListPermissionCatalogQuery().PermissionKey);
    }

    [Fact]
    public async Task Catalog_query_denies_a_user_without_the_read_permission()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserAccessor>(new StubCurrentUserAccessor());
        services.AddSingleton<IPermissionStore>(new StubPermissionStore(isAllowed: false));
        services.AddServerApplication();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<ISender>().Send(
            new ListPermissionCatalogQuery(),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser Current => CurrentUser.Authenticated(17, "operator", "Operator", true);
    }

    private sealed class StubPermissionStore : IPermissionStore
    {
        private readonly bool _isAllowed;
        public string? LastPermissionKey { get; private set; }

        public StubPermissionStore(bool isAllowed) => _isAllowed = isAllowed;

        public Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            LastPermissionKey = permissionKey;
            return Task.FromResult(new PermissionSnapshot(_isAllowed, false));
        }
    }
}
