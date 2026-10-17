using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class PermissionReleaseRegressionTests
{
    [Fact]
    public async System.Threading.Tasks.Task Registered_active_key_is_in_composed_application_catalog()
    {
        const string newKey = "Catalog.ReleaseScenario.Access";
        var registry = PermissionRegistry.Create(
            PermissionRegistryComposition.CreateDefault().Definitions.Concat(new[]
            {
                new PermissionDefinition(newKey, "Catalog", "Release scenario permission."),
            }));
        var services = new ServiceCollection();
        services.AddSingleton<IPermissionRegistry>(registry);
        services.AddSingleton<ICurrentUserAccessor>(new CurrentUserAccessor());
        services.AddSingleton<IPermissionStore>(new AllowAllPermissionStore());
        services.AddServerApplication();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<MediatR.ISender>()
            .Send(new ListPermissionCatalogQuery());

        Assert.False(result.IsError);
        var item = Assert.Single(result.Value.Items, candidate => candidate.Key == newKey);
        Assert.Equal("Active", item.Status);
    }

    private sealed class CurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser Current => CurrentUser.Authenticated(17, "root.admin", "Root", true);
    }

    private sealed class AllowAllPermissionStore : IPermissionStore
    {
        public System.Threading.Tasks.Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            System.Threading.CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(new PermissionSnapshot(true, false));
    }
}
