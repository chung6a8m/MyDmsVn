using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalIdentityApiClient : IIdentityApiClient
    {
        private readonly LocalRequestDispatcher _dispatcher;
        private readonly LocalDesktopSession _session;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public LocalIdentityApiClient(
            LocalRequestDispatcher dispatcher,
            LocalDesktopSession session,
            IServiceScopeFactory serviceScopeFactory)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public async Task<ApiResponse<CurrentUserDto>> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!_session.TryBeginAuthentication(out var authenticationGeneration))
            {
                return ApiResponse<CurrentUserDto>.Failure(
                    new ApiError(
                        ApiStatusCode.Conflict,
                        "Auth.AlreadyAuthenticated",
                        "Sign out before signing in as another user."));
            }
            var response = await _dispatcher.SendAsync(
                new LoginCommand(request.Username, request.Password),
                null,
                (result, correlationId) =>
                {
                    if (result.IsSuccess &&
                        !_session.IsAuthenticationCurrent(authenticationGeneration))
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
            cancellationToken.ThrowIfCancellationRequested();
            if (!response.IsSuccess)
            {
                return response;
            }

            using (var validationScope = _serviceScopeFactory.CreateScope())
            {
                var validator = validationScope.ServiceProvider
                    .GetRequiredService<IUserAccountValidator>();
                if (!await validator
                        .IsActiveAsync(response.Data!.UserId, cancellationToken)
                        .ConfigureAwait(false))
                {
                    _session.SignOut();
                    return ApiResponse<CurrentUserDto>.Failure(
                        new ApiError(
                            ApiStatusCode.Unauthorized,
                            "Auth.Unauthorized",
                            "The account is no longer available."));
                }
            }

            if (!_session.TrySignIn(response.Data!, authenticationGeneration))
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
            return await _dispatcher.SendAsync(
                new GetCurrentUserQuery(),
                null,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
