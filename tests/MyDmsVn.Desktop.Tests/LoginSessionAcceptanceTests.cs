using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
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
    public sealed class LoginSessionAcceptanceTests
    {
        [Fact]
        public void Login_validation_focuses_the_first_invalid_field_without_dispatch()
        {
            Run(() =>
            {
                var api = new IdentityClient();
                using (var form = Login(api))
                {
                    form.PasswordTextBox.Focus();
                    DialogKey(form, Keys.Enter);
                    Assert.Equal(0, api.Calls);
                    Pump(() => form.UsernameTextBox.ContainsFocus);
                    Assert.True(Children(form).OfType<BootstrapAlert>().Single().Visible);
                    Assert.NotEmpty(form.ViewModel.GetErrors(nameof(LoginViewModel.Username)).Cast<string>());
                }
            });
        }

        [Fact]
        public void Login_inputs_have_distinct_accessible_names_and_password_visibility_toggle()
        {
            Run(() =>
            {
                using (var form = Login(new IdentityClient()))
                {
                    Assert.Equal("Username", form.UsernameTextBox.AccessibleName);
                    Assert.Equal("Password", form.PasswordTextBox.AccessibleName);
                    var toggle = Children(form).OfType<BootstrapCheckBox>().Single();
                    Assert.True(form.PasswordTextBox.UseSystemPasswordChar);
                    toggle.Checked = true;
                    Assert.False(form.PasswordTextBox.UseSystemPasswordChar);
                    toggle.Checked = false;
                    Assert.True(form.PasswordTextBox.UseSystemPasswordChar);
                }
            });
        }

        [Fact]
        public void Escape_requests_cancel_and_closed_login_suppresses_a_late_authentication_result()
        {
            Run(() =>
            {
                var api = new IdentityClient();
                using (var form = Login(api))
                {
                    form.UsernameTextBox.Text = "operator";
                    form.PasswordTextBox.Text = "synthetic input";
                    form.LoginButton.PerformClick();
                    Assert.True(form.ViewModel.IsBusy);
                    DialogKey(form, Keys.Escape);
                    Assert.Equal(DialogResult.Cancel, form.DialogResult);
                    form.Close();
                    Assert.True(form.IsDisposed);
                    api.Complete(ApiResponse<CurrentUserDto>.Success(new CurrentUserDto(8, "operator", "Operator")));
                    System.Windows.Forms.Application.DoEvents();
                    Assert.False(form.ViewModel.IsAuthenticated);
                    Assert.Empty(form.ViewModel.Password);
                }
            });
        }
        [Theory]
        [InlineData("Auth.Unauthorized", "Phiên đăng nhập không còn hợp lệ. Vui lòng đăng nhập lại.")]
        [InlineData("Auth.InvalidCredentials", "Tên đăng nhập hoặc mật khẩu không đúng.")]
        [InlineData("Auth.SessionChanged", "Phiên đăng nhập không còn hợp lệ. Vui lòng đăng nhập lại.")]
        [InlineData("Database.Timeout", "Không thể kết nối máy chủ dữ liệu. Vui lòng thử lại.")]
        public void Rejected_login_keeps_form_open_with_safe_inline_error_and_retry_focus(string code, string message)
        {
            Run(() =>
            {
                var api = new IdentityClient();
                using (var form = Login(api))
                {
                    form.UsernameTextBox.Text = "operator";
                    form.PasswordTextBox.Text = "synthetic input";
                    form.LoginButton.PerformClick();
                    Assert.True(form.ViewModel.IsBusy);
                    Assert.False(form.UsernameTextBox.Enabled);
                    Assert.False(form.PasswordTextBox.Enabled);
                    api.Complete(ApiResponse<CurrentUserDto>.Failure(new ApiError(
                        ApiStatusCode.Unauthorized, code, "Synthetic sensitive SQL detail")));
                    Pump(() => !form.ViewModel.IsBusy && form.PasswordTextBox.ContainsFocus);
                    Assert.False(form.IsDisposed);
                    Assert.False(form.ViewModel.IsAuthenticated);
                    Assert.Equal(DialogResult.None, form.DialogResult);
                    Assert.Empty(form.PasswordTextBox.Text);
                    Assert.Equal(message, Children(form).OfType<BootstrapAlert>().Single().Text);
                    Assert.True(form.UsernameTextBox.Enabled);
                    Assert.True(form.PasswordTextBox.ContainsFocus, "Password focus: can=" + form.PasswordTextBox.CanFocus + ", enabled=" + form.PasswordTextBox.Enabled + ", visible=" + form.PasswordTextBox.Visible + ", active=" + form.ActiveControl?.GetType().Name + ", focused=" + string.Join(",", Children(form).Where(c => c.Focused).Select(c => c.GetType().Name)));
                    Assert.Equal(1, api.Calls);
                }
            });
        }

        [Fact]
        public void Enter_submits_once_while_busy_and_success_closes_the_login_form()
        {
            Run(() =>
            {
                var api = new IdentityClient();
                using (var form = Login(api))
                {
                    form.UsernameTextBox.Text = "operator";
                    form.PasswordTextBox.Text = "synthetic input";
                    form.PasswordTextBox.Focus();
                    DialogKey(form, Keys.Enter);
                    DialogKey(form, Keys.Enter);
                    Assert.Equal(1, api.Calls);
                    Assert.False(form.LoginButton.Enabled);
                    api.Complete(ApiResponse<CurrentUserDto>.Success(new CurrentUserDto(4, "operator", "Operator")));
                    Pump(() => form.IsDisposed);
                    Assert.Equal(DialogResult.OK, form.DialogResult);
                }
            });
        }

        [Theory]
        [InlineData(BootstrapThemeMode.Light)]
        [InlineData(BootstrapThemeMode.Dark)]
        public void Login_tab_order_focus_and_layout_are_usable_in_each_theme(BootstrapThemeMode theme)
        {
            Run(() =>
            {
                var original = BootstrapThemeManager.CurrentTheme;
                try
                {
                    BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(theme);
                    using (var form = Login(new IdentityClient()))
                    {
                        form.UsernameTextBox.Focus();
                        Pump(() => form.UsernameTextBox.ContainsFocus);
                        DialogKey(form, Keys.Tab);
                        Assert.True(form.PasswordTextBox.ContainsFocus, "Password focus: can=" + form.PasswordTextBox.CanFocus + ", enabled=" + form.PasswordTextBox.Enabled + ", visible=" + form.PasswordTextBox.Visible + ", active=" + form.ActiveControl?.GetType().Name + ", focused=" + string.Join(",", Children(form).Where(c => c.Focused).Select(c => c.GetType().Name)));
                        DialogKey(form, Keys.Tab);
                        Assert.True(Children(form).OfType<BootstrapCheckBox>().Single().Focused);
                        DialogKey(form, Keys.Tab);
                        Assert.True(form.LoginButton.Focused);
                        DialogKey(form, Keys.Tab);
                        Assert.True(form.CancelButtonControl.Focused);
                        foreach (var control in new Control[] { form.UsernameTextBox, form.PasswordTextBox, form.LoginButton, form.CancelButtonControl })
                        {
                            Assert.True(control.Parent!.ClientRectangle.Contains(control.Bounds));
                            Assert.True(control.Width > 0 && control.Height > 0);
                        }
                        using (var image = new Bitmap(form.Width, form.Height)) form.DrawToBitmap(image, form.ClientRectangle);
                    }
                }
                finally { BootstrapThemeManager.CurrentTheme = original; }
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Closed_shell_drains_late_initialization_without_affecting_the_next_user(bool fail)
        {
            Run(() =>
            {
                var notifications = new DesktopNotificationCenter();
                var session = new Session();
                var api = new DeferredFoundation();
                var oldVm = new FoundationViewModel(api, notifications);
                using (var oldShell = new FoundationShellForm(oldVm, session, notifications))
                {
                    oldShell.Show();
                    var pending = oldVm.InitializeAsync(CancellationToken.None);
                    oldShell.Close();
                    Assert.True(oldShell.IsDisposed);
                    var canceled = api.Token.IsCancellationRequested;
                    session.SignIn(9);
                    using (var nextShell = new FoundationShellForm(new FoundationViewModel(new ReadyFoundation(), notifications), session, notifications))
                    {
                        nextShell.Show();
                        api.Complete(fail);
                        Pump(() => pending.IsCompleted);
                        pending.GetAwaiter().GetResult();
                        Assert.True(canceled);
                        Assert.False(oldVm.IsReady);
                        Assert.Null(notifications.LastNotification);
                        Assert.Equal("User 9", nextShell.CurrentUserText);
                        Assert.True(nextShell.Navigation.Enabled);
                    }
                }
            });
        }

        [Fact]
        public void Session_expiry_clears_private_workspace_and_relogin_rejects_the_old_workspace_version()
        {
            Run(() =>
            {
                var session = new Session();
                using (var shell = new FoundationShellForm(new FoundationViewModel(new ReadyFoundation()), session, new DesktopNotificationCenter()))
                {
                    shell.Show();
                    var oldVersion = shell.WorkspaceSessionVersion;
                    var privateView = new Panel();
                    shell.OpenWorkspace("private", "Private", privateView, oldVersion);
                    shell.HandleSessionExpired();
                    Assert.True(privateView.IsDisposed);
                    Assert.Empty(shell.Workspace.TabPages);
                    Assert.False(shell.Navigation.Enabled);
                    session.SignIn(9);
                    var staleView = new Panel();
                    Assert.Null(shell.OpenWorkspace("old", "Old", staleView, oldVersion));
                    Assert.True(staleView.IsDisposed);
                    var currentView = new Panel();
                    Assert.NotNull(shell.OpenWorkspace("new", "New", currentView, shell.WorkspaceSessionVersion));
                    Assert.Equal("User 9", shell.CurrentUserText);
                    Assert.True(shell.Navigation.Enabled);
                }
            });
        }

        [Fact]
        public void Canceled_permission_check_cannot_enable_an_action_when_client_ignores_cancellation()
        {
            Run(() =>
            {
                var client = new DeferredPermission();
                using (var cancellation = new CancellationTokenSource())
                using (var host = new Form())
                using (var action = new Button())
                {
                    host.Controls.Add(action);
                    host.Show();
                    using (var binder = new PermissionActionBinder(client, new Session()))
                    {
                        var pending = binder.ApplyAsync(action, "Catalog.Products.Write", cancellation.Token);
                        Assert.False(action.Enabled);
                        cancellation.Cancel();
                        client.Complete();
                        Pump(() => pending.IsCompleted);
                        Assert.True(pending.IsCanceled);
                        Assert.False(action.Enabled);
                    }
                }
            });
        }

        [Fact]
        public void Disposed_action_ignores_delayed_permission_grant()
        {
            Run(() =>
            {
                var client = new DeferredPermission();
                using (var action = new Button())
                using (var binder = new PermissionActionBinder(client, new Session()))
                {
                    var pending = binder.ApplyAsync(action, "Catalog.Products.Write", CancellationToken.None);
                    action.Dispose();
                    var changes = 0;
                    action.EnabledChanged += (_, __) => changes++;
                    client.Complete();
                    Pump(() => pending.IsCompleted);
                    pending.GetAwaiter().GetResult();
                    Assert.Equal(0, changes);
                }
            });
        }

        private static LoginForm Login(IdentityClient api)
        {
            var form = new LoginForm(new LoginViewModel(api, new DesktopNotificationCenter()));
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            return form;
        }

        private static void DialogKey(Form form, Keys key) => typeof(Form)
            .GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, new object[] { key });

        private static IEnumerable<Control> Children(Control root)
        {
            foreach (Control control in root.Controls)
            {
                yield return control;
                foreach (var child in Children(control)) yield return child;
            }
        }

        private static void Run(Action action) => StaTest.Run(_ =>
        {
            using (var guard = new WinFormsTestGuard())
            {
                action();
                Assert.Empty(guard.Exceptions);
            }
        }, TimeSpan.FromSeconds(12));

        private static void Pump(Func<bool> completed)
        {
            var elapsed = Stopwatch.StartNew();
            while (!completed() && elapsed.Elapsed < TimeSpan.FromSeconds(3)) { System.Windows.Forms.Application.DoEvents(); Thread.Yield(); }
            Assert.True(completed(), "Controlled GUI completion exceeded its bound.");
            System.Windows.Forms.Application.DoEvents();
        }

        private sealed class IdentityClient : IIdentityApiClient
        {
            private readonly TaskCompletionSource<ApiResponse<CurrentUserDto>> _completion = new TaskCompletionSource<ApiResponse<CurrentUserDto>>();
            public int Calls { get; private set; }
            public Task<ApiResponse<CurrentUserDto>> LoginAsync(LoginRequest request, CancellationToken token) { Calls++; return _completion.Task; }
            public Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(CancellationToken token) => throw new NotSupportedException();
            public void Complete(ApiResponse<CurrentUserDto> result) => _completion.TrySetResult(result);
        }

        private sealed class DeferredPermission : IPermissionApiClient
        {
            private readonly TaskCompletionSource<ApiResponse<PermissionDecisionDto>> _completion = new TaskCompletionSource<ApiResponse<PermissionDecisionDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(string key, CancellationToken token) => _completion.Task;
            public void Complete() => _completion.TrySetResult(ApiResponse<PermissionDecisionDto>.Success(new PermissionDecisionDto("Catalog.Products.Write", true)));
        }

        private sealed class DeferredFoundation : IFoundationApiClient
        {
            private readonly TaskCompletionSource<ApiResponse<FoundationStatus>> _completion = new TaskCompletionSource<ApiResponse<FoundationStatus>>(TaskCreationOptions.RunContinuationsAsynchronously);
            public CancellationToken Token { get; private set; }
            public Task<ApiResponse<FoundationStatus>> GetStatusAsync(FoundationStatusRequest request, CancellationToken token) { Token = token; return _completion.Task; }
            public void Complete(bool fail) => _completion.TrySetResult(fail
                ? ApiResponse<FoundationStatus>.Failure(new ApiError(ApiStatusCode.InternalServerError, "OldRequest.Failed", "Old user's initialization error"))
                : ApiResponse<FoundationStatus>.Success(new FoundationStatus(true, "Old user's runtime")));
        }

        private sealed class ReadyFoundation : IFoundationApiClient
        {
            public Task<ApiResponse<FoundationStatus>> GetStatusAsync(FoundationStatusRequest request, CancellationToken token) => Task.FromResult(ApiResponse<FoundationStatus>.Success(new FoundationStatus(true, "Local")));
        }

        private sealed class Session : IDesktopSession
        {
            public event EventHandler? SessionChanged;
            public CurrentUserDto? CurrentUser { get; private set; } = new CurrentUserDto(4, "operator", "User 4");
            public DesktopSessionState State => IsAuthenticated ? DesktopSessionState.Authenticated : DesktopSessionState.Unauthenticated;
            public long Version { get; private set; } = 1;
            public bool IsAuthenticated => CurrentUser != null;
            public void SignOut() { CurrentUser = null; Version++; SessionChanged?.Invoke(this, EventArgs.Empty); }
            public void SignIn(int id) { CurrentUser = new CurrentUserDto(id, "user" + id, "User " + id); Version++; SessionChanged?.Invoke(this, EventArgs.Empty); }
        }
    }
}
