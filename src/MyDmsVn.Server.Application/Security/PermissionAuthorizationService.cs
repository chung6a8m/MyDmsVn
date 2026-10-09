using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.Application.Security
{
    public sealed class PermissionAuthorizationService : IPermissionAuthorizationService
    {
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IPermissionStore _permissionStore;

        public PermissionAuthorizationService(
            ICurrentUserAccessor currentUserAccessor,
            IPermissionStore permissionStore)
        {
            _currentUserAccessor = currentUserAccessor;
            _permissionStore = permissionStore;
        }

        public async Task<bool> IsAllowedAsync(
            string permissionKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentUser = _currentUserAccessor.Current;
            if (!currentUser.IsAuthenticated ||
                !currentUser.IsActive ||
                !currentUser.UserId.HasValue ||
                !PermissionKeys.IsDeclared(permissionKey))
            {
                return false;
            }

            return await IsAllowedAsync(
                    currentUser.UserId.Value,
                    permissionKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<bool> IsAllowedAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (userId <= 0 || !PermissionKeys.IsDeclared(permissionKey))
            {
                return false;
            }

            var snapshot = await _permissionStore
                .GetSnapshotAsync(
                    userId,
                    permissionKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (snapshot.DirectDecision.HasValue)
            {
                return snapshot.DirectDecision.Value;
            }

            return snapshot.HasRoleGrant;
        }
    }
}
