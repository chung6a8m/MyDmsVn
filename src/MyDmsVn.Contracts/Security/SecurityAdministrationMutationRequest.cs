using System;

namespace MyDmsVn.Contracts.Security;

public enum SecurityAdministrationTargetDto
{
    User = 0,
    Role = 1,
}

public enum SecurityAdministrationOperationDto
{
    Create = 0,
    Update = 1,
    SetActive = 2,
    SetPassword = 3,
    SetMembership = 4,
    SetPermission = 5,
}

/// <summary>A transport-safe security administration command. The actor is always established by the service session.</summary>
public sealed class SecurityAdministrationMutationRequest
{
    public SecurityAdministrationMutationRequest(
        SecurityAdministrationTargetDto target,
        SecurityAdministrationOperationDto operation,
        int targetId = 0,
        int relatedId = 0,
        string? name = null,
        string? displayName = null,
        string? email = null,
        bool? isActive = null,
        string? password = null,
        string? permissionKey = null,
        bool? decision = null,
        long? expectedRevision = null,
        long? expectedRelatedRevision = null,
        Guid? correlationId = null)
    {
        Target = target;
        Operation = operation;
        TargetId = targetId;
        RelatedId = relatedId;
        Name = name;
        DisplayName = displayName;
        Email = email;
        IsActive = isActive;
        Password = password;
        PermissionKey = permissionKey;
        Decision = decision;
        ExpectedRevision = expectedRevision;
        ExpectedRelatedRevision = expectedRelatedRevision;
        CorrelationId = correlationId ?? Guid.NewGuid();
    }

    public SecurityAdministrationTargetDto Target { get; }
    public SecurityAdministrationOperationDto Operation { get; }
    public int TargetId { get; }
    public int RelatedId { get; }
    public string? Name { get; }
    public string? DisplayName { get; }
    public string? Email { get; }
    public bool? IsActive { get; }
    public string? Password { get; }
    public string? PermissionKey { get; }
    public bool? Decision { get; }
    public long? ExpectedRevision { get; }
    public long? ExpectedRelatedRevision { get; }
    public Guid CorrelationId { get; }
}
