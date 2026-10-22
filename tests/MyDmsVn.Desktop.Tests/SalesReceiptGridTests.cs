using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Desktop.WinForms;
using Xunit;
using Grid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.Tests;

public sealed class SalesReceiptGridTests
{
    [Fact]
    public void Compact_944_grid_keeps_all_seven_columns_visible()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesGridHost();
            host.Form.ClientSize = new System.Drawing.Size(944, 600); System.Windows.Forms.Application.DoEvents();
            var last = host.Grid.PositionToRectangle(new SourceGrid.Position(1, 6));
            Assert.True(last.Right <= host.Grid.ClientSize.Width, "Line amount must be visible in the compact 944 client without horizontal navigation; last=" + last + ";client=" + host.Grid.ClientSize);
        }, TimeSpan.FromSeconds(15));
    }
    [Fact]
    public void Seven_columns_keep_unit_amount_and_ordinal_read_only()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesGridHost();
            Assert.Equal(new[] { "STT", "Product", "Unit", "Quantity", "Unit price", "Gift", "Line amount" },
                Enumerable.Range(0, host.Grid.ColumnsCount).Select(c => host.Grid[0, c].DisplayText));
            foreach (var c in new[] { 0, 2, 6 }) Assert.Null(host.Grid[1, c].Editor);
            Assert.Null(host.Editor.TotalCost); Assert.Null(host.Editor.GrossProfit);
        }, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Duplicate_id_zero_rows_keep_independent_cells_and_selection_after_removal()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesGridHost();
            var a = host.Editor.Lines[0]; var b = host.Editor.AddLine()!; b.ProductId = a.ProductId; b.UnitPrice = 9;
            Assert.NotEqual(a.ClientKey, b.ClientKey); host.Set(b, 3, "3.1250");
            Assert.Equal(3.125m, b.Quantity); Assert.Equal(1m, a.Quantity);
            Assert.True(host.Editor.MoveLine(b, 0)); Assert.Equal(3.125m, host.Grid[1, 3].Value);
            Assert.True(host.Editor.RemoveLine(a)); Assert.Equal(2, host.Grid.RowsCount);
            Assert.Equal(9m, host.Grid[1, 4].Value); Assert.Same(b, host.Editor.Lines.Single());
        }, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Gift_accessibility_retains_price_and_rechecks_write_guard()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesGridHost();
            var row = host.Editor.Lines[0]; row.UnitPrice = 12.5m;
            host.Set(row, 5, "true"); Assert.True(row.IsGift); Assert.Equal(12.5m, row.UnitPrice); Assert.Equal(0m, row.LineAmount);
            host.Editor.SetAccess(true, false, true);
            Assert.Throws<TargetInvocationException>(() => host.Set(row, 5, "false"));
            Assert.True(row.IsGift); Assert.Equal(12.5m, row.UnitPrice);
        }, TimeSpan.FromSeconds(15));
    }

    [Theory]
    [InlineData("1.00001")]
    [InlineData("0.000000000000000000000000000000000001")]
    [InlineData("1e2")]
    [InlineData("1,000")]
    public void Invalid_raw_quantity_is_retained_and_cannot_change_model(string raw)
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesGridHost();
            Assert.Throws<TargetInvocationException>(() => host.Set(host.Editor.Lines[0], 3, raw));
            Assert.Equal(1m, host.Editor.Lines[0].Quantity);
            Assert.Equal(raw, ((Control)host.Adapter.GetType().GetProperty("QuantityControl", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host.Adapter)!).Text);
        }, TimeSpan.FromSeconds(15));
    }
}

internal sealed class SalesGridHost : IDisposable
{
    internal readonly SalesGuiSession Session = new SalesGuiSession();
    internal readonly SalesGuiApi Api = new SalesGuiApi();
    internal readonly SalesReceiptEditorViewModel Editor;
    internal readonly CatalogLookupViewModel Products;
    internal readonly Form Form;
    internal readonly Grid Grid;
    internal readonly object Adapter;
    internal SalesGridHost()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        Editor = new SalesReceiptEditorViewModel(Api, Session, new ImmediateUiDispatcher(), SalesGuiApi.Calculator(), new SalesGuiBuffers());
        Editor.SetAccess(true, true, true); Editor.CustomerId = 1; Editor.WarehouseId = 2; Editor.EmployeeId = 3;
        var row = Editor.AddLine()!; row.SetProduct(7, "P7 - Product", true, "box");
        Products = SalesGuiApi.Lookup(CatalogKind.Product);
        Products.ActivateAsync(CancellationToken.None).GetAwaiter().GetResult();
        var assembly = typeof(GoodsReceiptControl).Assembly;
        var type = assembly.GetType("MyDmsVn.Desktop.WinForms.SalesReceiptLinesGrid");
        Assert.True(type != null, "Sales seven-column native grid has not been implemented.");
        Grid = (Grid)Activator.CreateInstance(type!, true)!;
        var adapterType = assembly.GetType("MyDmsVn.Desktop.WinForms.SalesReceiptLinesGridAdapter")!;
        Adapter = Activator.CreateInstance(adapterType, BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { Grid, Editor, Products, new Func<bool>(() => true) }, CultureInfo.InvariantCulture)!;
        Form = new Form { Size = new System.Drawing.Size(1000, 600), StartPosition = FormStartPosition.CenterScreen };
        Grid.Dock = DockStyle.Fill; Form.Controls.Add(Grid); Form.Show(); Form.Activate(); System.Windows.Forms.Application.DoEvents();
    }
    internal void Set(SalesReceiptLineViewModel row, int column, string text) => Adapter.GetType().GetMethod("SetAccessibleValue", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(Adapter, new object[] { Adapter.GetType().GetProperty("AccessibleGeneration", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Adapter)!, row, column, text });
    public void Dispose() { (Adapter as IDisposable)?.Dispose(); Form.Dispose(); Products.Dispose(); Editor.Dispose(); }
}

internal sealed class SalesGuiSession : IDesktopSession
{
    public event EventHandler? SessionChanged;
    public bool IsAuthenticated { get; private set; } = true;
    public DesktopSessionState State => IsAuthenticated ? DesktopSessionState.Authenticated : DesktopSessionState.Unauthenticated;
    public CurrentUserDto? CurrentUser => IsAuthenticated ? new CurrentUserDto(1, "test", "Test") : null;
    public long Version { get; private set; } = 1;
    public void SignOut() { IsAuthenticated = false; Version++; SessionChanged?.Invoke(this, EventArgs.Empty); }
}
internal sealed class SalesGuiBuffers : ISalesCommandBufferAdmission
{
    public Task<bool> TryCommitPendingEditsAsync(SalesCommandIntent intent, CancellationToken token) => Task.FromResult(true);
}
internal sealed class SalesGuiApi : ISalesReceiptApiClient
{
    public ISalesReceiptApiClient ForSession(long version) => this;
    internal bool Posted;
    internal bool UnknownCreate;
    internal readonly List<SalesReceiptListRequest> Lists = new List<SalesReceiptListRequest>();
    internal SalesReceiptDto? NextUpdate;
    internal SalesReceiptDto? Current = null;
    internal TaskCompletionSource<ApiResponse<SalesReceiptDto>>? SaveBarrier;
    public readonly List<SaveSalesReceiptRequest> Creates = new List<SaveSalesReceiptRequest>();
    public readonly List<UpdateSalesReceiptDraftRequest> Updates = new List<UpdateSalesReceiptDraftRequest>();
    public readonly List<PostSalesReceiptRequest> Posts = new List<PostSalesReceiptRequest>();
    internal Func<CancellationToken, Task<ApiResponse<SalesReceiptListPage>>>? ListHandler;
    internal Func<long, Task<ApiResponse<SalesReceiptDto>>>? GetHandler;
    internal Func<Task<ApiResponse<PostSalesReceiptResponse>>>? PostHandler;
    internal readonly List<long> Gets = new List<long>();
    public Task<ApiResponse<SalesReceiptListPage>> ListAsync(SalesReceiptListRequest r, CancellationToken t) { Lists.Add(r); if (ListHandler != null) return ListHandler(t); return Task.FromResult(ApiResponse<SalesReceiptListPage>.Success(new SalesReceiptListPage(new[] { new SalesReceiptListRow(41, "SA0000000041", new DateTime(2026, 10, 22), 1, "C1 - Customer", 2, "W2 - Warehouse", 3, "E3 - Employee", "Draft", Current?.Lines.Count ?? 1, Current?.TotalAmount ?? 4m) }, "next", null))); }
    public Task<ApiResponse<SalesReceiptDto>> GetAsync(long id, CancellationToken t) { Gets.Add(id); if (GetHandler != null) return GetHandler(id); return Task.FromResult(ApiResponse<SalesReceiptDto>.Success(Current ?? Receipt(id, Posted))); }
    public Task<ApiResponse<SalesReceiptDto>> CreateDraftAsync(SaveSalesReceiptRequest r, CancellationToken t) { Creates.Add(r); if (UnknownCreate) return Task.FromResult(ApiResponse<SalesReceiptDto>.Failure(new ApiError(ApiStatusCode.InternalServerError, "Test.UnknownCreate", "Review outcome"))); return Task.FromResult(ApiResponse<SalesReceiptDto>.Success(Receipt(41))); }
    public Task<ApiResponse<SalesReceiptDto>> UpdateDraftAsync(UpdateSalesReceiptDraftRequest r, CancellationToken t) { Updates.Add(r); if (SaveBarrier != null) return SaveBarrier.Task; return Task.FromResult(ApiResponse<SalesReceiptDto>.Success(NextUpdate ?? Receipt(r.SaleId))); }
    public Task<ApiResponse<PostSalesReceiptResponse>> PostAsync(PostSalesReceiptRequest r, CancellationToken t) { Posts.Add(r); if (PostHandler != null) return PostHandler(); return Task.FromResult(ApiResponse<PostSalesReceiptResponse>.Failure(new ApiError(ApiStatusCode.InternalServerError, "Test.Unknown", "Unknown outcome"))); }
    public static SalesReceiptDto Receipt(long id, bool posted = false, bool active = true) => new SalesReceiptDto(id, "SA0000000041", new DateTime(2026, 10, 22), 1, 2, 3, posted ? "Posted" : "Draft", "note", posted ? new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc) : (DateTime?)null, "AQIDBAUGBwg=",
        new[] { new SalesReceiptLineDto(91, 1, 7, "box", 1m, 4m, false, 4m, posted ? 2m : (decimal?)null, posted ? 2m : (decimal?)null, "P7 - Product", active) }, 4m, posted ? 2m : (decimal?)null, posted ? 2m : (decimal?)null, "C1 - Customer", active, "W2 - Warehouse", active, "E3 - Employee", active);
    public static ISalesReceiptPreviewCalculator Calculator() { using var p = new ServiceCollection().AddLocalDesktopAdapter().BuildServiceProvider(); return p.GetRequiredService<ISalesReceiptPreviewCalculator>(); }
    public static CatalogLookupViewModel Lookup(CatalogKind kind) => new CatalogLookupViewModel(new Source(kind), new WeakReferenceMessenger(), new ImmediateUiDispatcher(), new DesktopNotificationCenter(), new SystemAsyncDelay(), TimeSpan.Zero);
    private sealed class Source : ICatalogLookupSource
    {
        public Source(CatalogKind kind) { Kind = kind; }
        public CatalogKind Kind { get; }
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(new[] { new CatalogLookupDto(7, "P7", "Product", true), new CatalogLookupDto(8, "P8", "Other", true) }));
        public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken token) => Task.FromResult(ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(id, "P" + id, "Product", true)));
    }
}
