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

    public LocalSecurityAdministrationApiClient(LocalRequestDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>> ListUsersAsync(
        CancellationToken cancellationToken) =>
        _dispatcher.SendAsync(new ListSecurityUsersQuery(), null, cancellationToken);

    public Task<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(
        CancellationToken cancellationToken) =>
        _dispatcher.SendAsync(new ListSecurityRolesQuery(), null, cancellationToken);

    public Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(
        SecurityAdministrationMutationRequest request,
        CancellationToken cancellationToken) =>
        _dispatcher.SendAsync(
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
            null,
            cancellationToken);
}
