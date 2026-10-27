using System;
using System.Collections.Generic;
using System.Linq;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.Application.Security;

public enum SecurityDatasetState { NotLoaded, Loading, Ready, Forbidden, Failed }

public sealed class SecurityDataset<T>
{
    public SecurityDataset(SecurityDatasetState state, long sessionVersion, bool isFresh, IEnumerable<T>? items = null)
    {
        State = state;
        SessionVersion = sessionVersion;
        IsFresh = isFresh;
        Items = Array.AsReadOnly(state == SecurityDatasetState.Ready ? (items ?? throw new ArgumentNullException(nameof(items))).ToArray() : Array.Empty<T>());
    }
    public SecurityDatasetState State { get; }
    public long SessionVersion { get; }
    public bool IsFresh { get; }
    public IReadOnlyList<T> Items { get; }
    public bool IsCurrent(long sessionVersion) => State == SecurityDatasetState.Ready && IsFresh && SessionVersion == sessionVersion;
}
/// <summary>Loaded assignment evidence for display, not a coherent database snapshot or live authorization.</summary>
public sealed class SecurityProjectionSnapshot
{
    public SecurityProjectionSnapshot(SecurityUserDto? user, SecurityDataset<SecurityUserDto> users, SecurityDataset<SecurityRoleDto> roles, SecurityDataset<PermissionCatalogItemDto> catalog, long sessionVersion)
    {
        User = user == null ? null : new SecurityUserDto(user.UserId, user.Username, user.DisplayName, user.Email, user.IsActive, user.AssignmentRevision, Array.AsReadOnly(user.RoleIds.ToArray()), Array.AsReadOnly(user.DirectDecisions.ToArray()), user.IsProtectedAccount);
        Users = users;
        Roles = new SecurityDataset<SecurityRoleDto>(roles.State, roles.SessionVersion, roles.IsFresh, roles.Items.Select(r => new SecurityRoleDto(r.RoleId, r.Name, r.IsActive, r.AssignmentRevision, Array.AsReadOnly(r.MemberUserIds.ToArray()), Array.AsReadOnly(r.PermissionKeys.ToArray()))));
        Catalog = catalog;
        SessionVersion = sessionVersion;
    }
    public SecurityUserDto? User { get; }
    public SecurityDataset<SecurityUserDto> Users { get; }
    public SecurityDataset<SecurityRoleDto> Roles { get; }
    public SecurityDataset<PermissionCatalogItemDto> Catalog { get; }
    public long SessionVersion { get; }
}
