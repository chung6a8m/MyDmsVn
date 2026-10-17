using System.Collections.Generic;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Inventory;

namespace MyDmsVn.Server.Application.Security
{
    public static class PermissionRegistryComposition
    {
        public static PermissionRegistry CreateDefault()
        {
            var definitions = new List<PermissionDefinition>();
            definitions.AddRange(CatalogPermissionDefinitions.All);
            definitions.AddRange(InventoryPermissionDefinitions.All);
            definitions.AddRange(SecurityPermissionDefinitions.All);
            return PermissionRegistry.Create(definitions);
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
                    "Manage application users."),
                new PermissionDefinition(
                    SecurityPermissionKeys.PermissionsRead,
                    "Security",
                    "View the application permission catalog."),
                new PermissionDefinition(
                    SecurityPermissionKeys.RolesManage,
                    "Security",
                    "Manage application roles and permission assignments."),
            };
    }

    public static class SecurityPermissionKeys
    {
        public const string UsersManage = "Security.Users.Manage";
        public const string RolesManage = "Security.Roles.Manage";
        public const string PermissionsRead = "Security.Permissions.Read";
    }
}
