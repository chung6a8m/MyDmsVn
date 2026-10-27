using System;
using System.Collections.Generic;
using System.Linq;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.Application.Security;

public enum SecurityPermissionResult { Allowed, Denied, Unassigned, Unavailable }
public enum SecurityPermissionReason { DirectAllow, DirectDeny, ActiveRoleGrant, InactiveUser, InactivePermission, UnknownPermission, CatalogUnavailable, UserUnavailable, RoleDataUnavailable, ProtectedAccountUnverified, NoAssignment }
public sealed class SecurityPermissionExplanation
{
    public SecurityPermissionExplanation(string permissionKey, SecurityPermissionResult result, SecurityPermissionReason reason, IEnumerable<SecurityRoleDto> roleSources, bool? directDecision, bool isProvenanceComplete)
    {
        PermissionKey = permissionKey;
        Result = result;
        Reason = reason;
        RoleSources = Array.AsReadOnly(roleSources.ToArray());
        DirectDecision = directDecision;
        IsProvenanceComplete = isProvenanceComplete;
    }
    public string PermissionKey { get; }
    public SecurityPermissionResult Result { get; }
    public SecurityPermissionReason Reason { get; }
    public IReadOnlyList<SecurityRoleDto> RoleSources { get; }
    public bool? DirectDecision { get; }
    public bool IsProvenanceComplete { get; }
}
public static class SecurityPermissionProjection
{
    public static SecurityPermissionExplanation Evaluate(SecurityProjectionSnapshot snapshot, string permissionKey)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (string.IsNullOrWhiteSpace(permissionKey)) throw new ArgumentException("A canonical permission key is required.", nameof(permissionKey));
        var user = snapshot.Users.IsCurrent(snapshot.SessionVersion) ? snapshot.User : null;
        var currentRoles = snapshot.Roles.IsCurrent(snapshot.SessionVersion);
        var roles = currentRoles && user != null ? snapshot.Roles.Items.Where(r => user.RoleIds.Contains(r.RoleId)).ToArray() : Array.Empty<SecurityRoleDto>();
        var sources = roles.Where(r => r.IsActive && r.PermissionKeys.Contains(permissionKey, StringComparer.Ordinal)).ToArray();
        var complete = currentRoles && user != null && user.RoleIds.All(id => roles.Any(r => r.RoleId == id));
        var direct = user?.DirectDecisions.FirstOrDefault(d => StringComparer.Ordinal.Equals(d.PermissionKey, permissionKey))?.Decision;
        SecurityPermissionExplanation Result(SecurityPermissionResult result, SecurityPermissionReason reason) => new(permissionKey, result, reason, sources, direct, complete);
        if (!snapshot.Catalog.IsCurrent(snapshot.SessionVersion)) return Result(SecurityPermissionResult.Unavailable, SecurityPermissionReason.CatalogUnavailable);
        var key = snapshot.Catalog.Items.FirstOrDefault(k => StringComparer.Ordinal.Equals(k.Key, permissionKey));
        if (key == null) return Result(SecurityPermissionResult.Denied, SecurityPermissionReason.UnknownPermission);
        if (!StringComparer.Ordinal.Equals(key.Status, "Active")) return Result(SecurityPermissionResult.Denied, SecurityPermissionReason.InactivePermission);
        if (user == null || !snapshot.Users.IsCurrent(snapshot.SessionVersion)) return Result(SecurityPermissionResult.Unavailable, SecurityPermissionReason.UserUnavailable);
        if (!user.IsActive) return Result(SecurityPermissionResult.Denied, SecurityPermissionReason.InactiveUser);
        if (user.IsProtectedAccount) return Result(SecurityPermissionResult.Unavailable, SecurityPermissionReason.ProtectedAccountUnverified);
        if (direct.HasValue) return Result(direct.Value ? SecurityPermissionResult.Allowed : SecurityPermissionResult.Denied, direct.Value ? SecurityPermissionReason.DirectAllow : SecurityPermissionReason.DirectDeny);
        if (sources.Length != 0) return Result(SecurityPermissionResult.Allowed, SecurityPermissionReason.ActiveRoleGrant);
        return complete ? Result(SecurityPermissionResult.Unassigned, SecurityPermissionReason.NoAssignment) : Result(SecurityPermissionResult.Unavailable, SecurityPermissionReason.RoleDataUnavailable);
    }
}
