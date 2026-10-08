using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Security
{
    internal sealed class DenyAllPermissionStore : IPermissionStore
    {
        public Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PermissionSnapshot(null, false));
        }
    }
}
