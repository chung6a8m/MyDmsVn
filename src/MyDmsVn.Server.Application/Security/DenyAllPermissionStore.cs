using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Security
{
    internal sealed class DenyAllPermissionStore : IPermissionStore
    {
        public Task<bool?> GetDirectDecisionAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<bool?>(null);
        }

        public Task<bool> HasRoleGrantAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }
    }
}
