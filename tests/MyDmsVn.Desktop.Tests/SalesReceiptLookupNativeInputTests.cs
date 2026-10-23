using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

public sealed class SalesReceiptLookupNativeInputTests
{
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Numeric_native_grouping_preserves_model_and_external_same_display_paste_is_rejected(string theme)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("ProductLookup", theme, token, _output.WriteLine); host.AttachGuard(guard);
            host.Driver.Key(Keys.Escape);
            var position = new SourceGrid.Position(1, 3);
            host.Control.LineGrid.Focus();
            Assert.True(host.Control.LineGrid.Selection.Focus(position, true));
            new SourceGrid.CellContext(host.Control.LineGrid, position).StartEdit();
            var driver = new NativeLookupTypingDriver(host.Control.QuantityEditor);
            driver.SelectRange(0, driver.Text.Length);
            var candidate = string.Empty;
            foreach (var value in "1234")
            {
                driver.TypeCharacter(value);
                candidate += value;
                driver.AssertFocused();
                Assert.Equal(candidate, host.Control.QuantityEditor.PendingText);
                Assert.Equal(driver.Text.Length, driver.SelectionStart);
                Assert.Equal(0, driver.SelectionLength);
                Assert.Equal(1m, host.Control.Editor.Lines.Single().Quantity);
            }
            Assert.Equal("1,234", driver.Text);
            Assert.Equal(5, driver.SelectionStart);
            Assert.Equal(0, driver.SelectionLength);
            driver.SelectRange(0, driver.Text.Length);
            driver.Paste("1,234");
            Assert.Equal("1,234", driver.Text);
            Assert.Equal("1,234", host.Control.QuantityEditor.PendingText);
            host.Wait(host.Control.SaveAsync());
            Assert.Equal("1,234", driver.Text);
            Assert.Equal(1m, host.Control.Editor.Lines.Single().Quantity);
            Assert.Empty(host.Api.Updates);
            _output.WriteLine("UIN_T05_SALES_NUMERIC_NATIVE;theme=" + theme + ";windowDpi=" + host.Form.DeviceDpi + ";actualTestBitness=" + IntPtr.Size * 8);
        });
    }

    [Theory]
    [InlineData("Light", false)]
    [InlineData("Dark", false)]
    [InlineData("Light", true)]
    [InlineData("Dark", true)]
    public void Numeric_save_validation_resolves_after_native_Escape_or_corrected_Save(string theme, bool saveCorrection)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("ProductLookup", theme, token, _output.WriteLine); host.AttachGuard(guard);
            host.Driver.Key(Keys.Escape);
            var position = new SourceGrid.Position(1, 3); host.Control.LineGrid.Focus(); Assert.True(host.Control.LineGrid.Selection.Focus(position, true));
            new SourceGrid.CellContext(host.Control.LineGrid, position).StartEdit();
            var driver = new NativeLookupTypingDriver(host.Control.QuantityEditor); driver.SelectRange(0, driver.Text.Length);
            foreach (var value in "1.00001") driver.TypeCharacter(value);
            host.Wait(host.Control.SaveAsync());
            Assert.Equal("1.00001", driver.Text); Assert.Equal(7, driver.SelectionStart); Assert.Equal(0, driver.SelectionLength); driver.AssertFocused();
            var errors = (ErrorProvider)typeof(SalesReceiptControl).GetField("_errors", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(host.Control)!;
            var validation = errors.GetError(host.Control.LineGrid); Assert.NotEmpty(validation); Assert.Equal(validation, host.Control.OperationStatusLabel.Text);
            Assert.Empty(host.Api.Updates); Assert.Empty(host.Api.Posts); Assert.Equal(1m, host.Control.Editor.Lines.Single().Quantity);
            if (saveCorrection)
            {
                driver.SelectRange(0, driver.Text.Length); foreach (var value in "2.125") driver.TypeCharacter(value);
                Assert.Equal("2.125", driver.Text); Assert.Equal(5, driver.SelectionStart); Assert.Equal(0, driver.SelectionLength); driver.AssertFocused();
                host.Api.NextUpdate = new SalesReceiptDto(41, "SA0000000041", host.Control.Editor.SaleDate, 1, 2, 3, "Draft", "note", null, "AgIDBAUGBwg=",
                    new[] { new SalesReceiptLineDto(91, 1, 7, "box", 2.125m, 4m, false, 8.5m, null, null, "P7 - Product", true) }, 8.5m, null, null, "C1 - Customer", true, "W2 - Warehouse", true, "E3 - Employee", true);
                host.Wait(host.Control.SaveAsync());
                Assert.Equal(2.125m, host.Api.Updates.Single().Lines.Single().Qty); Assert.Equal(2.125m, host.Control.Editor.Lines.Single().Quantity);
            }
            else
            {
                driver.Key(Keys.Escape); host.Pump();
                Assert.Empty(host.Api.Updates); Assert.Equal(1m, host.Control.Editor.Lines.Single().Quantity);
            }
            Assert.Empty(errors.GetError(host.Control.LineGrid)); Assert.Equal("Ready", host.Control.OperationStatusLabel.Text);
            Assert.False(host.Control.Editor.IsDirty); Assert.Empty(host.Api.Posts);
            _output.WriteLine("SAL_NUMERIC_VALIDATION_RESOLVED;theme=" + theme + ";boundary=" + (saveCorrection ? "corrected-save" : "native-Escape") + ";windowDpi=" + host.Form.DeviceDpi + ";actualTestBitness=" + IntPtr.Size * 8);
        });
    }
    private readonly ITestOutputHelper _output;
    public SalesReceiptLookupNativeInputTests(ITestOutputHelper output) { _output = output; }
    public static IEnumerable<object[]> Targets => new[] { "CustomerLookup", "WarehouseLookup", "EmployeeLookup", "ProductLookup", "CustomerFilterLookup", "WarehouseFilterLookup", "EmployeeFilterLookup", "ProductFilterLookup" }
        .SelectMany(target => new[] { new object[] { target, "Light" }, new object[] { target, "Dark" } });

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Actual_list_denial_cancels_pending_native_product_query_and_late_results_cannot_reopen_detail(string theme)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("ProductLookup", theme, token, _output.WriteLine); host.AttachGuard(guard);
            host.Driver.SelectRange(0, host.Driver.Text.Length); host.Driver.TypeCharacter('g'); host.Until(() => host.Source.Count == 1);
            host.Api.ListHandler = t => Task.FromResult(ApiResponse<SalesReceiptListPage>.Failure(new ApiError(ApiStatusCode.Forbidden, "Test.Denied", "Denied")));
            var list = (SalesReceiptListViewModel)typeof(SalesReceiptControl).GetField("_list", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(host.Control)!;
            host.Wait(list.RefreshAsync(token)); Assert.True(host.Source.At(0).Token.IsCancellationRequested); Assert.True(host.Control.Editor.IsAccessDenied); Assert.Empty(host.Control.Editor.Lines);
            Assert.False(host.Control.ProductLookup.HasPendingText); host.Source.Complete(0); host.Pump();
            Assert.False(host.Control.TryShowReceiptDetail()); host.Wait(host.Control.SaveAsync()); host.Wait(host.Control.PostAsync()); Assert.Empty(host.Api.Updates); Assert.Empty(host.Api.Posts);
        });
    }
    [Theory]
    [InlineData("Light", "list")]
    [InlineData("Dark", "list")]
    [InlineData("Light", "save")]
    [InlineData("Dark", "save")]
    [InlineData("Light", "post")]
    [InlineData("Dark", "post")]
    public void Control_busy_rejects_header_lookup_search_commit_accessibility_and_programmatic_callbacks(string theme, string operation)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("CustomerLookup", theme, token, _output.WriteLine); host.AttachGuard(guard);
            var control = host.Control; if (operation == "save") control.Editor.Note = "owned save change";
            var date = control.Editor.SaleDate; var note = control.Editor.Note;
            var list = (SalesReceiptListViewModel)typeof(SalesReceiptControl).GetField("_list", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(control)!;
            var listCompletion = new TaskCompletionSource<ApiResponse<SalesReceiptListPage>>();
            var saveCompletion = new TaskCompletionSource<ApiResponse<SalesReceiptDto>>();
            var postCompletion = new TaskCompletionSource<ApiResponse<PostSalesReceiptResponse>>();
            Task pending;
            if (operation == "list") { host.Api.ListHandler = t => listCompletion.Task; pending = list.RefreshAsync(token); }
            else if (operation == "save") { host.Api.SaveBarrier = saveCompletion; pending = control.SaveAsync(); }
            else { host.Api.PostHandler = () => postCompletion.Task; pending = control.PostAsync(); }
            try
            {
                Assert.False(pending.IsCompleted); var names = new[] { "CustomerLookup", "WarehouseLookup", "EmployeeLookup" }; var counts = names.Select(host.Calls).ToArray();
                foreach (var name in names)
                {
                    var lookup = (BootstrapLookupBox)typeof(SalesReceiptControl).GetProperty(name)!.GetValue(control)!;
                    Assert.False(lookup.Enabled); lookup.Text = "blocked"; lookup.AccessibilityObject.Value = "blocked accessibility"; lookup.SelectValue(8);
                }
                Assert.False(control.SaleDateEditor.Enabled); Assert.False(control.NoteEditor.Enabled); control.SaleDateEditor.Value = date.AddDays(1); control.NoteEditor.Text = "blocked"; control.NoteEditor.AccessibilityObject.Value = "blocked accessibility";
                var gift = control.LineGrid.AccessibilityObject.GetChild(1)!.GetChild(5)!; Assert.Throws<InvalidOperationException>(() => gift.Value = "true");
                host.Wait(control.SaveAsync()); host.Wait(control.PostAsync()); host.Pump();
                Assert.Equal(counts, names.Select(host.Calls).ToArray()); Assert.Equal(1, control.Editor.CustomerId); Assert.Equal(2, control.Editor.WarehouseId); Assert.Equal(3, control.Editor.EmployeeId); Assert.Equal(date, control.Editor.SaleDate); Assert.Equal(note, control.Editor.Note); Assert.False(control.Editor.Lines.Single().IsGift);
                Assert.Equal(operation == "save" ? 1 : 0, host.Api.Updates.Count); Assert.Equal(operation == "post" ? 1 : 0, host.Api.Posts.Count);
            }
            finally
            {
                listCompletion.TrySetResult(ApiResponse<SalesReceiptListPage>.Failure(new ApiError(ApiStatusCode.InternalServerError, "Test.End", "End")));
                saveCompletion.TrySetResult(ApiResponse<SalesReceiptDto>.Success(SalesGuiApi.Receipt(41)));
                postCompletion.TrySetResult(ApiResponse<PostSalesReceiptResponse>.Failure(new ApiError(ApiStatusCode.BadRequest, "Test.Rejected", "Rejected")));
                host.Wait(pending);
            }
            Assert.True(control.NoteEditor.Enabled); control.NoteEditor.Text = "allowed"; Assert.Equal("allowed", control.Editor.Note);
        });
    }
    [Theory]
    [InlineData("Light", "1.00001")]
    [InlineData("Dark", "1.00001")]
    [InlineData("Light", "product")]
    [InlineData("Dark", "product")]
    public void Canceled_permission_reactivation_cannot_commit_or_scrub_retained_native_buffer(string theme, string raw)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            var permissions = new SalesPermissionGate();
            using var host = new SalesNativeHost("ProductLookup", theme, token, _output.WriteLine, permissions); host.AttachGuard(guard);
            NativeLookupTypingDriver driver;
            if (raw == "product") { driver = host.Driver; driver.SelectRange(0, driver.Text.Length); driver.TypeCharacter('g'); host.Until(() => host.Source.Count == 1); }
            else
            {
                host.Driver.Key(Keys.Escape); var position = new SourceGrid.Position(1, 3); host.Control.LineGrid.Focus(); Assert.True(host.Control.LineGrid.Selection.Focus(position, true)); new SourceGrid.CellContext(host.Control.LineGrid, position).StartEdit();
                driver = new NativeLookupTypingDriver(host.Control.QuantityEditor); driver.SelectRange(0, driver.Text.Length); foreach (var value in raw) driver.TypeCharacter(value);
            }
            driver.SelectRange(0, 1); var text = driver.Text; var line = host.Control.Editor.Lines.Single();
            permissions.Read = new TaskCompletionSource<ApiResponse<PermissionDecisionDto>>(); using var cancel = new CancellationTokenSource();
            var pending = host.Control.ActivateAsync(cancel.Token); Assert.False(pending.IsCompleted); cancel.Cancel();
            permissions.Read.SetResult(ApiResponse<PermissionDecisionDto>.Success(new PermissionDecisionDto("Sales.Receipts.Read", false))); host.Wait(pending);
            Assert.Equal(text, driver.Text); Assert.Equal(0, driver.SelectionStart); Assert.Equal(1, driver.SelectionLength); driver.AssertFocused();
            Assert.Same(line, host.Control.Editor.Lines.Single()); Assert.False(host.Control.Editor.IsAccessDenied); Assert.Equal(1m, line.Quantity); Assert.False(host.Control.Editor.IsDirty);
        });
    }
    [Theory]
    [InlineData("Light", "2.125")]
    [InlineData("Dark", "2.125")]
    [InlineData("Light", "1.00001")]
    [InlineData("Dark", "1.00001")]
    [InlineData("Light", "product")]
    [InlineData("Dark", "product")]
    public void Workspace_reactivation_preserves_unadmitted_native_buffer_range_focus_and_model(string theme, string raw)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("ProductLookup", theme, token, _output.WriteLine); host.AttachGuard(guard);
            NativeLookupTypingDriver driver;
            if (raw == "product")
            {
                driver = host.Driver; driver.SelectRange(0, driver.Text.Length); driver.TypeCharacter('g'); host.Until(() => host.Source.Count == 1);
                driver.SelectRange(0, 1);
            }
            else
            {
                host.Driver.Key(Keys.Escape); var position = new SourceGrid.Position(1, 3);
                host.Control.LineGrid.Focus(); Assert.True(host.Control.LineGrid.Selection.Focus(position, true)); new SourceGrid.CellContext(host.Control.LineGrid, position).StartEdit();
                driver = new NativeLookupTypingDriver(host.Control.QuantityEditor); driver.SelectRange(0, driver.Text.Length);
                foreach (var value in raw) driver.TypeCharacter(value); driver.SelectRange(1, 1);
            }
            var text = driver.Text; var caret = driver.SelectionStart; var range = driver.SelectionLength;
            var line = host.Control.Editor.Lines.Single(); var key = line.ClientKey; var grid = host.Control.LineGrid;
            host.ReleaseLookupHolds(); host.Wait(host.Control.ActivateAsync(token));
            Assert.Same(grid, host.Control.LineGrid); Assert.Same(line, host.Control.Editor.Lines.Single()); Assert.Equal(key, line.ClientKey);
            Assert.Equal(text, driver.Text); Assert.Equal(caret, driver.SelectionStart); Assert.Equal(range, driver.SelectionLength); driver.AssertFocused();
            Assert.Equal(1m, line.Quantity); Assert.Equal(7, line.ProductId); Assert.False(host.Control.Editor.IsDirty); Assert.Empty(host.Api.Updates); Assert.Empty(host.Api.Posts);
            if (raw == "product") { host.Source.Complete(0); host.Pump(); Assert.Equal(text, driver.Text); Assert.Equal(range, driver.SelectionLength); }
        });
    }
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Theme_switch_preserves_the_same_inline_lookup_session_native_text_and_caret(string theme)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("ProductLookup", theme, token, _output.WriteLine); host.AttachGuard(guard);
            host.Driver.SelectRange(0, host.Driver.Text.Length); host.Driver.TypeCharacter('g'); host.Until(() => host.Source.Count == 1); host.Observe("g", 1);
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(theme == "Light" ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light); host.Pump(); host.Observe("g", 1);
            host.Source.Complete(0); host.Until(() => host.ViewModel.Items.Any(option => option.Name == "Result1")); host.Observe("g", 1);
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(theme == "Light" ? BootstrapThemeMode.Light : BootstrapThemeMode.Dark); host.Pump(); host.Observe("g", 1);
            Assert.Equal(7, host.Control.Editor.Lines.Single().ProductId); Assert.False(host.Control.Editor.IsDirty);
        });
    }
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Native_Tab_ShiftTab_mouse_and_deactivation_preserve_row_session_and_manual_price(string theme)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("ProductLookup", theme, token, _output.WriteLine); host.AttachGuard(guard);
            host.ReleaseLookupHolds(); host.Driver.Key(Keys.Escape);
            var grid = host.Control.LineGrid; var quantity = new SourceGrid.Position(1, 3);
            grid.Focus(); grid.Selection.Focus(quantity, true); new SourceGrid.CellContext(grid, quantity).StartEdit();
            GoodsReceiptInlineEditorTests.Type("2.5"); GoodsReceiptInlineEditorTests.Key(Keys.Tab);
            Assert.Equal(2.5m, host.Control.Editor.Lines.Single().Quantity); Assert.Equal(new SourceGrid.Position(1, 4), grid.Selection.ActivePosition);
            new SourceGrid.CellContext(grid, new SourceGrid.Position(1, 4)).StartEdit(); GoodsReceiptInlineEditorTests.Type("6.25"); InlineReceiptTestDriver.ShiftTab();
            Assert.Equal(6.25m, host.Control.Editor.Lines.Single().UnitPrice); Assert.Equal(quantity, grid.Selection.ActivePosition);
            var gift = grid.PositionToRectangle(new SourceGrid.Position(1, 5)); InlineReceiptTestDriver.Click(grid, new System.Drawing.Point(gift.Left + gift.Width / 2, gift.Top + gift.Height / 2), host.Form);
            Assert.True(host.Control.Editor.Lines.Single().IsGift); Assert.Equal(6.25m, host.Control.Editor.Lines.Single().UnitPrice);
            grid.Focus(); Assert.True(grid.Selection.Focus(new SourceGrid.Position(1, 5), true)); Assert.True(grid.Focused);
            NativeLookupTypingDriver.KeyCharacter(grid, Keys.Space, ' '); Assert.False(host.Control.Editor.Lines.Single().IsGift);
            NativeLookupTypingDriver.KeyCharacter(grid, Keys.Space, ' '); Assert.True(host.Control.Editor.Lines.Single().IsGift);
            using (var other = new Form { Size = new System.Drawing.Size(240, 180) }) { other.Show(); NativeLookupTypingDriver.Activate(other); host.Pump(); }
            NativeLookupTypingDriver.Activate(host.Form); Assert.True(host.Control.Editor.Lines.Single().IsGift); Assert.Empty(host.Api.Posts); Assert.Empty(host.Api.Updates);
            _output.WriteLine("Native Tab/ShiftTab/mouse gift/deactivation; same row/manual price retained; theme=" + theme);
        });
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Shared_warehouse_notification_reloads_both_hosts_without_resolving_pending_native_query(string theme)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("WarehouseLookup", theme, token, _output.WriteLine); host.AttachGuard(guard);
            host.Driver.SelectRange(0, host.Driver.Text.Length); host.Driver.TypeCharacter('g'); host.Until(() => host.Source.Count == 1); host.Observe("g", 1);
            var filterBefore = host.Calls("WarehouseFilterLookup");
            host.Messenger.Send(new CatalogChangedMessage(CatalogKind.Warehouse, 2, CatalogChangeOperation.Updated));
            host.Until(() => host.Source.Count >= 2 && host.Calls("WarehouseFilterLookup") > filterBefore);
            Assert.True(host.Source.At(0).Token.IsCancellationRequested); host.Source.Complete(0); host.Pump(); host.Observe("g", 1);
            host.Source.Complete(1); host.Until(() => host.ViewModel.Items.Any(option => option.Name == "Result2")); host.Observe("g", 1);
            Assert.Equal(2, host.Control.Editor.WarehouseId); Assert.False(host.Control.Editor.IsDirty);
            NativeLookupTypingDriver.Click(host.Control.BackToReceiptsButton, new System.Drawing.Point(8, 8));
            Assert.False(host.Lookup.HasPendingText); Assert.True(host.Control.TryShowReceiptDetail()); Assert.Equal(2, host.Control.Editor.WarehouseId);
        });
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Posted_history_keeps_inactive_header_and_product_labels_while_optional_filters_remain_active_only(string theme)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("WarehouseLookup", theme, token, _output.WriteLine); host.AttachGuard(guard); host.ReleaseLookupHolds();
            host.Api.Current = SalesGuiApi.Receipt(41, true, false); host.Wait(host.Control.SelectReceiptAsync(41, token));
            foreach (var lookup in new[] { host.Control.CustomerLookup, host.Control.WarehouseLookup, host.Control.EmployeeLookup })
            { Assert.False(lookup.Enabled); Assert.False(((CatalogLookupOption)lookup.SelectedItem!).IsAvailableForNewSelection); Assert.False(string.IsNullOrWhiteSpace(lookup.Text)); }
            Assert.Equal("P7 - Product", host.Control.LineGrid[1, 1].DisplayText); Assert.Equal("box", host.Control.LineGrid[1, 2].DisplayText);
            foreach (var lookup in new[] { host.Control.CustomerFilterLookup, host.Control.WarehouseFilterLookup, host.Control.EmployeeFilterLookup, host.Control.ProductFilterLookup })
                Assert.All(((System.Collections.IEnumerable)lookup.DataSource!).Cast<CatalogLookupOption>(), option => Assert.True(option.IsAvailableForNewSelection));
            Assert.True(host.Control.TryShowReceiptList()); Assert.True(host.Control.TryShowReceiptDetail()); Assert.Equal("Posted", host.Control.Editor.Status);
        });
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Session_change_scrubs_control_and_rejects_late_lookup_publication(string theme)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("CustomerLookup", theme, token, _output.WriteLine); host.AttachGuard(guard);
            host.Driver.SelectRange(0, host.Driver.Text.Length); host.Driver.TypeCharacter('x'); host.Until(() => host.Source.Count == 1);
            host.Session.SignOut(); host.Source.ReleaseAll(); host.Wait(host.Control.LastOperation); host.Pump();
            Assert.Empty(host.Control.Editor.Lines); Assert.Equal(0, host.Control.Editor.SaleId); Assert.False(host.Control.SaveButton.Enabled); Assert.False(host.Control.PostButton.Enabled);
            Assert.False(host.Lookup.HasPendingText); Assert.Empty(((System.Collections.IEnumerable)host.Lookup.DataSource!).Cast<object>());
        });
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Native_per_character_rebinding_keeps_raw_caret_range_focus_and_committed_identity(string target, string theme)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost(target, theme, token, _output.WriteLine); host.AttachGuard(guard);
            guard.Attach(host.Lookup.ResultsGrid); var committed = host.Lookup.SelectedValue;
            var driver = host.Driver; var source = host.Source;
            driver.SelectRange(0, driver.Text.Length);
            driver.TypeCharacter('g'); host.Until(() => source.Count >= 1); host.Observe("g", 1);
            driver.TypeCharacter('u'); host.Until(() => source.Count >= 2); host.Observe("gu", 2);
            Assert.True(source.At(0).Token.IsCancellationRequested);
            source.Complete(0); host.Pump(); host.Observe("gu", 2);
            driver.TypeCharacter('i'); host.Until(() => source.Count >= 3); host.Observe("gui", 3);
            driver.SelectRange(1, 1); host.Observe("gui", 1, 1);
            source.Complete(2); host.Until(() => host.ViewModel.Items.Any(option => option.Name == "Result3")); host.Observe("gui", 1, 1);
            driver.TypeCharacter('a'); host.Until(() => source.Count >= 4); host.Observe("gai", 2);
            source.Complete(3); host.Until(() => host.ViewModel.Items.Any(option => option.Name == "Result4")); host.Observe("gai", 2);
            driver.SelectRange(1, 0); driver.TypeCharacter('x'); host.Until(() => source.Count >= 5); host.Observe("gxai", 2);
            source.Complete(4); host.Until(() => host.ViewModel.Items.Any(option => option.Name == "Result5")); host.Observe("gxai", 2);
            source.Complete(1); host.Pump(); host.Observe("gxai", 2);
            driver.SelectRange(0, 4); driver.Paste("paste"); host.Until(() => source.Count >= 6); host.Observe("paste", 5);
            source.Complete(5); host.Until(() => host.ViewModel.Items.Any(option => option.Name == "Result6")); host.Observe("paste", 5);
            Assert.Equal(committed, host.Lookup.SelectedValue); Assert.False(host.Control.Editor.IsDirty);
            Assert.Equal(1, host.Control.Editor.CustomerId); Assert.Equal(2, host.Control.Editor.WarehouseId); Assert.Equal(3, host.Control.Editor.EmployeeId);
            Assert.Equal(7, host.Control.Editor.Lines.Single().ProductId);
            _output.WriteLine("SAL hosted native WM_CHAR regression; target=" + target + ";theme=" + theme + ";actualTestBitness=" + IntPtr.Size * 8 + ";windowDpi=" + host.Form.DeviceDpi + ";client=" + host.Form.ClientSize + ";declaredScale=" + (Environment.GetEnvironmentVariable("MYDMSVN_SAL_EXPECTED_SCALE") ?? "none-hosted-only") + ";cleanupReportedAfterDispose=True");
        });
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Native_lookup_keyboard_cancel_selection_and_admitted_disposal_are_owned(string target, string theme)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost(target, theme, token, _output.WriteLine); host.AttachGuard(guard);
            guard.Attach(host.Lookup.ResultsGrid); host.Source.Hold = false;
            var driver = host.Driver; var old = host.Lookup.SelectedValue;
            driver.SelectRange(0, driver.Text.Length); driver.TypeCharacter('G');
            driver.Key(Keys.F4); Assert.True(host.Lookup.IsDropDownOpen);
            driver.Key(Keys.Down); driver.Key(Keys.Up); driver.Key(Keys.Escape);
            Assert.False(host.Lookup.IsDropDownOpen);
            if (target == "ProductLookup") { driver.Key(Keys.Escape); host.BeginProduct(); driver = new NativeLookupTypingDriver(host.Lookup); }
            driver.SelectRange(0, driver.Text.Length); driver.TypeCharacter('G'); driver.Key(Keys.F4); driver.Key(Keys.Down); driver.Key(Keys.Enter);
            host.Pump(); Assert.False(host.Lookup.HasPendingText);
            if (target == "ProductLookup") Assert.True(host.Control.Editor.Lines.Single().ProductId > 0);
            else if (target.EndsWith("FilterLookup", StringComparison.Ordinal)) Assert.Empty(host.Api.Posts);
            else Assert.True(host.Lookup.SelectedValue is int);
            host.Refocus(); driver = host.Driver;
            driver.SelectRange(0, driver.Text.Length); driver.TypeCharacter('z'); host.Source.Hold = true; driver.TypeCharacter('q');
            host.Until(() => host.Source.Count > 0); host.Control.Dispose(); host.Source.ReleaseAll(); host.Pump();
            Assert.Empty(host.Control.Editor.Lines); Assert.False(host.Control.Editor.CanEdit);
            _output.WriteLine("Native F4/Down/Up/Enter/Escape; pending request canceled/disposal fenced; target=" + target + ";theme=" + theme + ";actualTestBitness=" + IntPtr.Size * 8);
        });
    }
}

internal sealed class SalesNativeHost : IDisposable
{
    private readonly CancellationToken _token;
    private readonly BootstrapTheme _previousTheme = BootstrapThemeManager.CurrentTheme;
    private readonly SynchronizationContext? _previousContext = SynchronizationContext.Current;
    private readonly WindowsFormsSynchronizationContext _context;
    private readonly List<Task> _tasks = new List<Task>();
    private readonly Dictionary<string, SalesNativeSource> _sources = new Dictionary<string, SalesNativeSource>();
    private readonly Dictionary<string, CatalogLookupViewModel> _models = new Dictionary<string, CatalogLookupViewModel>();
    private readonly Action<string>? _output;
    internal readonly WeakReferenceMessenger Messenger = new WeakReferenceMessenger();
    private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
    internal readonly Form Form;
    internal readonly SalesReceiptControl Control;
    internal readonly SalesGuiApi Api = new SalesGuiApi();
    internal readonly SalesGuiSession Session = new SalesGuiSession();
    internal readonly BootstrapLookupBox Lookup;
    internal NativeLookupTypingDriver Driver;
    internal readonly string Target;
    internal SalesNativeSource Source => _sources[Target];
    internal CatalogLookupViewModel ViewModel => _models[Target];
    internal SalesNativeHost(string target, string theme, CancellationToken token, Action<string>? output = null, IPermissionApiClient? permissions = null)
    {
        _token = token; Target = target; _output = output;
        try
        {
        BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(theme == "Dark" ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
        Form = new Form { ClientSize = new System.Drawing.Size(1200, 760), StartPosition = FormStartPosition.CenterScreen };
        Form.Show(); _context = new WindowsFormsSynchronizationContext(); SynchronizationContext.SetSynchronizationContext(_context);
        var ui = new SynchronizationContextUiDispatcher(_context); var counters = new Dictionary<CatalogKind, int>();
        Control = new SalesReceiptControl(Api, Session, ui, SalesGuiApi.Calculator(), kind =>
        {
            counters.TryGetValue(kind, out var count); counters[kind] = count + 1;
            var name = kind + (count == 0 ? "Lookup" : "FilterLookup");
            var source = new SalesNativeSource(kind); _sources[name] = source;
            var vm = new CatalogLookupViewModel(source, Messenger, ui, new DesktopNotificationCenter(), new SystemAsyncDelay(), TimeSpan.Zero);
            _models[name] = vm; return vm;
        }, permissions ?? new SalesGuiPermissions(), new CatalogPresentationClient())
        { Dock = DockStyle.Fill };
        Form.Controls.Add(Control); Wait(Control.ActivateAsync(token)); Wait(Control.SelectReceiptAsync(41, token));
        if (target.EndsWith("FilterLookup", StringComparison.Ordinal)) Assert.True(Control.TryShowReceiptList());
        Lookup = (BootstrapLookupBox)Control.GetType().GetProperty(target)!.GetValue(Control)!;
        if (target == "ProductLookup") BeginProduct();
        NativeLookupTypingDriver.Activate(Form); Driver = new NativeLookupTypingDriver(Lookup);
        Source.Hold = true;
        var expected = Environment.GetEnvironmentVariable("MYDMSVN_SAL_EXPECTED_SCALE");
        if (expected != null)
        {
            using var probe = new GuiProcessProbeFixture(package: false, inline: true);
            var start = probe.Start(Form.Handle.ToInt64() + " measure-sales " + Process.GetCurrentProcess().Id);
            start.EnvironmentVariables["MYDMSVN_SAL_EXPECTED_SCALE"] = expected;
            var task = GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(15), token); Wait(task); var result = task.GetAwaiter().GetResult();
            Assert.True(result.CleanupVerified); Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
            _output?.Invoke("SAL_WORKFLOW_THEME=" + theme + ";EXPECTED_NATIVE_SCALE=" + expected + ";OBSERVER_PROCESS_CLEANUP=PASS;" + result.StandardOutput);
        }
        }
        catch (Exception original)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }
    internal int Calls(string target) => _sources[target].LookupCalls;
    internal void AttachGuard(WinFormsTestGuard guard)
    {
        foreach (var name in new[] { "CustomerLookup", "WarehouseLookup", "EmployeeLookup", "ProductLookup", "CustomerFilterLookup", "WarehouseFilterLookup", "EmployeeFilterLookup", "ProductFilterLookup" })
            guard.Attach(((BootstrapLookupBox)Control.GetType().GetProperty(name)!.GetValue(Control)!).ResultsGrid);
    }
    internal void ReleaseLookupHolds() { foreach (var source in _sources.Values) source.Hold = false; }
    internal void BeginProduct()
    {
        var p = new SourceGrid.Position(1, 1); Control.LineGrid.Focus(); Assert.True(Control.LineGrid.Selection.Focus(p, true)); new SourceGrid.CellContext(Control.LineGrid, p).StartEdit(); Pump();
    }
    internal void Refocus()
    {
        if (Target == "ProductLookup" && !Lookup.Visible) BeginProduct();
        Driver = new NativeLookupTypingDriver(Lookup);
    }
    internal void Observe(string text, int caret, int range = 0)
    {
        TrackLatest();
        Assert.Equal(text, Driver.Text); Assert.Equal(caret, Driver.SelectionStart); Assert.Equal(range, Driver.SelectionLength); Driver.AssertFocused();
    }
    private void TrackLatest() { if (Control != null && !_tasks.Contains(Control.LastOperation)) _tasks.Add(Control.LastOperation); }
    internal void Pump() { System.Windows.Forms.Application.DoEvents(); Assert.Equal(_thread, Thread.CurrentThread.ManagedThreadId); }
    internal void Until(Func<bool> predicate)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(5)) { _token.ThrowIfCancellationRequested(); Pump(); Thread.Yield(); }
        Assert.True(predicate(), "Sales native fixture boundary timed out."); Pump(); TrackLatest();
    }
    internal void Wait(Task task) { _tasks.Add(task); Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    public void Dispose()
    {
        var cleanupVerified = false;
        try
        {
            TrackLatest();
            if (Control != null && !Control.IsDisposed) Control.Dispose();
            if (Control == null) foreach (var model in _models.Values) model.Dispose();
            foreach (var source in _sources.Values) source.ReleaseAll();
            var clock = Stopwatch.StartNew();
            while ((_tasks.Any(task => !task.IsCompleted) || _sources.Values.Any(source => source.Pending.Any(task => !task.IsCompleted))) && clock.Elapsed < TimeSpan.FromSeconds(5)) Pump();
            Assert.All(_tasks, task => Assert.True(task.IsCompleted)); Assert.All(_sources.Values.SelectMany(source => source.Pending), task => Assert.True(task.IsCompleted));
            Pump();
            cleanupVerified = true;
        }
        catch (Exception error) { if (_tasks.Any(task => !task.IsCompleted)) GuiTestSafety.Poison(error); throw; }
        finally
        {
            Form?.Dispose(); if (Form != null) Assert.True(Form.IsDisposed);
            SynchronizationContext.SetSynchronizationContext(_previousContext); _context?.Dispose(); BootstrapThemeManager.CurrentTheme = _previousTheme;
        }
        if (cleanupVerified) _output?.Invoke("OWNED_SALES_NATIVE_CLEANUP=PASS;trackedOperations=" + _tasks.Count + ";heldRequests=" + _sources.Values.Sum(source => source.Count) + ";allTaskAndSourceCompletionsDrained=True;formDisposed=" + (Form?.IsDisposed ?? true));
    }
    internal static void Run(Action<CancellationToken, WinFormsTestGuard> action)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            ExceptionDispatchInfo? error = null;
            using var loop = new ApplicationContext(); using var startup = new Control(); startup.CreateControl();
            startup.BeginInvoke(new Action(() => { try { action(token, guard); } catch (Exception e) { error = ExceptionDispatchInfo.Capture(e); } finally { loop.ExitThread(); } }));
            System.Windows.Forms.Application.Run(loop); error?.Throw();
        }, TimeSpan.FromSeconds(35));
    }
}
internal sealed class SalesNativeSource : ICatalogLookupSource
{
    private int _calls;
    internal int LookupCalls => Volatile.Read(ref _calls);
    private readonly List<Entry> _pending = new List<Entry>();
    internal SalesNativeSource(CatalogKind kind) { Kind = kind; }
    public CatalogKind Kind { get; }
    internal bool Hold;
    internal bool SelectedIsActive = true;
    internal int Count { get { lock (_pending) return _pending.Count; } }
    internal Entry At(int index) { lock (_pending) return _pending[index]; }
    internal IEnumerable<Task> Pending { get { lock (_pending) return _pending.Select(entry => (Task)entry.Completion.Task).ToArray(); } }
    private CatalogLookupDto[] Options(int sequence = 0) => new[] { new CatalogLookupDto(7, "G7", "Guide", SelectedIsActive), new CatalogLookupDto(8, "G8", "Result" + sequence, true) };
    public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token)
    {
        Interlocked.Increment(ref _calls);
        if (!Hold) return Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(Options()));
        var entry = new Entry(token); lock (_pending) _pending.Add(entry); return entry.Completion.Task;
    }
    public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken token) => Task.FromResult(ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(id, "C" + id, "Historical label", SelectedIsActive)));
    internal void Complete(int index) => At(index).Completion.TrySetResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(Options(index + 1)));
    internal void ReleaseAll() { for (var i = 0; i < Count; i++) Complete(i); }
    internal sealed class Entry
    {
        internal Entry(CancellationToken token) { Token = token; }
        internal readonly CancellationToken Token;
        internal readonly TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>> Completion = new TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
