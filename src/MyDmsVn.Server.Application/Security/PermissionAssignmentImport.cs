using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.Application.Security;

public sealed class PermissionAssignmentImportTargetState
{
    public PermissionAssignmentImportTargetState(
        string name,
        bool isActive,
        bool isProtected,
        long assignmentRevision,
        IReadOnlyDictionary<string, bool> existingDecisions)
    {
        Name = name;
        IsActive = isActive;
        IsProtected = isProtected;
        AssignmentRevision = assignmentRevision;
        ExistingDecisions = existingDecisions;
    }

    public string Name { get; }
    public bool IsActive { get; }
    public bool IsProtected { get; }
    public long AssignmentRevision { get; }
    public IReadOnlyDictionary<string, bool> ExistingDecisions { get; }
}

public interface IPermissionAssignmentImportStore
{
    Task<ErrorOr<PermissionAssignmentImportTargetState>> ReadTargetAsync(
        PermissionImportTargetTypeDto targetType,
        int targetId,
        int actorUserId,
        CancellationToken cancellationToken);

    Task<ErrorOr<PermissionAssignmentImportResultDto>> ApplyAsync(
        ParsedPermissionAssignmentImportDto import,
        int actorUserId,
        long expectedTargetRevision,
        Guid correlationId,
        CancellationToken cancellationToken);
}

public sealed class PreviewPermissionAssignmentImportQuery
    : AuthorizedActorApplicationRequest<PermissionAssignmentImportPreviewDto>
{
    public PreviewPermissionAssignmentImportQuery(
        PermissionImportTargetTypeDto targetType,
        int targetId,
        string? json)
    {
        TargetType = targetType;
        TargetId = targetId;
        Json = json;
    }

    public PermissionImportTargetTypeDto TargetType { get; }
    public int TargetId { get; }
    public string? Json { get; }
    public override string PermissionKey => TargetType == PermissionImportTargetTypeDto.User
        ? PermissionKeys.SecurityUsersManage
        : TargetType == PermissionImportTargetTypeDto.Role
            ? PermissionKeys.SecurityRolesManage
            : string.Empty;
}

public sealed class ApplyPermissionAssignmentImportCommand
    : AuthorizedActorApplicationRequest<PermissionAssignmentImportResultDto>
{
    public ApplyPermissionAssignmentImportCommand(
        PermissionImportTargetTypeDto targetType,
        int targetId,
        string? json,
        long expectedTargetRevision,
        string? expectedCatalogDigest,
        Guid? correlationId = null)
    {
        TargetType = targetType;
        TargetId = targetId;
        Json = json;
        ExpectedTargetRevision = expectedTargetRevision;
        ExpectedCatalogDigest = expectedCatalogDigest;
        CorrelationId = correlationId ?? Guid.NewGuid();
    }

    public PermissionImportTargetTypeDto TargetType { get; }
    public int TargetId { get; }
    public string? Json { get; }
    public long ExpectedTargetRevision { get; }
    public string? ExpectedCatalogDigest { get; }
    public Guid CorrelationId { get; }
    public override string PermissionKey => TargetType == PermissionImportTargetTypeDto.User
        ? PermissionKeys.SecurityUsersManage
        : TargetType == PermissionImportTargetTypeDto.Role
            ? PermissionKeys.SecurityRolesManage
            : string.Empty;
}

internal sealed class PreviewPermissionAssignmentImportHandler
    : IRequestHandler<PreviewPermissionAssignmentImportQuery, ErrorOr<PermissionAssignmentImportPreviewDto>>
{
    private readonly PermissionAssignmentImportParser _parser;
    private readonly IPermissionRegistry _registry;
    private readonly IPermissionAssignmentImportStore _store;

    public PreviewPermissionAssignmentImportHandler(
        PermissionAssignmentImportParser parser,
        IPermissionRegistry registry,
        IPermissionAssignmentImportStore store)
    {
        _parser = parser;
        _registry = registry;
        _store = store;
    }

    public async Task<ErrorOr<PermissionAssignmentImportPreviewDto>> Handle(
        PreviewPermissionAssignmentImportQuery request,
        CancellationToken cancellationToken)
    {
        var parsed = _parser.Parse(request.Json);
        if (!parsed.IsValid || parsed.Value == null)
        {
            return Error.Validation("Security.ImportInvalid", parsed.Errors.FirstOrDefault() ?? "Import is invalid.");
        }

        if (!MatchesRequestedTarget(parsed.Value, request.TargetType, request.TargetId))
        {
            return Error.Validation("Security.ImportTargetMismatch", "The selected target does not match the file target.");
        }

        var state = await _store.ReadTargetAsync(
            request.TargetType,
            request.TargetId,
            request.AuthorizedUserId,
            cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Errors;
        }

        if (!state.Value.IsActive || state.Value.IsProtected)
        {
            return Error.Conflict("Security.ImportTargetUnavailable", "The selected target is inactive or protected.");
        }

        var additions = new List<PermissionAssignmentImportEntryDto>();
        foreach (var entry in parsed.Value.Entries)
        {
            if (!state.Value.ExistingDecisions.TryGetValue(entry.PermissionKey, out var existing))
            {
                additions.Add(entry);
            }
            else if (existing != entry.Granted)
            {
                return Error.Conflict(
                    "Security.ImportDecisionConflict",
                    "An existing assignment has a different decision; the import was not applied.");
            }
        }

        var catalog = PermissionCatalogFactory.Create(_registry, PermissionCatalogFactory.GetApplicationVersion());
        return new PermissionAssignmentImportPreviewDto(
            parsed.Value,
            state.Value.Name,
            state.Value.AssignmentRevision,
            catalog.CatalogDigest,
            new ReadOnlyCollection<PermissionAssignmentImportEntryDto>(additions));
    }

    private static bool MatchesRequestedTarget(
        ParsedPermissionAssignmentImportDto import,
        PermissionImportTargetTypeDto targetType,
        int targetId) => import.TargetType == targetType && import.TargetId == targetId;
}

internal sealed class ApplyPermissionAssignmentImportHandler
    : IRequestHandler<ApplyPermissionAssignmentImportCommand, ErrorOr<PermissionAssignmentImportResultDto>>
{
    private readonly PermissionAssignmentImportParser _parser;
    private readonly IPermissionRegistry _registry;
    private readonly IPermissionAssignmentImportStore _store;

    public ApplyPermissionAssignmentImportHandler(
        PermissionAssignmentImportParser parser,
        IPermissionRegistry registry,
        IPermissionAssignmentImportStore store)
    {
        _parser = parser;
        _registry = registry;
        _store = store;
    }

    public Task<ErrorOr<PermissionAssignmentImportResultDto>> Handle(
        ApplyPermissionAssignmentImportCommand request,
        CancellationToken cancellationToken)
    {
        var parsed = _parser.Parse(request.Json);
        if (!parsed.IsValid || parsed.Value == null)
        {
            return Task.FromResult<ErrorOr<PermissionAssignmentImportResultDto>>(Error.Validation(
                "Security.ImportInvalid", parsed.Errors.FirstOrDefault() ?? "Import is invalid."));
        }

        if (request.TargetId <= 0 || parsed.Value.TargetType != request.TargetType ||
            parsed.Value.TargetId != request.TargetId)
        {
            return Task.FromResult<ErrorOr<PermissionAssignmentImportResultDto>>(Error.Validation(
                "Security.ImportTargetMismatch", "The selected target does not match the file target."));
        }

        var currentCatalog = PermissionCatalogFactory.Create(_registry, PermissionCatalogFactory.GetApplicationVersion());
        if (!string.Equals(request.ExpectedCatalogDigest, currentCatalog.CatalogDigest, StringComparison.Ordinal))
        {
            return Task.FromResult<ErrorOr<PermissionAssignmentImportResultDto>>(Error.Conflict(
                "Security.ImportCatalogChanged", "The permission catalog changed after preview; preview the file again."));
        }

        if (request.ExpectedTargetRevision < 0)
        {
            return Task.FromResult<ErrorOr<PermissionAssignmentImportResultDto>>(Error.Validation(
                "Security.ImportRevisionInvalid", "The preview revision is invalid."));
        }

        return _store.ApplyAsync(
            parsed.Value,
            request.AuthorizedUserId,
            request.ExpectedTargetRevision,
            request.CorrelationId,
            cancellationToken);
    }
}
