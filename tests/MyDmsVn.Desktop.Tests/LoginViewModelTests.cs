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
    }
}
