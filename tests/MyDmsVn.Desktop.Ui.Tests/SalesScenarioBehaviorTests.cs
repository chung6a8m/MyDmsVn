using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SalesScenarioBehaviorTests
{
    [Fact]
    public void S01_duplicate_product_lines_keep_ids_and_save_the_submitted_edits()
    {
        ScenarioControlHost.Run(SalesScenarioId.S01Draft, (host, guard) =>
        {
            var editor = host.Control.Editor;
            Assert.Equal(new long[] { 101, 102 }, editor.Lines.Select(line => line.SaleLineId));
            Assert.All(editor.Lines, line => Assert.Equal(11, line.ProductId));
            editor.Lines[0].Quantity = 3m;
            editor.Note = "Edited in UI Lab";
            Assert.True(editor.IsDirty);

            host.Wait(host.Control.SaveAsync());

            var update = Assert.IsType<UpdateSalesReceiptDraftRequest>(Assert.Single(host.Data.Journal.Entries,
                entry => entry.Operation == "Update.request").Request);
            Assert.Equal("Edited in UI Lab", update.Note);
            Assert.Equal(new long[] { 101, 102 }, update.Lines.Select(line => line.SaleLineId));
            Assert.Equal(new decimal[] { 3m, 1m }, update.Lines.Select(line => line.Qty));
            Assert.Equal(new bool[] { false, true }, update.Lines.Select(line => line.IsGift));
            Assert.Equal(new long[] { 101, 102 }, host.Data.SalesApi.CurrentSale!.Lines.Select(line => line.SaleLineId));
            Assert.Equal(36m, host.Data.SalesApi.CurrentSale.TotalAmount);
            Assert.False(editor.IsDirty);
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S02_invalid_native_quantity_keeps_raw_text_and_prevents_save_or_post()
    {
        ScenarioControlHost.Run(SalesScenarioId.S02Validation, (host, guard) =>
        {
            var editor = host.Control.Editor;
            editor.SetCustomer(1, "Demo Customer", true);
            editor.SetWarehouse(2, "Demo Warehouse", true);
            editor.SetEmployee(3, "Demo Employee", true);
            var line = editor.AddLine()!;
            line.SetProduct(11, "Demo Product", true, "pcs");
            line.Quantity = 1m;
            line.UnitPrice = 12m;
            var grid = host.Control.LineGrid;
            grid.Focus();
            var position = new SourceGrid.Position(1, 3);
            Assert.True(grid.Selection.Focus(position, true));
            new SourceGrid.CellContext(grid, position).StartEdit();
            var native = new NativeLookupTypingDriver(host.Control.QuantityEditor);
            native.SelectRange(0, native.Text.Length);
            foreach (var character in "1.00001") native.TypeCharacter(character);
            host.Wait(host.Control.SaveAsync());
            host.Wait(host.Control.PostAsync());
            Assert.Equal("1.00001", native.Text);
            Assert.Equal("1.00001", host.Control.QuantityEditor.PendingText);
            Assert.Equal(1m, line.Quantity);
            Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Create.request" || entry.Operation == "Post.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S02_server_field_error_attaches_to_submitted_line_after_local_input_is_valid()
    {
        ScenarioControlHost.Run(SalesScenarioId.S02Validation, (host, guard) =>
        {
            var editor = host.Control.Editor;
            editor.SetCustomer(1, "Demo Customer", true);
            editor.SetWarehouse(2, "Demo Warehouse", true);
            editor.SetEmployee(3, "Demo Employee", true);
            var line = editor.AddLine()!;
            line.SetProduct(11, "Demo Product", true, "pcs");
            line.Quantity = 3m;
            line.UnitPrice = 12m;
            host.Data.SalesApi.NextSaveResponse = ApiResponse<SalesReceiptDto>.Failure(
                new ApiError(ApiStatusCode.BadRequest, "SalesReceipt.Invalid", "Correct the quantity.", new[]
                {
                    new ApiErrorDetail("Validation.PrecisionScale", "Quantity rejected by server.", "Request.Lines[0].Qty"),
                }));
            host.Wait(host.Control.SaveAsync());
            Assert.Single(host.Data.Journal.Entries, entry => entry.Operation == "Create.request");
            var issue = Assert.Single(host.Control.Editor.ValidationIssues);
            Assert.Same(line, issue.Line);
            Assert.Equal("qty", issue.Field);
            Assert.Equal("server", issue.Source);
            Assert.True(host.Control.Editor.IsDirty);
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S02_invalid_native_sale_date_is_the_only_save_blocker()
    {
        ScenarioControlHost.Run(SalesScenarioId.S02Validation, (host, guard) =>
        {
            var editor = host.Control.Editor;
            editor.SetCustomer(1, "Demo Customer", true);
            editor.SetWarehouse(2, "Demo Warehouse", true);
            editor.SetEmployee(3, "Demo Employee", true);
            var line = editor.AddLine()!;
            line.SetProduct(11, "Demo Product A", true, "pcs");
            line.Quantity = 3m;
            line.UnitPrice = 12m;
            Assert.Equal(0, editor.SaleId);
            Assert.True(editor.IsDirty);
            Assert.Equal(36m, editor.TotalAmount);
            host.Control.SaleDateEditor.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
            var date = new NativeLookupTypingDriver(host.Control.SaleDateEditor);
            date.SelectRange(0, date.Text.Length);
            foreach (var character in "31/02/2026") date.TypeCharacter(character);
            host.Wait(host.Control.SaveAsync());
            Assert.Equal("31/02/2026", date.Text);
            Assert.Equal("31/02/2026", host.Control.SaleDateEditor.PendingText);
            Assert.Equal(BootstrapValidationState.Invalid, host.Control.SaleDateEditor.ValidationState);
            Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Create.request");
            Assert.Equal(0, editor.SaleId);
            Assert.Equal(3m, line.Quantity);
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S02_invalid_native_sale_date_is_the_only_post_blocker()
    {
        ScenarioControlHost.Run(SalesScenarioId.S01Draft, (host, guard) =>
        {
            var editor = host.Control.Editor;
            Assert.Equal(SalesScenarioData.SeedSaleId, editor.SaleId);
            Assert.False(editor.IsDirty);
            Assert.Equal(24m, editor.TotalAmount);
            host.Control.SaleDateEditor.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
            var date = new NativeLookupTypingDriver(host.Control.SaleDateEditor);
            date.SelectRange(0, date.Text.Length);
            foreach (var character in "31/02/2026") date.TypeCharacter(character);
            host.Wait(host.Control.PostAsync());
            Assert.Equal("31/02/2026", date.Text);
            Assert.Equal("31/02/2026", host.Control.SaleDateEditor.PendingText);
            Assert.Equal(BootstrapValidationState.Invalid, host.Control.SaleDateEditor.ValidationState);
            Assert.False(editor.IsDirty);
            Assert.False(editor.RequiresPostOutcomeReload);
            Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Post.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S03_newer_lookup_result_wins_even_when_canceled_older_result_arrives_late()
    {
        ScenarioControlHost.Run(SalesScenarioId.S03LookupRace, (host, guard) =>
        {
            var grid = host.Control.LineGrid;
            grid.Focus();
            var position = new SourceGrid.Position(1, 1);
            Assert.True(grid.Selection.Focus(position, true));
            new SourceGrid.CellContext(grid, position).StartEdit();
            var lookup = host.Control.ProductLookup;
            var native = new NativeLookupTypingDriver(lookup);
            var source = host.Data.CreatedLookupSources.First(item => item.Kind == CatalogKind.Product);
            var model = host.Data.CreatedLookupViewModels.First();
            var race = host.Data.ArmLookupRace(source);
            var before = source.LookupCalls.Count;
            native.SelectRange(0, native.Text.Length);
            native.TypeCharacter('P');
            host.Until(() => source.LookupCalls.Count >= before + 1, "Lookup A was not dispatched.");
            native.TypeCharacter('B');
            host.Until(() => source.LookupCalls.Count >= before + 2, "Lookup B was not dispatched.");
            Assert.True(race.A.IgnoreCancellation);
            const int bOnlyProductId = 1203;
            Assert.True(race.B.Complete(ApiResponse<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>>.Success(
                new[] { new CatalogLookupDto(bOnlyProductId, "B-1203", "Only B returned this", true) })));
            host.Until(() => model.Items.Any(option => option.Id == bOnlyProductId),
                "Lookup B did not publish its result before A completed.");
            race.CompleteA();
            host.WaitLast(); // Includes CatalogLookupViewModel and UI continuations, beyond fake API completion.
            Assert.Contains(model.Items, option => option.Id == bOnlyProductId);
            Assert.Contains(((System.Collections.IEnumerable)lookup.DataSource!).Cast<CatalogLookupOption>(), option => option.Id == bOnlyProductId);
            Assert.Equal("PB", native.Text);
            Assert.Equal(11, lookup.SelectedValue);
            Assert.Equal(11, host.Control.Editor.Lines[0].ProductId);
            Assert.Equal("pcs", host.Control.Editor.Lines[0].Unit);
            Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Update.request" || entry.Operation == "Post.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S03_session_replacement_fences_late_lookup_and_mutations()
    {
        ScenarioControlHost.Run(SalesScenarioId.S03LookupRace, (host, guard) =>
        {
            var lookup = host.Control.CustomerLookup;
            var native = new NativeLookupTypingDriver(lookup);
            var source = host.Data.CreatedLookupSources.First(item => item.Kind == CatalogKind.Customer);
            var race = host.Data.ArmLookupRace(source);
            var before = source.LookupCalls.Count;
            native.SelectRange(0, native.Text.Length);
            native.TypeCharacter('P');
            host.Until(() => source.LookupCalls.Count >= before + 1, "Lookup A was not dispatched.");
            native.TypeCharacter('B');
            host.Until(() => source.LookupCalls.Count >= before + 2, "Lookup B was not dispatched.");
            host.Data.Session.Replace();
            race.CompleteB(); race.CompleteA();
            host.Pump();
            Assert.Empty(host.Control.Editor.Lines);
            Assert.Equal(0, host.Control.Editor.SaleId);
            Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Update.request" || entry.Operation == "Post.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S04_sale_date_month_caret_and_calendar_keyboard_keep_focus()
    {
        ScenarioControlHost.Run(SalesScenarioId.S04DateCaret, (host, guard) =>
        {
            var sale = host.Control.SaleDateEditor;
            sale.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
            sale.Text = "12/10/2025";
            var saleKeys = new NativeLookupTypingDriver(sale);
            saleKeys.SelectRange(4, 0);
            saleKeys.Key(Keys.Up);
            Assert.Equal("12/11/2025", sale.PendingText);
            Assert.Equal(4, saleKeys.SelectionStart);
            saleKeys.Key(Keys.Up);
            Assert.Equal("12/12/2025", sale.PendingText);
            Assert.Equal(4, saleKeys.SelectionStart);
            saleKeys.Key(Keys.F4);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
            Assert.Equal("12/12/2025", sale.PendingText);
            saleKeys.AssertFocused();
            Assert.Equal(4, saleKeys.SelectionStart);
            Assert.Equal(0, saleKeys.SelectionLength);
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S04_native_filter_characters_leave_query_closed_until_apply()
    {
        ScenarioControlHost.Run(SalesScenarioId.S04DateCaret, (host, guard) =>
        {
            Assert.True(host.Control.TryShowReceiptList());
            var from = host.Control.FromDateEditor;
            from.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
            var before = host.Data.Journal.Entries.Count(entry => entry.Operation == "List.request");
            var fromKeys = new NativeLookupTypingDriver(from);
            fromKeys.SelectRange(0, fromKeys.Text.Length);
            var raw = string.Empty;
            foreach (var character in "5/11/2026")
            {
                fromKeys.TypeCharacter(character);
                raw += character;
                Assert.Equal(raw, from.PendingText);
                Assert.Equal(raw.Length, fromKeys.SelectionStart);
                Assert.Equal(0, fromKeys.SelectionLength);
                fromKeys.AssertFocused();
                Assert.Equal(before, host.Data.Journal.Entries.Count(entry => entry.Operation == "List.request"));
            }
            host.Control.ApplyFiltersButton.PerformClick();
            host.WaitLast();
            var list = Assert.IsType<SalesReceiptListRequest>(host.Data.Journal.Entries.Last(
                entry => entry.Operation == "List.request").Request);
            Assert.Equal(new DateTime(2026, 11, 5), list.FromDate);
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S04_to_date_native_month_arrows_keep_caret_until_apply()
    {
        ScenarioControlHost.Run(SalesScenarioId.S04DateCaret, (host, guard) =>
        {
            Assert.True(host.Control.TryShowReceiptList());
            var to = host.Control.ToDateEditor;
            to.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
            to.Text = "5/1";
            var keys = new NativeLookupTypingDriver(to);
            keys.SelectRange(3, 0);
            var before = host.Data.Journal.Entries.Count(entry => entry.Operation == "List.request");
            keys.Key(Keys.Up);
            Assert.Equal("05/02/2026", to.PendingText);
            Assert.Equal(5, keys.SelectionStart);
            Assert.Equal(0, keys.SelectionLength);
            keys.Key(Keys.Up);
            Assert.Equal("05/03/2026", to.PendingText);
            Assert.Equal(5, keys.SelectionStart);
            Assert.Equal(before, host.Data.Journal.Entries.Count(entry => entry.Operation == "List.request"));
            host.Control.ApplyFiltersButton.PerformClick();
            host.WaitLast();
            var request = Assert.IsType<SalesReceiptListRequest>(host.Data.Journal.Entries.Last(
                entry => entry.Operation == "List.request").Request);
            Assert.Equal(new DateTime(2026, 3, 5), request.ToDate);
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S05_dirty_close_cancel_discard_and_save_are_decided_by_coordinator()
    {
        ScenarioControlHost.Run(SalesScenarioId.S05DirtyClose, (host, guard) =>
        {
            host.Control.Editor.Note = "Pending close";
            Assert.True(host.Control.CaptureCloseState().IsDirty);
            var canceled = new WorkspaceCloseCoordinator((_, _) => Task.FromResult(WorkspaceCloseChoice.Cancel));
            var cancel = canceled.RequestCloseAsync(host.Control, CancellationToken.None);
            host.Wait(cancel);
            Assert.Equal(WorkspaceCloseDecision.KeepOpen, cancel.Result);
            Assert.True(host.Control.Editor.IsDirty);
            Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Update.request");

            var save = new WorkspaceCloseCoordinator((_, _) => Task.FromResult(WorkspaceCloseChoice.Save));
            var saved = save.RequestCloseAsync(host.Control, CancellationToken.None);
            host.Wait(saved);
            Assert.Equal(WorkspaceCloseDecision.Admitted, saved.Result);
            Assert.False(host.Control.Editor.IsDirty);
            Assert.Single(host.Data.Journal.Entries, entry => entry.Operation == "Update.request");

            host.Control.Editor.Note = "Discard this change";
            var discard = new WorkspaceCloseCoordinator((_, _) => Task.FromResult(WorkspaceCloseChoice.Discard));
            var discarded = discard.RequestCloseAsync(host.Control, CancellationToken.None);
            host.Wait(discarded);
            Assert.Equal(WorkspaceCloseDecision.Admitted, discarded.Result);
            Assert.True(host.Control.Editor.IsDirty);
            Assert.Single(host.Data.Journal.Entries, entry => entry.Operation == "Update.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S05_failed_save_keeps_dirty_workspace_open()
    {
        ScenarioControlHost.Run(SalesScenarioId.S05DirtyClose, (host, guard) =>
        {
            host.Control.Editor.Note = "Rejected change";
            host.Data.SalesApi.NextSaveResponse = ApiResponse<SalesReceiptDto>.Failure(
                new ApiError(ApiStatusCode.BadRequest, "SalesReceipt.Invalid", "Rejected by scenario."));
            var coordinator = new WorkspaceCloseCoordinator((_, _) => Task.FromResult(WorkspaceCloseChoice.Save));
            var close = coordinator.RequestCloseAsync(host.Control, CancellationToken.None);
            host.Wait(close);
            Assert.Equal(WorkspaceCloseDecision.KeepOpen, close.Result);
            Assert.True(host.Control.Editor.IsDirty);
            Assert.Single(host.Data.Journal.Entries, entry => entry.Operation == "Update.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S05_pending_native_buffer_survives_cancel_and_rejected_close_save()
    {
        ScenarioControlHost.Run(SalesScenarioId.S05DirtyClose, (host, guard) =>
        {
            var grid = host.Control.LineGrid;
            grid.Focus();
            var position = new SourceGrid.Position(1, 3);
            Assert.True(grid.Selection.Focus(position, true));
            new SourceGrid.CellContext(grid, position).StartEdit();
            var native = new NativeLookupTypingDriver(host.Control.QuantityEditor);
            native.SelectRange(0, native.Text.Length);
            foreach (var character in "1.00001") native.TypeCharacter(character);
            Assert.True(host.Control.CaptureCloseState().IsDirty);
            var cancel = new WorkspaceCloseCoordinator((_, _) => Task.FromResult(WorkspaceCloseChoice.Cancel));
            var canceled = cancel.RequestCloseAsync(host.Control, CancellationToken.None);
            host.Wait(canceled);
            Assert.Equal(WorkspaceCloseDecision.KeepOpen, canceled.Result);
            Assert.Equal("1.00001", native.Text);
            var save = new WorkspaceCloseCoordinator((_, _) => Task.FromResult(WorkspaceCloseChoice.Save));
            var rejected = save.RequestCloseAsync(host.Control, CancellationToken.None);
            host.Wait(rejected);
            Assert.Equal(WorkspaceCloseDecision.KeepOpen, rejected.Result);
            Assert.Equal("1.00001", native.Text);
            Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Update.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S05_back_rejects_invalid_native_buffer_without_losing_document_or_dispatching_mutation()
    {
        ScenarioControlHost.Run(SalesScenarioId.S05DirtyClose, (host, guard) =>
        {
            var editor = host.Control.Editor;
            var identity = editor.EditorIdentity;
            var line = editor.Lines[0];
            var grid = host.Control.LineGrid;
            grid.Focus();
            var position = new SourceGrid.Position(1, 3);
            Assert.True(grid.Selection.Focus(position, true));
            new SourceGrid.CellContext(grid, position).StartEdit();
            var native = new NativeLookupTypingDriver(host.Control.QuantityEditor);
            native.SelectRange(0, native.Text.Length);
            foreach (var character in "1.00001") native.TypeCharacter(character);

            host.Control.BackToReceiptsButton.PerformClick();
            host.Pump();
            Assert.False(host.Control.TryShowReceiptList());
            Assert.Equal(ReceiptWorkspaceMode.Detail, host.Control.WorkspaceMode);
            Assert.Equal("1.00001", host.Control.QuantityEditor.PendingText);
            Assert.Equal("1.00001", native.Text);
            Assert.Equal(identity, editor.EditorIdentity);
            Assert.Equal(SalesScenarioData.SeedSaleId, editor.SaleId);
            Assert.Same(line, editor.Lines[0]);
            Assert.Equal(2m, line.Quantity);
            Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Update.request" ||
                entry.Operation == "Post.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void S06_unknown_post_stays_locked_until_same_id_authoritative_reload()
    {
        ScenarioControlHost.Run(SalesScenarioId.S06UnknownPost, (host, guard) =>
        {
            host.Wait(host.Control.PostAsync());
            Assert.True(host.Control.Editor.RequiresPostOutcomeReload);
            Assert.Single(host.Data.Journal.Entries, entry => entry.Operation == "Post.request");
            var prompts = 0;
            var close = new WorkspaceCloseCoordinator((_, _) => { prompts++; return Task.FromResult(WorkspaceCloseChoice.Discard); });
            var blocked = close.RequestCloseAsync(host.Control, CancellationToken.None);
            host.Wait(blocked);
            Assert.Equal(WorkspaceCloseDecision.KeepOpen, blocked.Result);
            Assert.Equal(0, prompts);
            host.Wait(host.Control.PostAsync());
            Assert.Single(host.Data.Journal.Entries, entry => entry.Operation == "Post.request");

            host.Control.ReloadButton.PerformClick();
            host.WaitLast();
            Assert.True(host.Control.Editor.RequiresPostOutcomeReload);
            host.Control.ReloadButton.PerformClick();
            host.WaitLast();
            Assert.False(host.Control.Editor.RequiresPostOutcomeReload);
            Assert.Equal(SalesScenarioData.SeedSaleId, host.Control.Editor.SaleId);
            Assert.Equal("Posted", host.Control.Editor.Status);
            Assert.Single(host.Data.Journal.Entries, entry => entry.Operation == "Post.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void Empty_sales_list_projects_no_document_rows()
    {
        ScenarioControlHost.Run(SalesScenarioId.S02Validation, (host, guard) =>
        {
            Assert.True(host.Control.TryShowReceiptList());
            var response = Assert.IsType<ApiResponse<SalesReceiptListPage>>(Assert.Single(host.Data.Journal.Entries,
                entry => entry.Operation == "List.response").Request);
            Assert.Empty(response.Data!.Items);
            Assert.Equal(1, host.Control.ReceiptGrid.Rows.Count); // Header only.
            Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Get.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void Denied_read_scrubs_sales_presentation_and_blocks_commands()
    {
        ScenarioControlHost.Run(SalesScenarioId.S01Draft,
            data => data.Permissions.Set("Sales.Receipts.Read", false),
            (host, guard) =>
            {
                Assert.Empty(host.Control.Editor.Lines);
                Assert.Equal(0, host.Control.Editor.SaleId);
                Assert.False(host.Control.SaveButton.Enabled);
                Assert.False(host.Control.PostButton.Enabled);
                host.Wait(host.Control.SaveAsync());
                host.Wait(host.Control.PostAsync());
                Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Get.request" ||
                    entry.Operation == "Update.request" || entry.Operation == "Post.request");
                Assert.Empty(guard.Exceptions);
            });
    }

    [Fact]
    public void Denied_write_blocks_save_while_independent_post_permission_remains_usable()
    {
        ScenarioControlHost.Run(SalesScenarioId.S01Draft,
            data => data.Permissions.Set("Sales.Receipts.Write", false),
            (host, guard) =>
            {
                Assert.False(host.Control.Editor.CanEdit);
                host.Wait(host.Control.SaveAsync());
                Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Update.request");
                host.Wait(host.Control.PostAsync());
                Assert.Single(host.Data.Journal.Entries, entry => entry.Operation == "Post.request");
                Assert.Equal("Posted", host.Control.Editor.Status);
                Assert.Empty(guard.Exceptions);
            });
    }

    [Fact]
    public void Denied_post_blocks_post_while_write_still_saves_draft()
    {
        ScenarioControlHost.Run(SalesScenarioId.S01Draft,
            data => data.Permissions.Set("Sales.Receipts.Post", false),
            (host, guard) =>
            {
                host.Wait(host.Control.PostAsync());
                Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Post.request");
                host.Control.Editor.Note = "Allowed draft write";
                host.Wait(host.Control.SaveAsync());
                Assert.Single(host.Data.Journal.Entries, entry => entry.Operation == "Update.request");
                Assert.Equal("Draft", host.Control.Editor.Status);
                Assert.Empty(guard.Exceptions);
            });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Busy_post_gate_blocks_other_commands_then_preserves_coherent_result(bool succeeds)
    {
        ScenarioControlHost.Run(SalesScenarioId.S01Draft, (host, guard) =>
        {
            var gate = new ScenarioGate<ApiResponse<PostSalesReceiptResponse>>();
            host.Data.SalesApi.PostGate = gate;
            var post = host.Control.PostAsync();
            host.Until(() => host.Data.Journal.Entries.Any(entry => entry.Operation == "Post.request"),
                "Post did not enter its fixture gate.");
            Assert.True(host.Control.Editor.IsBusy);
            host.Control.Editor.Note = "Blocked while busy";
            host.Wait(host.Control.SaveAsync());
            host.Wait(host.Control.PostAsync());
            Assert.Single(host.Data.Journal.Entries, entry => entry.Operation == "Post.request");
            Assert.DoesNotContain(host.Data.Journal.Entries, entry => entry.Operation == "Update.request");

            var response = succeeds
                ? ApiResponse<PostSalesReceiptResponse>.Success(new PostSalesReceiptResponse(
                    SalesScenarioData.SeedSaleId, "Posted", new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc), "lab-v2"))
                : ApiResponse<PostSalesReceiptResponse>.Failure(new ApiError(ApiStatusCode.Conflict,
                    "UiLab.PostRejected", "The scenario rejected Post."));
            Assert.True(gate.Complete(response));
            host.Wait(post);
            Assert.False(host.Control.Editor.IsBusy);
            Assert.Equal(succeeds ? "Posted" : "Draft", host.Data.SalesApi.CurrentSale!.Status);
            Assert.Equal(succeeds ? "Posted" : "Draft", host.Control.Editor.Status);
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void Draft_preview_recalculates_price_and_gift_with_independent_expected_amounts()
    {
        ScenarioControlHost.Run(SalesScenarioId.S01Draft, (host, guard) =>
        {
            var editor = host.Control.Editor;
            Assert.Equal(24m, editor.TotalAmount);
            editor.Lines[0].UnitPrice = 7.25m;
            Assert.Equal(14.5m, editor.Lines[0].LineAmount);
            Assert.Equal(0m, editor.Lines[1].LineAmount);
            Assert.Equal(14.5m, editor.TotalAmount);
            editor.Lines[1].IsGift = false;
            Assert.Equal(12m, editor.Lines[1].LineAmount);
            Assert.Equal(26.5m, editor.TotalAmount);
            editor.Lines[1].IsGift = true;
            Assert.Equal(0m, editor.Lines[1].LineAmount);
            Assert.Equal(14.5m, editor.TotalAmount);
            Assert.Empty(guard.Exceptions);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Scenario_fake_rejects_precision_beyond_four_places_without_changing_seed(bool invalidQuantity)
    {
        using var data = SalesScenarioData.Create(SalesScenarioId.S01Draft);
        var seed = data.SalesApi.CurrentSale!;
        var lines = new[]
        {
            new SaveSalesReceiptLineRequest(101, 11, invalidQuantity ? 1.00001m : 1m,
                invalidQuantity ? 12m : 12.00001m, false),
            new SaveSalesReceiptLineRequest(102, 11, 1m, 12m, true),
        };
        var request = new UpdateSalesReceiptDraftRequest(seed.SaleId, seed.Version, seed.SaleDate,
            seed.CustomerId, seed.WarehouseId, seed.EmployeeId, seed.Note, lines);
        var response = await data.SalesApi.ForSession(data.Session.Version).UpdateDraftAsync(request, CancellationToken.None);
        Assert.False(response.IsSuccess);
        Assert.Equal(seed.Version, data.SalesApi.CurrentSale!.Version);
        Assert.Equal(24m, data.SalesApi.CurrentSale.TotalAmount);
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Update.request");
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Update.response");
    }

    [Fact]
    public async Task Session_replacement_during_held_post_gate_cannot_mutate_sale()
    {
        using var data = SalesScenarioData.Create(SalesScenarioId.S01Draft);
        var seed = data.SalesApi.CurrentSale!;
        var gate = new ScenarioGate<ApiResponse<PostSalesReceiptResponse>>();
        data.SalesApi.PostGate = gate;
        var oldClient = data.SalesApi.ForSession(data.Session.Version);
        var pending = oldClient.PostAsync(new PostSalesReceiptRequest(seed.SaleId, seed.Version), CancellationToken.None);
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Post.request");
        data.Session.Replace();
        Assert.True(gate.Complete(ApiResponse<PostSalesReceiptResponse>.Success(new PostSalesReceiptResponse(
            seed.SaleId, "Posted", new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc), "lab-v2"))));

        var response = await pending;
        Assert.False(response.IsSuccess);
        Assert.Equal("Auth.SessionChanged", response.Error!.Code);
        Assert.Equal("Draft", data.SalesApi.CurrentSale!.Status);
        Assert.Equal(seed.Version, data.SalesApi.CurrentSale.Version);
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Post.request");
    }
}
