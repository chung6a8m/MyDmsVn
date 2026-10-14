using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Inventory;

namespace MyDmsVn.Server.Application.Security
{
    public static class PermissionReleaseManifest
    {
        public const string CurrentReleaseId = "2026.10.13-p3.1";
        public const string CurrentManifestFingerprint =
            "E36C3C33CBE469CCB04724725BC1812CFA5226D64ADBE60DDA11093F4816E6FE";

        public static bool MatchesCurrent(IPermissionRegistry registry)
        {
            return registry != null &&
                   string.Equals(registry.ReleaseId, CurrentReleaseId, System.StringComparison.Ordinal) &&
                   string.Equals(
                       PermissionManifestFingerprint.Compute(registry),
                       CurrentManifestFingerprint,
                       System.StringComparison.Ordinal);
        }
    }

    public static class PermissionManifestFingerprint
    {
        public static string Compute(IPermissionRegistry registry)
        {
            if (registry == null)
            {
                throw new System.ArgumentNullException(nameof(registry));
            }

            var values = new List<string> { registry.ReleaseId };
            foreach (var definition in registry.Definitions)
            {
                values.Add(definition.Key);
                values.Add(definition.Module);
                values.Add(definition.Description);
                values.Add(definition.LifecycleStatus.ToString());
                values.Add(definition.AdministratorGrantPolicy.ToString());
            }

            var canonical = new StringBuilder();
            foreach (var value in values)
            {
                var safe = value ?? string.Empty;
                canonical.Append(safe.Length.ToString(CultureInfo.InvariantCulture));
                canonical.Append(':');
                canonical.Append(safe);
                canonical.Append('|');
            }

            using (var sha256 = SHA256.Create())
            {
                var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                return System.BitConverter.ToString(digest).Replace("-", string.Empty);
            }
        }
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
