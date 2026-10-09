using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalPermissionApiClient : IPermissionApiClient
    {
        private readonly ISender _sender;

        public LocalPermissionApiClient(ISender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public async Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
            string permissionKey,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                throw new ArgumentException("A permission key is required.", nameof(permissionKey));
            }

            var result = await _sender
                .Send(new CheckPermissionQuery(permissionKey), cancellationToken)
                .ConfigureAwait(false);
            return ApiResponseMapper.Map(result);
        }
    }
}
