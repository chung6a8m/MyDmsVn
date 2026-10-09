using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application.Security
{
    internal sealed class CheckPermissionQueryHandler
        : IRequestHandler<CheckPermissionQuery, ErrorOr<PermissionDecisionDto>>
    {
        private readonly IPermissionAuthorizationService _authorization;

        public CheckPermissionQueryHandler(IPermissionAuthorizationService authorization)
        {
            _authorization = authorization;
        }

        public async Task<ErrorOr<PermissionDecisionDto>> Handle(
            CheckPermissionQuery request,
            CancellationToken cancellationToken)
        {
            var isAllowed = await _authorization
                .IsAllowedAsync(request.PermissionKey, cancellationToken)
                .ConfigureAwait(false);
            return new PermissionDecisionDto(request.PermissionKey, isAllowed);
        }
    }
}
