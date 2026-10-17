using System;
using System.Linq;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class PermissionRegistryTests
{
    [Fact]
    public void Default_registry_composes_every_declared_key_deterministically()
    {
        var registry = PermissionRegistryComposition.CreateDefault();

        Assert.Equal(
            registry.Definitions.Select(definition => definition.Key)
                .OrderBy(key => key, StringComparer.Ordinal),
            registry.Definitions.Select(definition => definition.Key));
        Assert.All(registry.Definitions, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Module));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
            Assert.Equal(PermissionLifecycleStatus.Active, definition.LifecycleStatus);
        });
    }

    [Fact]
    public void Registry_rejects_case_collisions_and_invalid_metadata()
    {
        var first = Definition("Catalog.Products.Read");
        var collision = Definition("catalog.products.read");

        Assert.Throws<InvalidOperationException>(
            () => PermissionRegistry.Create(new[] { first, collision }));
        Assert.Throws<ArgumentException>(
            () => Definition(new string('A', PermissionDefinition.MaximumKeyLength + 1)));
        Assert.Throws<ArgumentException>(
            () => new PermissionDefinition("Catalog.Products.Read", " ", "Read products."));
        Assert.Throws<ArgumentException>(
            () => new PermissionDefinition("Catalog.Products.Read", "Catalog", " "));
    }

    [Fact]
    public void Registry_validation_rejects_a_referenced_key_missing_from_composition()
    {
        var registry = PermissionRegistry.Create(
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
    public void Deprecated_definition_remains_registered_but_is_not_active()
    {
        var definition = new PermissionDefinition(
            PermissionKeys.SecurityUsersManage,
            "Security",
            "Manage users.",
            PermissionLifecycleStatus.Deprecated);

        Assert.Equal(PermissionLifecycleStatus.Deprecated, definition.LifecycleStatus);
    }

    private static PermissionDefinition Definition(string key) =>
        new(key, "Catalog", "Read products.");
}
