using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class LocalIdentityApiClientTests
    {
        [Fact]
        public async Task Successful_login_updates_the_shared_local_session_and_current_user_query()
        {
            using (var provider = CreateProvider())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();

                var login = await client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);
                var current = await client.GetCurrentUserAsync(CancellationToken.None);

                Assert.True(login.IsSuccess);
                Assert.True(current.IsSuccess);
                Assert.True(session.IsAuthenticated);
                Assert.Equal("Warehouse Operator", session.CurrentUser!.DisplayName);
                Assert.Equal(login.Data!.UserId, current.Data!.UserId);
            }
        }

        [Fact]
        public async Task Signing_out_clears_the_server_current_user_for_subsequent_requests()
        {
            using (var provider = CreateProvider())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                await client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);

                session.SignOut();
                var current = await client.GetCurrentUserAsync(CancellationToken.None);

                Assert.False(session.IsAuthenticated);
                Assert.False(current.IsSuccess);
                Assert.Equal("Auth.Unauthorized", current.Error!.Code);
            }
        }

        [Fact]
        public async Task Signing_out_invalidates_a_login_that_is_still_in_flight()
        {
            var delayedHandler = new DelayedLoginHandler();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>>(delayedHandler);

            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var loginTask = client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);
                await delayedHandler.Started;

                session.SignOut();
                delayedHandler.Complete();
                var response = await loginTask;

                Assert.False(response.IsSuccess);
                Assert.Equal("Auth.SessionChanged", response.Error!.Code);
                Assert.Equal(ApiStatusCode.Unauthorized, response.Error.Status);
                Assert.False(session.IsAuthenticated);
            }
        }

        private static ServiceProvider CreateProvider()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                SuccessfulLoginHandler>();
            return services.BuildServiceProvider();
        }

        private sealed class SuccessfulLoginHandler
            : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            public Task<ErrorOr<CurrentUserDto>> Handle(
                LoginCommand request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<ErrorOr<CurrentUserDto>>(
                    new CurrentUserDto(42, request.Username, "Warehouse Operator"));
            }
        }

        private sealed class DelayedLoginHandler
            : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            private readonly TaskCompletionSource<bool> _started =
                new TaskCompletionSource<bool>();
            private readonly TaskCompletionSource<ErrorOr<CurrentUserDto>> _completion =
                new TaskCompletionSource<ErrorOr<CurrentUserDto>>();

            public Task Started => _started.Task;

            public Task<ErrorOr<CurrentUserDto>> Handle(
                LoginCommand request,
                CancellationToken cancellationToken)
            {
                _started.TrySetResult(true);
                return _completion.Task;
            }

            public void Complete()
            {
                _completion.SetResult(
                    new CurrentUserDto(42, "operator", "Warehouse Operator"));
            }
        }
    }
}
