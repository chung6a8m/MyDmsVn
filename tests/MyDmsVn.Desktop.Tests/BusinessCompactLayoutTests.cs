using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

public sealed class BusinessCompactLayoutTests
{
    private readonly ITestOutputHelper _output;
    public BusinessCompactLayoutTests(ITestOutputHelper output) { _output = output; }

    [Theory]
    [InlineData(944, 600, BootstrapThemeMode.Light)]
    [InlineData(944, 600, BootstrapThemeMode.Dark)]
    [InlineData(1680, 900, BootstrapThemeMode.Light)]
    [InlineData(1680, 900, BootstrapThemeMode.Dark)]
    public void Compact_shell_retains_visible_fields_and_native_focus_order(int width, int height, BootstrapThemeMode mode) => StaTest.Run(token =>
    {
        var original = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
            using var guard = new WinFormsTestGuard();
            using var fixture = new ReceiptAcceptanceTests.Fixture();
            using var receipt = fixture.CreateControl();
            using var shell = new FoundationShellForm(new FoundationViewModel(new FoundationShellFormTests.ReadyApiClient(), fixture.Notifications));
            shell.ClientSize = new Size(width + shell.Navigation.Width + 8, height + 110);
            shell.OpenWorkspace("receipt", "Goods Receipts", receipt, shell.WorkspaceSessionVersion);
            shell.TopMost = true;
            shell.Show();
            ReceiptAcceptanceTests.Load(receipt, token);
            if (width == 944) Capture(shell, "shell-" + mode);
            shell.TopMost = false;
            // The contract is measured on the workspace itself, independently of shell chrome.
            using var viewport = new Form { ClientSize = new Size(width, height), AutoScaleMode = AutoScaleMode.Dpi, TopMost = true, StartPosition = FormStartPosition.Manual, Location = new Point(10, 10) };
            receipt.Parent = viewport;
            viewport.Show();
            var physical = PhysicalViewport(width, height, viewport.DeviceDpi);
            viewport.ClientSize = physical;
            Assert.Equal(physical, viewport.ClientSize);
            Pump();
            Assert.Equal("Segoe UI", receipt.Font.Name);
            Assert.Equal(10f, receipt.Font.SizeInPoints);
            Assert.Equal(10f, receipt.ReceiptGrid[1, 1].View.Font.SizeInPoints);
            Assert.Equal(260 * receipt.DeviceDpi / 96, receipt.Controls.OfType<SplitContainer>().Single().Panel1.Width);
            Assert.True(receipt.ReceiptGrid.Rows[1].Height >= 36 * receipt.DeviceDpi / 96);
            Assert.True(ScreenBounds(receipt.WarehouseLookup).Bottom < ScreenBounds(receipt.LineGrid).Top, "Metadata must be above the dominant lines.");
            var caption = receipt.WarehouseLookup.Parent!.Controls.OfType<Label>().Single();
            Assert.InRange(ScreenBounds(receipt.WarehouseLookup).Top - ScreenBounds(caption).Bottom, 0, 8 * receipt.DeviceDpi / 96);
            Assert.True(receipt.LineGrid.Width > receipt.Width / 2);
            Assert.True(receipt.LineGrid.Height >= viewport.ClientSize.Height / 3);
            foreach (var control in new Control[] { receipt.ReceiptDateEditor, receipt.WarehouseLookup, receipt.EmployeeLookup, receipt.NoteEditor,
                receipt.SaveButton, receipt.PostButton, receipt.AddLineButton, receipt.RemoveLineButton, receipt.OperationStatusLabel, receipt.TotalLabel })
                Assert.True(ScreenBounds(viewport).Contains(ScreenBounds(control)), control.GetType().Name + " must be visible: " + ScreenBounds(control));
            Assert.Equal(224 * shell.DeviceDpi / 96, shell.Navigation.Width);
            receipt.WarehouseLookup.Focus();
            GoodsReceiptInlineEditorTests.Key(Keys.Tab);
            Assert.True(receipt.EmployeeLookup.ContainsFocus, "Tab from Warehouse must reach Employee.");
            GoodsReceiptInlineEditorTests.Key(Keys.Tab);
            Assert.True(receipt.NoteEditor.ContainsFocus);
            GoodsReceiptInlineEditorTests.Key(Keys.Tab);
            Assert.True(receipt.LineGrid.ContainsFocus);
            Capture(viewport, mode + "-" + width);

            _output.WriteLine("DPI=" + receipt.DeviceDpi + ";VIEWPORT=" + receipt.ClientSize + ";LINES=" + receipt.LineGrid.Bounds);
            Assert.Empty(guard.Exceptions);
        }
        finally { BootstrapThemeManager.CurrentTheme = original; }
    }, TimeSpan.FromSeconds(20));

    [Theory]
    [InlineData(BootstrapThemeMode.Light)]
    [InlineData(BootstrapThemeMode.Dark)]
    public void Cell_editors_and_popups_remain_usable_after_scroll_resize_and_theme_change(BootstrapThemeMode mode) => StaTest.Run(token =>
    {
        var original = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
            using var guard = new WinFormsTestGuard();
            using var host = new GoodsReceiptInlineEditorTests.SessionHost();
            host.Grid.FindForm()!.TopMost = true;
            host.Begin(1, 2);
            var input = host.NativeTextBox();
            Assert.Equal(HorizontalAlignment.Right, input.TextAlign);
            input.Text = "8.00001";
            input.SelectionStart = 2;
            var editor = host.Adapter.QuantityControl;
            host.Grid.FindForm()!.ClientSize = new Size(640, 240);
            host.Grid.CustomScrollPosition = new Point(1, 0);
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode == BootstrapThemeMode.Light ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
            Pump();
            Assert.Same(editor, host.Adapter.QuantityControl);
            Assert.Equal("8.00001", input.Text);
            Assert.Equal(2, input.SelectionStart);
            Assert.True(input.ClientRectangle.Height >= TextRenderer.MeasureText("123", input.Font).Height, "Native client=" + input.ClientRectangle + "; font=" + input.Font + "; measured=" + TextRenderer.MeasureText("123", input.Font) + "; row=" + host.Grid.Rows[1].Height + "; wrapper=" + editor.Size);
            GoodsReceiptInlineEditorTests.Key(Keys.Enter);
            Assert.Equal(2.5m, host.Editor.Lines[0].Quantity);
            Assert.NotNull(host.Adapter.EditError);
            GoodsReceiptInlineEditorTests.Key(Keys.Escape);
            host.Begin(1, 2);
            input = host.NativeTextBox();
            input.Text = "7.25";
            GoodsReceiptInlineEditorTests.Key(Keys.Tab);
            Assert.Equal(7.25m, host.Editor.Lines[0].Quantity);
            Assert.Equal(3, host.Grid.Selection.ActivePosition.Column);
            Assert.Equal(3, host.Grid.RowsCount);
            GoodsReceiptInlineEditorTests.Key(Keys.Escape);
            host.Begin(1, 1);
            host.Adapter.ProductControl.Text = "P8";
            GoodsReceiptInlineEditorTests.Key(Keys.F4);
            Assert.True(host.Adapter.ProductControl.IsDropDownOpen);
            var popup = host.Adapter.ProductControl.ResultsGrid;
            var bounds = popup.RectangleToScreen(popup.ClientRectangle);
            Assert.True(Screen.FromControl(popup).WorkingArea.Contains(bounds), "Popup outside working area: " + bounds);
            Assert.Equal(7, host.Editor.Lines[0].ProductId);
            CapturePopup(popup, "native-popup-" + BootstrapThemeManager.CurrentTheme.Mode);
            Capture(host.Grid.FindForm()!, "popup-" + BootstrapThemeManager.CurrentTheme.Mode);
            GoodsReceiptInlineEditorTests.Key(Keys.Escape);
            GoodsReceiptInlineEditorTests.Key(Keys.Escape);

            Assert.Empty(guard.Exceptions);
        }
        finally { BootstrapThemeManager.CurrentTheme = original; }
    }, TimeSpan.FromSeconds(20));

    [Theory]
    [InlineData(BootstrapThemeMode.Light)]
    [InlineData(BootstrapThemeMode.Dark)]
    public void Compact_errors_posted_and_uncertain_reload_remain_reachable(BootstrapThemeMode mode) => StaTest.Run(token =>
    {
        var original = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
            using var guard = new WinFormsTestGuard();
            using var fixture = new ReceiptAcceptanceTests.Fixture();
            using var receipt = fixture.CreateControl();
            using var form = new Form { ClientSize = new Size(944, 600), TopMost = true, StartPosition = FormStartPosition.Manual, Location = new Point(10, 10) };
            form.Controls.Add(receipt); form.Show();
            ReceiptAcceptanceTests.Load(receipt, token);
            receipt.LineGrid.Focus(); receipt.LineGrid.Selection.Focus(new SourceGrid.Position(1, 2), true);
            GoodsReceiptInlineEditorTests.Key(Keys.F2);
            receipt.QuantityEditor.Text = "8.00001";
            receipt.SaveButton.PerformClick();
            Assert.Contains("decimal places", receipt.OperationStatusLabel.Text);
            Assert.Equal(AccessibleRole.Alert, receipt.OperationStatusLabel.AccessibleRole);
            Assert.Equal(2.5m, fixture.Editor.Lines[0].Quantity);
            Capture(form, "invalid-" + mode);
            GoodsReceiptInlineEditorTests.Key(Keys.Escape);
            fixture.Api.CommitThenLosePostResponse = true;
            receipt.PostButton.PerformClick(); receipt.LastOperation.GetAwaiter().GetResult();
            Assert.True(fixture.Editor.RequiresPostOutcomeReload);
            Assert.False(receipt.SaveButton.Enabled);
            Assert.True(receipt.RefreshButton.Enabled);
            Assert.Contains("Posting outcome unknown", receipt.OperationStatusLabel.Text);
            Capture(form, "uncertain-" + mode);
            form.ClientSize = new Size(620, 440); Pump();
            for (var parent = receipt.SaveButton.Parent; parent != null; parent = parent.Parent)
                if (parent is ScrollableControl scroll && scroll.AutoScroll) scroll.ScrollControlIntoView(receipt.SaveButton);
            Assert.True(ScreenBounds(form).Contains(ScreenBounds(receipt.SaveButton)), "Constrained footer must be reachable by scrolling.");
            form.ClientSize = new Size(944, 600); Pump();
            receipt.RefreshButton.PerformClick(); receipt.LastOperation.GetAwaiter().GetResult();
            Assert.Equal("Posted", receipt.StatusLabel.Text);
            Assert.False(receipt.SaveButton.Enabled);
            Assert.False(receipt.PostButton.Enabled);
            Capture(form, "posted-" + mode);
            Assert.Empty(guard.Exceptions);
        }
        finally { BootstrapThemeManager.CurrentTheme = original; }
    }, TimeSpan.FromSeconds(20));

    [Fact]
    public void Navigation_preserves_nested_keyboard_selection_and_clears_removed_identity() => StaTest.Run(_ =>
    {
        using var guard = new WinFormsTestGuard();
        using var shell = new FoundationShellForm(new FoundationViewModel(new FoundationShellFormTests.ReadyApiClient(), new DesktopNotificationCenter()));
        var group = new BusinessCompactNavigationItem { Text = "Group", Expanded = true };
        var child = new BusinessCompactNavigationItem { Text = "Child" };
        group.Items.Add(child);
        shell.Navigation.Items.Add(group);
        shell.Show(); Pump();
        shell.Navigation.SelectedItem = child;
        Assert.True(child.Selected);
        Assert.Same(child, shell.Navigation.SelectedItem);
        shell.Navigation.Collapse();
        Assert.Equal(64 * shell.DeviceDpi / 96, shell.Navigation.Width);
        Assert.NotNull(shell.Navigation.Controls.Cast<Control>().SelectMany(control => control.Controls.Cast<Control>()).Single(control => control.AccessibleName == "Goods Receipts"));
        shell.Navigation.Expand();
        var buttons = shell.Navigation.Controls.Cast<Control>().SelectMany(control => control.Controls.Cast<Control>()).OfType<Button>().ToArray();
        var parent = buttons.Single(button => button.AccessibleName == "Group");
        parent.Focus(); GoodsReceiptInlineEditorTests.Key(Keys.Left);
        Assert.False(group.Expanded);
        var currentParent = shell.Navigation.Controls.Cast<Control>().SelectMany(control => control.Controls.Cast<Control>()).OfType<Button>().Single(button => button.AccessibleName == "Group");
        currentParent.Focus(); GoodsReceiptInlineEditorTests.Key(Keys.Right);
        Assert.True(group.Expanded);
        buttons = shell.Navigation.Controls.Cast<Control>().SelectMany(control => control.Controls.Cast<Control>()).OfType<Button>().ToArray();
        parent = buttons.Single(button => button.AccessibleName == "Group");
        parent.Focus(); GoodsReceiptInlineEditorTests.Key(Keys.Down);
        Assert.True(buttons.Single(button => button.AccessibleName == "Child").Focused);
        group.Enabled = false;
        shell.Navigation.SelectedItem = shell.Navigation.Items[0];
        shell.Navigation.SelectedItem = child;
        Assert.NotSame(child, shell.Navigation.SelectedItem);
        group.Enabled = true;
        shell.Navigation.SelectedItem = child;
        group.Items.Remove(child);
        Assert.Null(shell.Navigation.SelectedItem);
        Assert.False(child.Selected);
        Assert.Empty(guard.Exceptions);
    }, TimeSpan.FromSeconds(15));

    [Fact]
    public void Logical_viewports_convert_to_physical_pixels_without_simulating_native_dpi()
    {
        Assert.Equal(new Size(1180, 750), PhysicalViewport(944, 600, 120));
        Assert.Equal(new Size(2100, 1125), PhysicalViewport(1680, 900, 120));
    }
    private static Size PhysicalViewport(int width, int height, int dpi) => new Size((int)Math.Ceiling(width * dpi / 96d), (int)Math.Ceiling(height * dpi / 96d));

    [Fact]
    public void Splitter_minimum_preserves_reachable_master_filters() => StaTest.Run(token =>
    {
        using var fixture = new ReceiptAcceptanceTests.Fixture();
        using var receipt = fixture.CreateControl();
        using var form = new Form { ClientSize = new Size(944, 600) };
        form.Controls.Add(receipt); form.Show();
        ReceiptAcceptanceTests.Load(receipt, token);
        var split = receipt.Controls.OfType<SplitContainer>().Single();
        split.SplitterDistance = Math.Max(25, split.Panel1MinSize);
        Pump();
        Assert.True(split.Panel1.Width >= receipt.SearchBox.Width + receipt.SearchBox.Margin.Horizontal, "An allowed splitter endpoint must retain the filters.");
        Assert.True(split.Panel2.Width >= 400 * receipt.DeviceDpi / 96);
    }, TimeSpan.FromSeconds(15));

    private static void Capture(Form form, string name)
    {
        form.Refresh(); Pump();
        var root = System.IO.Path.GetFullPath("TestResults/bci-t05-window-captures");
        System.IO.Directory.CreateDirectory(root);
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var dc = graphics.GetHdc();
            try { Assert.True(PrintWindow(form.Handle, dc, 3), "Owned HWND capture failed."); }
            finally { graphics.ReleaseHdc(dc); }
        }
        bitmap.Save(System.IO.Path.Combine(root, name + "-dpi" + form.DeviceDpi + ".png"));
    }
    private static void CapturePopup(Control grid, string name)
    {
        var window = GetAncestor(grid.Handle, 2);
        Assert.NotEqual(grid.FindForm()?.Handle ?? IntPtr.Zero, window);
        Assert.True(GetWindowRect(window, out var rectangle));
        var bounds = Rectangle.FromLTRB(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
        Assert.True(Screen.FromControl(grid).WorkingArea.Contains(bounds), "Actual popup window must fit its monitor working area.");
        var root = System.IO.Path.GetFullPath("TestResults/bci-t05-window-captures");
        System.IO.Directory.CreateDirectory(root);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var dc = graphics.GetHdc();
            try { Assert.True(PrintWindow(window, dc, 2)); }
            finally { graphics.ReleaseHdc(dc); }
        }
        bitmap.Save(System.IO.Path.Combine(root, name + "-dpi" + grid.DeviceDpi + ".png"));
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRectangle { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    private static Rectangle ScreenBounds(Control control) => control.RectangleToScreen(control.ClientRectangle);
    private static void Pump() { System.Windows.Forms.Application.DoEvents(); }
}
