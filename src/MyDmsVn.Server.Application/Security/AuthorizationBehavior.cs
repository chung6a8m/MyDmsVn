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
        private readonly ISecurityAuditWriter _auditWriter;
        private readonly IUtcClock _clock;

        public AuthorizationBehavior(
            ICurrentUserAccessor currentUserAccessor,
            IPermissionAuthorizationService authorizationService,
            ISecurityAuditWriter auditWriter,
            IUtcClock clock)
        {
            _currentUserAccessor = currentUserAccessor;
            _authorizationService = authorizationService;
            _auditWriter = auditWriter;
            _clock = clock;
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
                await AuditDeniedAsync(request, null, cancellationToken).ConfigureAwait(false);
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
                await AuditDeniedAsync(request, null, cancellationToken).ConfigureAwait(false);
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
                await AuditDeniedAsync(
                    request,
                    currentUser.UserId.Value,
                    cancellationToken).ConfigureAwait(false);
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

        private Task AuditDeniedAsync(
            TRequest request,
            int? userId,
            CancellationToken cancellationToken)
        {
            if (request is not ISecurityAuditedRequest auditedRequest)
            {
                return Task.CompletedTask;
            }

            return _auditWriter.WriteAsync(
                new SecurityAuditEntry(
                    auditedRequest.AuditAction,
                    SecurityAuditOutcome.Denied,
                    userId,
                    _clock.UtcNow,
                    auditedRequest.AuditDocumentType,
                    auditedRequest.AuditDocumentId),
                businessOperationCommitted: false,
                cancellationToken);
        }
    }
}
