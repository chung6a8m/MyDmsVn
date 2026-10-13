using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MyDmsVn.Server.Application.Security
{
    public enum PermissionLifecycleStatus
    {
        PendingActivation = 0,
        Active = 1,
        Deprecated = 2,
    }

    public enum AdministratorGrantPolicy
    {
        NotEligible = 0,
        Eligible = 1,
        Sensitive = 2,
    }

    public sealed class PermissionDefinition
    {
        public const int MaximumKeyLength = 100;
        public const int MaximumModuleLength = 100;
        public const int MaximumDescriptionLength = 500;

        public PermissionDefinition(
            string key,
            string module,
            string description,
            PermissionLifecycleStatus lifecycleStatus,
            AdministratorGrantPolicy administratorGrantPolicy)
        {
            Key = ValidateKey(key);
            Module = ValidateText(module, nameof(module), MaximumModuleLength);
            Description = ValidateText(description, nameof(description), MaximumDescriptionLength);
            if (!Enum.IsDefined(typeof(PermissionLifecycleStatus), lifecycleStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleStatus));
            }

            if (!Enum.IsDefined(typeof(AdministratorGrantPolicy), administratorGrantPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(administratorGrantPolicy));
            }

            LifecycleStatus = lifecycleStatus;
            AdministratorGrantPolicy = administratorGrantPolicy;
        }

        public string Key { get; }
        public string Module { get; }
        public string Description { get; }
        public PermissionLifecycleStatus LifecycleStatus { get; }
        public AdministratorGrantPolicy AdministratorGrantPolicy { get; }

        private static string ValidateKey(string key)
        {
            var value = ValidateText(key, nameof(key), MaximumKeyLength);
            var segmentLength = 0;
            foreach (var character in value)
            {
                if (character == '.')
                {
                    if (segmentLength == 0)
                    {
                        throw new ArgumentException(
                            "Permission keys must contain non-empty segments.",
                            nameof(key));
                    }

                    segmentLength = 0;
                    continue;
                }

                if (!((character >= 'A' && character <= 'Z') ||
                      (character >= 'a' && character <= 'z') ||
                      (character >= '0' && character <= '9')))
                {
                    throw new ArgumentException(
                        "Permission keys may contain only ASCII letters, digits and dots.",
                        nameof(key));
                }

                segmentLength++;
            }

            if (segmentLength == 0 || value.IndexOf('.') < 1)
            {
                throw new ArgumentException(
                    "Permission keys must be namespaced with non-empty segments.",
                    nameof(key));
            }

            return value;
        }

        private static string ValidateText(string value, string parameterName, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-blank value is required.", parameterName);
            }

            if (value.Length > maximumLength)
            {
                throw new ArgumentException(
                    $"The value cannot exceed {maximumLength} characters.",
                    parameterName);
            }

            return value;
        }
    }

    public interface IPermissionRegistry
    {
        string ReleaseId { get; }
        IReadOnlyList<PermissionDefinition> Definitions { get; }
        bool TryGet(string permissionKey, out PermissionDefinition definition);
        void ValidateReferencedKeys(IEnumerable<string> referencedKeys);
    }

    public sealed class PermissionRegistry : IPermissionRegistry
    {
        private readonly IReadOnlyDictionary<string, PermissionDefinition> _byKey;

        private PermissionRegistry(string releaseId, IReadOnlyList<PermissionDefinition> definitions)
        {
            ReleaseId = releaseId;
            Definitions = definitions;
            _byKey = new ReadOnlyDictionary<string, PermissionDefinition>(
                definitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal));
        }

        public string ReleaseId { get; }
        public IReadOnlyList<PermissionDefinition> Definitions { get; }

        public static PermissionRegistry Create(
            string releaseId,
            IEnumerable<PermissionDefinition> definitions)
        {
            if (string.IsNullOrWhiteSpace(releaseId))
            {
                throw new ArgumentException("A release identifier is required.", nameof(releaseId));
            }

            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var ordered = definitions
                .Select(definition => definition ?? throw new ArgumentException(
                    "Permission definitions cannot contain null entries.",
                    nameof(definitions)))
                .OrderBy(definition => definition.Key, StringComparer.Ordinal)
                .ToArray();
            var caseInsensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in ordered)
            {
                if (!caseInsensitiveKeys.Add(definition.Key))
                {
                    throw new InvalidOperationException(
                        $"Permission key collision detected for '{definition.Key}'.");
                }
            }

            return new PermissionRegistry(
                releaseId,
                new ReadOnlyCollection<PermissionDefinition>(ordered));
        }

        public bool TryGet(string permissionKey, out PermissionDefinition definition)
        {
            if (permissionKey == null)
            {
                definition = null!;
                return false;
            }

            return _byKey.TryGetValue(permissionKey, out definition!);
        }

        public void ValidateReferencedKeys(IEnumerable<string> referencedKeys)
        {
            if (referencedKeys == null)
            {
                throw new ArgumentNullException(nameof(referencedKeys));
            }

            var missing = referencedKeys
                .Where(key => key == null || !_byKey.ContainsKey(key))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    "Permission registry is missing referenced keys: " +
                    string.Join(", ", missing.Select(key => key ?? "<null>")));
            }
        }
    }
}
