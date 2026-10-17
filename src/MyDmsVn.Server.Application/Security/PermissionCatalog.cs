using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Server.Application.Security;

public static class PermissionCatalogFactory
{
    public static PermissionCatalogDto CreateDefault() =>
        Create(PermissionRegistryComposition.CreateDefault(), GetApplicationVersion());

    public static PermissionCatalogDto Create(IPermissionRegistry registry, string applicationVersion)
    {
        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        if (string.IsNullOrWhiteSpace(applicationVersion))
        {
            throw new ArgumentException("An application version is required.", nameof(applicationVersion));
        }

        var items = registry.Definitions
            .OrderBy(definition => definition.Key, StringComparer.Ordinal)
            .Select(definition => new PermissionCatalogItemDto(
                definition.Key,
                definition.Module,
                definition.Description,
                definition.LifecycleStatus.ToString()))
            .ToArray();
        var readOnlyItems = new ReadOnlyCollection<PermissionCatalogItemDto>(items);
        var digest = ComputeDigest(items);
        return new PermissionCatalogDto(applicationVersion, digest, readOnlyItems);
    }

    public static string GetApplicationVersion()
    {
        var version = typeof(PermissionCatalogFactory).Assembly.GetName().Version;
        return version?.ToString() ?? "0.0.0.0";
    }

    private static string ComputeDigest(IEnumerable<PermissionCatalogItemDto> items)
    {
        var canonical = new StringBuilder();
        foreach (var item in items)
        {
            AppendField(canonical, item.Key);
            AppendField(canonical, item.Module);
            AppendField(canonical, item.Description);
            AppendField(canonical, item.Status);
        }

        using (var algorithm = SHA256.Create())
        {
            return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))
                .Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private static void AppendField(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
        builder.Append(';');
    }
}
