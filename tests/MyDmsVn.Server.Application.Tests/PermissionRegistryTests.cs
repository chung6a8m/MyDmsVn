using System;
using System.Linq;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class PermissionRegistryTests
{
    [Fact]
    public void Default_registry_is_deterministic_and_covers_every_declared_key()
    {
        var registry = PermissionRegistryComposition.CreateDefault();

        Assert.Equal(PermissionReleaseManifest.CurrentReleaseId, registry.ReleaseId);
        Assert.Equal(
            PermissionKeys.All.OrderBy(key => key, StringComparer.Ordinal),
            registry.Definitions.Select(definition => definition.Key));
        Assert.All(
            registry.Definitions,
            definition =>
            {
                Assert.False(string.IsNullOrWhiteSpace(definition.Module));
                Assert.False(string.IsNullOrWhiteSpace(definition.Description));
            });
    }

    [Fact]
    public void Current_release_is_bound_to_the_exact_manifest_fingerprint()
    {
        var registry = PermissionRegistryComposition.CreateDefault();

        Assert.Equal(
            PermissionReleaseManifest.CurrentManifestFingerprint,
            PermissionManifestFingerprint.Compute(registry));
        Assert.True(PermissionReleaseManifest.MatchesCurrent(registry));
    }

    [Fact]
    public void Registry_rejects_case_collisions_and_invalid_metadata()
    {
        var first = Definition("Catalog.Products.Read");
        var collision = Definition("catalog.products.read");

        Assert.Throws<InvalidOperationException>(
            () => PermissionRegistry.Create("release-1", new[] { first, collision }));
        Assert.Throws<ArgumentException>(
            () => Definition(new string('A', PermissionDefinition.MaximumKeyLength + 1)));
        Assert.Throws<ArgumentException>(
            () => new PermissionDefinition(
                "Catalog.Products.Read",
                " ",
                "Read products.",
                PermissionLifecycleStatus.Active,
                AdministratorGrantPolicy.Eligible));
        Assert.Throws<ArgumentException>(
            () => new PermissionDefinition(
                "Catalog.Products.Read",
                "Catalog",
                " ",
                PermissionLifecycleStatus.Active,
                AdministratorGrantPolicy.Eligible));
    }

    [Fact]
    public void Registry_validation_rejects_a_referenced_key_missing_from_manifest()
    {
        var registry = PermissionRegistry.Create(
            "release-1",
            new[] { Definition(PermissionKeys.CatalogProductsRead) });

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.ValidateReferencedKeys(
                new[]
                {
                    PermissionKeys.CatalogProductsRead,
                    PermissionKeys.CatalogProductsWrite,
                }));

        Assert.Contains(PermissionKeys.CatalogProductsWrite, exception.Message);
    }

    [Fact]
    public void Catalog_activation_and_administrator_grant_policies_are_independent()
    {
        var definition = new PermissionDefinition(
            PermissionKeys.SecurityUsersManage,
            "Security",
            "Manage users.",
            PermissionLifecycleStatus.Active,
            AdministratorGrantPolicy.Sensitive);

        Assert.Equal(PermissionLifecycleStatus.Active, definition.LifecycleStatus);
        Assert.Equal(AdministratorGrantPolicy.Sensitive, definition.AdministratorGrantPolicy);
    }

    private static PermissionDefinition Definition(string key)
    {
        return new PermissionDefinition(
            key,
            "Catalog",
            "Read products.",
            PermissionLifecycleStatus.Active,
            AdministratorGrantPolicy.Eligible);
    }
}
