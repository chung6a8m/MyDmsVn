using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;
using Box = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapFormattedTextBox;

namespace MyDmsVn.Desktop.Tests;

public sealed class SalesReceiptControlTests
{
    [Fact]
    public void Saved_read_and_post_only_draft_posts_through_real_buffer_admission_without_write()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            host.Editor.SetAccess(true, false, true); Assert.False(host.Editor.CanEdit);
            Assert.True(host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapButton>("PostButton").Enabled);
            host.CallTask("PostAsync"); Assert.Single(host.Api.Posts); Assert.Empty(host.Api.Updates); Assert.True(host.Editor.RequiresPostOutcomeReload);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Pending_header_query_blocks_save_and_post_without_committing_text_to_identity()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var customer = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapLookupBox>("CustomerLookup"); customer.Focus(); customer.Text = "unresolved";
            host.CallTask("SaveAsync"); host.CallTask("PostAsync"); Assert.Empty(host.Api.Updates); Assert.Empty(host.Api.Posts);
            Assert.Equal(1, host.Editor.CustomerId); Assert.Equal("unresolved", customer.Text); Assert.True(customer.HasPendingText);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Unknown_create_retains_native_document_locks_and_requires_explicit_identified_review()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load(); Assert.True(host.Editor.NewDraft());
            host.Editor.CustomerId = 1; host.Editor.WarehouseId = 2; host.Editor.EmployeeId = 3; var row = host.Editor.AddLine()!; row.SetProduct(7, "Product", true, "box"); row.UnitPrice = 4;
            host.Api.UnknownCreate = true; host.CallTask("SaveAsync"); Assert.True(host.Editor.RequiresCreateOutcomeReview); Assert.Same(row, host.Editor.Lines.Single());
            host.CallTask("SaveAsync"); Assert.Single(host.Api.Creates); Assert.False(host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapButton>("NewButton").Enabled);
            Assert.True(host.Value<TextBox>("OutcomeSaleIdEditor").Visible); host.Value<TextBox>("OutcomeSaleIdEditor").Text = "41";
            host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapButton>("ReviewCreateButton").PerformClick(); host.Wait(host.Value<Task>("LastOperation"));
            Assert.False(host.Editor.RequiresCreateOutcomeReview); Assert.Equal(41, host.Editor.SaleId); Assert.True(host.Editor.CanEdit); Assert.Single(host.Api.Creates);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Changed_pending_page_size_disables_old_cursors_then_applies_first_page()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost();
            var next = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapButton>("NextPageButton"); Assert.True(next.Enabled);
            host.Value<NumericUpDown>("PageSizeEditor").Value = 1; Assert.False(next.Enabled); Assert.Contains("Pending", host.Value<Label>("AppliedCriteriaLabel").Text);
            host.CallTask("ApplyFiltersAsync"); Assert.Equal(1, host.Api.Lists.Last().PageSize); Assert.Null(host.Api.Lists.Last().Cursor); Assert.True(next.Enabled);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void WinForms_composition_resolves_sales_factory_and_shell_navigation_without_sql()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); var services = new ServiceCollection();
            services.AddServerApplication(); services.AddLocalDesktopAdapter(); services.AddMyDmsVnDesktopWinForms(new SynchronizationContext());
            using var provider = services.BuildServiceProvider();
            using var control = provider.GetRequiredService<ISalesReceiptControlFactory>().Create();
            using var shell = provider.GetRequiredService<FoundationShellForm>();
            Assert.IsType<SalesReceiptControl>(control); Assert.Contains(shell.Navigation.Items, item => Equals(item.Tag, "sales-receipts"));
            Assert.Null(shell.OpenSalesReceipts()); Assert.Empty(shell.Workspace.TabPages);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Compact_944_list_keeps_whole_bill_total_visible()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            host.Form.ClientSize = new System.Drawing.Size(944, 600); Assert.True((bool)host.Call("TryShowReceiptList")!); System.Windows.Forms.Application.DoEvents();
            var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("ReceiptGrid");
            var last = grid.PositionToRectangle(new SourceGrid.Position(1, 7));
            Assert.True(last.Right <= grid.ClientSize.Width, "Whole-bill total must be visible at compact944;last=" + last + ";client=" + grid.ClientSize);
        }, TimeSpan.FromSeconds(20));
    }
    [Theory]
    [InlineData("switch")]
    [InlineData("remove")]
    [InlineData("reorder")]
    [InlineData("document")]
    [InlineData("session")]
    [InlineData("dispose")]
    [InlineData("denied")]
    public void Deferred_product_metadata_respects_row_document_session_disposal_and_failure(string boundary)
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); var products = new SalesDeferredProducts(); using var host = new SalesControlHost(products); host.Load();
            var original = host.Editor.Lines.Single();
            try
            {
                host.SelectProduct(8); Assert.Single(products.Pending); var survivor = original;
                if (boundary == "switch") { host.SelectProduct(7); host.SelectProduct(8); products.CompleteAt(2, "latest"); }
                else if (boundary == "remove") { survivor = host.Editor.AddLine()!; survivor.SetProduct(8, "Other row", true, "other-unit"); host.Editor.RemoveLine(original); }
                else if (boundary == "reorder") { survivor = host.Editor.AddLine()!; survivor.SetProduct(8, "Other row", true, "other-unit"); host.Editor.MoveLine(original, 1); }
                else if (boundary == "document") Assert.True(host.Editor.NewDraft());
                else if (boundary == "session") host.Session.SignOut();
                else if (boundary == "dispose") host.Control.Dispose();
                else products.Pending[0].TrySetResult(ApiResponse<ProductDto>.Failure(new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Denied")));
                products.CompleteAll("old-unit"); host.Wait(host.Value<Task>("LastOperation"));
                if (boundary == "switch") Assert.Equal("latest", original.Unit);
                else if (boundary == "remove") Assert.Equal("other-unit", survivor.Unit);
                else if (boundary == "reorder") { Assert.Equal("old-unit", original.Unit); Assert.Equal("other-unit", survivor.Unit); Assert.Same(original, host.Editor.Lines[1]); }
                else Assert.Equal("", original.Unit);
                Assert.Equal(4m, original.UnitPrice); Assert.Empty(host.Api.Updates); Assert.Empty(host.Api.Posts);
            }
            finally { products.CompleteAll("cleanup"); host.Wait(host.Value<Task>("LastOperation")); }
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Late_catalog_unit_cannot_overwrite_saved_authoritative_snapshot()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); var products = new SalesDeferredProducts(); using var host = new SalesControlHost(products); host.Load();
            host.Begin(1); var lookup = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapLookupBox>("ProductLookup");
            Assert.True(lookup.SelectValue(8)); var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid");
            Assert.True(new SourceGrid.CellContext(grid, new SourceGrid.Position(1, 1)).EndEdit(false));
            Assert.Equal(8, host.Editor.Lines.Single().ProductId); Assert.Single(products.Pending);
            host.Api.NextUpdate = new SalesReceiptDto(41, "SA0000000041", host.Editor.SaleDate, 1, 2, 3, "Draft", "note", null, "AgIDBAUGBwg=",
                new[] { new SalesReceiptLineDto(91, 1, 8, "authoritative-unit", 1m, 4m, false, 4m, null, null, "P8 - Other", true) }, 4m, null, null, "C1 - Customer", true, "W2 - Warehouse", true, "E3 - Employee", true);
            host.CallTask("SaveAsync"); Assert.Equal("authoritative-unit", host.Editor.Lines.Single().Unit);
            products.CompleteAll("old-catalog-unit"); host.Wait(host.Value<Task>("LastOperation"));
            Assert.Equal("authoritative-unit", host.Editor.Lines.Single().Unit); Assert.False(host.Editor.IsDirty); Assert.Equal(4m, host.Editor.Lines.Single().UnitPrice);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Selecting_product_commits_current_readonly_unit_without_populating_selling_price()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            host.Begin(1); var lookup = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapLookupBox>("ProductLookup");
            Assert.True(lookup.SelectValue(8));
            var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid");
            Assert.True(new SourceGrid.CellContext(grid, new SourceGrid.Position(1, 1)).EndEdit(false));
            Assert.Equal("ream", host.Editor.Lines.Single().Unit); Assert.Equal(4m, host.Editor.Lines.Single().UnitPrice);
        }, TimeSpan.FromSeconds(20));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolved_numeric_validation_clears_footer_and_provider_without_clearing_operation_feedback(bool saveCorrection)
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var footer = host.Value<Label>("OperationStatusLabel");
            var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid");
            var errors = (ErrorProvider)host.Control.GetType().GetField("_errors", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host.Control)!;
            // An unrelated operation message must survive the inline validation lifecycle.
            const string operationMessage = "An unrelated operation needs review.";
            host.Control.GetType().GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host.Control, operationMessage);
            host.Begin(3); host.Value<Box>("QuantityEditor").Text = "1.00001";
            host.CallTask("SaveAsync");
            Assert.Empty(host.Api.Updates); Assert.Empty(host.Api.Posts); Assert.Equal(1m, host.Editor.Lines.Single().Quantity);
            var validation = errors.GetError(grid); Assert.NotEmpty(validation); Assert.Equal(validation, footer.Text);
            if (saveCorrection)
            {
                host.Value<Box>("QuantityEditor").Text = "2.125";
                host.Api.NextUpdate = new SalesReceiptDto(41, "SA0000000041", host.Editor.SaleDate, 1, 2, 3, "Draft", "note", null, "AgIDBAUGBwg=",
                    new[] { new SalesReceiptLineDto(91, 1, 7, "box", 2.125m, 4m, false, 8.5m, null, null, "P7 - Product", true) }, 8.5m, null, null, "C1 - Customer", true, "W2 - Warehouse", true, "E3 - Employee", true);
                host.CallTask("SaveAsync");
                Assert.Equal(2.125m, host.Api.Updates.Single().Lines.Single().Qty); Assert.Equal(2.125m, host.Editor.Lines.Single().Quantity); Assert.False(host.Editor.IsDirty);
            }
            else
            {
                Assert.True(new SourceGrid.CellContext(grid, new SourceGrid.Position(1, 3)).EndEdit(true));
                Assert.Empty(host.Api.Updates); Assert.Equal(1m, host.Editor.Lines.Single().Quantity); Assert.False(host.Editor.IsDirty);
            }
            Assert.Empty(errors.GetError(grid)); Assert.Equal(operationMessage, footer.Text); Assert.Empty(host.Api.Posts);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Save_commits_real_numeric_buffer_before_snapshot()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            host.Begin(3); host.Value<Box>("QuantityEditor").Text = "2.125";
            host.CallTask("SaveAsync"); Assert.Equal(2.125m, host.Api.Updates.Single().Lines.Single().Qty);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Post_commits_raw_cell_then_rejects_new_dirty_state_without_post()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            host.Begin(4); host.Value<Box>("UnitPriceEditor").Text = "9"; host.CallTask("PostAsync");
            Assert.Empty(host.Api.Posts); Assert.Equal(9m, host.Editor.Lines.Single().UnitPrice); Assert.True(host.Editor.IsDirty);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Back_rejects_invalid_raw_edit_without_native_focus_validation_or_loss()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            host.Begin(3); host.Value<Box>("QuantityEditor").Text = "1.00001";
            Assert.False((bool)host.Call("TryShowReceiptList")!);
            Assert.Equal("1.00001", host.Value<Box>("QuantityEditor").Text); Assert.Equal(1m, host.Editor.Lines.Single().Quantity);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Query_only_Back_cancels_pending_lookup_and_reuses_same_control_adapter()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var lookup = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapLookupBox>("CustomerLookup");
            lookup.Focus(); lookup.Text = "uncommitted query";
            Assert.True((bool)host.Call("TryShowReceiptList")!); Assert.False(lookup.HasPendingText);
            Assert.True((bool)host.Call("TryShowReceiptDetail")!); Assert.Equal(1, host.Editor.CustomerId); Assert.False(host.Editor.IsDirty);
        }, TimeSpan.FromSeconds(20));
    }
}
internal sealed class SalesControlHost : IDisposable
{
    internal readonly SalesGuiApi Api = new SalesGuiApi(); internal readonly SalesGuiSession Session = new SalesGuiSession();
    internal readonly Form Form; internal readonly Control Control; internal readonly SalesReceiptEditorViewModel Editor;
    internal SalesControlHost(IProductApiClient? products = null, IPermissionApiClient? permissions = null)
    {
        var type = typeof(GoodsReceiptControl).Assembly.GetType("MyDmsVn.Desktop.WinForms.SalesReceiptControl");
        Assert.True(type != null, "Sales native control and command buffer admission have not been implemented.");
        Control = (Control)Activator.CreateInstance(type!, new object[] { Api, Session, new ImmediateUiDispatcher(), SalesGuiApi.Calculator(), new Func<CatalogKind, CatalogLookupViewModel>(SalesGuiApi.Lookup), permissions ?? new SalesGuiPermissions(), products ?? new CatalogPresentationClient() })!;
        Editor = Value<SalesReceiptEditorViewModel>("Editor");
        Form = new Form { Size = new System.Drawing.Size(1200, 760), StartPosition = FormStartPosition.CenterScreen };
        Control.Dock = DockStyle.Fill; Form.Controls.Add(Control); Form.Show(); Form.Activate(); System.Windows.Forms.Application.DoEvents();
        CallTask("ActivateAsync", CancellationToken.None);
    }
    internal T Value<T>(string name) => (T)Control.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Control)!;
    internal object? Call(string name, params object[] args) => Control.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Control, args);
    internal void CallTask(string name, params object[] args)
    {
        Wait((Task)Call(name, args)!);
    }
    internal void Wait(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(8);
        while (!task.IsCompleted && DateTime.UtcNow < until) { System.Windows.Forms.Application.DoEvents(); Thread.Yield(); }
        Assert.True(task.IsCompleted, "Owned operation did not drain."); task.GetAwaiter().GetResult();
    }
    internal void Load() => CallTask("SelectReceiptAsync", 41L, CancellationToken.None);
    internal void SelectProduct(int id)
    {
        Begin(1); Assert.True(Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapLookupBox>("ProductLookup").SelectValue(id));
        var grid = Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid");
        Assert.True(new SourceGrid.CellContext(grid, new SourceGrid.Position(1, 1)).EndEdit(false));
    }
    internal void Begin(int column)
    {
        var grid = Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid");
        grid.Focus(); var position = new SourceGrid.Position(1, column); Assert.True(grid.Selection.Focus(position, true));
        new SourceGrid.CellContext(grid, position).StartEdit(); System.Windows.Forms.Application.DoEvents();
    }
    public void Dispose() { Form.Dispose(); }
}
internal sealed class SalesGuiPermissions : IPermissionApiClient
{
    public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(string key, CancellationToken token) => Task.FromResult(ApiResponse<PermissionDecisionDto>.Success(new PermissionDecisionDto(key, true)));
}

internal sealed class SalesDeferredProducts : IProductApiClient
{
    internal readonly System.Collections.Generic.List<TaskCompletionSource<ApiResponse<ProductDto>>> Pending = new System.Collections.Generic.List<TaskCompletionSource<ApiResponse<ProductDto>>>();
    internal readonly System.Collections.Generic.List<int> Ids = new System.Collections.Generic.List<int>();
    public Task<ApiResponse<ProductDto>> GetAsync(int id, CancellationToken token) { Ids.Add(id); var completion = new TaskCompletionSource<ApiResponse<ProductDto>>(); Pending.Add(completion); return completion.Task; }
    internal void CompleteAt(int index, string unit) => Pending[index].TrySetResult(ApiResponse<ProductDto>.Success(new ProductDto(Ids[index], "P" + Ids[index], "Other", unit, true)));
    internal void CompleteAll(string unit) { for (var i = 0; i < Pending.Count; i++) Pending[i].TrySetResult(ApiResponse<ProductDto>.Success(new ProductDto(Ids[i], "P" + Ids[i], "Other", unit, true))); }
    public Task<ApiResponse<PagedResult<ProductDto>>> ListAsync(CatalogListRequest r, CancellationToken t) => throw new NotSupportedException();
    public Task<ApiResponse<ProductDto>> CreateAsync(SaveProductRequest r, CancellationToken t) => throw new NotSupportedException();
    public Task<ApiResponse<ProductDto>> UpdateAsync(int id, SaveProductRequest r, CancellationToken t) => throw new NotSupportedException();
    public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool active, CancellationToken t) => throw new NotSupportedException();
    public Task<ApiResponse<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest r, CancellationToken t) => throw new NotSupportedException();
}
