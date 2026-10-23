using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed partial class GoodsReceiptControlTests
{
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Goods_required_date_native_characters_keep_model_until_enter(string theme)
        => Integrated((fixture, control) =>
        {
            var previous = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(theme == "Dark" ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
                var input = control.ReceiptDateEditor;
                var original = fixture.Editor.ReceiptDate;
                var driver = new NativeLookupTypingDriver(input);
                driver.SelectRange(0, driver.Text.Length);
                var candidate = string.Empty;
                foreach (var ch in "5/11/2026")
                {
                    driver.TypeCharacter(ch);
                    candidate += ch;
                    Assert.Equal(candidate, input.PendingText);
                    Assert.Equal(candidate.Length, driver.SelectionStart);
                    Assert.Equal(0, driver.SelectionLength);
                    Assert.Equal(original, fixture.Editor.ReceiptDate);
                }
                driver.Key(Keys.Enter);
                Assert.Equal(new DateTime(2026, 11, 5), fixture.Editor.ReceiptDate);
                Assert.Equal("05/11/2026", input.PendingText);
                Console.WriteLine(UiNextNativeScaleObservation.Observe(control.FindForm()!, CancellationToken.None));
                Console.WriteLine("UIN_T06_GOODS_DATE_NATIVE;theme=" + theme + ";windowDpi=" + control.FindForm()!.DeviceDpi);
            }
            finally { BootstrapThemeManager.CurrentTheme = previous; }
        });

    [Fact]
    public void Required_date_keeps_raw_edit_through_state_and_blocks_save_post_and_back_until_valid()
        => Integrated((fixture, control) =>
        {
            var input = control.ReceiptDateEditor;
            input.Text = "31/11/2026";
            typeof(GoodsReceiptControl).GetMethod("ApplyState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, null);
            Assert.Equal("31/11/2026", input.PendingText);
            Assert.True(input.HasPendingEdit);
            Assert.False(control.TryShowReceiptList());
            control.SaveButton.PerformClick();
            control.LastOperation.GetAwaiter().GetResult();
            Assert.Equal(AccessibleRole.Alert, control.OperationStatusLabel.AccessibleRole);
            Assert.Contains("valid receipt date", input.AccessibleDescription);
            control.PostButton.PerformClick();
            control.LastOperation.GetAwaiter().GetResult();
            Assert.Null(fixture.Api.LastUpdate);
            Assert.Null(fixture.Api.LastPost);
            Assert.Equal("31/11/2026", input.PendingText);
            input.Text = "5/11/2026";
            control.SaveButton.PerformClick();
            control.LastOperation.GetAwaiter().GetResult();
            Assert.NotNull(fixture.Api.LastUpdate);
            Assert.Equal(new DateTime(2026, 11, 5), fixture.Api.LastUpdate!.ReceiptDate);
            Assert.Equal(DateTimeKind.Unspecified, fixture.Api.LastUpdate.ReceiptDate.Kind);
        });

    [Fact]
    public void Goods_authoritative_date_change_cancels_old_pending_candidate()
        => Integrated((fixture, control) =>
        {
            var input = control.ReceiptDateEditor;
            input.Text = "31/11/2026";
            fixture.Editor.ReceiptDate = new DateTime(2026, 11, 7);
            Assert.False(input.HasPendingEdit);
            Assert.Equal("07/11/2026", input.PendingText);
            Assert.Equal(new DateTime(2026, 11, 7), input.Value);
        });
}

public sealed class DesktopDateInputIntegrationTests
{
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Sales_required_date_native_characters_keep_model_until_enter(string theme)
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            var previous = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(theme == "Dark" ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
                using var host = new SalesControlHost(); host.Load();
                var input = ((SalesReceiptControl)host.Control).SaleDateEditor;
                var original = host.Editor.SaleDate;
                var driver = new NativeLookupTypingDriver(input);
                driver.SelectRange(0, driver.Text.Length);
                var candidate = string.Empty;
                foreach (var ch in "5/11/2026")
                {
                    driver.TypeCharacter(ch);
                    candidate += ch;
                    Assert.Equal(candidate, input.PendingText);
                    Assert.Equal(candidate.Length, driver.SelectionStart);
                    Assert.Equal(0, driver.SelectionLength);
                    Assert.Equal(original, host.Editor.SaleDate);
                }
                driver.Key(Keys.Enter);
                Assert.Equal(new DateTime(2026, 11, 5), host.Editor.SaleDate);
                Assert.Equal("05/11/2026", input.PendingText);
                Console.WriteLine(UiNextNativeScaleObservation.Observe(host.Form, CancellationToken.None));
                Console.WriteLine("UIN_T06_SALES_DATE_NATIVE;theme=" + theme + ";windowDpi=" + host.Form.DeviceDpi);
                Assert.Empty(guard.Exceptions);
            }
            finally { BootstrapThemeManager.CurrentTheme = previous; }
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Sales_required_date_keeps_pending_text_and_blocks_commands_and_navigation()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            using var host = new SalesControlHost();
            host.Load();
            var control = (SalesReceiptControl)host.Control;
            var input = control.SaleDateEditor;
            input.Text = "31/11/2026";
            host.Call("ApplyState");
            Assert.Equal("31/11/2026", input.PendingText);
            Assert.False(control.TryShowReceiptList());
            host.CallTask("SaveAsync");
            Assert.Equal(AccessibleRole.Alert, control.OperationStatusLabel.AccessibleRole);
            Assert.Contains("valid sale date", input.AccessibleDescription);
            host.CallTask("PostAsync");
            Assert.Empty(host.Api.Updates);
            Assert.Empty(host.Api.Posts);
            Assert.Equal("31/11/2026", input.PendingText);
            input.Text = "5/11/2026";
            host.CallTask("SaveAsync");
            Assert.Single(host.Api.Updates);
            Assert.Equal(new DateTime(2026, 11, 5), host.Api.Updates.Single().SaleDate);
            Assert.Equal(DateTimeKind.Unspecified, host.Api.Updates.Single().SaleDate.Kind);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Sales_authoritative_date_change_cancels_old_pending_candidate()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            using var host = new SalesControlHost(); host.Load();
            var input = ((SalesReceiptControl)host.Control).SaleDateEditor;
            input.Text = "31/11/2026";
            host.Editor.SaleDate = new DateTime(2026, 11, 7);
            Assert.False(input.HasPendingEdit);
            Assert.Equal("07/11/2026", input.PendingText);
            Assert.Equal(new DateTime(2026, 11, 7), input.Value);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Sales_signout_scrubs_pending_date_text()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            using var host = new SalesControlHost(); host.Load();
            var input = ((SalesReceiptControl)host.Control).SaleDateEditor;
            input.Text = "private candidate";
            host.Session.SignOut();
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(string.Empty, input.PendingText);
            Assert.Null(input.Value);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(20));
    }
}
