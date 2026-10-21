using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

public sealed class BusinessCompactRefinementTests : IClassFixture<RefinementProbeFixture>
{
    private readonly ITestOutputHelper _output;
    private readonly RefinementProbeFixture _probe;
    public BusinessCompactRefinementTests(ITestOutputHelper output, RefinementProbeFixture probe) { _output = output; _probe = probe; }

    [Theory]
    [InlineData(BootstrapThemeMode.Light)]
    [InlineData(BootstrapThemeMode.Dark)]
    public void Shell_has_grouped_icon_navigation_and_compact_session_actions(BootstrapThemeMode mode) => Run((token, guard) =>
    {
        var original = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
            using var shell = Shell();
            shell.SetCurrentUser("BCF Operator");
            shell.Show();
            var controls = Descendants(shell.Navigation).ToArray();
            Assert.All(shell.Navigation.Items, item => Assert.NotNull(item.Icon));
            foreach (var group in new[] { "CATALOG", "INVENTORY", "SYSTEM" })
                Assert.Contains(controls, control => control is Label && control.Text == group);
            Assert.Contains(controls, control => control.Text == "MyDmsVn");
            Assert.Single(controls, control => control.Text == "BCF Operator");
            var theme = Assert.Single(controls.OfType<Button>(), control => control.AccessibleName == "Theme toggle");
            var signOut = Assert.Single(controls.OfType<Button>(), control => control.AccessibleName == "Sign out");
            Assert.True(theme.Height <= 36 * shell.DeviceDpi / 96);
            Assert.True(signOut.Height <= 36 * shell.DeviceDpi / 96);
            Assert.Equal(224 * shell.DeviceDpi / 96, shell.Navigation.Width);
            shell.Navigation.Collapse();
            Assert.Equal(64 * shell.DeviceDpi / 96, shell.Navigation.Width);
            Assert.True(theme.Visible && signOut.Visible);
            Assert.All(Descendants(shell.Navigation).OfType<Button>().Where(control => control.Tag is BusinessCompactNavigationItem),
                button => { Assert.Equal("", button.Text); Assert.False(string.IsNullOrWhiteSpace(button.AccessibleName)); });
            shell.Navigation.Expand();
            var previousMode = BootstrapThemeManager.CurrentTheme.Mode;
            NativeLookupTypingDriver.Activate(shell);
            theme.Focus();
            GoodsReceiptInlineEditorTests.Key(Keys.Space);
            Assert.NotEqual(previousMode, BootstrapThemeManager.CurrentTheme.Mode);
            Assert.True(theme.Focused);
            var selected = shell.Navigation.Items.Single(item => Equals(item.Tag, "goods-receipts"));
            shell.Navigation.SelectedItem = selected;
            Assert.Same(selected, shell.Navigation.SelectedItem);
            foreach (var button in Descendants(shell.Navigation).OfType<Button>().Where(button => button.Tag is BusinessCompactNavigationItem && button.Enabled))
                Assert.True(Contrast(button.ForeColor, button.BackColor) >= 4.5, "Navigation label contrast: " + button.AccessibleName);
            Assert.True(Contrast(BootstrapThemeManager.CurrentTheme.Colors.Focus, shell.Navigation.BackColor) >= 3);
            Assert.Equal("BCF Operator", shell.CurrentUserText);
            Assert.Empty(guard.Exceptions);
        }
        finally { BootstrapThemeManager.CurrentTheme = original; }
    });

    [Theory]
    [InlineData("WarehouseFilterLookup")]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Graphite_chrome_and_focus_remain_readable_after_theme_switch(string target) => Run((token, guard) =>
    {
        using var fixture = new GoodsReceiptLookupFixture(target, token);
        guard.Attach(fixture.Control.WarehouseFilterLookup.ResultsGrid);
        guard.Attach(fixture.Control.WarehouseLookup.ResultsGrid);
        guard.Attach(fixture.Control.EmployeeLookup.ResultsGrid);
        using var shell = Shell();
        shell.ClientSize = new Size(1200, 760);
        shell.TopMost = true;
        var page = shell.OpenWorkspace("receipt", "Goods Receipts", fixture.Control, shell.WorkspaceSessionVersion);
        fixture.Form.Hide();
        shell.Show();
        NativeLookupTypingDriver.Activate(shell);
        fixture.Refocus();
        fixture.TypeText("gui");
        fixture.Driver.SelectRange(1, 1);
        var nativeEdit = fixture.Driver.FocusedHandle;
        shell.ToggleTheme();
        if (BootstrapThemeManager.CurrentTheme.Mode != BootstrapThemeMode.Dark) shell.ToggleTheme();
        System.Windows.Forms.Application.DoEvents();
        var capture = Capture(shell, "pending-" + target, token);
        if (target == "WarehouseLookup") Capture(shell, "pending-physical-" + target, token, screen: true);
        using (var image = new Bitmap(capture))
        {
            var x = (int)((shell.Workspace.Right - 12) * image.Width / (double)shell.ClientSize.Width);
            var y = (int)((shell.Workspace.Top + 12) * image.Height / (double)shell.ClientSize.Height);
            Assert.True(image.GetPixel(x, y).GetBrightness() < .25f, "Rendered unused workspace header must have a Graphite surface.");
            var accessory = fixture.Lookup.Controls.Cast<Control>().Single(control => control.AccessibleName == "Open lookup results");
            var origin = shell.PointToClient(accessory.PointToScreen(Point.Empty));
            var surface = BootstrapThemeManager.CurrentTheme.Colors.Surface.ToArgb();
            Assert.Equal(surface, image.GetPixel(origin.X + 1, origin.Y + 1).ToArgb());
            var glyphPixels = 0;
            for (var row = 2; row < accessory.Height - 2; row++)
                for (var column = 2; column < accessory.Width - 2; column++)
                    if (image.GetPixel(origin.X + column, origin.Y + row).ToArgb() != surface) glyphPixels++;
            Assert.True(glyphPixels > 5, "The actual lookup must paint a visible chevron on its theme surface.");
        }
        Assert.True(shell.Navigation.BackColor.GetBrightness() < .15f, "Graphite navigation must be quieter than the workspace.");
        Assert.Equal("gui", fixture.Driver.Text);
        Assert.Equal(1, fixture.Driver.SelectionStart);
        Assert.Equal(1, fixture.Driver.SelectionLength);
        fixture.Driver.AssertFocused();
        Assert.Equal(nativeEdit, fixture.Driver.FocusedHandle);
        Assert.Same(page, shell.Workspace.SelectedTab);
        Assert.True(fixture.Lookup.HasPendingText);
        Assert.Equal(3, fixture.Editor.WarehouseId);
        Assert.Equal(5, fixture.Editor.EmployeeId);
        Assert.Null(fixture.Control.WarehouseFilterLookup.SelectedValue);
        Assert.False(fixture.Editor.IsDirty);
        shell.ClientSize = new Size(944, 600);
        System.Windows.Forms.Application.DoEvents();
        Assert.Equal("gui", fixture.Driver.Text);
        Assert.Equal(1, fixture.Driver.SelectionStart);
        Assert.Equal(1, fixture.Driver.SelectionLength);
        fixture.Driver.AssertFocused();
        shell.ToggleTheme();
        Assert.Equal("gui", fixture.Driver.Text);
        Assert.Equal(nativeEdit, fixture.Driver.FocusedHandle);
        _output.WriteLine("Theme and resize preserve raw gui, range1+1, committed IDs, native HWND/focus and workspace identity; target=" + target);
        Assert.Empty(guard.Exceptions);
    });

    [Theory]
    [InlineData(1680, 900, BootstrapThemeMode.Light)]
    [InlineData(1680, 900, BootstrapThemeMode.Dark)]
    [InlineData(1200, 760, BootstrapThemeMode.Light)]
    [InlineData(1200, 760, BootstrapThemeMode.Dark)]
    [InlineData(944, 600, BootstrapThemeMode.Light)]
    [InlineData(944, 600, BootstrapThemeMode.Dark)]
    public void Shared_shell_capture_preserves_catalog_identity_and_reachable_session_at_logical_viewports(int width, int height, BootstrapThemeMode mode) => Run((token, guard) =>
    {
        var original = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
            using var model = new ProductCatalogViewModel(new ReferenceProducts(), new WeakReferenceMessenger(),
                new ImmediateUiDispatcher(), new DesktopNotificationCenter(), new SystemAsyncDelay(), TimeSpan.Zero);
            using var content = new CatalogControl(model);
            using var shell = Shell();
            // Exact logical layout fixture, including windows larger than this monitor.
            // This changes no production minimum; physical captures are separately admitted.
            var size = new Size(width, height);
            shell.MinimumSize = new Size(size.Width + shell.Width - shell.ClientSize.Width, size.Height + shell.Height - shell.ClientSize.Height);
            shell.ClientSize = size;
            shell.SetCurrentUser("BCF Operator");
            shell.OpenWorkspace("products", "Products", content, shell.WorkspaceSessionVersion);
            shell.OpenWorkspace("receipt-preview", "Goods Receipts", new Panel(), shell.WorkspaceSessionVersion);
            shell.OpenWorkspace("balances-preview", "Tồn kho", new Panel(), shell.WorkspaceSessionVersion);
            shell.Workspace.SelectedIndex = 0;
            shell.Navigation.SelectedItem = shell.Navigation.Items.Single(item => Equals(item.Tag, CatalogKind.Product));
            shell.TopMost = true;
            shell.Show();
            NativeLookupTypingDriver.Activate(shell);
            content.ActivateAsync(token).GetAwaiter().GetResult();
            model.SelectAsync(1, token).GetAwaiter().GetResult();
            content.EditorControls["name"].Focus();
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(size, shell.ClientSize);
            Assert.Equal(1, model.SelectedId);
            Assert.Equal("SP001", model.Code);
            Assert.Equal("A4 paper 80gsm", model.Name);
            var page = shell.Workspace.SelectedTab;
            shell.ToggleTheme(); shell.ToggleTheme();
            Assert.Same(page, shell.Workspace.SelectedTab);
            Assert.Equal(1, model.SelectedId);
            Assert.Equal("SP001", model.Code);
            foreach (var button in Descendants(shell.Navigation.SessionArea).OfType<Button>())
                Assert.True(shell.Navigation.SessionArea.ClientRectangle.Contains(button.Bounds));
            var last = Descendants(shell.Navigation).OfType<Button>().Single(button => button.AccessibleName == "Security administration");
            var scrolling = Assert.IsType<FlowLayoutPanel>(last.Parent);
            scrolling.ScrollControlIntoView(last);
            Assert.True(scrolling.ClientRectangle.IntersectsWith(last.Bounds));
            scrolling.AutoScrollPosition = Point.Empty;
            Capture(shell, "catalog-shell-" + width + "x" + height, token);
            if (width == 1200) Capture(shell, "catalog-shell-physical-" + width + "x" + height, token, screen: true);
            _output.WriteLine("Measured logical client=" + shell.ClientSize + "; mode=" + mode + "; product state=18 synthetic rows, selected SP001, page1, scroll origin; catalog composition remains T04 work.");
            Assert.Empty(guard.Exceptions);
        }
        finally { BootstrapThemeManager.CurrentTheme = original; }
    });

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static FoundationShellForm Shell() => new FoundationShellForm(
        new FoundationViewModel(new FoundationShellFormTests.ReadyApiClient(), new DesktopNotificationCenter()));

    [Fact]
    public void Graphite_tab_headers_render_full_caption_without_bright_bands() => Run((token, guard) =>
    {
        var original = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(BootstrapThemeMode.Dark);
            using var shell = Shell();
            shell.ClientSize = new Size(1200, 760);
            foreach (var title in new[] { "Products", "Goods Receipts", "Tồn kho" })
                shell.OpenWorkspace(title, title, new Panel(), shell.WorkspaceSessionVersion);
            shell.Workspace.SelectedIndex = 0;
            shell.Show();
            NativeLookupTypingDriver.Activate(shell);
            Assert.True(Descendants(shell.Navigation).OfType<Button>().Single(button => button.Name == "CompactBrand").Focus());
            Assert.False(shell.Workspace.Focused);
            var path = Capture(shell, "tab-edges", token);
            using var image = new Bitmap(path);
            var failures = new List<string>();
            using var nativeGraphics = shell.Workspace.CreateGraphics();
            using var captureGraphics = Graphics.FromImage(image);
            for (var index = 0; index < shell.Workspace.TabCount; index++)
            {
                var rectangle = shell.Workspace.GetTabRect(index);
                var textWidth = TextRenderer.MeasureText(shell.Workspace.TabPages[index].Text, shell.Workspace.Font, new Size(10000, 100), TextFormatFlags.SingleLine).Width;
                var nativeWidth = TextRenderer.MeasureText(nativeGraphics, shell.Workspace.TabPages[index].Text, shell.Workspace.Font, new Size(10000, 100), TextFormatFlags.SingleLine).Width;
                var bitmapWidth = TextRenderer.MeasureText(captureGraphics, shell.Workspace.TabPages[index].Text, shell.Workspace.Font, new Size(10000, 100), TextFormatFlags.SingleLine).Width;
                _output.WriteLine("TAB_METRICS=" + shell.Workspace.TabPages[index].Text + ";font=" + shell.Workspace.Font + ";deviceDpi=" + shell.Workspace.DeviceDpi + ";graphicsDpi=" + nativeGraphics.DpiX + ";captureDpi=" + captureGraphics.DpiX + ";bounds=" + rectangle + ";defaultText=" + textWidth + ";nativeText=" + nativeWidth + ";bitmapText=" + bitmapWidth);
                if (rectangle.Width < textWidth + 24) failures.Add("Caption padding too narrow: " + shell.Workspace.TabPages[index].Text + " bounds=" + rectangle + " text=" + textWidth);
                if (index != shell.Workspace.SelectedIndex && image.GetPixel(shell.Workspace.Left + rectangle.Left + rectangle.Width / 2, shell.Workspace.Top + rectangle.Bottom - 1).GetBrightness() >= .5f)
                    failures.Add("Bright native bottom band at tab=" + index);
                var pixel = image.GetPixel(shell.Workspace.Left + rectangle.Left + 1, shell.Workspace.Top + rectangle.Top + 1);
                if (pixel.GetBrightness() >= .5f) failures.Add("Bright native tab edge: " + index + " color=" + pixel);
            }
            var receiptHeader = shell.Workspace.GetTabRect(1);
            using var expectedCaption = new Bitmap(400, receiptHeader.Height);
            using (var expectedGraphics = Graphics.FromImage(expectedCaption))
            {
                expectedGraphics.Clear(BootstrapThemeManager.CurrentTheme.Colors.Surface);
                TextRenderer.DrawText(expectedGraphics, "Goods Receipts", shell.Workspace.Font, new Rectangle(0, 0, 400, receiptHeader.Height),
                    BootstrapThemeManager.CurrentTheme.Colors.MutedText, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }
            int Glyphs(Bitmap bitmap, Rectangle area)
            {
                var count = 0;
                for (var y = area.Top + 3; y < area.Bottom - 3; y++)
                    for (var x = area.Left + 3; x < area.Right - 3; x++)
                        if (bitmap.GetPixel(x, y).GetBrightness() > .5f) count++;
                return count;
            }
            var actualGlyphs = Glyphs(image, new Rectangle(shell.Workspace.Left + receiptHeader.Left, shell.Workspace.Top + receiptHeader.Top, receiptHeader.Width, receiptHeader.Height));
            var expectedGlyphs = Glyphs(expectedCaption, new Rectangle(Point.Empty, expectedCaption.Size));
            _output.WriteLine("CAPTION_GLYPH_PIXELS=actual:" + actualGlyphs + ";fullCaption:" + expectedGlyphs);
            if (actualGlyphs < expectedGlyphs * .95) failures.Add("Full Goods Receipts caption is not rendered: actual=" + actualGlyphs + ";expected=" + expectedGlyphs);
            for (var band = 0; band < 2; band++)
                for (var x = 0; x < shell.Workspace.GetTabRect(shell.Workspace.TabCount - 1).Right; x++)
                    if (image.GetPixel(shell.Workspace.Left + x, shell.Workspace.Top + band).GetBrightness() >= .5f)
                    { failures.Add("Bright native top band at x=" + x + ";y=" + band); break; }
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
            Assert.Empty(guard.Exceptions);
        }
        finally { BootstrapThemeManager.CurrentTheme = original; }
    });

    [Theory]
    [InlineData(BootstrapThemeMode.Light)]
    [InlineData(BootstrapThemeMode.Dark)]
    public void Workspace_tabs_keep_native_keyboard_overflow_and_focus_after_theme_switch(BootstrapThemeMode mode) => Run((token, guard) =>
    {
        var original = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
            using var shell = Shell();
            shell.ClientSize = new Size(944, 600);
            for (var index = 0; index < 12; index++)
                shell.OpenWorkspace("page-" + index, "Workspace " + index + " – Tồn kho", new Panel(), shell.WorkspaceSessionVersion);
            shell.Workspace.SelectedIndex = 0;
            shell.Show();
            NativeLookupTypingDriver.Activate(shell);
            shell.Workspace.Focus();
            for (var index = 1; index < 12; index++)
            {
                GoodsReceiptInlineEditorTests.Key(Keys.Right);
                Assert.Equal(index, shell.Workspace.SelectedIndex);
            }
            var page = shell.Workspace.SelectedTab;
            var handle = shell.Workspace.Handle;
            shell.ToggleTheme();
            System.Windows.Forms.Application.DoEvents();
            Assert.Same(page, shell.Workspace.SelectedTab);
            Assert.Equal(handle, shell.Workspace.Handle);
            Assert.True(shell.Workspace.Focused);
            GoodsReceiptInlineEditorTests.Key(Keys.Left);
            Assert.Equal(10, shell.Workspace.SelectedIndex);
            // Request the same focus cues enabled by native keyboard navigation.
            SendMessage(shell.Handle, 0x0127, new IntPtr(2 | (1 << 16)), IntPtr.Zero);
            SendMessage(shell.Workspace.Handle, 0x0128, new IntPtr(2 | (1 << 16)), IntPtr.Zero);
            Assert.True((bool)typeof(Control).GetProperty("ShowFocusCues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(shell.Workspace)!);
            var paintedHeader = Rectangle.Empty;
            shell.Workspace.DrawItem += (_, args) => { if (args.Index == shell.Workspace.SelectedIndex) paintedHeader = args.Bounds; };
            var capture = Capture(shell, "overflow-tabs", token);
            using var image = new Bitmap(capture);
            Assert.False(paintedHeader.IsEmpty);
            var selected = paintedHeader;
            var inset = BusinessCompactAppearance.Scale(shell.Workspace, BootstrapThemeManager.CurrentTheme.Metrics.FocusBorderWidth);
            var x = shell.Workspace.Left + selected.Left + selected.Width / 2;
            var expectedFocus = BootstrapThemeManager.CurrentTheme.Colors.Focus.ToArgb();
            Assert.Equal(expectedFocus, image.GetPixel(x, shell.Workspace.Top + selected.Top + inset).ToArgb());
            Assert.Equal(expectedFocus, image.GetPixel(x, shell.Workspace.Top + selected.Bottom - inset - 1).ToArgb());
            Assert.Empty(guard.Exceptions);
        }
        finally { BootstrapThemeManager.CurrentTheme = original; }
    });
    [Fact]
    public void Graphite_styles_application_owned_pages_added_after_shell_creation() => Run((token, guard) =>
    {
        var original = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(BootstrapThemeMode.Dark);
            using var shell = Shell();
            var content = new Panel { Dock = DockStyle.Fill };
            var label = new Label { Text = "Application-owned field", AutoSize = true };
            content.Controls.Add(label);
            shell.OpenWorkspace("late-page", "Late page", content, shell.WorkspaceSessionVersion);
            shell.Show();
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Body, content.BackColor);
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Text, label.ForeColor);
            var late = new Panel();
            content.Controls.Add(late);
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Body, late.BackColor);
            shell.ToggleTheme();
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Body, content.BackColor);
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Text, label.ForeColor);
            Assert.Empty(guard.Exceptions);
        }
        finally { BootstrapThemeManager.CurrentTheme = original; }
    });

    private string Capture(Form shell, string name, CancellationToken token, bool screen = false)
    {
        shell.Refresh();
        System.Windows.Forms.Application.DoEvents();
        var directory = Path.Combine(GuiSqlEnvironment.RepositoryRoot(), "TestResults/bcf-t03-shell-captures");
        Directory.CreateDirectory(directory);
        var scale = Environment.GetEnvironmentVariable("MYDMSVN_BCF_EXPECTED_SCALE") ?? "100";
        var path = Path.Combine(directory, name + "-native" + scale + "-" + BootstrapThemeManager.CurrentTheme.Mode + ".png");
        var start = _probe.Probe.Start(shell.Handle.ToInt64() + (screen ? " capture-shell-screen " : " capture-shell ") + Process.GetCurrentProcess().Id + " \"" + path + "\"");
        start.EnvironmentVariables["MYDMSVN_BCF_EXPECTED_SCALE"] = scale;
        var operation = GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(15), token);
        // The supervisor owns cancellation and cleanup; keep pumping until its result is final.
        while (!operation.IsCompleted) { System.Windows.Forms.Application.DoEvents(); Thread.Yield(); }
        var result = operation.GetAwaiter().GetResult();
        Assert.True(result.CleanupVerified);
        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.Contains(screen ? "SCREEN_CLIENT_CAPTURE=" : "PRINTWINDOW_CLIENT_CAPTURE=", result.StandardOutput);
        _output.WriteLine(result.StandardOutput);
        return path;
    }

    private sealed class ReferenceProducts : IProductApiClient
    {
        private static readonly string[] Names = { "A4 paper 80gsm", "Blue ballpoint pen", "Black ballpoint pen", "A4 lever arch file", "A5 notebook", "2B pencil", "30cm ruler", "White eraser", "Butterfly clips 32mm", "Transparent tape", "Sticky notes", "Clear document sleeve", "Stapler", "Staples", "Highlighter", "A4 presentation folder", "Office scissors", "Paper cutter" };
        private static readonly ProductDto[] Products = Names.Select((name, index) => new ProductDto(index + 1, "SP" + (index + 1).ToString("D3"), name, "pcs", index != 6 && index != 12 && index != 17)).ToArray();
        public Task<ApiResponse<PagedResult<ProductDto>>> ListAsync(CatalogListRequest request, CancellationToken token) =>
            Task.FromResult(ApiResponse<PagedResult<ProductDto>>.Success(new PagedResult<ProductDto>(Products, request.PageNumber, request.PageSize, Products.Length)));
        public Task<ApiResponse<ProductDto>> GetAsync(int id, CancellationToken token) => Task.FromResult(ApiResponse<ProductDto>.Success(Products.Single(item => item.Id == id)));
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<ProductDto>> CreateAsync(SaveProductRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<ProductDto>> UpdateAsync(int id, SaveProductRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool active, CancellationToken token) => throw new NotSupportedException();
    }

    private static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>()
        .SelectMany(control => new[] { control }.Concat(Descendants(control)));

    private static double Contrast(Color first, Color second)
    {
        double Linear(byte channel) { var value = channel / 255d; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        double Luminance(Color color) => .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        var a = Luminance(first); var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private void Run(Action<CancellationToken, WinFormsTestGuard> scenario) => StaTest.Run(token =>
    {
        using var guard = new WinFormsTestGuard();
        ExceptionDispatchInfo? failure = null;
        using var loop = new ApplicationContext();
        using var startup = new Control();
        startup.CreateControl();
        startup.BeginInvoke(new Action(() =>
        {
            try { scenario(token, guard); }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally { loop.ExitThread(); }
        }));
        System.Windows.Forms.Application.Run(loop);
        _output.WriteLine("T03 guarded STA complete; exceptions=" + guard.Exceptions.Count);
        Assert.Empty(guard.Exceptions);
        failure?.Throw();
    }, TimeSpan.FromSeconds(40));
}

public sealed class RefinementProbeFixture : IDisposable
{
    internal GuiProcessProbeFixture Probe { get; } = new GuiProcessProbeFixture(package: false, inline: true);
    public void Dispose() => Probe.Dispose();
}
