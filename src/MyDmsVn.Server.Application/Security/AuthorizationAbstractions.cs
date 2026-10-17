using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Security
{
    public interface IAuthorizedRequest
    {
        string PermissionKey { get; }
    }

    public interface IAuthorizedActorRequest : IAuthorizedRequest
    {
        void BindAuthorizedUser(int userId);
    }

    public interface IPermissionStore
    {
        Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken);
    }

    public sealed class PermissionSnapshot
    {
        public PermissionSnapshot(
            bool? directDecision,
            bool hasRoleGrant,
            bool isAccountActive = true,
            string? databaseUsername = null,
            int? boundSuperuserUserId = null,
            string? boundNormalizedUsername = null)
        {
            DirectDecision = directDecision;
            HasRoleGrant = hasRoleGrant;
            IsAccountActive = isAccountActive;
            DatabaseUsername = databaseUsername;
            BoundSuperuserUserId = boundSuperuserUserId;
            BoundNormalizedUsername = boundNormalizedUsername;
        }

        public bool? DirectDecision { get; }

        public bool HasRoleGrant { get; }

        public bool IsAccountActive { get; }

        public string? DatabaseUsername { get; }

        public int? BoundSuperuserUserId { get; }

        public string? BoundNormalizedUsername { get; }
    }

    public interface IPermissionAuthorizationService
    {
        Task<bool> IsAllowedAsync(string permissionKey, CancellationToken cancellationToken);

        Task<bool> IsAllowedAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken);
    }
}
