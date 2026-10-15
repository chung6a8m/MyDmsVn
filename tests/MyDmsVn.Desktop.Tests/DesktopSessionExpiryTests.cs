using System;
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
    public sealed class DesktopSessionExpiryTests
    {
        [Fact]
        public async Task Idle_boundary_expires_session_and_background_dispatch_does_not_reset_activity()
        {
            var clock = new FakeMonotonicClock();
            var services = new ServiceCollection();
            services.AddSingleton<IMonotonicClock>(clock);
            services.AddSingleton(new DesktopSessionOptions(TimeSpan.FromMinutes(15)));
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddScoped<IUserAccountValidator, ActiveAccountValidator>();
            services.AddTransient<IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>, LoginHandler>();
            using (var provider = services.BuildServiceProvider())
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var activity = provider.GetRequiredService<IDesktopSessionActivity>();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None)).IsSuccess);

                clock.Advance(TimeSpan.FromMinutes(10));
                Assert.True((await identity.GetCurrentUserAsync(CancellationToken.None)).IsSuccess);
                clock.Advance(TimeSpan.FromMinutes(5));

                Assert.True(activity.ExpireIfIdle());
                Assert.False(session.IsAuthenticated);
                Assert.Equal(DesktopSessionState.Expired, session.State);
            }
        }

        private sealed class FakeMonotonicClock : IMonotonicClock
        {
            public TimeSpan Elapsed { get; private set; }
            public void Advance(TimeSpan duration) => Elapsed += duration;
        }

        private sealed class ActiveAccountValidator : IUserAccountValidator
        {
            public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken) =>
                Task.FromResult(true);
        }

        private sealed class LoginHandler : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            public Task<ErrorOr<CurrentUserDto>> Handle(LoginCommand request, CancellationToken cancellationToken) =>
                Task.FromResult<ErrorOr<CurrentUserDto>>(
                    new CurrentUserDto(42, request.Username, "Operator"));
        }
    }
}
