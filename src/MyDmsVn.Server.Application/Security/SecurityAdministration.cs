using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using MediatR;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.Application.Security;

public interface ISecurityAdministrationStore
{
    Task<IReadOnlyList<SecurityUserDto>> ListUsersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SecurityRoleDto>> ListRolesAsync(CancellationToken cancellationToken);

    Task<ErrorOr<SecurityAdministrationResultDto>> ApplyAsync(
        SecurityAdministrationMutation mutation,
        CancellationToken cancellationToken);
}

public enum SecurityAdministrationTarget
{
    User = 0,
    Role = 1,
}

public enum SecurityAdministrationOperation
{
    Create = 0,
    Update = 1,
    SetActive = 2,
    SetPassword = 3,
    SetMembership = 4,
    SetPermission = 5,
}

public sealed class SecurityAdministrationMutation
{
    public SecurityAdministrationMutation(
        SecurityAdministrationTarget target,
        SecurityAdministrationOperation operation,
        int actorUserId,
        int targetId,
        int relatedId,
        string? name,
        string? normalizedName,
        string? displayName,
        string? email,
        bool? isActive,
        string? passwordHash,
        string? passwordAlgorithm,
        string? permissionKey,
        bool? decision,
        long? expectedRevision,
        long? expectedRelatedRevision,
        Guid correlationId)
    {
        Target = target;
        Operation = operation;
        ActorUserId = actorUserId;
        TargetId = targetId;
        RelatedId = relatedId;
        Name = name;
        NormalizedName = normalizedName;
        DisplayName = displayName;
        Email = email;
        IsActive = isActive;
        PasswordHash = passwordHash;
        PasswordAlgorithm = passwordAlgorithm;
        PermissionKey = permissionKey;
        Decision = decision;
        ExpectedRevision = expectedRevision;
        ExpectedRelatedRevision = expectedRelatedRevision;
        CorrelationId = correlationId;
    }

    public SecurityAdministrationTarget Target { get; }
    public SecurityAdministrationOperation Operation { get; }
    public int ActorUserId { get; }
    public int TargetId { get; }
    public int RelatedId { get; }
    public string? Name { get; }
    public string? NormalizedName { get; }
    public string? DisplayName { get; }
    public string? Email { get; }
    public bool? IsActive { get; }
    public string? PasswordHash { get; }
    public string? PasswordAlgorithm { get; }
    public string? PermissionKey { get; }
    public bool? Decision { get; }
    public long? ExpectedRevision { get; }
    public long? ExpectedRelatedRevision { get; }
    public Guid CorrelationId { get; }
}

public sealed class ListSecurityUsersQuery
    : ApplicationRequest<IReadOnlyList<SecurityUserDto>>, IAuthorizedRequest
{
    public string PermissionKey => PermissionKeys.SecurityUsersManage;
}

public sealed class ListSecurityRolesQuery
    : ApplicationRequest<IReadOnlyList<SecurityRoleDto>>, IAuthorizedRequest
{
    public string PermissionKey => PermissionKeys.SecurityRolesManage;
}

public sealed class MutateSecurityAdministrationCommand
    : AuthorizedActorApplicationRequest<SecurityAdministrationResultDto>
{
    public MutateSecurityAdministrationCommand(
        SecurityAdministrationTarget target,
        SecurityAdministrationOperation operation,
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
        PermissionKeyValue = permissionKey;
        Decision = decision;
        ExpectedRevision = expectedRevision;
        ExpectedRelatedRevision = expectedRelatedRevision;
        CorrelationId = correlationId ?? Guid.NewGuid();
    }

    public SecurityAdministrationTarget Target { get; }
    public SecurityAdministrationOperation Operation { get; }
    public int TargetId { get; }
    public int RelatedId { get; }
    public string? Name { get; }
    public string? DisplayName { get; }
    public string? Email { get; }
    public bool? IsActive { get; }
    public string? Password { get; }
    public string? PermissionKeyValue { get; }
    public bool? Decision { get; }
    public long? ExpectedRevision { get; }
    public long? ExpectedRelatedRevision { get; }
    public Guid CorrelationId { get; }

    public override string PermissionKey => Target == SecurityAdministrationTarget.User
        ? PermissionKeys.SecurityUsersManage
        : Target == SecurityAdministrationTarget.Role
            ? PermissionKeys.SecurityRolesManage
            : string.Empty;
}

internal sealed class ListSecurityUsersQueryHandler
    : IRequestHandler<ListSecurityUsersQuery, ErrorOr<IReadOnlyList<SecurityUserDto>>>
{
    private readonly ISecurityAdministrationStore _store;

    public ListSecurityUsersQueryHandler(ISecurityAdministrationStore store) => _store = store;

    public async Task<ErrorOr<IReadOnlyList<SecurityUserDto>>> Handle(
        ListSecurityUsersQuery request,
        CancellationToken cancellationToken) =>
        ErrorOrFactory.From<IReadOnlyList<SecurityUserDto>>(
            await _store.ListUsersAsync(cancellationToken).ConfigureAwait(false));
}

internal sealed class ListSecurityRolesQueryHandler
    : IRequestHandler<ListSecurityRolesQuery, ErrorOr<IReadOnlyList<SecurityRoleDto>>>
{
    private readonly ISecurityAdministrationStore _store;

    public ListSecurityRolesQueryHandler(ISecurityAdministrationStore store) => _store = store;

    public async Task<ErrorOr<IReadOnlyList<SecurityRoleDto>>> Handle(
        ListSecurityRolesQuery request,
        CancellationToken cancellationToken) =>
        ErrorOrFactory.From<IReadOnlyList<SecurityRoleDto>>(
            await _store.ListRolesAsync(cancellationToken).ConfigureAwait(false));
}

internal sealed class MutateSecurityAdministrationCommandHandler
    : IRequestHandler<MutateSecurityAdministrationCommand, ErrorOr<SecurityAdministrationResultDto>>
{
    private readonly ISecurityAdministrationStore _store;
    private readonly IPermissionRegistry _registry;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SuperuserIdentityPolicy? _superuserPolicy;

    public MutateSecurityAdministrationCommandHandler(
        ISecurityAdministrationStore store,
        IPermissionRegistry registry,
        IPasswordHasher passwordHasher,
        SuperuserIdentityPolicy? superuserPolicy = null)
    {
        _store = store;
        _registry = registry;
        _passwordHasher = passwordHasher;
        _superuserPolicy = superuserPolicy;
    }

    public Task<ErrorOr<SecurityAdministrationResultDto>> Handle(
        MutateSecurityAdministrationCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedName = request.Target == SecurityAdministrationTarget.User &&
                             request.Name != null
            ? UsernameNormalizer.Normalize(request.Name)
            : request.Name?.Trim().ToUpperInvariant();

        if (request.Target == SecurityAdministrationTarget.User &&
            request.Operation == SecurityAdministrationOperation.Create &&
            _superuserPolicy != null &&
            string.Equals(
                normalizedName,
                _superuserPolicy.ConfiguredNormalizedUsername,
                StringComparison.Ordinal))
        {
            return Task.FromResult<ErrorOr<SecurityAdministrationResultDto>>(Error.Validation(
                "Security.ReservedSuperuserName",
                "The configured superuser username is reserved for maintenance operations."));
        }

        if (request.Operation == SecurityAdministrationOperation.SetPermission &&
            (request.PermissionKeyValue == null ||
             !_registry.TryGet(request.PermissionKeyValue, out var definition) ||
             definition.LifecycleStatus != PermissionLifecycleStatus.Active ||
             !string.Equals(
                 request.PermissionKeyValue,
                 request.PermissionKeyValue.Trim(),
                 StringComparison.Ordinal)))
        {
            return Task.FromResult<ErrorOr<SecurityAdministrationResultDto>>(Error.Validation(
                "Security.PermissionKeyInvalid",
                "Only a canonical, registered Active permission key can be assigned."));
        }

        var mutation = new SecurityAdministrationMutation(
            request.Target,
            request.Operation,
            request.AuthorizedUserId,
            request.TargetId,
            request.RelatedId,
            request.Name,
            normalizedName,
            request.DisplayName,
            request.Email,
            request.IsActive ??
                (request.Operation == SecurityAdministrationOperation.Create ? true : (bool?)null),
            request.Operation == SecurityAdministrationOperation.SetPassword ||
            (request.Target == SecurityAdministrationTarget.User &&
             request.Operation == SecurityAdministrationOperation.Create)
                ? _passwordHasher.Hash(request.Password!)
                : null,
            _passwordHasher.Algorithm,
            request.PermissionKeyValue,
            request.Decision,
            request.ExpectedRevision,
            request.ExpectedRelatedRevision,
            request.CorrelationId);
        return _store.ApplyAsync(mutation, cancellationToken);
    }
}

public sealed class MutateSecurityAdministrationCommandValidator
    : AbstractValidator<MutateSecurityAdministrationCommand>
{
    public MutateSecurityAdministrationCommandValidator(IPermissionRegistry registry)
    {
        RuleFor(request => request.Target).IsInEnum();
        RuleFor(request => request.Operation).IsInEnum();
        RuleFor(request => request)
            .Must(HasSupportedTargetOperationPair)
            .WithErrorCode("Security.OperationInvalid")
            .WithMessage("The requested operation is not supported for this security target.");
        RuleFor(request => request.TargetId)
            .GreaterThan(0)
            .When(request => request.Operation != SecurityAdministrationOperation.Create);
        RuleFor(request => request.ExpectedRevision)
            .NotNull()
            .GreaterThanOrEqualTo(0)
            .When(request => request.Operation != SecurityAdministrationOperation.Create);
        RuleFor(request => request.ExpectedRelatedRevision)
            .NotNull()
            .GreaterThanOrEqualTo(0)
            .When(request => request.Target == SecurityAdministrationTarget.User &&
                             request.Operation == SecurityAdministrationOperation.SetMembership);
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100)
            .When(request => request.Operation == SecurityAdministrationOperation.Create ||
                             request.Operation == SecurityAdministrationOperation.Update);
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(100)
            .When(request => request.Target == SecurityAdministrationTarget.User &&
                             (request.Operation == SecurityAdministrationOperation.Create ||
                              request.Operation == SecurityAdministrationOperation.Update));
        RuleFor(request => request.Email)
            .MaximumLength(254)
            .When(request => request.Email != null);
        RuleFor(request => request.Password)
            .Must(password => password != null && password.Length > 0 &&
                              System.Text.Encoding.UTF8.GetByteCount(password) <= 72)
            .When(request => request.Target == SecurityAdministrationTarget.User &&
                             (request.Operation == SecurityAdministrationOperation.Create ||
                              request.Operation == SecurityAdministrationOperation.SetPassword));
        RuleFor(request => request.RelatedId)
            .GreaterThan(0)
            .When(request => request.Operation == SecurityAdministrationOperation.SetMembership);
        RuleFor(request => request.IsActive)
            .NotNull()
            .When(request => request.Operation == SecurityAdministrationOperation.SetActive ||
                             request.Operation == SecurityAdministrationOperation.SetMembership);
        RuleFor(request => request.PermissionKeyValue)
            .Must(key => key != null && registry.TryGet(key, out var definition) &&
                         definition.LifecycleStatus == PermissionLifecycleStatus.Active &&
                         string.Equals(key, key.Trim(), StringComparison.Ordinal))
            .When(request => request.Operation == SecurityAdministrationOperation.SetPermission);
        RuleFor(request => request.Decision)
            .NotNull()
            .When(request => request.Target == SecurityAdministrationTarget.Role &&
                             request.Operation == SecurityAdministrationOperation.SetPermission);
    }

    private static bool HasSupportedTargetOperationPair(
        MutateSecurityAdministrationCommand request)
    {
        if (request.Target == SecurityAdministrationTarget.User)
        {
            return request.Operation != SecurityAdministrationOperation.SetActive ||
                   request.IsActive.HasValue;
        }

        return request.Target == SecurityAdministrationTarget.Role &&
               (request.Operation == SecurityAdministrationOperation.Create ||
                request.Operation == SecurityAdministrationOperation.Update ||
                request.Operation == SecurityAdministrationOperation.SetActive ||
                request.Operation == SecurityAdministrationOperation.SetPermission);
    }
}
