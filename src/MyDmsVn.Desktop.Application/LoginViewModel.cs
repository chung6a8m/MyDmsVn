using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class LoginViewModel : DesktopViewModelBase, IDisposable
    {
        private readonly IIdentityApiClient _identityClient;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private bool _isAuthenticated;
        private bool _isPasswordVisible;
        private bool _disposed;

        public LoginViewModel(
            IIdentityApiClient identityClient,
            IDesktopNotificationService notifications,
            IDesktopExceptionReporter? exceptionReporter = null)
            : base(notifications, exceptionReporter)
        {
            _identityClient = identityClient ?? throw new ArgumentNullException(nameof(identityClient));
            LoginCommand = new AsyncRelayCommand(LoginFromCommandAsync);
            CancelCommand = new RelayCommand(CancelCurrentOperation);
            TogglePasswordVisibilityCommand = new RelayCommand(
                () => IsPasswordVisible = !IsPasswordVisible);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value ?? string.Empty);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value ?? string.Empty);
        }

        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            private set => SetProperty(ref _isAuthenticated, value);
        }

        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            private set => SetProperty(ref _isPasswordVisible, value);
        }

        public IAsyncRelayCommand LoginCommand { get; }

        public IRelayCommand CancelCommand { get; }

        public IRelayCommand TogglePasswordVisibilityCommand { get; }

        public Task LoginAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LoginViewModel));
            }

            if (IsBusy)
            {
                return Task.CompletedTask;
            }

            if (!ValidateInput())
            {
                return Task.CompletedTask;
            }

            return ExecuteBusyAsync(
                async operationCancellation =>
                {
                    try
                    {
                        var response = await _identityClient.LoginAsync(
                            new LoginRequest(Username.Trim(), Password),
                            operationCancellation).ConfigureAwait(false);
                        if (response.IsSuccess)
                        {
                            IsAuthenticated = true;
                            PublishNotification(
                                DesktopNotificationKind.Success,
                                "Đăng nhập thành công.");
                            return;
                        }

                        IsAuthenticated = false;
                        ApplyApiError(MapError(response.Error!));
                    }
                    catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        IsAuthenticated = false;
                        ApplyUnexpectedError(exception);
                    }
                    finally
                    {
                        Password = string.Empty;
                    }
                },
                cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelCurrentOperation();
            Password = string.Empty;
        }

        private Task LoginFromCommandAsync(CancellationToken cancellationToken) =>
            LoginAsync(cancellationToken);

        private bool ValidateInput()
        {
            var details = new List<ApiErrorDetail>();
            if (string.IsNullOrWhiteSpace(Username))
            {
                details.Add(new ApiErrorDetail(
                    "Validation.Required",
                    "Vui lòng nhập tên đăng nhập.",
                    nameof(Username)));
            }

            if (string.IsNullOrEmpty(Password))
            {
                details.Add(new ApiErrorDetail(
                    "Validation.Required",
                    "Vui lòng nhập mật khẩu.",
                    nameof(Password)));
            }

            if (details.Count == 0)
            {
                ResetValidation();
                return true;
            }

            ApplyApiError(
                new ApiError(
                    ApiStatusCode.BadRequest,
                    "Validation.Failed",
                    "Vui lòng kiểm tra thông tin đăng nhập.",
                    details));
            return false;
        }

        private static ApiError MapError(ApiError error)
        {
            string message;
            switch (error.Code)
            {
                case "Auth.InvalidCredentials":
                    message = "Tên đăng nhập hoặc mật khẩu không đúng.";
                    break;
                case "Auth.Unauthorized":
                case "Auth.SessionChanged":
                    message = "Phiên đăng nhập không còn hợp lệ. Vui lòng đăng nhập lại.";
                    break;
                case "Database.Unavailable":
                case "Database.Timeout":
                    message = "Không thể kết nối máy chủ dữ liệu. Vui lòng thử lại.";
                    break;
                case "Schema.Incompatible":
                    message = "Phiên bản cơ sở dữ liệu không tương thích.";
                    break;
                default:
                    message = "Không thể đăng nhập. Vui lòng thử lại hoặc liên hệ hỗ trợ.";
                    break;
            }

            return new ApiError(
                error.Status,
                error.Code,
                message,
                error.Details,
                error.CorrelationId);
        }
    }
}
