using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SecurityNativeAndResetTests
{
    [Theory]
    [InlineData(900, 600, false)]
    [InlineData(900, 600, true)]
    [InlineData(1200, 760, false)]
    [InlineData(1200, 760, true)]
    public void Native_search_preserves_caret_focus_and_committed_owner(int width, int height, bool dark) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            var priorTheme = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
                var form = control.FindForm()!;
                form.ClientSize = new Size(width, height);
                NativeLookupTypingDriver.Activate(form);
                var owner = control.ViewModel.SelectedUserId;
                var search = control.UserSearch;
                Assert.True(search.Focus());
                var handle = search.Handle;
                var text = "";
                foreach (var character in "inactive")
                {
                    SendMessage(handle, 0x0102, new IntPtr(character), IntPtr.Zero);
                    text += character;
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal(text, search.Text);
                    Assert.Equal(text.Length, search.SelectionStart);
                    Assert.Equal(0, search.SelectionLength);
                    Assert.Equal(handle, GetFocus());
                    Assert.Equal(owner, control.ViewModel.SelectedUserId);
                }
                Assert.Single(control.Users.Items);
                Assert.Equal(102, ((SecurityUserDto)control.Users.Items[0]).UserId);
                SendMessage(handle, 0x00B1, new IntPtr(2), new IntPtr(4));
                Assert.Equal(2, search.SelectionStart);
                Assert.Equal(2, search.SelectionLength);
                SendMessage(handle, 0x0102, new IntPtr('X'), IntPtr.Zero);
                System.Windows.Forms.Application.DoEvents();
                Assert.Equal("inXtive", search.Text);
                Assert.Equal(3, search.SelectionStart);
                Assert.Equal(0, search.SelectionLength);
                Assert.Empty(control.Users.Items);
                Assert.Equal(handle, GetFocus());
                Assert.Equal(owner, control.ViewModel.SelectedUserId);
                Assert.True(search.RectangleToScreen(search.ClientRectangle).Width > 0);
                Assert.DoesNotContain(scene.Journal.Entries, e => e.Operation.Contains("Mutate"));
            }
            finally { BootstrapThemeManager.CurrentTheme = priorTheme; }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Security_reset_drains_rearmed_held_reads_before_replacement(bool ignoresCancellation) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            using var form = new UiLabForm();
            form.Show();
            Until(() => form.CurrentSalesControl != null && !form.IsResetting);
            form.ModuleSelector.SelectedIndex = 1;
            Until(() => form.CurrentSecurityControl != null && !form.IsResetting);
            for (var round = 0; round < 2; round++)
            {
                var old = form.CurrentSecurityControl!;
                var data = form.CurrentSecurityScenarioData!;
                var gate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>> { IgnoreCancellation = ignoresCancellation };
                data.Administration.UsersGate = gate;
                var reload = old.ActivateAsync(default);
                Until(() => data.Administration.OutstandingOperations.Any(t => !t.IsCompleted));
                var reset = form.ResetForTestAsync();
                Until(() => reset.IsCompleted);
                reset.GetAwaiter().GetResult();
                Assert.True(reload.IsCompleted);
                Assert.True(old.IsDisposed);
                Assert.True(gate.IsCompleted);
                Assert.False(form.IsPoisoned);
                Assert.NotSame(old, form.CurrentSecurityControl);
                Assert.NotSame(data, form.CurrentSecurityScenarioData);
                Assert.All(data.Administration.OutstandingOperations, t => Assert.True(t.IsCompleted));
                Assert.False(gate.Complete(ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>.Success(data.Data.Users)));
                Assert.Equal(3, form.CurrentSecurityControl!.Users.Items.Count);
            }
            form.CloseWorkspaceButton.PerformClick();
            Until(() => form.CurrentSecurityControl == null);
        });

    static void Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(7))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }
        Assert.True(condition(), "Security native/Reset operation did not complete within its bounded pump.");
    }

    [DllImport("user32.dll")] static extern IntPtr GetFocus();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
