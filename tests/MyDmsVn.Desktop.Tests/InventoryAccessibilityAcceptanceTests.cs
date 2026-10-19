using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
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

public sealed class InventoryAccessibilityAcceptanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stock_filters_expose_distinct_meaningful_names_to_accessibility_clients(bool card)
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new StockFixture(card);
            using var host = Show(fixture.Control);
            Named(fixture.Warehouse, "Warehouse ID");
            Named(fixture.Product, "Product ID");
            if (fixture.Card != null)
            {
                Named(fixture.Card.FromUtcFilter, "From UTC");
                Named(fixture.Card.ToUtcFilter, "To UTC");
                Assert.Equal(4, new[]
                {
                    fixture.Warehouse.AccessibleName, fixture.Product.AccessibleName,
                    fixture.Card.FromUtcFilter.AccessibleName, fixture.Card.ToUtcFilter.AccessibleName,
                }.Distinct(StringComparer.Ordinal).Count());
            }
            else Named(fixture.Balance!.SearchTextBox, "Search stock balances");
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false, BootstrapThemeMode.Light)]
    [InlineData(false, BootstrapThemeMode.Dark)]
    [InlineData(true, BootstrapThemeMode.Light)]
    [InlineData(true, BootstrapThemeMode.Dark)]
    public void Stock_filter_tab_focus_and_readonly_rows_survive_theme_render_and_layout(
        bool card, BootstrapThemeMode mode)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            var original = BootstrapThemeManager.CurrentTheme;
            try
            {
                using var fixture = new StockFixture(card);
                guard.Attach(fixture.Grid);
                using var host = Show(fixture.Control);
                fixture.Activate(token);
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                host.PerformLayout();
                System.Windows.Forms.Application.DoEvents();

                TabBetween(fixture.Warehouse, fixture.Product);
                if (fixture.Card != null)
                {
                    TabBetween(fixture.Product, fixture.Card.FromUtcFilter);
                    TabBetween(fixture.Card.FromUtcFilter, fixture.Card.ToUtcFilter);
                }
                else TabBetween(fixture.Balance!.SearchTextBox, fixture.Warehouse);

                Assert.True(fixture.Grid.ReadOnly);
                Assert.False(fixture.Grid.AllowUserToAddRows);
                Assert.False(fixture.Grid.AllowUserToDeleteRows);
                Assert.Single(fixture.Grid.Rows);
                Assert.Equal("Ready", fixture.Status.Text);
                AssertVisibleLayout(fixture.Warehouse);
                AssertVisibleLayout(fixture.Product);
                AssertVisibleLayout(fixture.Grid);
                Render(fixture.Control);
                Assert.Equal(mode, BootstrapThemeManager.CurrentTheme.Mode);
                Assert.Empty(guard.Exceptions);
            }
            finally { BootstrapThemeManager.CurrentTheme = original; }
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(BootstrapThemeMode.Light)]
    [InlineData(BootstrapThemeMode.Dark)]
    public void Receipt_supports_enter_selection_filter_and_editor_tab_focus_and_theme_render(BootstrapThemeMode mode)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            var original = BootstrapThemeManager.CurrentTheme;
            try
            {
                using var fixture = new ReceiptFixture("Draft");
                using var host = Show(fixture.Control);
                fixture.Control.ActivateAsync(token).GetAwaiter().GetResult();
                Assert.True(fixture.Control.ReceiptGrid.Selection.Focus(new SourceGrid.Position(1, 0), true));
                var key = RaiseEnter(fixture.Control.ReceiptGrid);
                fixture.Control.LastOperation.GetAwaiter().GetResult();

                Assert.True(key.Handled);
                Assert.True(key.SuppressKeyPress);
                Assert.Equal(41, fixture.Editor.Id);
                Assert.Equal(2, fixture.Control.LineGrid.RowsCount);
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                host.PerformLayout();
                System.Windows.Forms.Application.DoEvents();

                TabBetween(fixture.Control.SearchBox, fixture.Control.WarehouseFilterLookup);
                TabBetween(fixture.Control.WarehouseFilterLookup, fixture.Control.StatusFilter);
                TabBetween(fixture.Control.WarehouseLookup, fixture.Control.EmployeeLookup);
                TabBetween(fixture.Control.EmployeeLookup, fixture.Control.NoteEditor);
                TabBetween(fixture.Control.QuantityEditor, fixture.Control.UnitCostEditor);
                Named(fixture.Control.ReceiptDateEditor, "Receipt date");
                Named(fixture.Control.QuantityEditor, "Quantity");
                Named(fixture.Control.UnitCostEditor, "Unit cost");
                AssertVisibleLayout(fixture.Control.ReceiptGrid);
                AssertVisibleLayout(fixture.Control.LineGrid);
                AssertVisibleLayout(fixture.Control.SaveButton);
                Render(fixture.Control);
                Assert.Equal("Draft", fixture.Control.StatusLabel.Text);
                Assert.True(fixture.Control.SaveButton.Enabled);
                Assert.Empty(guard.Exceptions);
            }
            finally { BootstrapThemeManager.CurrentTheme = original; }
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(BootstrapThemeMode.Light)]
    [InlineData(BootstrapThemeMode.Dark)]
    public void Posted_receipt_disables_mutation_and_excludes_editors_from_focus_in_each_theme(BootstrapThemeMode mode)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            var original = BootstrapThemeManager.CurrentTheme;
            try
            {
                using var fixture = new ReceiptFixture("Posted");
                using var host = Show(fixture.Control);
                fixture.Control.ActivateAsync(token).GetAwaiter().GetResult();
                fixture.Control.SelectReceiptAsync(41, token).GetAwaiter().GetResult();
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                System.Windows.Forms.Application.DoEvents();

                foreach (var editor in new Control[]
                {
                    fixture.Control.ReceiptDateEditor, fixture.Control.WarehouseLookup,
                    fixture.Control.EmployeeLookup, fixture.Control.NoteEditor,
                    fixture.Control.ProductLookup, fixture.Control.QuantityEditor,
                    fixture.Control.UnitCostEditor, fixture.Control.SaveButton,
                    fixture.Control.PostButton, fixture.Control.AddLineButton, fixture.Control.RemoveLineButton,
                })
                {
                    Assert.False(editor.Enabled);
                    Assert.False(editor.CanFocus);
                }
                TabBetween(fixture.Control.SearchBox, fixture.Control.WarehouseFilterLookup);
                Assert.Equal("Posted", fixture.Control.StatusLabel.Text);
                Assert.Equal("AQIDBAUGBwg=", fixture.Control.VersionLabel.Text);
                Assert.Equal("10.00", fixture.Control.TotalLabel.Text);
                Render(fixture.Control);
                Assert.Empty(guard.Exceptions);
            }
            finally { BootstrapThemeManager.CurrentTheme = original; }
        }, TimeSpan.FromSeconds(10));
    }

    private static void Named(Control control, string expected)
    {
        Assert.Equal(expected, control.AccessibleName);
        Assert.Equal(expected, control.AccessibilityObject.Name);
    }

    private static Form Show(Control control)
    {
        var form = new Form { ClientSize = new Size(1200, 800), ShowInTaskbar = false, AutoScaleMode = AutoScaleMode.Dpi };
        form.Controls.Add(control);
        form.Show();
        System.Windows.Forms.Application.DoEvents();
        Assert.True(control.IsHandleCreated);
        return form;
    }

    private static void TabBetween(Control first, Control second)
    {
        Assert.Same(first.Parent, second.Parent);
        Assert.True(first.TabStop);
        Assert.True(second.TabStop);
        first.Select();
        Assert.True(first.ContainsFocus, "First control must receive actual native focus.");
        Assert.NotNull(first.Parent);
        // Native tab selection uses the siblings' TabIndex/TabStop and skips static labels.
        Assert.True(first.Parent!.SelectNextControl(first, true, true, false, false));
        Assert.True(second.ContainsFocus, "Forward tab must focus the next logical input.");
        Assert.True(second.Parent!.SelectNextControl(second, false, true, false, false));
        Assert.True(first.ContainsFocus, "Backward tab must return to the previous logical input.");
    }

    private static void AssertVisibleLayout(Control control)
    {
        Assert.True(control.Visible);
        Assert.True(control.Width > 0);
        Assert.True(control.Height > 0);
        Assert.NotNull(control.Parent);
        Assert.True(control.Parent!.ClientRectangle.IntersectsWith(control.Bounds));
    }

    private static void Render(Control control)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, control.ClientRectangle);
        Assert.True(control.IsHandleCreated);
    }

    private static KeyEventArgs RaiseEnter(Control grid)
    {
        var method = grid.GetType().GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var key = new KeyEventArgs(Keys.Enter);
        method!.Invoke(grid, new object[] { key });
        return key;
    }

    private sealed class StockFixture : IDisposable
    {
        public StockFixture(bool card)
        {
            var notifications = new DesktopNotificationCenter();
            if (card) Card = new StockCardControl(new StockCardViewModel(new Api(), new ImmediateUiDispatcher(), notifications));
            else Balance = new StockBalanceControl(new StockBalanceViewModel(new Api(), new ImmediateUiDispatcher(), notifications, new Delay()));
        }
        public StockCardControl? Card { get; }
        public StockBalanceControl? Balance { get; }
        public Control Control => (Control?)Card ?? Balance!;
        public NumericUpDown Warehouse => Card?.WarehouseFilter ?? Balance!.WarehouseFilter;
        public NumericUpDown Product => Card?.ProductFilter ?? Balance!.ProductFilter;
        public DataGridView Grid => Card?.StockCardGrid ?? Balance!.BalanceGrid;
        public Label Status => Card?.OperationStatusLabel ?? Balance!.OperationStatusLabel;
        public void Activate(CancellationToken token)
        {
            if (Card != null)
            {
                Warehouse.Value = 2;
                Product.Value = 7;
                Card.LastOperation.GetAwaiter().GetResult();
            }
            else Balance!.ActivateAsync(token).GetAwaiter().GetResult();
        }
        public void Dispose() => Control.Dispose();
    }

    private sealed class ReceiptFixture : IDisposable
    {
        public ReceiptFixture(string status)
        {
            var api = new Api { ReceiptStatus = status };
            var dispatcher = new ImmediateUiDispatcher();
            var notifications = new DesktopNotificationCenter();
            var messenger = new WeakReferenceMessenger();
            var delay = new Delay();
            var list = new GoodsReceiptListViewModel(api, dispatcher, notifications, delay);
            Editor = new GoodsReceiptEditorViewModel(api,
                new CatalogLookupViewModel(new Lookup(CatalogKind.Warehouse, 2), messenger, dispatcher, notifications, delay),
                new CatalogLookupViewModel(new Lookup(CatalogKind.Employee, 5), messenger, dispatcher, notifications, delay),
                new GoodsReceiptProductOptionsViewModel(new Lookup(CatalogKind.Product, 7), messenger, dispatcher, notifications, delay),
                dispatcher, notifications);
            Control = new GoodsReceiptControl(list, Editor);
        }
        public GoodsReceiptEditorViewModel Editor { get; }
        public GoodsReceiptControl Control { get; }
        public void Dispose() => Control.Dispose();
    }

    private sealed class Delay : IAsyncDelay
    {
        public Task DelayAsync(TimeSpan interval, CancellationToken token) => Task.CompletedTask;
    }

    private sealed class Lookup : ICatalogLookupSource
    {
        private readonly int _id;
        public Lookup(CatalogKind kind, int id) { Kind = kind; _id = id; }
        public CatalogKind Kind { get; }
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) =>
            Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(new[] { new CatalogLookupDto(_id, "C" + _id, "Choice", true) }));
        public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken token) =>
            Task.FromResult(ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(id, "C" + id, "Choice", true)));
    }

    private sealed class Api : IInventoryApiClient, IGoodsReceiptApiClient
    {
        public string ReceiptStatus { get; set; } = "Draft";
        private GoodsReceiptDto Receipt => new GoodsReceiptDto(41, "GR0000000041", new DateTime(2026, 10, 19),
            2, 5, ReceiptStatus, "received", ReceiptStatus == "Posted" ? new DateTime(2026, 10, 19, 1, 2, 3, DateTimeKind.Utc) : (DateTime?)null,
            "AQIDBAUGBwg=", new[] { new GoodsReceiptLineDto(91, 1, 7, 2.5m, 4m, "P7 - Product", true) },
            "W2 - Warehouse", true, "E5 - Employee", true);
        public Task<ApiResponse<PagedResult<StockBalanceRow>>> GetStockBalancesAsync(StockBalanceQuery request, CancellationToken token) =>
            Task.FromResult(ApiResponse<PagedResult<StockBalanceRow>>.Success(new PagedResult<StockBalanceRow>(
                new[] { new StockBalanceRow(2, "W2", "Warehouse", true, 7, "P7", "Product", true, 2.5m) }, 1, 25, 1)));
        public Task<ApiResponse<PagedResult<StockCardRow>>> GetStockCardAsync(StockCardQuery request, CancellationToken token) =>
            Task.FromResult(ApiResponse<PagedResult<StockCardRow>>.Success(new PagedResult<StockCardRow>(
                new[] { new StockCardRow(91, 2, "W2", "Warehouse", true, 7, "P7", "Product", true,
                    new DateTime(2026, 10, 19, 1, 2, 3, DateTimeKind.Utc), "GoodsReceipt", 41, "GR0000000041", 2.5m) }, 1, 25, 1)));
        public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(GoodsReceiptListRequest request, CancellationToken token) =>
            Task.FromResult(ApiResponse<PagedResult<GoodsReceiptDto>>.Success(new PagedResult<GoodsReceiptDto>(new[] { Receipt }, 1, 25, 1)));
        public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken token) =>
            Task.FromResult(ApiResponse<GoodsReceiptDto>.Success(Receipt));
        public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(UpdateGoodsReceiptDraftRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(PostGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
    }
}
