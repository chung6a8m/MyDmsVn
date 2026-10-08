using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Security
{
    public interface IAuthorizedRequest
    {
        string PermissionKey { get; }
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
        public PermissionSnapshot(bool? directDecision, bool hasRoleGrant)
        {
            DirectDecision = directDecision;
            HasRoleGrant = hasRoleGrant;
        }

        public bool? DirectDecision { get; }

        public bool HasRoleGrant { get; }
    }

    public interface IPermissionAuthorizationService
    {
        Task<bool> IsAllowedAsync(string permissionKey, CancellationToken cancellationToken);
    }
}
