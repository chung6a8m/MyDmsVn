using System;

namespace MyDmsVn.Contracts
{
    public sealed class PermissionDecisionDto
    {
        public PermissionDecisionDto(string permissionKey, bool isAllowed)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                throw new ArgumentException("A permission key is required.", nameof(permissionKey));
            }

            PermissionKey = permissionKey;
            IsAllowed = isAllowed;
        }

        public string PermissionKey { get; }

        public bool IsAllowed { get; }
    }
}
