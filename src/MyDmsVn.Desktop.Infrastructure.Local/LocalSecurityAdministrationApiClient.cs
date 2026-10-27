using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Desktop.Infrastructure.Local;

internal sealed class LocalSecurityAdministrationApiClient : ISecurityAdministrationApiClient
{
    private readonly LocalRequestDispatcher _dispatcher;
    private readonly long? _expectedSessionVersion;

    public LocalSecurityAdministrationApiClient(LocalRequestDispatcher dispatcher) : this(dispatcher, null) { }

    private LocalSecurityAdministrationApiClient(LocalRequestDispatcher dispatcher, long? expectedSessionVersion)
    {
        _dispatcher = dispatcher;
        _expectedSessionVersion = expectedSessionVersion;
    }

    public ISecurityAdministrationApiClient ForSession(long expectedSessionVersion)
    {
        if (_expectedSessionVersion.HasValue)
        {
            if (_expectedSessionVersion.Value != expectedSessionVersion)
                throw new InvalidOperationException("A Security administration client cannot be rebound to another session.");
            return this;
        }
        return new LocalSecurityAdministrationApiClient(_dispatcher, expectedSessionVersion);
    }

    private Task<ApiResponse<T>> SendAsync<T>(MyDmsVn.Server.Application.ApplicationRequest<T> request, CancellationToken token) =>
        _expectedSessionVersion.HasValue
            ? _dispatcher.SendForCurrentSessionAsync(request, null, token, _expectedSessionVersion)
            : _dispatcher.SendAsync(request, null, token);

    public Task<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>> ListUsersAsync(
        CancellationToken cancellationToken) =>
        SendAsync(new ListSecurityUsersQuery(), cancellationToken);

    public Task<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(
        CancellationToken cancellationToken) =>
        SendAsync(new ListSecurityRolesQuery(), cancellationToken);

    public Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(
        SecurityAdministrationMutationRequest request,
        CancellationToken cancellationToken) =>
        SendAsync(
            new MutateSecurityAdministrationCommand(
                (SecurityAdministrationTarget)(int)request.Target,
                (SecurityAdministrationOperation)(int)request.Operation,
                request.TargetId,
                request.RelatedId,
                request.Name,
                request.DisplayName,
                request.Email,
                request.IsActive,
                request.Password,
                request.PermissionKey,
                request.Decision,
                request.ExpectedRevision,
                request.ExpectedRelatedRevision,
                request.CorrelationId),
            cancellationToken);

    public Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewPermissionImportAsync(
        PermissionImportTargetTypeDto targetType,
        int targetId,
        string json,
        CancellationToken cancellationToken) =>
        SendAsync(
            new PreviewPermissionAssignmentImportQuery(targetType, targetId, json),
            cancellationToken);

    public Task<ApiResponse<PermissionAssignmentImportResultDto>> ApplyPermissionImportAsync(
        PermissionImportTargetTypeDto targetType,
        int targetId,
        PermissionAssignmentImportRequest request,
        CancellationToken cancellationToken) =>
        SendAsync(
            new ApplyPermissionAssignmentImportCommand(
                targetType,
                targetId,
                request.Json,
                request.ExpectedTargetRevision,
                request.ExpectedCatalogDigest),
            cancellationToken);
}
