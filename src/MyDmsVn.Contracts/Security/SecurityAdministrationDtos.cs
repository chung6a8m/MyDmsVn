using System.Collections.Generic;

namespace MyDmsVn.Contracts.Security;

public sealed class SecurityUserDto
{
    public SecurityUserDto(
        int userId,
        string username,
        string displayName,
        string? email,
        bool isActive,
        long assignmentRevision,
        IReadOnlyList<int> roleIds,
        IReadOnlyList<SecurityDecisionDto> directDecisions,
        bool isProtectedAccount = false)
    {
        UserId = userId;
        Username = username;
        DisplayName = displayName;
        Email = email;
        IsActive = isActive;
        AssignmentRevision = assignmentRevision;
        RoleIds = roleIds;
        DirectDecisions = directDecisions;
        IsProtectedAccount = isProtectedAccount;
    }

    public int UserId { get; }
    public string Username { get; }
    public string DisplayName { get; }
    public string? Email { get; }
    public bool IsActive { get; }
    public long AssignmentRevision { get; }
    public IReadOnlyList<int> RoleIds { get; }
    public IReadOnlyList<SecurityDecisionDto> DirectDecisions { get; }
    public bool IsProtectedAccount { get; }
}

public sealed class SecurityRoleDto
{
    public SecurityRoleDto(
        int roleId,
        string name,
        bool isActive,
        long assignmentRevision,
        IReadOnlyList<int> memberUserIds,
        IReadOnlyList<string> permissionKeys)
    {
        RoleId = roleId;
        Name = name;
        IsActive = isActive;
        AssignmentRevision = assignmentRevision;
        MemberUserIds = memberUserIds;
        PermissionKeys = permissionKeys;
    }

    public int RoleId { get; }
    public string Name { get; }
    public bool IsActive { get; }
    public long AssignmentRevision { get; }
    public IReadOnlyList<int> MemberUserIds { get; }
    public IReadOnlyList<string> PermissionKeys { get; }
}

public sealed class SecurityDecisionDto
{
    public SecurityDecisionDto(string permissionKey, bool? decision)
    {
        PermissionKey = permissionKey;
        Decision = decision;
    }

    public string PermissionKey { get; }
    public bool? Decision { get; }
}

public sealed class SecurityAdministrationResultDto
{
    public SecurityAdministrationResultDto(
        long assignmentRevision,
        bool changed,
        long? relatedAssignmentRevision = null)
    {
        AssignmentRevision = assignmentRevision;
        Changed = changed;
        RelatedAssignmentRevision = relatedAssignmentRevision;
    }

    public long AssignmentRevision { get; }
    public bool Changed { get; }
    public long? RelatedAssignmentRevision { get; }
}
