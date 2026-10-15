using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application.Identity
{
    internal sealed class GetCurrentUserQueryHandler
        : IRequestHandler<GetCurrentUserQuery, ErrorOr<CurrentUserDto>>
    {
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IUserAccountValidator _accountValidator;

        public GetCurrentUserQueryHandler(
            ICurrentUserAccessor currentUserAccessor,
            IUserAccountValidator accountValidator)
        {
            _currentUserAccessor = currentUserAccessor;
            _accountValidator = accountValidator;
        }

        public async Task<ErrorOr<CurrentUserDto>> Handle(
            GetCurrentUserQuery request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentUser = _currentUserAccessor.Current;
            if (!currentUser.IsAuthenticated ||
                !currentUser.IsActive ||
                !currentUser.UserId.HasValue ||
                currentUser.Username == null ||
                currentUser.DisplayName == null)
            {
                return
                    Error.Unauthorized(
                        "Auth.Unauthorized",
                        "Authentication is required.");
            }

            if (!await _accountValidator
                    .IsActiveAsync(currentUser.UserId.Value, cancellationToken)
                    .ConfigureAwait(false))
            {
                return Error.Unauthorized(
                    "Auth.Unauthorized",
                    "Authentication is required.");
            }

            return
                new CurrentUserDto(
                    currentUser.UserId.Value,
                    currentUser.Username,
                    currentUser.DisplayName);
        }
    }
}
