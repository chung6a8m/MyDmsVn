using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class LoginViewModelTests
    {
        [Fact]
        public async Task Successful_login_clears_password_and_marks_the_view_model_authenticated()
        {
            var client = new StubIdentityClient(
                ApiResponse<CurrentUserDto>.Success(
                    new CurrentUserDto(42, "operator", "Operator")));
            var viewModel = new LoginViewModel(client, new DesktopNotificationCenter())
            {
                Username = "operator",
                Password = "secret",
            };

            await viewModel.LoginAsync(CancellationToken.None);

            Assert.True(viewModel.IsAuthenticated);
            Assert.Equal(string.Empty, viewModel.Password);
            Assert.False(viewModel.IsBusy);
        }

        [Fact]
        public async Task Invalid_credentials_use_safe_vietnamese_message_and_clear_password()
        {
            var client = new StubIdentityClient(
                ApiResponse<CurrentUserDto>.Failure(
                    new ApiError(
                        ApiStatusCode.Unauthorized,
                        "Auth.InvalidCredentials",
                        "technical detail")));
            var viewModel = new LoginViewModel(client, new DesktopNotificationCenter())
            {
                Username = "operator",
                Password = "wrong",
            };

            await viewModel.LoginAsync(CancellationToken.None);

            Assert.False(viewModel.IsAuthenticated);
            Assert.Equal("Tên đăng nhập hoặc mật khẩu không đúng.", viewModel.ErrorMessage);
            Assert.Equal(string.Empty, viewModel.Password);
        }

        [Fact]
        public async Task Empty_fields_are_rejected_without_calling_the_backend()
        {
            var client = new StubIdentityClient(
                ApiResponse<CurrentUserDto>.Success(
                    new CurrentUserDto(42, "operator", "Operator")));
            var viewModel = new LoginViewModel(client, new DesktopNotificationCenter());

            await viewModel.LoginAsync(CancellationToken.None);

            Assert.False(viewModel.IsAuthenticated);
            Assert.Equal(0, client.LoginCount);
            Assert.True(viewModel.HasErrors);
        }

        [Fact]
        public void Expired_session_shows_an_explicit_reauthentication_notice()
        {
            var client = new StubIdentityClient(
                ApiResponse<CurrentUserDto>.Success(
                    new CurrentUserDto(42, "operator", "Operator")));
            var viewModel = new LoginViewModel(
                client,
                new DesktopNotificationCenter(),
                session: new StubSession(DesktopSessionState.Expired));

            Assert.Equal(
                "Phiên đăng nhập không còn hợp lệ. Vui lòng đăng nhập lại.",
                viewModel.ErrorMessage);
        }

        [Fact]
        public async Task Pending_login_sets_busy_and_rejects_duplicate_submit()
        {
            var client = new ControllableIdentityClient();
            using (var viewModel = new LoginViewModel(client, new DesktopNotificationCenter())
            {
                Username = "operator",
                Password = "secret",
            })
            {
                var first = viewModel.LoginAsync(CancellationToken.None);

                Assert.True(viewModel.IsBusy);
                Assert.Equal(1, client.LoginCount);
                await viewModel.LoginAsync(CancellationToken.None);
                Assert.Equal(1, client.LoginCount);

                client.Complete(ApiResponse<CurrentUserDto>.Success(
                    new CurrentUserDto(42, "operator", "Operator")));
                await first;

                Assert.True(viewModel.IsAuthenticated);
                Assert.False(viewModel.IsBusy);
                Assert.Equal(string.Empty, viewModel.Password);
            }
        }

        [Fact]
        public async Task Cancellation_clears_busy_and_password_without_authenticating()
        {
            var client = new ControllableIdentityClient();
            using (var viewModel = new LoginViewModel(client, new DesktopNotificationCenter())
            {
                Username = "operator",
                Password = "secret",
            })
            using (var cancellation = new CancellationTokenSource())
            {
                var login = viewModel.LoginAsync(cancellation.Token);
                Assert.True(viewModel.IsBusy);

                cancellation.Cancel();
                await login;

                Assert.False(viewModel.IsBusy);
                Assert.False(viewModel.IsAuthenticated);
                Assert.Equal(string.Empty, viewModel.Password);
            }
        }

        [Fact]
        public async Task Database_timeout_uses_safe_vietnamese_message()
        {
            var client = new StubIdentityClient(
                ApiResponse<CurrentUserDto>.Failure(
                    new ApiError(
                        ApiStatusCode.InternalServerError,
                        "Database.Timeout",
                        "server detail")));
            using (var viewModel = new LoginViewModel(client, new DesktopNotificationCenter())
            {
                Username = "operator",
                Password = "secret",
            })
            {
                await viewModel.LoginAsync(CancellationToken.None);

                Assert.Equal(
                    "Không thể kết nối máy chủ dữ liệu. Vui lòng thử lại.",
                    viewModel.ErrorMessage);
                Assert.Equal(string.Empty, viewModel.Password);
            }
        }

        [Fact]
        public async Task Disabled_session_response_uses_explicit_vietnamese_reauthentication_message()
        {
            var client = new StubIdentityClient(
                ApiResponse<CurrentUserDto>.Failure(
                    new ApiError(
                        ApiStatusCode.Unauthorized,
                        "Auth.Unauthorized",
                        "account detail")));
            using (var viewModel = new LoginViewModel(client, new DesktopNotificationCenter())
            {
                Username = "operator",
                Password = "secret",
            })
            {
                await viewModel.LoginAsync(CancellationToken.None);

                Assert.Equal(
                    "Phiên đăng nhập không còn hợp lệ. Vui lòng đăng nhập lại.",
                    viewModel.ErrorMessage);
            }
        }

        [Fact]
        public async Task Unexpected_login_failure_uses_safe_vietnamese_message()
        {
            using (var viewModel = new LoginViewModel(
                new ThrowingIdentityClient(),
                new DesktopNotificationCenter())
            {
                Username = "operator",
                Password = "secret",
            })
            {
                await viewModel.LoginAsync(CancellationToken.None);

                Assert.StartsWith(
                    "Không thể đăng nhập. Vui lòng thử lại hoặc liên hệ hỗ trợ.",
                    viewModel.ErrorMessage);
                Assert.DoesNotContain("sensitive detail", viewModel.ErrorMessage);
            }
        }

        private sealed class StubIdentityClient : IIdentityApiClient
        {
            private readonly ApiResponse<CurrentUserDto> _response;

            public StubIdentityClient(ApiResponse<CurrentUserDto> response)
            {
                _response = response;
            }

            public int LoginCount { get; private set; }

            public Task<ApiResponse<CurrentUserDto>> LoginAsync(
                LoginRequest request,
                CancellationToken cancellationToken)
            {
                LoginCount++;
                return Task.FromResult(_response);
            }

            public Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }

        private sealed class StubSession : IDesktopSession
        {
            public StubSession(DesktopSessionState state)
            {
                State = state;
            }

            public event EventHandler? SessionChanged;
            public bool IsAuthenticated => false;
            public DesktopSessionState State { get; }
            public CurrentUserDto? CurrentUser => null;
            public long Version => 0;
            public void SignOut() => SessionChanged?.Invoke(this, EventArgs.Empty);
        }

        private sealed class ControllableIdentityClient : IIdentityApiClient
        {
            private readonly TaskCompletionSource<ApiResponse<CurrentUserDto>> _completion =
                new TaskCompletionSource<ApiResponse<CurrentUserDto>>();

            public int LoginCount { get; private set; }

            public Task<ApiResponse<CurrentUserDto>> LoginAsync(
                LoginRequest request,
                CancellationToken cancellationToken)
            {
                LoginCount++;
                cancellationToken.Register(() => _completion.TrySetCanceled());
                return _completion.Task;
            }

            public Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(
                CancellationToken cancellationToken) =>
                throw new InvalidOperationException("Current user was not expected.");

            public void Complete(ApiResponse<CurrentUserDto> response) =>
                _completion.TrySetResult(response);
        }

        private sealed class ThrowingIdentityClient : IIdentityApiClient
        {
            public Task<ApiResponse<CurrentUserDto>> LoginAsync(
                LoginRequest request,
                CancellationToken cancellationToken) =>
                throw new InvalidOperationException("sensitive detail");

            public Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(
                CancellationToken cancellationToken) =>
                throw new InvalidOperationException("Current user was not expected.");
        }
    }
}
