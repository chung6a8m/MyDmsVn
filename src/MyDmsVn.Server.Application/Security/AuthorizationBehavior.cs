using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.Application.Security
{
    public sealed class AuthorizationBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IRequest<TResponse>, IApplicationErrorResponse<TResponse>
    {
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IPermissionAuthorizationService _authorizationService;

        public AuthorizationBehavior(
            ICurrentUserAccessor currentUserAccessor,
            IPermissionAuthorizationService authorizationService)
        {
            _currentUserAccessor = currentUserAccessor;
            _authorizationService = authorizationService;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (request is not IAuthorizedRequest authorizedRequest)
            {
                return await next().ConfigureAwait(false);
            }

            var currentUser = _currentUserAccessor.Current;
            if (!currentUser.IsAuthenticated || !currentUser.IsActive)
            {
                return request.FromErrors(
                    new[]
                    {
                        Error.Unauthorized(
                            "Auth.Unauthorized",
                            "Authentication is required."),
                    });
            }

            if (!currentUser.UserId.HasValue)
            {
                return request.FromErrors(
                    new[]
                    {
                        Error.Unauthorized(
                            "Auth.Unauthorized",
                            "Authentication is required."),
                    });
            }

            if (!await _authorizationService
                    .IsAllowedAsync(
                        currentUser.UserId.Value,
                        authorizedRequest.PermissionKey,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return request.FromErrors(
                    new[]
                    {
                        Error.Forbidden(
                            "Auth.Forbidden",
                            "The operation is not permitted."),
                    });
            }

            if (authorizedRequest is IAuthorizedActorRequest actorRequest)
            {
                actorRequest.BindAuthorizedUser(currentUser.UserId.Value);
            }

            return await next().ConfigureAwait(false);
        }
    }
}
