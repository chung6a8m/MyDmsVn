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
        Task<bool?> GetDirectDecisionAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken);

        Task<bool> HasRoleGrantAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken);
    }

    public interface IPermissionAuthorizationService
    {
        Task<bool> IsAllowedAsync(string permissionKey, CancellationToken cancellationToken);
    }
}
