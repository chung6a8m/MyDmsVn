using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.Application.Security
{
    public sealed class PermissionAuthorizationService : IPermissionAuthorizationService
    {
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IPermissionStore _permissionStore;
        private readonly IPermissionRegistry _permissionRegistry;
        private readonly SuperuserIdentityPolicy? _superuserIdentityPolicy;

        public PermissionAuthorizationService(
            ICurrentUserAccessor currentUserAccessor,
            IPermissionStore permissionStore,
            IPermissionRegistry permissionRegistry,
            SuperuserIdentityPolicy? superuserIdentityPolicy = null)
        {
            _currentUserAccessor = currentUserAccessor;
            _permissionStore = permissionStore;
            _permissionRegistry = permissionRegistry;
            _superuserIdentityPolicy = superuserIdentityPolicy;
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
                !_permissionRegistry.TryGet(permissionKey, out var definition) ||
                definition.LifecycleStatus != PermissionLifecycleStatus.Active)
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
            var currentUser = _currentUserAccessor.Current;
            if (userId <= 0 ||
                !currentUser.IsAuthenticated ||
                !currentUser.IsActive ||
                !currentUser.UserId.HasValue ||
                currentUser.UserId.Value != userId ||
                !_permissionRegistry.TryGet(permissionKey, out var definition) ||
                definition.LifecycleStatus != PermissionLifecycleStatus.Active)
            {
                return false;
            }

            var snapshot = await _permissionStore
                .GetSnapshotAsync(
                    userId,
                    permissionKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!snapshot.IsAccountActive)
            {
                return false;
            }

            if (_superuserIdentityPolicy != null &&
                _superuserIdentityPolicy.Matches(
                    userId,
                    snapshot.DatabaseUsername,
                    snapshot.BoundSuperuserUserId,
                    snapshot.BoundNormalizedUsername))
            {
                return true;
            }

            if (snapshot.DirectDecision.HasValue)
            {
                return snapshot.DirectDecision.Value;
            }

            return snapshot.HasRoleGrant;
        }
    }
}
