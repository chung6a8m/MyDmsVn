using System.Collections.Generic;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Inventory;

namespace MyDmsVn.Server.Application.Security
{
    public static class PermissionReleaseManifest
    {
        public const string CurrentReleaseId = "2026.10.13-p3.1";
    }

    public static class PermissionRegistryComposition
    {
        public static PermissionRegistry CreateDefault()
        {
            var definitions = new List<PermissionDefinition>();
            definitions.AddRange(CatalogPermissionDefinitions.All);
            definitions.AddRange(InventoryPermissionDefinitions.All);
            definitions.AddRange(SecurityPermissionDefinitions.All);
            var registry = PermissionRegistry.Create(
                PermissionReleaseManifest.CurrentReleaseId,
                definitions);
            registry.ValidateReferencedKeys(PermissionKeys.All);
            return registry;
        }
    }

    public static class SecurityPermissionDefinitions
    {
        public static IReadOnlyList<PermissionDefinition> All { get; } =
            new[]
            {
                new PermissionDefinition(
                    SecurityPermissionKeys.UsersManage,
                    "Security",
                    "Manage application users.",
                    PermissionLifecycleStatus.Active,
                    AdministratorGrantPolicy.Sensitive),
                new PermissionDefinition(
                    SecurityPermissionKeys.RolesManage,
                    "Security",
                    "Manage application roles and permission assignments.",
                    PermissionLifecycleStatus.Active,
                    AdministratorGrantPolicy.Sensitive),
            };
    }

    public static class SecurityPermissionKeys
    {
        public const string UsersManage = "Security.Users.Manage";
        public const string RolesManage = "Security.Roles.Manage";
    }
}
