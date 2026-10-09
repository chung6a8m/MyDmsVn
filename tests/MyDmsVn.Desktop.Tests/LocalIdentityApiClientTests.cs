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
    }
}
