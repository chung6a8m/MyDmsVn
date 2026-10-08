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

        public GetCurrentUserQueryHandler(ICurrentUserAccessor currentUserAccessor)
        {
            _currentUserAccessor = currentUserAccessor;
        }

        public Task<ErrorOr<CurrentUserDto>> Handle(
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
                return Task.FromResult<ErrorOr<CurrentUserDto>>(
                    Error.Unauthorized(
                        "Auth.Unauthorized",
                        "Authentication is required."));
            }

            return Task.FromResult<ErrorOr<CurrentUserDto>>(
                new CurrentUserDto(
                    currentUser.UserId.Value,
                    currentUser.Username,
                    currentUser.DisplayName));
        }
    }
}
