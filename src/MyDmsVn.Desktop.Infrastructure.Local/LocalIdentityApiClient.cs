using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalIdentityApiClient : IIdentityApiClient
    {
        private readonly LocalRequestDispatcher _dispatcher;
        private readonly LocalDesktopSession _session;

        public LocalIdentityApiClient(
            LocalRequestDispatcher dispatcher,
            LocalDesktopSession session)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
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
            var response = await _dispatcher.SendAsync(
                new LoginCommand(request.Username, request.Password),
                null,
                (result, correlationId) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (result.IsSuccess &&
                        !_session.TrySignIn(result.Data!, authenticationGeneration))
                    {
                        return ApiResponse<CurrentUserDto>.Failure(
                            new ApiError(
                                ApiStatusCode.Unauthorized,
                                "Auth.SessionChanged",
                                "The login result is no longer current. Sign in again.",
                                correlationId: correlationId));
                    }

                    return result;
                },
                cancellationToken).ConfigureAwait(false);
            return response;
        }

        public async Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(
            CancellationToken cancellationToken)
        {
            return await _dispatcher.SendAsync(
                new GetCurrentUserQuery(),
                null,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
