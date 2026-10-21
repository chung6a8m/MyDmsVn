using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

public sealed class GoodsReceiptLookupBindingTests : IClassFixture<LookupGateEnvironment>
{
    private readonly ITestOutputHelper _output;
    private readonly LookupGateEnvironment _gate;
    public GoodsReceiptLookupBindingTests(ITestOutputHelper output, LookupGateEnvironment gate) { _output = output; _gate = gate; }

    [Theory]
    [InlineData("WarehouseFilterLookup")]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Same_identity_binding_preserves_pending_text_and_selection(string target) => Run(target, fixture =>
    {
        fixture.TypeText("gui");
        fixture.Driver.SelectRange(1, 1);
        fixture.RebindOptions();
        Pending(fixture, "gui", 1, 1);
        AssertIds(fixture, 3, 5, null, false);
    });

    [Theory]
    [InlineData("WarehouseFilterLookup")]
    [InlineData("WarehouseLookup")]
    public void Shared_warehouse_results_preserve_each_hosts_role(string target) => Run(target, fixture =>
    {
        // Nonfocused sibling Text is explicit binding setup, not per-key evidence.
        var sibling = target == "WarehouseLookup" ? fixture.Control.WarehouseFilterLookup : fixture.Control.WarehouseLookup;
        sibling.Text = "sibling query";
        fixture.Track(fixture.Control.LastOperation);
        fixture.TypeText("gui");
        fixture.Driver.SelectRange(1, 1);
        fixture.RebindOptions();
        Pending(fixture, "gui", 1, 1);
        Assert.Equal("sibling query", sibling.Text);
        Assert.True(sibling.HasPendingText);
        AssertIds(fixture, 3, 5, null, false);
        Assert.All(fixture.Api.Requests, request => Assert.Null(request.WarehouseId));
    });

    [Theory]
    [InlineData("WarehouseFilterLookup", 13)]
    [InlineData("WarehouseLookup", 13)]
    [InlineData("EmployeeLookup", 15)]
    public void Only_explicit_selection_changes_filter_or_header(string target, int secondId) => Run(target, fixture =>
    {
        fixture.Type('g');
        fixture.Driver.SelectRange(0, fixture.Driver.Text.Length);
        fixture.Type('\b'); // Native deletion gives an empty query with deterministic source order.
        fixture.Driver.Key(Keys.F4);
        Assert.True(fixture.Lookup.IsDropDownOpen);
        fixture.Driver.Key(Keys.Home);
        fixture.Driver.Key(Keys.Down);
        Assert.Equal(secondId, Assert.IsType<CatalogLookupOption>(fixture.Lookup.HighlightedItem).Id);
        AssertIds(fixture, 3, 5, null, false);
        fixture.Driver.Key(Keys.Up);
        AssertIds(fixture, 3, 5, null, false);
        fixture.Driver.Key(Keys.Down);
        fixture.Driver.Key(Keys.Enter);
        WaitFilterCommit(fixture);
        Assert.False(fixture.Lookup.IsDropDownOpen);
        Assert.False(fixture.Lookup.HasPendingText);
        Assert.Equal(secondId, fixture.Lookup.SelectedValue);
        AssertIds(fixture, target == "WarehouseLookup" ? 13 : 3, target == "EmployeeLookup" ? 15 : 5,
            target == "WarehouseFilterLookup" ? 13 : (int?)null, target != "WarehouseFilterLookup");
        Assert.Equal(target == "WarehouseFilterLookup" ? 13 : (int?)null, fixture.Api.Requests.Last().WarehouseId);
        Assert.Equal(0, fixture.Api.PostCalls);
    });

    [Theory]
    [InlineData("WarehouseLookup", "Historic warehouse", 3)]
    [InlineData("EmployeeLookup", "Historic employee", 5)]
    public void Historical_selection_survives_search(string target, string label, int id) => Run(target, fixture =>
    {
        fixture.Source.SelectedIsActive = false;
        fixture.Editor.Load(GoodsReceiptLookupFixture.ReceiptClient.Receipt(active: false));
        fixture.Driver.SelectRange(0, fixture.Driver.Text.Length);
        fixture.TypeText("gui");
        fixture.Driver.SelectRange(1, 1);
        fixture.RebindOptions();
        Pending(fixture, "gui", 1, 1);
        Assert.Equal(id, fixture.Lookup.SelectedValue);
        Assert.Equal(label, fixture.Lookup.CommittedDisplayText);
        Assert.False(Assert.Single(fixture.LookupViewModel.Items, item => item.Id == id).IsAvailableForNewSelection);
        Assert.All(Assert.IsAssignableFrom<IEnumerable<CatalogLookupOption>>(fixture.Control.WarehouseFilterLookup.DataSource),
            item => Assert.True(item.IsAvailableForNewSelection));
        fixture.Driver.Key(Keys.Escape);
        Assert.Equal(label, fixture.Driver.Text);
        Assert.False(fixture.Lookup.HasPendingText);
        AssertIds(fixture, 3, 5, null, false);
    });

    [Theory]
    [InlineData("WarehouseLookup", "New")]
    [InlineData("EmployeeLookup", "New")]
    [InlineData("WarehouseLookup", "Select")]
    [InlineData("EmployeeLookup", "Select")]
    [InlineData("WarehouseLookup", "Refresh")]
    [InlineData("EmployeeLookup", "Refresh")]
    [InlineData("WarehouseLookup", "Denied")]
    [InlineData("EmployeeLookup", "Denied")]
    [InlineData("WarehouseLookup", "Dispose")]
    [InlineData("EmployeeLookup", "Dispose")]
    [InlineData("WarehouseLookup", "Session replacement")]
    [InlineData("EmployeeLookup", "Session replacement")]
    [InlineData("WarehouseFilterLookup", "New")]
    [InlineData("WarehouseFilterLookup", "Select")]
    [InlineData("WarehouseFilterLookup", "Refresh")]
    [InlineData("WarehouseFilterLookup", "Denied")]
    [InlineData("WarehouseFilterLookup", "Dispose")]
    [InlineData("WarehouseFilterLookup", "Session replacement")]
    public void Late_completion_cannot_publish_after_boundary(string target, string boundary) => Run(target, fixture =>
    {
        var old = fixture.Type('g');
        var response = fixture.AdmitSearch(0);
        fixture.WarehouseSource.Hold = fixture.EmployeeSource.Hold = false;
        if (boundary == "New") fixture.Control.NewButton.PerformClick();
        else if (boundary == "Select")
        {
            fixture.Api.Current = GoodsReceiptLookupFixture.ReceiptClient.Receipt(42);
            fixture.Wait(fixture.Track(fixture.Control.SelectReceiptAsync(42, fixture.Token)));
        }
        else if (boundary == "Refresh")
        {
            fixture.Control.RefreshButton.PerformClick();
            fixture.Wait(fixture.Track(fixture.Control.LastOperation));
        }
        else if (boundary == "Denied")
        {
            fixture.Api.DenyList = true;
            fixture.Wait(fixture.Track(fixture.Control.ActivateAsync(fixture.Token)));
        }
        else fixture.Control.Dispose();
        var items = fixture.LookupViewModel.Items.ToArray();
        using var replacement = boundary == "Session replacement" ? new GoodsReceiptLookupFixture(target, fixture.Token) : null;
        fixture.Source.Complete(response, new CatalogLookupDto(98, "STALE", "Old session result", true));
        fixture.Wait(old);
        Assert.DoesNotContain(fixture.LookupViewModel.Items, item => item.Id == 98);
        Assert.Equal(items.Select(item => item.Id), fixture.LookupViewModel.Items.Select(item => item.Id));
        if (replacement != null) AssertIds(replacement, 3, 5, null, false);
        if (!fixture.Control.IsDisposed)
        {
            if (target != "WarehouseFilterLookup") Assert.False(fixture.Lookup.HasPendingText);
            Assert.False(fixture.Lookup.IsDropDownOpen);
            Assert.Equal(boundary == "New" || boundary == "Denied" ? (long?)null : boundary == "Select" ? 42 : 41, fixture.Editor.Id);
        }
    });

    [Theory]
    [InlineData("WarehouseLookup", "New")]
    [InlineData("EmployeeLookup", "New")]
    [InlineData("WarehouseLookup", "Select")]
    [InlineData("EmployeeLookup", "Select")]
    [InlineData("WarehouseLookup", "Refresh")]
    [InlineData("EmployeeLookup", "Refresh")]
    public void Rejected_dirty_navigation_preserves_native_query_and_range(string target, string command) => Run(target, fixture =>
    {
        fixture.Editor.Note = "unsaved";
        fixture.TypeText("gui");
        fixture.Driver.SelectRange(1, 1);
        var getCalls = fixture.Api.GetCalls;
        var listCalls = fixture.Api.Requests.Count;
        if (command == "Select") fixture.Wait(fixture.Track(fixture.Control.SelectReceiptAsync(42, fixture.Token)));
        else (command == "New" ? fixture.Control.NewButton : fixture.Control.RefreshButton).PerformClick();
        Pending(fixture, "gui", 1, 1);
        Assert.Equal(41, fixture.Editor.Id);
        Assert.Equal(getCalls, fixture.Api.GetCalls);
        Assert.Equal(listCalls, fixture.Api.Requests.Count);
        Assert.Equal("unsaved", fixture.Editor.Note);
    });

    [Theory]
    [InlineData("WarehouseLookup", "Posted")]
    [InlineData("EmployeeLookup", "Posted")]
    [InlineData("WarehouseLookup", "Busy")]
    [InlineData("EmployeeLookup", "Busy")]
    [InlineData("WarehouseLookup", "Denied")]
    [InlineData("EmployeeLookup", "Denied")]
    [InlineData("WarehouseLookup", "Recovery")]
    [InlineData("EmployeeLookup", "Recovery")]
    public void Locked_header_rejects_stale_selection_callbacks(string target, string state) => Run(target, fixture =>
    {
        if (state == "Posted") fixture.Editor.Load(GoodsReceiptLookupFixture.ReceiptClient.Receipt(status: "Posted"));
        else if (state == "Busy")
        {
            fixture.Api.HoldList = true;
            fixture.Track(fixture.List.RefreshAsync(fixture.Token));
        }
        else if (state == "Denied")
        {
            fixture.WarehouseSource.Hold = fixture.EmployeeSource.Hold = false;
            fixture.Api.DenyList = true;
            fixture.Wait(fixture.Track(fixture.Control.ActivateAsync(fixture.Token)));
        }
        else
        {
            fixture.Wait(fixture.Track(fixture.Editor.PostAsync(fixture.Token)));
            Assert.True(fixture.Editor.RequiresPostOutcomeReload);
            Assert.Equal(1, fixture.Api.PostCalls);
        }
        Assert.False(fixture.Lookup.Enabled);
        var lookupCalls = fixture.Source.LookupCalls;
        fixture.Source.Hold = false;
        fixture.Lookup.Text = "stale inaccessible query";
        fixture.Track(fixture.Control.LastOperation);
        fixture.ReleaseDelays();
        fixture.Wait(fixture.Control.LastOperation);
        Assert.Equal(lookupCalls, fixture.Source.LookupCalls);
        var warehouse = fixture.Editor.WarehouseId;
        var employee = fixture.Editor.EmployeeId;
        // Deliberately inject the public programmatic setter to emulate a stale callback;
        // disabled controls cannot be reached by the native keyboard driver.
        fixture.Lookup.SelectValue(target == "WarehouseLookup" ? 13 : 15);
        Assert.Equal(warehouse, fixture.Editor.WarehouseId);
        Assert.Equal(employee, fixture.Editor.EmployeeId);
        Assert.False(fixture.Editor.IsDirty);
        if (state == "Recovery")
        {
            fixture.Control.PostButton.PerformClick();
            Assert.Equal(1, fixture.Api.PostCalls);
            Assert.True(fixture.Editor.RequiresPostOutcomeReload);
        }
    });

    [Theory]
    [InlineData("WarehouseFilterLookup")]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Ignored_cancellation_response_cannot_replace_newer_query(string target) => Run(target, fixture =>
    {
        var old = fixture.Type('g');
        var oldResponse = fixture.AdmitSearch(0);
        var latest = fixture.Type('u');
        var latestResponse = fixture.AdmitSearch(1);
        fixture.Source.Complete(latestResponse, new CatalogLookupDto(77, "GUI", "Current query", true));
        fixture.Wait(latest);
        Pending(fixture, "gu", 2);
        Assert.Contains(fixture.LookupViewModel.Items, item => item.Id == 77);
        fixture.Source.Complete(oldResponse, new CatalogLookupDto(98, "STALE", "Old query", true));
        fixture.Wait(old);
        Pending(fixture, "gu", 2);
        Assert.DoesNotContain(fixture.LookupViewModel.Items, item => item.Id == 98);
        AssertIds(fixture, 3, 5, null, false);
    });

    [Theory]
    [InlineData("WarehouseFilterLookup", true)]
    [InlineData("WarehouseLookup", true)]
    [InlineData("EmployeeLookup", true)]
    [InlineData("WarehouseFilterLookup", false)]
    [InlineData("WarehouseLookup", false)]
    [InlineData("EmployeeLookup", false)]
    public void Escape_restores_committed_selection_with_popup_open_or_closed(string target, bool open) => Run(target, fixture =>
    {
        fixture.TypeText("gui");
        if (open) { fixture.Driver.Key(Keys.F4); fixture.Driver.Key(Keys.Down); }
        else fixture.Lookup.CloseDropDown();
        fixture.Driver.Key(Keys.Escape);
        Assert.False(fixture.Lookup.IsDropDownOpen);
        Assert.False(fixture.Lookup.HasPendingText);
        Assert.Equal(fixture.InitialText, fixture.Driver.Text);
        AssertIds(fixture, 3, 5, null, false);
    });

    [Theory]
    [InlineData("WarehouseFilterLookup", false)]
    [InlineData("WarehouseLookup", false)]
    [InlineData("EmployeeLookup", false)]
    [InlineData("WarehouseFilterLookup", true)]
    [InlineData("WarehouseLookup", true)]
    [InlineData("EmployeeLookup", true)]
    public void Tab_and_shift_tab_resolve_exact_text_then_move_native_focus(string target, bool reverse) => Run(target, fixture =>
    {
        fixture.TypeText("G - Guitar");
        AssertIds(fixture, 3, 5, null, false);
        if (reverse) InlineReceiptTestDriver.ShiftTab(); else fixture.Driver.Key(Keys.Tab);
        WaitFilterCommit(fixture);
        _output.WriteLine("After Tab: focused=" + Control.FromHandle(fixture.Driver.FocusedHandle)?.GetType().Name +
            "; filter focus=" + fixture.Control.WarehouseFilterLookup.ContainsFocus + "; search focus=" + fixture.Control.SearchBox.ContainsFocus +
            "; status focus=" + fixture.Control.StatusFilter.ContainsFocus);
        Assert.False(fixture.Lookup.ContainsFocus);
        Assert.False(fixture.Lookup.IsDropDownOpen);
        Assert.False(fixture.Lookup.HasPendingText);
        Assert.Equal(target == "EmployeeLookup" ? 15 : 13, fixture.Lookup.SelectedValue);
        AssertIds(fixture, target == "WarehouseLookup" ? 13 : 3, target == "EmployeeLookup" ? 15 : 5,
            target == "WarehouseFilterLookup" ? 13 : (int?)null, target != "WarehouseFilterLookup");
    });

    [Theory]
    [InlineData("WarehouseFilterLookup")]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Unmatched_tab_restores_without_committing_a_highlight(string target) => Run(target, fixture =>
    {
        fixture.TypeText("unmatched");
        fixture.Driver.Key(Keys.F4);
        fixture.Driver.Key(Keys.Tab);
        Assert.False(fixture.Lookup.ContainsFocus);
        Assert.False(fixture.Lookup.HasPendingText);
        Assert.Equal(fixture.InitialText, fixture.Lookup.Text);
        AssertIds(fixture, 3, 5, null, false);
    });

    [Theory]
    [InlineData("WarehouseFilterLookup")]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Native_paste_retains_query_caret_and_identity_through_rebind(string target) => Run(target, fixture =>
    {
        fixture.Driver.Paste("gui");
        fixture.Track(fixture.Control.LastOperation);
        Pending(fixture, "gui", 3);
        fixture.RebindOptions();
        Pending(fixture, "gui", 3);
        AssertIds(fixture, 3, 5, null, false);
    });

    [Theory]
    [InlineData("WarehouseFilterLookup")]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Popup_mouse_selects_non_first_result_once(string target) => Run(target, fixture =>
    {
        fixture.Type('g');
        fixture.Driver.SelectRange(0, fixture.Driver.Text.Length);
        fixture.Type('\b');
        fixture.Driver.Key(Keys.F4);
        var results = fixture.Lookup.ResultsGrid;
        Assert.Equal(2, results.Rows.Count);
        var committed = 0;
        fixture.Lookup.SelectedValueChanged += (_, __) => committed++;
        var bounds = results.GetCellDisplayRectangle(0, 1, true);
        Assert.True(bounds.Width > 0 && bounds.Height > 0);
        NativeLookupTypingDriver.Click(results, new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
        WaitFilterCommit(fixture);
        Assert.Equal(target == "EmployeeLookup" ? 15 : 13, fixture.Lookup.SelectedValue);
        Assert.Equal(1, committed);
        Assert.False(fixture.Lookup.IsDropDownOpen);
        AssertIds(fixture, target == "WarehouseLookup" ? 13 : 3, target == "EmployeeLookup" ? 15 : 5,
            target == "WarehouseFilterLookup" ? 13 : (int?)null, target != "WarehouseFilterLookup");
    });

    [Theory]
    [InlineData("WarehouseFilterLookup", false)]
    [InlineData("WarehouseLookup", false)]
    [InlineData("EmployeeLookup", false)]
    [InlineData("WarehouseFilterLookup", true)]
    [InlineData("WarehouseLookup", true)]
    [InlineData("EmployeeLookup", true)]
    public void Outside_click_or_owner_deactivation_does_not_commit_highlight(string target, bool deactivate) => Run(target, fixture =>
    {
        NativeLookupTypingDriver.Activate(fixture.Form);
        fixture.Type('g');
        fixture.Driver.Key(Keys.F4);
        fixture.Driver.Key(Keys.Down);
        if (deactivate)
        {
            using var other = new Form { ClientSize = new Size(200, 100) };
            var input = new TextBox { Dock = DockStyle.Fill, Multiline = true };
            other.Controls.Add(input);
            other.Show();
            NativeLookupTypingDriver.Activate(other);
            NativeLookupTypingDriver.Click(input, new Point(20, 20));
            fixture.PumpUntil(() => Form.ActiveForm == other, "second owned form activation");
            fixture.PumpUntil(() => !fixture.Lookup.IsDropDownOpen, "owner deactivation closes popup");
        }
        else
        {
            var noteInput = fixture.Control.NoteEditor.Controls.OfType<TextBox>().Single();
            NativeLookupTypingDriver.Click(noteInput, new Point(5, 5));
            fixture.PumpUntil(() => !fixture.Lookup.IsDropDownOpen, "outside click closes popup");
            Assert.False(fixture.Lookup.HasPendingText);
            Assert.Equal(fixture.InitialText, fixture.Lookup.Text);
        }
        AssertIds(fixture, 3, 5, null, false);
    });

    [Theory]
    [InlineData("WarehouseLookup", "New")]
    [InlineData("EmployeeLookup", "New")]
    [InlineData("WarehouseLookup", "Refresh")]
    [InlineData("EmployeeLookup", "Refresh")]
    public void Command_mouse_down_and_rejected_click_preserve_header_focus(string target, string command) => Run(target, fixture =>
    {
        fixture.Editor.Note = "unsaved";
        fixture.TypeText("gui");
        fixture.Driver.SelectRange(1, 1);
        var button = command == "New" ? fixture.Control.NewButton : fixture.Control.RefreshButton;
        InlineReceiptTestDriver.MouseDown(button);
        InlineReceiptTestDriver.MouseUp(button, inside: false);
        Pending(fixture, "gui", 1, 1);
        InlineReceiptTestDriver.MouseDown(button);
        InlineReceiptTestDriver.MouseUp(button);
        Pending(fixture, "gui", 1, 1);
        Assert.Equal(41, fixture.Editor.Id);
        Assert.Equal("unsaved", fixture.Editor.Note);
        Assert.Equal(3, fixture.Editor.WarehouseId);
        Assert.Equal(5, fixture.Editor.EmployeeId);
    });

    [Theory]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Denied_recovery_reload_keeps_post_outcome_lock(string target) => Run(target, fixture =>
    {
        fixture.Wait(fixture.Track(fixture.Editor.PostAsync(fixture.Token)));
        Assert.True(fixture.Editor.RequiresPostOutcomeReload);
        fixture.Api.DenyList = true;
        fixture.Control.RefreshButton.PerformClick();
        fixture.Wait(fixture.Track(fixture.Control.LastOperation));
        Assert.True(fixture.Editor.RequiresPostOutcomeReload);
        Assert.False(fixture.Lookup.Enabled);
        Assert.False(fixture.Control.NewButton.Enabled);
        Assert.False(fixture.Control.PostButton.Enabled);
        Assert.Equal(1, fixture.Api.PostCalls);
    });

    [Fact]
    public void Filter_rejects_busy_callback_but_allows_posted_inspection() => Run("WarehouseFilterLookup", fixture =>
    {
        fixture.Editor.Load(GoodsReceiptLookupFixture.ReceiptClient.Receipt(status: "Posted"));
        Assert.True(fixture.Lookup.Enabled);
        fixture.Api.HoldList = true;
        var busy = fixture.Track(fixture.List.RefreshAsync(fixture.Token));
        Assert.False(fixture.Lookup.Enabled);
        var requests = fixture.Api.Requests.Count;
        fixture.Lookup.SelectValue(13); // Stale callback must not supersede the admitted request.
        fixture.Track(fixture.Control.LastOperation);
        Assert.Equal(requests, fixture.Api.Requests.Count);
        fixture.Api.HoldList = false;
        fixture.Api.ReleaseList();
        fixture.Wait(busy);
        fixture.Lookup.SelectValue(13);
        WaitFilterCommit(fixture);
        AssertIds(fixture, 3, 5, 13, false);
        Assert.Equal(13, fixture.Api.Requests.Last().WarehouseId);
    });

    [Theory]
    [InlineData("Select")]
    [InlineData("Refresh")]
    [InlineData("New")]
    public void Focused_filter_commit_reserves_admission_before_dispatcher_yield(string command) => Run("WarehouseFilterLookup", fixture =>
    {
        var requests = fixture.Api.Requests.Count;
        var gets = fixture.Api.GetCalls;
        fixture.Lookup.SelectValue(13);
        var filter = fixture.Track(fixture.Control.LastOperation);
        Assert.False(filter.IsCompleted);
        if (command == "Select") fixture.Track(fixture.Control.SelectReceiptAsync(42, fixture.Token));
        else if (command == "Refresh") fixture.Control.RefreshButton.PerformClick();
        else fixture.Control.NewButton.PerformClick();
        Assert.Equal(gets, fixture.Api.GetCalls);
        Assert.Equal(requests, fixture.Api.Requests.Count);
        Assert.Equal(41L, fixture.Editor.Id);
        fixture.Wait(filter);
        Assert.Equal(13, fixture.Api.Requests.Last().WarehouseId);
        AssertIds(fixture, 3, 5, 13, false);
        // Once the committed filter finishes, a subsequent command can be admitted.
        fixture.Api.Current = GoodsReceiptLookupFixture.ReceiptClient.Receipt(42);
        fixture.Wait(fixture.Control.SelectReceiptAsync(42, fixture.Token));
        Assert.Equal(42L, fixture.Editor.Id);
    });

    [Theory]
    [InlineData("WarehouseLookup", false, true)]
    [InlineData("EmployeeLookup", false, true)]
    [InlineData("WarehouseLookup", true, true)]
    [InlineData("EmployeeLookup", true, true)]
    [InlineData("WarehouseLookup", false, false)]
    [InlineData("EmployeeLookup", false, false)]
    [InlineData("WarehouseLookup", true, false)]
    [InlineData("EmployeeLookup", true, false)]
    public void Save_or_post_resolves_header_before_deciding(string target, bool post, bool pointer) => Run(target, fixture =>
    {
        fixture.Editor.Load(GoodsReceiptLookupFixture.ReceiptClient.Receipt(line: true));
        fixture.Refocus();
        fixture.Driver.SelectRange(0, fixture.Driver.Text.Length);
        fixture.TypeText("G - Guitar");
        Assert.False(fixture.Editor.IsDirty);
        var button = post ? fixture.Control.PostButton : fixture.Control.SaveButton;
        Assert.True(button.Enabled);
        if (pointer)
        {
            InlineReceiptTestDriver.MouseDown(button);
            InlineReceiptTestDriver.MouseUp(button);
        }
        else button.PerformClick();
        fixture.Wait(fixture.Track(fixture.Control.LastOperation));
        if (!post)
        {
            Assert.NotNull(fixture.Api.LastUpdate);
            Assert.Equal(target == "WarehouseLookup" ? 13 : 3, fixture.Api.LastUpdate!.WarehouseId);
            Assert.Equal(target == "EmployeeLookup" ? 15 : 5, fixture.Api.LastUpdate.EmployeeId);
        }
        Assert.Equal(target == "WarehouseLookup" ? 13 : 3, fixture.Editor.WarehouseId);
        Assert.Equal(target == "EmployeeLookup" ? 15 : 5, fixture.Editor.EmployeeId);
        Assert.True(fixture.Editor.IsDirty);
        Assert.False(fixture.Lookup.HasPendingText);
        Assert.Equal(0, fixture.Api.PostCalls); // Dirty selection requires explicit Save before Post.
        Assert.False(fixture.Editor.RequiresPostOutcomeReload);
    });

    [Theory]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Save_lookup_resolution_cannot_cross_an_admitted_document_boundary(string target) => Run(target, fixture =>
    {
        fixture.Editor.Load(GoodsReceiptLookupFixture.ReceiptClient.Receipt(line: true));
        fixture.Refocus();
        fixture.Driver.SelectRange(0, fixture.Driver.Text.Length);
        fixture.TypeText("G - Guitar");
        fixture.Control.SaveButton.PerformClick();
        var save = fixture.Track(fixture.Control.LastOperation);
        fixture.Control.NewButton.PerformClick();
        fixture.Wait(save);
        Assert.Null(fixture.Editor.Id);
        Assert.Null(fixture.Api.LastUpdate);
        Assert.Empty(fixture.Editor.Lines);
        Assert.False(fixture.Editor.IsDirty);
        Assert.Equal(0, fixture.Api.PostCalls);
    });

    [Theory]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Allowed_query_only_back_cancels_requests_without_committing(string target) => Run(target, fixture =>
    {
        var old = fixture.Type('g');
        var response = fixture.AdmitSearch(0);
        Assert.True(fixture.Control.TryShowReceiptList());
        Assert.Equal("List", fixture.Control.WorkspaceMode.ToString());
        fixture.Source.Complete(response, new CatalogLookupDto(98, "STALE", "Old detail result", true));
        fixture.Wait(old);
        Assert.DoesNotContain(fixture.LookupViewModel.Items, option => option.Id == 98);
        Assert.False(fixture.Lookup.HasPendingText);
        AssertIds(fixture, 3, 5, null, false);
        var reads = fixture.Api.GetCalls;
        fixture.Control.OpenCurrentReceiptButton.PerformClick();
        Assert.Equal("Detail", fixture.Control.WorkspaceMode.ToString());
        Assert.Equal(reads, fixture.Api.GetCalls);
    });

    [Theory]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Back_click_with_non_first_highlight_preserves_native_range_when_rejected(string target) => Run(target, fixture =>
    {
        fixture.Editor.Note = "unsaved";
        fixture.TypeText("gui");
        fixture.Driver.Key(Keys.F4);
        fixture.Driver.Key(Keys.Home);
        fixture.Driver.Key(Keys.Down);
        Assert.NotEqual(fixture.Lookup.SelectedValue, Assert.IsType<CatalogLookupOption>(fixture.Lookup.HighlightedItem).Id);
        fixture.Driver.SelectRange(1, 1);
        InlineReceiptTestDriver.MouseDown(fixture.Control.BackToReceiptsButton);
        InlineReceiptTestDriver.MouseUp(fixture.Control.BackToReceiptsButton);
        Pending(fixture, "gui", 1, 1);
        Assert.Equal("Detail", fixture.Control.WorkspaceMode.ToString());
        AssertIds(fixture, 3, 5, null, true);
    });

    [Fact]
    public void Back_rejects_busy_and_uncertain_post_and_does_not_restore_denied_rows() => Run("WarehouseLookup", fixture =>
    {
        fixture.Api.HoldList = true;
        var load = fixture.Track(fixture.List.RefreshAsync(fixture.Token));
        Assert.False(fixture.Control.TryShowReceiptList());
        Assert.Equal("Detail", fixture.Control.WorkspaceMode.ToString());
        fixture.Api.HoldList = false;
        fixture.Api.ReleaseList(); fixture.Wait(load);
        fixture.WarehouseSource.Hold = fixture.EmployeeSource.Hold = false;
        fixture.Wait(fixture.Track(fixture.Editor.PostAsync(fixture.Token)));
        Assert.True(fixture.Editor.RequiresPostOutcomeReload);
        Assert.False(fixture.Control.TryShowReceiptList());
        fixture.Api.DenyList = true;
        fixture.Wait(fixture.Track(fixture.Control.ActivateAsync(fixture.Token)));
        Assert.False(fixture.Control.TryShowReceiptList()); // Uncertain Post lock survives denial.
        Assert.Equal(1, fixture.Control.ReceiptGrid.RowsCount);
        Assert.False(fixture.Control.OpenCurrentReceiptButton.Visible);
    });

    [Fact]
    public void Clean_denied_list_back_cannot_restore_cached_receipt() => Run("WarehouseLookup", fixture =>
    {
        fixture.WarehouseSource.Hold = fixture.EmployeeSource.Hold = false;
        fixture.Api.DenyList = true;
        fixture.Wait(fixture.Track(fixture.Control.ActivateAsync(fixture.Token)));
        Assert.True(fixture.Control.TryShowReceiptList());
        Assert.Equal(1, fixture.Control.ReceiptGrid.RowsCount);
        Assert.Empty(fixture.Editor.Lines);
        Assert.Null(fixture.Editor.Id);
        Assert.False(fixture.Control.OpenCurrentReceiptButton.Visible);
    });

    [Fact]
    public void Allowed_product_query_only_back_cancels_late_response_without_reloading_document() => Run("WarehouseLookup", fixture =>
    {
        fixture.Editor.Load(GoodsReceiptLookupFixture.ReceiptClient.Receipt(line: true));
        var line = fixture.Editor.Lines[0];
        var grid = fixture.Control.LineGrid;
        grid.Focus(); grid.Selection.Focus(new SourceGrid.Position(1, 1), true);
        GoodsReceiptInlineEditorTests.Key(Keys.F2);
        var driver = new NativeLookupTypingDriver(fixture.Control.ProductLookup);
        fixture.ProductSource.Hold = true;
        driver.SelectRange(0, driver.Text.Length);
        var pending = fixture.Type(driver, 'g');
        fixture.CompleteDelay(0);
        fixture.PumpUntil(() => fixture.ProductSource.Count > 0, "Product query admission");
        Assert.True(fixture.Control.TryShowReceiptList());
        fixture.ProductSource.Complete(0, new CatalogLookupDto(98, "STALE", "Old Product query", true));
        fixture.Wait(pending);
        Assert.DoesNotContain(fixture.Editor.ProductOptions.Items, option => option.Id == 98);
        Assert.Equal(7, line.ProductId);
        Assert.False(fixture.Editor.IsDirty);
        var reads = fixture.Api.GetCalls;
        fixture.Wait(fixture.Track(fixture.Control.SelectReceiptAsync(41, fixture.Token)));
        Assert.Same(line, fixture.Editor.Lines[0]);
        Assert.Equal(reads, fixture.Api.GetCalls);
        Assert.False(fixture.Control.ProductLookup.HasPendingText);
    });

    [Theory]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Pending_header_query_survives_three_two_one_column_reflow(string target) => Run(target, fixture =>
    {
        fixture.TypeText("gui");
        fixture.Driver.SelectRange(1, 1);
        var window = fixture.Driver.FocusedHandle;
        var metadata = Assert.Single(Descendants(fixture.Control).OfType<TableLayoutPanel>(), panel => panel.Name == "ReceiptMetadata");
        Assert.Equal(3, metadata.ColumnCount);
        fixture.Form.ClientSize = new Size(800, 700);
        System.Windows.Forms.Application.DoEvents();
        Assert.Equal(2, metadata.ColumnCount);
        Pending(fixture, "gui", 1, 1);
        fixture.Form.ClientSize = new Size(480, 700);
        System.Windows.Forms.Application.DoEvents();
        Assert.Equal(1, metadata.ColumnCount);
        Pending(fixture, "gui", 1, 1);
        Assert.Equal(window, fixture.Driver.FocusedHandle);
        Assert.Equal(1, metadata.GetColumnSpan(fixture.Control.NoteEditor.Parent!));
    });

    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(control => new[] { control }.Concat(Descendants(control)));

    private void Run(string target, Action<GoodsReceiptLookupFixture> scenario)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            ExceptionDispatchInfo? failure = null;
            using var loop = new ApplicationContext();
            using var startup = new Control();
            startup.CreateControl();
            startup.BeginInvoke(new Action(() =>
            {
                try
                {
                    using (var fixture = new GoodsReceiptLookupFixture(target, token))
                    {
                        guard.Attach(fixture.Control.WarehouseFilterLookup.ResultsGrid);
                        guard.Attach(fixture.Control.WarehouseLookup.ResultsGrid);
                        guard.Attach(fixture.Control.EmployeeLookup.ResultsGrid);
                        NativeLookupTypingDriver.Activate(fixture.Form);
                        fixture.Driver.AssertFocused();
                        _output.WriteLine(_gate.Observe(fixture));
                        _output.WriteLine("target=" + target + "; hosted DPI=" + fixture.Form.DeviceDpi + "; x86=" + (IntPtr.Size == 4));
                        scenario(fixture);
                    }
                }
                catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
                finally { loop.ExitThread(); }
            }));
            System.Windows.Forms.Application.Run(loop);
            _output.WriteLine("Owned source/debounce/control operations drained; fixture disposed; guard exceptions=" + guard.Exceptions.Count);
            Assert.Empty(guard.Exceptions);
            failure?.Throw();
        }, TimeSpan.FromSeconds(30));
    }

    private static void Pending(GoodsReceiptLookupFixture fixture, string text, int start, int length = 0)
    {
        Assert.Equal(text, fixture.Driver.Text);
        Assert.Equal(start, fixture.Driver.SelectionStart);
        Assert.Equal(length, fixture.Driver.SelectionLength);
        Assert.True(fixture.Lookup.HasPendingText);
    }
    private static void WaitFilterCommit(GoodsReceiptLookupFixture fixture)
    {
        // Only filter commits start a list operation. Header selection is synchronous;
        // LastOperation there still refers to the deliberately held query being canceled.
        if (fixture.Target == "WarehouseFilterLookup") fixture.Wait(fixture.Track(fixture.Control.LastOperation));
    }
    private static void AssertIds(GoodsReceiptLookupFixture fixture, int? warehouse, int? employee, int? filter, bool dirty)
    {
        Assert.Equal(warehouse, fixture.Editor.WarehouseId);
        Assert.Equal(employee, fixture.Editor.EmployeeId);
        Assert.Equal(filter, fixture.Control.WarehouseFilterLookup.SelectedValue as int?);
        Assert.Equal(dirty, fixture.Editor.IsDirty);
    }
}
