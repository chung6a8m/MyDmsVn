using System.Collections.Generic;

namespace MyDmsVn.Contracts.Security;

public sealed class PermissionCatalogDto
{
    public PermissionCatalogDto(
        string applicationVersion,
        string catalogDigest,
        IReadOnlyList<PermissionCatalogItemDto> items)
    {
        ApplicationVersion = applicationVersion;
        CatalogDigest = catalogDigest;
        Items = items;
    }

    public string ApplicationVersion { get; }

    public string CatalogDigest { get; }

    public IReadOnlyList<PermissionCatalogItemDto> Items { get; }
}

public sealed class PermissionCatalogItemDto
{
    public PermissionCatalogItemDto(string key, string module, string description, string status)
    {
        Key = key;
        Module = module;
        Description = description;
        Status = status;
    }

    public string Key { get; }

    public string Module { get; }

    public string Description { get; }

    public string Status { get; }
}
