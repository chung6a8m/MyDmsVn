using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed partial class BusinessCompactRefinementTests
{
    [Theory]
    [InlineData(BootstrapThemeMode.Light, 1200)]
    [InlineData(BootstrapThemeMode.Dark, 1200)]
    [InlineData(BootstrapThemeMode.Light, 1680)]
    [InlineData(BootstrapThemeMode.Dark, 1680)]
    public void Product_catalog_composition_matches_reference_in_both_themes(BootstrapThemeMode mode, int width) => Run((token, guard) =>
    {
        var original = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(mode);
            var source = new CatalogPresentationClient();
            using var vm = new ProductCatalogViewModel(source, new WeakReferenceMessenger(), new ImmediateUiDispatcher(), new DesktopNotificationCenter(), new SystemAsyncDelay(), TimeSpan.Zero);
            using var catalog = new CatalogControl(vm);
            using var shell = Shell();
            shell.ClientSize = new Size(width, width == 1680 ? 900 : 760);
            shell.SetCurrentUser("BCF Operator");
            shell.OpenWorkspace("products", "Products", catalog, shell.WorkspaceSessionVersion);
            shell.Navigation.SelectedItem = shell.Navigation.Items.Single(item => Equals(item.Tag, CatalogKind.Product));
            shell.Show();
            Assert.Equal(width, shell.ClientSize.Width);
            NativeLookupTypingDriver.Activate(shell);
            WaitCatalog(catalog.ActivateAsync(token));
            WaitCatalog(vm.SelectAsync(1, token));
            Assert.True(catalog.Grid.Selection.IsSelectedRow(1), "The inspected row must remain visibly selected while an editor has focus.");
            var title = Named(catalog, "CatalogTitle");
            var heading = Named(catalog, "CatalogInspectorHeading");
            Assert.Equal("Products", title.Text);
            Assert.Equal("30 products", Named(catalog, "CatalogResultCount").Text);
            Assert.Equal("Product details", heading.Text);
            Assert.Equal("SP001", Named(catalog, "CatalogSelectedCode").Text);
            Assert.Equal("Active", Named(catalog, "CatalogStatus").Text);
            Assert.Equal("Add product", catalog.NewButton.Text);
            Assert.Equal("Code", catalog.Grid[0, 0].Value);
            Assert.Equal("Name", catalog.Grid[0, 1].Value);
            Assert.Equal("Unit", catalog.Grid[0, 2].Value);
            Assert.Equal("ream", catalog.Grid[1, 2].Value);
            var toolbarY = catalog.PointToClient(title.PointToScreen(Point.Empty)).Y;
            Assert.InRange(catalog.PointToClient(catalog.SearchBox.PointToScreen(Point.Empty)).Y, toolbarY - 12, toolbarY + 24);
            Assert.True(catalog.Grid.PointToScreen(Point.Empty).X < heading.PointToScreen(Point.Empty).X);
            Assert.InRange(catalog.Grid.Rows[1].Height, 28, 38);
            Assert.Contains("1–25 / 30", Named(catalog, "CatalogResultRange").Text);
            catalog.EditorControls["name"].Focus();
            var directory = Path.Combine(GuiSqlEnvironment.RepositoryRoot(), "TestResults/bcf-t04-captures/native" + (Environment.GetEnvironmentVariable("MYDMSVN_BCF_EXPECTED_SCALE") ?? "100"));
            Directory.CreateDirectory(directory);
            _output.WriteLine("T04 matched fixture: logical=" + shell.ClientSize + "; catalog=" + catalog.Bounds + "; toolbar=" + title.Parent!.Parent!.Bounds + "; grid=" + catalog.Grid.Bounds + "; inspector scroll owner=" + Named(catalog, "CatalogInspector").Bounds + "; save=" + catalog.SaveButton.RectangleToScreen(catalog.SaveButton.ClientRectangle) + "; errors=" + Named(catalog, "CatalogError").Bounds + "; selected=1; page=1; total=30; grid scroll=" + catalog.Grid.CustomScrollPosition);
            File.Copy(Capture(shell, "t04-products-" + width, token), Path.Combine(directory, "products-" + width + "-" + mode + ".png"), true);
            if (width == 1200) File.Copy(Capture(shell, "t04-products-physical-1200", token, screen: true), Path.Combine(directory, "products-physical-1200-" + mode + ".png"), true);
            // Reflow must preserve the actual editor instance, native focus, query and selection.
            var editor = catalog.EditorControls["name"];
            editor.Text = "Giấy A4 — bản sửa chưa lưu";
            var nativeEditor = Assert.Single(editor.Controls.OfType<TextBox>());
            nativeEditor.Select(4, 2);
            var handle = editor.Handle;
            shell.ClientSize = new Size(944, 600);
            System.Windows.Forms.Application.DoEvents();
            shell.ToggleTheme();
            System.Windows.Forms.Application.DoEvents();
            Assert.Same(editor, catalog.EditorControls["name"]);
            Assert.Equal(handle, editor.Handle);
            Assert.Equal("Giấy A4 — bản sửa chưa lưu", editor.Text);
            Assert.Equal(4, nativeEditor.SelectionStart);
            Assert.Equal(2, nativeEditor.SelectionLength);
            Assert.True(editor.ContainsFocus);
            Assert.Equal(1, vm.SelectedId);
            _output.WriteLine("T04 logical client=" + shell.ClientSize + "; catalog=" + catalog.Bounds + "; grid=" + catalog.Grid.Bounds + "; inspector=" + heading.Parent!.Bounds + "; query=" + vm.Search + "; page=" + vm.PageNumber + "; selected=" + vm.SelectedId + "; scroll=" + catalog.Grid.CustomScrollPosition);
            var split = Assert.Single(Descendants(catalog).OfType<SplitContainer>());
            Assert.Equal(Orientation.Horizontal, split.Orientation);
            var inspector = Assert.IsAssignableFrom<ScrollableControl>(Named(catalog, "CatalogInspector"));
            inspector.ScrollControlIntoView(catalog.DeactivateButton);
            System.Windows.Forms.Application.DoEvents();
            var commandBounds = inspector.RectangleToClient(catalog.DeactivateButton.RectangleToScreen(catalog.DeactivateButton.ClientRectangle));
            _output.WriteLine("T04 inspector client=" + inspector.ClientRectangle + "; deactivate=" + commandBounds + "; scroll=" + inspector.AutoScrollPosition + "; extent=" + inspector.DisplayRectangle);
            Assert.True(inspector.ClientRectangle.Contains(commandBounds), "The entire Deactivate action must be reachable after scrolling, not merely a clipped strip.");
            File.Copy(Capture(shell, "t04-products-constrained", token), Path.Combine(directory, "products-944-" + mode + "-opposite-theme.png"), true);
        }
        finally { BootstrapThemeManager.CurrentTheme = original; }
    });

    [Fact]
    public void Catalog_toolbar_preserves_search_paging_and_editor_commands() => Run((token, guard) =>
    {
        var messenger = new WeakReferenceMessenger();
        var changes = new List<CatalogChangedMessage>();
        messenger.Register<CatalogChangedMessage>(changes, (_, m) => changes.Add(m));
        var source = new CatalogPresentationClient();
        using var vm = new ProductCatalogViewModel(source, messenger, new ImmediateUiDispatcher(), new DesktopNotificationCenter(), new SystemAsyncDelay(), TimeSpan.Zero);
        using var catalog = new CatalogControl(vm);
        using var host = new Form { ClientSize = new Size(1200, 760), Controls = { catalog } };
        host.Show();
        WaitCatalog(catalog.ActivateAsync(token));
        Assert.Contains("Select a row", Named(catalog, "CatalogSelectedCode").Text);
        catalog.NextPageButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.Equal(2, vm.PageNumber);
        Assert.Equal("26–30 / 30", Named(catalog, "CatalogResultRange").Text);
        catalog.SearchBox.Text = "SP027"; WaitCatalog(catalog.LastOperation);
        Assert.Equal(1, vm.PageNumber);
        Assert.Equal(1, vm.TotalCount);
        Assert.Equal("1 product", Named(catalog, "CatalogResultCount").Text);
        WaitCatalog(vm.SelectAsync(27, token));
        Assert.Equal("Inactive", Named(catalog, "CatalogStatus").Text);
        catalog.ActivateButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.Equal("Active", Named(catalog, "CatalogStatus").Text);
        catalog.DeactivateButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.Equal("Inactive", Named(catalog, "CatalogStatus").Text);
        // An off-page editor keeps metadata from its actual loaded entity.
        catalog.SearchBox.Text = "SP001"; WaitCatalog(catalog.LastOperation);
        Assert.False(catalog.Grid.Selection.IsSelectedRow(1), "A filtered-out inspected ID must not transfer its highlight to a different row at the same index.");
        Assert.Equal("SP027", Named(catalog, "CatalogSelectedCode").Text);
        Assert.Equal("Inactive", Named(catalog, "CatalogStatus").Text);
        catalog.ActivateButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.Equal("Active", Named(catalog, "CatalogStatus").Text);
        catalog.NewButton.PerformClick();
        Assert.Equal("New product", Named(catalog, "CatalogInspectorHeading").Text);
        catalog.EditorControls["code"].Text = "NEW";
        catalog.EditorControls["name"].Text = "Long Unicode — sản phẩm mới";
        catalog.EditorControls["unit"].Text = "box";
        catalog.SaveButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.Equal("NEW", Named(catalog, "CatalogSelectedCode").Text);
        Assert.Contains(changes, m => m.Operation == CatalogChangeOperation.Created);
        catalog.CancelButton.PerformClick();
        Assert.Equal("Long Unicode — sản phẩm mới", catalog.EditorControls["name"].Text); // Existing cancel stops work; it does not discard edits.
        catalog.SearchBox.Text = "no-match"; WaitCatalog(catalog.LastOperation);
        Assert.Equal("0 products", Named(catalog, "CatalogResultCount").Text);
        Assert.Equal("No results", Named(catalog, "CatalogResultsState").Text);
        source.FailList = true;
        catalog.RefreshButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.Contains("Synthetic catalog failure", Named(catalog, "CatalogError").Text);
        Assert.Empty(guard.Exceptions);
    });

    [Theory]
    [InlineData(CatalogKind.Product, "code,name,unit")]
    [InlineData(CatalogKind.Warehouse, "code,name,address")]
    [InlineData(CatalogKind.Employee, "code,name,phone,userId")]
    [InlineData(CatalogKind.Customer, "code,name,address,phone,taxCode")]
    public void Shared_catalog_layout_keeps_every_field_validation_and_activation(CatalogKind kind, string fields) => Run((token, guard) =>
    {
        var source = new CatalogPresentationClient();
        var messenger = new WeakReferenceMessenger();
        var dispatcher = new ImmediateUiDispatcher();
        var notifications = new DesktopNotificationCenter();
        var delay = new SystemAsyncDelay();
        using var catalog = kind == CatalogKind.Product ? new CatalogControl(new ProductCatalogViewModel(source, messenger, dispatcher, notifications, delay, TimeSpan.Zero))
            : kind == CatalogKind.Warehouse ? new CatalogControl(new WarehouseCatalogViewModel(source, messenger, dispatcher, notifications, delay, TimeSpan.Zero))
            : kind == CatalogKind.Employee ? new CatalogControl(new EmployeeCatalogViewModel(source, messenger, dispatcher, notifications, delay, TimeSpan.Zero))
            : new CatalogControl(new CustomerCatalogViewModel(source, messenger, dispatcher, notifications, delay, TimeSpan.Zero));
        using var host = new Form { ClientSize = new Size(944, 600), Controls = { catalog } };
        host.Show();
        WaitCatalog(catalog.ActivateAsync(token));
        Assert.Equal(fields.Split(','), catalog.EditorControls.Keys.ToArray());
        Assert.True(catalog.Grid.Selection.Focus(new SourceGrid.Position(1, 1), true));
        var invoke = catalog.Grid.GetType().GetMethod("OnKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        invoke!.Invoke(catalog.Grid, new object[] { new KeyEventArgs(Keys.Enter) });
        WaitCatalog(catalog.LastOperation);
        Assert.Equal("Active", Named(catalog, "CatalogStatus").Text);
        catalog.DeactivateButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.Equal("Inactive", Named(catalog, "CatalogStatus").Text);
        catalog.ActivateButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.Equal("Active", Named(catalog, "CatalogStatus").Text);
        catalog.NewButton.PerformClick();
        foreach (var field in catalog.EditorControls) field.Value.Text = field.Key == "userId" ? "12" : "Unicode — thử nghiệm";
        source.FailSave = true;
        catalog.SaveButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.Equal("Code is required.", catalog.GetFieldError("code"));
        Assert.Contains("Correct the fields", Named(catalog, "CatalogError").Text);
        Assert.All(catalog.EditorControls.Values, c => Assert.True(c.Enabled));
        var inspector = Assert.IsAssignableFrom<ScrollableControl>(Named(catalog, "CatalogInspector"));
        foreach (var action in catalog.EditorControls.Values.Cast<Control>().Concat(new Control[] { catalog.SaveButton, catalog.CancelButton, catalog.ActivateButton, catalog.DeactivateButton }))
        {
            inspector.ScrollControlIntoView(action);
            System.Windows.Forms.Application.DoEvents();
            Assert.True(inspector.ClientRectangle.Contains(inspector.RectangleToClient(action.RectangleToScreen(action.ClientRectangle))), "Every shared catalog field/action must be fully reachable: " + action.AccessibleName + action.Text);
        }
    });

    private static Control Named(Control root, string name) => Assert.Single(Descendants(root), c => c.Name == name);

    [Fact]
    public void Catalog_open_row_preserves_keyboard_position_and_raw_edit_across_pages() => Run((token, guard) =>
    {
        var source = new CatalogPresentationClient();
        using var vm = new ProductCatalogViewModel(source, new WeakReferenceMessenger(), new ImmediateUiDispatcher(), new DesktopNotificationCenter(), new SystemAsyncDelay(), TimeSpan.Zero);
        using var catalog = new CatalogControl(vm);
        using var host = new Form { ClientSize = new Size(1200, 760), Controls = { catalog } };
        host.Show();
        WaitCatalog(catalog.ActivateAsync(token));
        Assert.True(catalog.Grid.Selection.Focus(new SourceGrid.Position(10, 0), true));
        var invoke = catalog.Grid.GetType().GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        invoke!.Invoke(catalog.Grid, new object[] { new KeyEventArgs(Keys.Enter) });
        WaitCatalog(catalog.LastOperation);
        Assert.Equal(10, vm.SelectedId);
        Assert.Equal(10, catalog.Grid.Selection.ActivePosition.Row);
        catalog.Grid.ProcessSpecialGridKey(new KeyEventArgs(Keys.Down));
        Assert.Equal(11, catalog.Grid.Selection.ActivePosition.Row);
        var editor = catalog.EditorControls["name"];
        editor.Focus();
        editor.Text = "Pending raw edit";
        var native = Assert.Single(editor.Controls.OfType<TextBox>());
        native.Select(2, 3);
        catalog.RefreshButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.Equal("Pending raw edit", editor.Text);
        // Explicit refresh/paging keeps the existing busy policy: native fields are disabled
        // during loading. Focus/range retention is asserted separately for layout/theme only.
        catalog.NextPageButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.False(catalog.Grid.Selection.IsSelectedRow(10));
        Assert.Equal(10, vm.SelectedId);
        catalog.PreviousPageButton.PerformClick(); WaitCatalog(catalog.LastOperation);
        Assert.True(catalog.Grid.Selection.IsSelectedRow(10));
        Assert.Equal(10, catalog.Grid.Selection.ActivePosition.Row);
        Assert.Equal("Pending raw edit", editor.Text);
    });

    [Theory]
    [InlineData(BootstrapThemeMode.Light)]
    [InlineData(BootstrapThemeMode.Dark)]
    public void Catalog_loading_and_error_states_keep_cancel_and_raw_editor_values(BootstrapThemeMode mode) => Run((token, guard) =>
    {
        var original = BootstrapThemeManager.CurrentTheme;
        var source = new CatalogPresentationClient();
        Task? refresh = null;
        try
        {
            BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(mode);
            using var vm = new ProductCatalogViewModel(source, new WeakReferenceMessenger(), new ImmediateUiDispatcher(), new DesktopNotificationCenter(), new SystemAsyncDelay(), TimeSpan.Zero);
            using var catalog = new CatalogControl(vm);
            using var host = new Form { ClientSize = new Size(1200, 760), Controls = { catalog } };
            host.Show();
            WaitCatalog(catalog.ActivateAsync(token));
            WaitCatalog(vm.SelectAsync(1, token));
            catalog.EditorControls["name"].Text = "Giá trị chưa lưu — retained";
            source.ListBarrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            refresh = catalog.ActivateAsync(token);
            Assert.False(refresh.IsCompleted);
            Assert.Equal("Loading results…", Named(catalog, "CatalogResultsState").Text);
            Assert.False(catalog.SaveButton.Enabled);
            Assert.True(catalog.CancelButton.Enabled);
            catalog.CancelButton.PerformClick();
            source.ListBarrier.TrySetResult(true);
            WaitCatalog(refresh);
            Assert.Equal("Giá trị chưa lưu — retained", catalog.EditorControls["name"].Text);
            source.ListBarrier = null;
            source.FailList = true;
            catalog.RefreshButton.PerformClick(); WaitCatalog(catalog.LastOperation);
            Assert.Equal("No results", Named(catalog, "CatalogResultsState").Text);
            Assert.Contains("Synthetic catalog failure", Named(catalog, "CatalogError").Text);
            Assert.Equal("Giá trị chưa lưu — retained", catalog.EditorControls["name"].Text);
            Assert.Equal("SP001", Named(catalog, "CatalogSelectedCode").Text);
            Assert.Equal(1, vm.SelectedId);
        }
        finally
        {
            source.ListBarrier?.TrySetResult(true);
            if (refresh != null) WaitCatalog(refresh);
            BootstrapThemeManager.CurrentTheme = original;
        }
    });
    private static void WaitCatalog(Task operation)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!operation.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(5)) { System.Windows.Forms.Application.DoEvents(); Thread.Yield(); }
        Assert.True(operation.IsCompleted, "Catalog operation must drain within five seconds.");
        operation.GetAwaiter().GetResult();
        System.Windows.Forms.Application.DoEvents();
    }
}
