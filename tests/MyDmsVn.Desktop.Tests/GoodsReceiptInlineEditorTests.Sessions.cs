using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed partial class GoodsReceiptInlineEditorTests
{
    [Fact]
    public void State_refresh_does_not_replace_the_active_cell()
    {
        SessionTest(host =>
        {
            host.Begin(1, 2);
            var cell = host.Grid[1, 2];
            Type("3.12500");
            var edit = host.NativeTextBox();
            edit.Select(2, 3);
            host.Editor.Note = "unrelated state";
            host.Editor.Lines[1].UnitCost = 9m;
            host.Editor.Lines[0].Quantity = 6m; // Conflicting projection waits for the boundary.
            Assert.Same(cell, host.Grid[1, 2]);
            Assert.Equal("3.12500", edit.Text);
            Assert.Equal(2, edit.SelectionStart);
            Assert.Equal(3, edit.SelectionLength);
            Assert.True(edit.Focused);
            Assert.Equal(18m, host.Grid[2, 4].Value);
            var changes = 0;
            host.Editor.Lines[0].PropertyChanged += (_, e) => { if (e.PropertyName == "Quantity") changes++; };
            Assert.True(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.Equal(3.125m, host.Editor.Lines[0].Quantity);
            Assert.Equal(1, changes);
            Assert.Equal(12.5m, host.Grid[1, 4].Value);
            Assert.Same(cell, host.Grid[1, 2]);
        });
    }

    [Fact]
    public void Pending_cell_edits_participate_in_dirty_command_guards()
    {
        SessionTest(host =>
        {
            host.Begin(1, 2);
            Type("7.25000");
            Assert.False(host.Editor.IsDirty);
            Assert.False(host.Adapter.TryFinishEdit(ReceiptEditIntent.NewReceipt));
            Assert.False(host.Adapter.TryFinishEdit(ReceiptEditIntent.SelectReceipt));
            Assert.False(host.Adapter.TryFinishEdit(ReceiptEditIntent.Refresh));
            Assert.Equal("7.25000", host.NativeTextBox().Text);
            Assert.Equal(2.5m, host.Editor.Lines[0].Quantity);
            Assert.True(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.True(host.Editor.IsDirty);
            Assert.Equal(7.25m, host.Editor.Lines[0].Quantity);
            host.Begin(1, 2);
            Type("8.00001");
            Assert.False(host.Adapter.TryFinishEdit(ReceiptEditIntent.Post));
            Assert.Equal("8.00001", host.NativeTextBox().Text);
            host.Adapter.CancelActiveEdit();
            Assert.Equal(7.25m, host.Editor.Lines[0].Quantity);
        });
    }

    [Fact]
    public void Unsaved_zero_id_lines_are_distinguished_by_reference()
    {
        SessionTest(host =>
        {
            host.Editor.Load(null);
            var first = host.Editor.AddLine();
            var second = host.Editor.AddLine();
            Assert.Equal(0, first.Id);
            Assert.Equal(0, second.Id);
            host.Begin(2, 2);
            Type("4.75");
            Assert.True(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.Equal(1m, first.Quantity);
            Assert.Equal(4.75m, second.Quantity);
            host.Begin(1, 2);
            Type("99");
            var oldCell = host.Grid[1, 2];
            var oldContext = new SourceGrid.CellContext(host.Grid, new SourceGrid.Position(1, 2), oldCell);
            host.Editor.RemoveLine(first);
            var replacement = host.Editor.AddLine();
            // A stale controller notification must not apply to the row that inherited its ordinal.
            oldCell.Controller.OnValueChanged(oldContext, EventArgs.Empty);
            Assert.Equal(4.75m, second.Quantity);
            Assert.Equal(1m, replacement.Quantity);
            host.Begin(1, 2);
            Type("6");
            host.Adapter.CancelActiveEdit();
            Assert.Equal(4.75m, second.Quantity);
            host.Begin(2, 2);
            Type("88");
            var removedCell = host.Grid[2, 2];
            var removedContext = host.Context(2, 2);
            host.Editor.RemoveLine(replacement);
            var sameOrdinal = host.Editor.AddLine();
            removedCell.Controller.OnValueChanged(removedContext, EventArgs.Empty);
            Assert.Equal(1m, sameOrdinal.Quantity);
            Assert.Equal(1m, host.Grid[2, 2].Value);
        });
    }

    [Fact]
    public void Reloaded_line_references_replace_old_edit_targets_safely()
    {
        SessionTest(host =>
        {
            host.Begin(2, 2);
            Type("77");
            var oldLine = host.Editor.Lines[1];
            var oldCell = host.Grid[2, 2];
            var oldContext = new SourceGrid.CellContext(host.Grid, new SourceGrid.Position(2, 2), oldCell);
            host.Editor.Load(SessionHost.Receipt());
            host.Adapter.ResetDocument(host.Editor.Lines);
            Assert.NotSame(oldLine, host.Editor.Lines[1]);
            Assert.Equal(new SourceGrid.Position(2, 2), host.Grid.Selection.ActivePosition);
            oldCell.Controller.OnValueChanged(oldContext, EventArgs.Empty);
            Assert.Equal(2m, host.Editor.Lines[1].Quantity);
            var changes = 0;
            host.Editor.Lines[1].PropertyChanged += (_, e) => { if (e.PropertyName == "Quantity") changes++; };
            host.Begin(2, 2);
            Type("3");
            Assert.True(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.Equal(3m, host.Editor.Lines[1].Quantity);
            Assert.Equal(2m, oldLine.Quantity);
            Assert.Equal(1, changes);
        });
    }

    [Fact]
    public void A_rebound_inactive_option_cannot_be_committed_from_the_old_shared_lookup_snapshot()
    {
        SessionTest(host =>
        {
            host.Begin(1, 1);
            var editor = (MyDmsVn.BootstrapSourceGrid.Editors.BootstrapLookupBoxEditor)host.Grid[1, 1].Editor;
            host.Editor.Lines[1].SetProduct(8, "P8 - Inactive", false);
            host.Editor.ProductOptions.TrackLines(host.Editor.Lines);
            Assert.True(editor.BootstrapControl.SelectValue(8)); // Presentation snapshot is deliberately deferred while editing.
            Assert.False(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.Equal(7, host.Editor.Lines[0].ProductId);
            Assert.NotNull(host.Adapter.EditError);
            host.Adapter.CancelActiveEdit();
            Assert.Equal(7, host.Grid[1, 1].Value);
        });
    }

    [Fact]
    public void A_saved_new_line_uses_its_own_selection_fallback_instead_of_a_previous_persisted_id()
    {
        SessionTest(host =>
        {
            host.Grid.Selection.Focus(new SourceGrid.Position(1, 2), true);
            host.Editor.AddLine();
            host.Begin(3, 2);
            Type("12.5");
            Assert.True(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            host.Editor.Load(SessionHost.Receipt(true));
            host.Adapter.ResetDocument(host.Editor.Lines);
            Assert.Equal(103, host.Editor.Lines[2].Id);
            Assert.Equal(new SourceGrid.Position(3, 2), host.Grid.Selection.ActivePosition);
            host.Begin(3, 2);
            Type("13");
            Assert.True(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.Equal(13m, host.Editor.Lines[2].Quantity);
            Assert.Equal(2.5m, host.Editor.Lines[0].Quantity);
        });
    }

    [Fact]
    public void A_cell_value_notification_without_validated_input_cannot_mutate_the_model()
    {
        SessionTest(host =>
        {
            host.Begin(1, 2);
            Type("8.00001");
            host.Editor.Lines[0].Quantity = 6m;
            host.Grid[1, 2].Value = 99m;
            Assert.Equal(6m, host.Editor.Lines[0].Quantity);
            Assert.False(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.Equal(6m, host.Editor.Lines[0].Quantity);
            host.Adapter.CancelActiveEdit();
            Assert.Equal(6m, host.Grid[1, 2].Value);
        });
    }

    [Fact]
    public void Invalid_candidate_does_not_roll_back_a_concurrent_model_update()
    {
        SessionTest(host =>
        {
            host.Begin(1, 2);
            Type("8.00001");
            host.Editor.Lines[0].Quantity = 6m;
            var changes = 0;
            host.Editor.Lines[0].PropertyChanged += (_, e) => { if (e.PropertyName == "Quantity") changes++; };
            Assert.False(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.Equal(6m, host.Editor.Lines[0].Quantity);
            Assert.Equal(0, changes);
            Assert.Equal("8.00001", host.NativeTextBox().Text);
            Assert.NotNull(host.Adapter.EditError);
            host.Adapter.CancelActiveEdit();
            Assert.Equal(6m, host.Grid[1, 2].Value);
        });
    }

    [Fact]
    public void Appending_a_line_preserves_the_active_cell_until_the_edit_boundary()
    {
        SessionTest(host =>
        {
            host.Begin(1, 2);
            Type("5.12500");
            var cell = host.Grid[1, 2];
            var added = host.Editor.AddLine();
            host.Editor.Note = "notification during append";
            Assert.Same(cell, host.Grid[1, 2]);
            Assert.Equal("5.12500", host.NativeTextBox().Text);
            Assert.True(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.Equal(5.125m, host.Editor.Lines[0].Quantity);
            Assert.Equal(1m, added.Quantity);
            Assert.Equal(4, host.Grid.RowsCount);
        });
    }

    [Fact]
    public void Query_only_navigation_cancels_without_selecting_a_highlight_or_dirtying_the_receipt()
    {
        SessionTest(host =>
        {
            host.Begin(1, 1);
            Type("pending");
            Assert.False(host.Editor.IsDirty);
            Assert.True(host.Adapter.TryFinishEdit(ReceiptEditIntent.NewReceipt));
            Assert.False(host.Editor.IsDirty);
            Assert.Equal(7, host.Editor.Lines[0].ProductId);
            host.Begin(1, 1);
            var editor = (MyDmsVn.BootstrapSourceGrid.Editors.BootstrapLookupBoxEditor)host.Grid[1, 1].Editor;
            Assert.True(editor.BootstrapControl.SelectValue(8));
            Assert.True(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.Equal(8, Assert.IsType<int>(host.Grid[1, 1].Value));
            Assert.Equal("P8 - Product", host.Grid[1, 1].DisplayText);
            Assert.Equal(8, host.Editor.Lines[0].ProductId);
        });
    }

    [Fact]
    public void Editability_is_checked_at_start_and_commit_and_disposal_releases_sessions()
    {
        SessionTest(host =>
        {
            host.Begin(1, 2);
            Type("10");
            host.CanEdit = false;
            Assert.False(host.Adapter.TryFinishEdit(ReceiptEditIntent.Save));
            Assert.Equal(2.5m, host.Editor.Lines[0].Quantity);
            host.Adapter.RefreshEditability();
            host.Context(1, 2).StartEdit();
            Assert.False(host.Grid[1, 2].Editor.IsEditing);
            host.CanEdit = true;
            host.Adapter.RefreshEditability();
            host.Begin(1, 1);
            Key(Keys.F4);
            var editor = (MyDmsVn.BootstrapSourceGrid.Editors.BootstrapLookupBoxEditor)host.Grid[1, 1].Editor;
            Assert.True(editor.BootstrapControl.IsDropDownOpen);
            var popup = editor.BootstrapControl.ResultsGrid;
            host.Adapter.Dispose();
            Assert.False(host.Grid.IsDisposed);
            Assert.True(editor.Control.IsDisposed);
            Assert.True(popup.IsDisposed);
            var cell = host.Grid[1, 2];
            host.Editor.Lines[0].Quantity = 8m;
            Assert.Equal(2.5m, cell.Value); // No disposed adapter subscription survives.
        });
    }

    private static void SessionTest(Action<SessionHost> action) => StaTest.Run(_ =>
    {
        using var guard = new WinFormsTestGuard();
        using var host = new SessionHost();
        action(host);
        Assert.Empty(guard.Exceptions);
    }, TimeSpan.FromSeconds(15));

    internal sealed class SessionHost : IDisposable
    {
        internal readonly GoodsReceiptEditorViewModel Editor;
        internal readonly GoodsReceiptLinesGrid Grid = new GoodsReceiptLinesGrid { Dock = DockStyle.Fill };
        internal readonly GoodsReceiptLinesGridAdapter Adapter;
        private readonly Form _form = new Form { ClientSize = new Size(850, 260), ShowInTaskbar = false };
        internal IntPtr FormHandle => _form.Handle;
        internal bool CanEdit = true;
        internal readonly SessionLookup ProductSource = new SessionLookup(CatalogKind.Product);
        internal SessionHost()
        {
            var dispatcher = new ImmediateUiDispatcher();
            var messenger = new WeakReferenceMessenger();
            var notifications = new DesktopNotificationCenter();
            var delay = new SessionDelay();
            Editor = new GoodsReceiptEditorViewModel(new SessionApi(),
                new CatalogLookupViewModel(new SessionLookup(CatalogKind.Warehouse), messenger, dispatcher, notifications, delay),
                new CatalogLookupViewModel(new SessionLookup(CatalogKind.Employee), messenger, dispatcher, notifications, delay),
                new GoodsReceiptProductOptionsViewModel(ProductSource, messenger, dispatcher, notifications, delay),
                dispatcher, notifications);
            Editor.Load(Receipt());
            Editor.ProductOptions.ActivateAsync(CancellationToken.None).GetAwaiter().GetResult();
            Adapter = new GoodsReceiptLinesGridAdapter(Grid, Editor, () => CanEdit);
            _form.Controls.Add(Grid);
            _form.Show();
            Pump();
        }
        internal static GoodsReceiptDto Receipt(bool savedNewLine = false, string status = "Draft")
        {
            var lines = new List<GoodsReceiptLineDto> { new GoodsReceiptLineDto(101, 1, 7, 2.5m, 4m, "P7 - Product", true),
                new GoodsReceiptLineDto(102, 2, 8, 2m, 3m, "P8 - Product", true) };
            if (savedNewLine) lines.Add(new GoodsReceiptLineDto(103, 3, 9, 12.5m, 0m, "P9 - New", true));
            return new GoodsReceiptDto(41, "GR41", new DateTime(2026, 10, 20), 3, 5, status, "", null, "AQIDBAUGBwg=", lines);
        }
        internal SourceGrid.CellContext Context(int row, int column) => new SourceGrid.CellContext(Grid, new SourceGrid.Position(row, column), Grid[row, column]);
        internal void Begin(int row, int column)
        {
            Assert.True(Grid.Focus());
            Assert.True(Grid.Selection.Focus(new SourceGrid.Position(row, column), true));
            var context = Context(row, column);
            context.Cell.View.Measure(context, Size.Empty);
            Key(Keys.F2);
            Assert.True(context.IsEditing(), "Native F2 must start the real shared editor.");
        }
        internal TextBox NativeTextBox()
        {
            var control = Control.FromHandle(GetFocus());
            return Assert.IsType<TextBox>(control);
        }
        public void Dispose() { Adapter.Dispose(); _form.Dispose(); Editor.Dispose(); }
    }
    private sealed class SessionDelay : IAsyncDelay
    {
        public Task DelayAsync(TimeSpan interval, CancellationToken token) => Task.CompletedTask;
    }
    internal sealed class SessionLookup : ICatalogLookupSource
    {
        internal SessionLookup(CatalogKind kind) { Kind = kind; }
        public CatalogKind Kind { get; }
        internal string ProductName = "Product";
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) =>
            Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(new[] { new CatalogLookupDto(7, "P7", ProductName, true), new CatalogLookupDto(8, "P8", "Product", true) }));
        public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class SessionApi : IGoodsReceiptApiClient
    {
        public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(GoodsReceiptListRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(UpdateGoodsReceiptDraftRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(PostGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
    }
}
