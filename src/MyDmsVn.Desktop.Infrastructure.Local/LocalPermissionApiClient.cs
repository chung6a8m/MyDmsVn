using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalPermissionApiClient : IPermissionApiClient
    {
        private readonly LocalRequestDispatcher _dispatcher;

        public LocalPermissionApiClient(LocalRequestDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public async Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
            string permissionKey,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                throw new ArgumentException("A permission key is required.", nameof(permissionKey));
            }

            return await _dispatcher.SendAsync(
                new CheckPermissionQuery(permissionKey),
                null,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
