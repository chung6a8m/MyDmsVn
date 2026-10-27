using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class LoginFormTests
    {
        [Fact]
        public void Login_form_is_compact_keyboard_accessible_and_masks_password()
        {
            StaTest.Run(
                _ =>
                {
                    using (var guard = new WinFormsTestGuard())
                    using (var form = new LoginForm(
                        new LoginViewModel(
                            new NeverCalledIdentityClient(),
                            new DesktopNotificationCenter())))
                    {
                        Assert.True(form.PasswordTextBox.UseSystemPasswordChar);
                        Assert.Same(form.LoginButton, form.AcceptButton);
                        Assert.Same(form.CancelButtonControl, form.CancelButton);
                        Assert.True(form.UsernameTextBox.TabIndex < form.PasswordTextBox.TabIndex);
                        Assert.IsType<BootstrapTextBox>(form.UsernameTextBox);
                        Assert.IsType<BootstrapTextBox>(form.PasswordTextBox);
                        Assert.IsType<BootstrapButton>(form.LoginButton);
                        Assert.IsType<BootstrapButton>(form.CancelButtonControl);
                        Assert.Contains(Descendants(form), control => control is BootstrapCheckBox);
                        Assert.Contains(Descendants(form), control => control is BootstrapAlert);
                        Assert.Contains(Descendants(form), control => control is BootstrapProgressBar);
                        Assert.Equal(AutoScaleMode.Dpi, form.AutoScaleMode);
                        Assert.NotEqual(IntPtr.Zero, form.Handle);
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Login_form_renders_after_theme_change_and_disposes_without_ui_exceptions()
        {
            StaTest.Run(
                _ =>
                {
                    var originalTheme = BootstrapThemeManager.CurrentTheme;
                    using (var guard = new WinFormsTestGuard())
                    {
                        try
                        {
                            var identity = new ControllableIdentityClient();
                            using (var form = new LoginForm(
                                new LoginViewModel(
                                    identity,
                                    new DesktopNotificationCenter())))
                            {
                                form.Show();
                                System.Windows.Forms.Application.DoEvents();
                                AssertContained(form.LoginButton);
                                AssertContained(form.CancelButtonControl);

                                form.ViewModel.Username = "operator";
                                form.ViewModel.Password = "secret";
                                var login = form.ViewModel.LoginAsync(CancellationToken.None);
                                System.Windows.Forms.Application.DoEvents();
                                var progress = Descendants(form).OfType<BootstrapProgressBar>().Single();
                                Assert.True(progress.Visible);
                                AssertContained(progress);

                                identity.Complete(ApiResponse<CurrentUserDto>.Failure(
                                    new ApiError(
                                        ApiStatusCode.InternalServerError,
                                        "Unexpected",
                                        "Sensitive detail.",
                                        correlationId: "0123456789abcdef0123456789abcdef")));
                                login.GetAwaiter().GetResult();
                                System.Windows.Forms.Application.DoEvents();
                                var alert = Descendants(form).OfType<BootstrapAlert>().Single();
                                Assert.True(alert.Visible);
                                Assert.Equal(alert.Text, alert.AccessibilityObject.Name);
                                Assert.DoesNotContain("Sensitive detail.", alert.AccessibilityObject.Name);
                                AssertContained(alert);
                                Assert.True(
                                    alert.Height >= 52,
                                    $"Alert height {alert.Height}px cannot display a wrapped support message.");
                                var availableTextHeight = alert.ClientSize.Height - alert.Padding.Vertical;
                                var measuredText = TextRenderer.MeasureText(
                                    alert.Text,
                                    alert.Font,
                                    new Size(
                                        alert.ClientSize.Width - alert.Padding.Horizontal,
                                        int.MaxValue),
                                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                                Assert.True(
                                    measuredText.Height <= availableTextHeight,
                                    $"Alert text needs {measuredText.Height}px but only {availableTextHeight}px is available.");

                                BootstrapThemeManager.CurrentTheme =
                                    BootstrapTheme.CreateDefault(BootstrapThemeMode.Dark);

                                using (var bitmap = new Bitmap(
                                    form.ClientSize.Width,
                                    form.ClientSize.Height))
                                {
                                    form.DrawToBitmap(bitmap, form.ClientRectangle);
                                    form.Dispose();
                                    form.Dispose();
                                }

                                Assert.True(form.IsDisposed);
                                Assert.Empty(guard.Exceptions);
                            }
                        }
                        finally
                        {
                            BootstrapThemeManager.CurrentTheme = originalTheme;
                        }
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Closed_login_ignores_late_success_or_failure_without_notifications_or_dialogs(bool fail)
        {
            StaTest.Run(
                _ =>
                {
                    using (var guard = new WinFormsTestGuard())
                    {
                        var identity = new ControllableIdentityClient();
                        var notifications = new DesktopNotificationCenter();
                        var viewModel = new LoginViewModel(identity, notifications);
                        using (var form = new LoginForm(viewModel))
                        {
                            form.Show();
                            form.UsernameTextBox.Text = "operator";
                            form.PasswordTextBox.Text = "synthetic input";
                            var login = viewModel.LoginAsync(CancellationToken.None);
                            Assert.True(viewModel.IsBusy);
                            form.Close();
                            Assert.True(form.IsDisposed);
                            var changesAfterClose = 0;
                            viewModel.PropertyChanged += (_, __) => changesAfterClose++;
                            if (fail) identity.Fail(new InvalidOperationException("Synthetic sensitive backend detail"));
                            else identity.Complete(ApiResponse<CurrentUserDto>.Success(new CurrentUserDto(42, "operator", "Operator")));
                            PumpUntilCompleted(login);
                            login.GetAwaiter().GetResult();
                            System.Windows.Forms.Application.DoEvents();
                            Assert.False(viewModel.IsAuthenticated);
                            Assert.Null(notifications.LastNotification);
                            Assert.Equal(0, changesAfterClose);
                            Assert.Empty(guard.Exceptions);
                        }
                    }
                }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Login_error_reporter_failure_keeps_safe_inline_error_without_modal_dialog()
        {
            StaTest.Run(
                _ =>
                {
                    using (var guard = new WinFormsTestGuard())
                    {
                        var identity = new ControllableIdentityClient();
                        var reporter = new ThrowingReporter();
                        using (var form = new LoginForm(new LoginViewModel(identity, new DesktopNotificationCenter(), reporter)))
                        {
                            form.Show();
                            form.UsernameTextBox.Text = "operator";
                            form.PasswordTextBox.Text = "synthetic input";
                            var login = form.ViewModel.LoginAsync(CancellationToken.None);
                            identity.Fail(new InvalidOperationException("Synthetic sensitive backend detail"));
                            PumpUntilCompleted(login);
                            login.GetAwaiter().GetResult();
                            System.Windows.Forms.Application.DoEvents();
                            var alert = Descendants(form).OfType<BootstrapAlert>().Single();
                            Assert.True(alert.Visible);
                                Assert.Equal(alert.Text, alert.AccessibilityObject.Name);
                                Assert.DoesNotContain("Sensitive detail.", alert.AccessibilityObject.Name);
                            Assert.Contains("Support ID:", alert.Text);
                            Assert.DoesNotContain("sensitive", alert.Text);
                            Assert.Equal(1, reporter.Calls);
                            Assert.Empty(guard.Exceptions);
                        }
                    }
                }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Background_login_completion_publishes_state_on_the_form_owner_thread()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                {
                    var identity = new ControllableIdentityClient();
                    using (var form = new LoginForm(new LoginViewModel(identity, new DesktopNotificationCenter())))
                    {
                        form.Show();
                        var ownerThread = Thread.CurrentThread.ManagedThreadId;
                        var publicationThread = 0;
                        form.ViewModel.PropertyChanged += (_, change) =>
                        {
                            if (change.PropertyName == nameof(LoginViewModel.IsAuthenticated))
                                publicationThread = Thread.CurrentThread.ManagedThreadId;
                        };
                        form.UsernameTextBox.Text = "operator";
                        form.PasswordTextBox.Text = "synthetic input";
                        var login = form.ViewModel.LoginAsync(CancellationToken.None);
                        Task.Run(() => identity.Complete(ApiResponse<CurrentUserDto>.Success(
                            new CurrentUserDto(42, "operator", "Operator")))).GetAwaiter().GetResult();
                        PumpUntilCompleted(login);
                        login.GetAwaiter().GetResult();
                        Assert.Equal(ownerThread, publicationThread);
                        Assert.Empty(guard.Exceptions);
                    }
                }
            }, TimeSpan.FromSeconds(10));
        }

        private static void PumpUntilCompleted(Task operation)
        {
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!operation.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(2))
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Yield();
            }
            Assert.True(operation.IsCompleted, "Login completion did not drain while pumping STA messages.");
        }

        private sealed class ThrowingReporter : IDesktopExceptionReporter
        {
            public int Calls { get; private set; }
            public string Report(string operationName, Exception exception)
            {
                Calls++;
                throw new System.IO.IOException("Synthetic reporting failure");
            }
        }

        private static void AssertContained(Control control)
        {
            Assert.True(control.Width > 0);
            Assert.True(control.Height > 0);
            Assert.True(
                control.Parent!.ClientRectangle.Contains(control.Bounds),
                $"{control.GetType().Name} bounds {control.Bounds} exceed parent client bounds {control.Parent.ClientRectangle}.");
        }

        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var descendant in Descendants(child))
                {
                    yield return descendant;
                }
            }
        }

        private sealed class NeverCalledIdentityClient : IIdentityApiClient
        {
            public Task<ApiResponse<CurrentUserDto>> LoginAsync(
                LoginRequest request,
                CancellationToken cancellationToken) =>
                throw new Xunit.Sdk.XunitException("Login was not expected.");

            public Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(
                CancellationToken cancellationToken) =>
                throw new Xunit.Sdk.XunitException("Current user was not expected.");
        }

        private sealed class ControllableIdentityClient : IIdentityApiClient
        {
            private readonly TaskCompletionSource<ApiResponse<CurrentUserDto>> _login =
                new TaskCompletionSource<ApiResponse<CurrentUserDto>>();

            public Task<ApiResponse<CurrentUserDto>> LoginAsync(
                LoginRequest request,
                CancellationToken cancellationToken) =>
                _login.Task;

            public Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(
                CancellationToken cancellationToken) =>
                throw new Xunit.Sdk.XunitException("Current user was not expected.");

            public void Complete(ApiResponse<CurrentUserDto> response) =>
                _login.TrySetResult(response);

            public void Fail(Exception error) => _login.TrySetException(error);
        }
    }
}
