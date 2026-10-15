using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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
                        Assert.NotEqual(IntPtr.Zero, form.Handle);
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(10));
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
    }
}
