using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    public sealed class PermissionActionBinder
    {
        private readonly IPermissionApiClient _permissionApiClient;

        public PermissionActionBinder(IPermissionApiClient permissionApiClient)
        {
            _permissionApiClient = permissionApiClient ??
                throw new ArgumentNullException(nameof(permissionApiClient));
        }

        public async Task ApplyAsync(
            Control action,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                throw new ArgumentException("A permission key is required.", nameof(permissionKey));
            }

            action.Enabled = false;
            var response = await _permissionApiClient
                .CheckAsync(permissionKey, cancellationToken);
            action.Enabled = response.IsSuccess && response.Data!.IsAllowed;
        }
    }
}
