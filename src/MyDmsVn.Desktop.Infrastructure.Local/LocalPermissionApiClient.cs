using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalPermissionApiClient : IPermissionApiClient
    {
        private readonly IPermissionAuthorizationService _authorization;

        public LocalPermissionApiClient(IPermissionAuthorizationService authorization)
        {
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public async Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
            string permissionKey,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                throw new ArgumentException("A permission key is required.", nameof(permissionKey));
            }

            var isAllowed = await _authorization
                .IsAllowedAsync(permissionKey, cancellationToken)
                .ConfigureAwait(false);
            return ApiResponse<PermissionDecisionDto>.Success(
                new PermissionDecisionDto(permissionKey, isAllowed));
        }
    }
}
