using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalIdentityApiClient : IIdentityApiClient
    {
        private readonly ISender _sender;
        private readonly LocalDesktopSession _session;

        public LocalIdentityApiClient(ISender sender, LocalDesktopSession session)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task<ApiResponse<CurrentUserDto>> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var authenticationGeneration = _session.BeginAuthentication();
            var result = await _sender
                .Send(new LoginCommand(request.Username, request.Password), cancellationToken)
                .ConfigureAwait(false);
            var response = ApiResponseMapper.Map(result);
            if (response.IsSuccess &&
                !_session.TrySignIn(response.Data!, authenticationGeneration))
            {
                return ApiResponse<CurrentUserDto>.Failure(
                    new ApiError(
                        ApiStatusCode.Unauthorized,
                        "Auth.SessionChanged",
                        "The login result is no longer current. Sign in again."));
            }

            return response;
        }

        public async Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(
            CancellationToken cancellationToken)
        {
            var result = await _sender
                .Send(new GetCurrentUserQuery(), cancellationToken)
                .ConfigureAwait(false);
            return ApiResponseMapper.Map(result);
        }
    }
}
