using System;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application.Security
{
    public sealed class CheckPermissionQuery : ApplicationRequest<PermissionDecisionDto>
    {
        public CheckPermissionQuery(string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                throw new ArgumentException("A permission key is required.", nameof(permissionKey));
            }

            PermissionKey = permissionKey;
        }

        public string PermissionKey { get; }
    }
}
