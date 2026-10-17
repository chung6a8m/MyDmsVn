using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class PermissionCatalogClientTests
{
    [Fact]
    public void Snapshot_comparison_marks_add_remove_and_metadata_change_deterministically()
    {
        var current = Catalog(
            new PermissionCatalogItemDto("A.Read", "A", "Read A.", "Active"),
            new PermissionCatalogItemDto("B.Write", "B", "Write B revised.", "Active"));
        var previous = Catalog(
            new PermissionCatalogItemDto("B.Write", "B", "Write B.", "Active"),
            new PermissionCatalogItemDto("C.Read", "C", "Read C.", "Deprecated"));

        var comparison = PermissionCatalogSnapshotComparer.Compare(current, previous);

        Assert.True(comparison.HasPreviousSnapshot);
        Assert.Equal(new[] { "A.Read", "B.Write", "C.Read" }, comparison.Items.Select(item => item.Key));
        Assert.Equal(PermissionCatalogChangeKind.Added, comparison.Items[0].ChangeKind);
        Assert.Equal(PermissionCatalogChangeKind.Changed, comparison.Items[1].ChangeKind);
        Assert.Equal(PermissionCatalogChangeKind.Removed, comparison.Items[2].ChangeKind);
    }

    [Fact]
    public void Missing_previous_snapshot_does_not_label_current_permissions_as_added()
    {
        var comparison = PermissionCatalogSnapshotComparer.Compare(
            Catalog(new PermissionCatalogItemDto("A.Read", "A", "Read A.", "Active")),
            previous: null);

        Assert.False(comparison.HasPreviousSnapshot);
        Assert.Single(comparison.Items);
        Assert.Equal(PermissionCatalogChangeKind.NotCompared, comparison.Items[0].ChangeKind);
    }

    [Fact]
    public async Task Local_catalog_client_dispatches_the_protected_application_query()
    {
        var services = new ServiceCollection();
        services.AddServerApplication();
        services.AddLocalDesktopAdapter();

        using var provider = services.BuildServiceProvider();
        var response = await provider.GetRequiredService<IPermissionCatalogApiClient>()
            .GetCatalogAsync(CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal("Auth.Unauthorized", response.Error!.Code);
    }

    private static PermissionCatalogDto Catalog(params PermissionCatalogItemDto[] items) =>
        new("1.0.0.0", "digest", items);
}
