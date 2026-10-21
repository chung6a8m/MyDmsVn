using System;
using System.Drawing;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;

namespace MyDmsVn.Desktop.WinForms;

public sealed partial class GoodsReceiptControl
{
    private SplitContainer _workspaceSplit = null!;
    private FlowLayoutPanel _workspaceCommands = null!;
    private TableLayoutPanel _workspaceFooter = null!;
    private Label _workspaceTitle = null!;
    private Label? _compactTitle;
    private Label _savedState = null!;
    private Label _lineCount = null!;
    private Label _totalCaption = null!;
    private Action _layoutReceipt = () => { };
    private bool _hasDetail;
    private Point _listScroll;
    internal ReceiptWorkspaceMode WorkspaceMode { get; private set; } = ReceiptWorkspaceMode.List;
    public BootstrapButton BackToReceiptsButton { get; private set; } = null!;
    public BootstrapButton OpenCurrentReceiptButton { get; private set; } = null!;

    internal bool TryShowReceiptList()
    {
        // Navigation admission precedes all native focus validation. Posted inspection is
        // permitted; uncertain Post recovery and a rejected dirty/raw edit remain authoritative.
        if (IsOperationBusy || _editor.RequiresPostOutcomeReload) return false;
        if (WorkspaceMode == ReceiptWorkspaceMode.List) return true;
        if (!CanAbandonEditor(ReceiptEditIntent.ReturnToList)) return false;
        CancelEditorLookupEdits();
        WorkspaceMode = ReceiptWorkspaceMode.List;
        ApplyPresentationState();
        ReceiptGrid.CustomScrollPosition = _listScroll;
        return true;
    }

    internal bool TryShowReceiptDetail()
    {
        if (IsOperationBusy || _editor.RequiresPostOutcomeReload || _list.IsAccessDenied || _isAccessDeniedEditorLocked || !_hasDetail) return false;
        if (WorkspaceMode == ReceiptWorkspaceMode.Detail) return true;
        if (!CanAbandonEditor(ReceiptEditIntent.SelectReceipt)) return false;
        ShowReceiptDetail();
        return true;
    }

    private void ShowReceiptDetail()
    {
        if (WorkspaceMode == ReceiptWorkspaceMode.List) _listScroll = ReceiptGrid.CustomScrollPosition;
        _hasDetail = true;
        WorkspaceMode = ReceiptWorkspaceMode.Detail;
        ApplyPresentationState();
    }

    private void ApplyPresentationState()
    {
        if (_workspaceSplit == null || IsDisposed || Disposing) return;
        var detail = WorkspaceMode == ReceiptWorkspaceMode.Detail;
        _inline.RefreshEditability();
        if (_workspaceSplit.Panel1Collapsed != detail || _workspaceSplit.Panel2Collapsed == detail)
        {
            _workspaceSplit.SuspendLayout();
            _workspaceSplit.Panel1Collapsed = false;
            _workspaceSplit.Panel2Collapsed = false;
            _workspaceSplit.Panel1Collapsed = detail;
            _workspaceSplit.Panel2Collapsed = !detail;
            _workspaceSplit.ResumeLayout(true);
        }
        BackToReceiptsButton.Visible = detail;
        OpenCurrentReceiptButton.Visible = !detail && _hasDetail && !_list.IsAccessDenied && !_isAccessDeniedEditorLocked;
        OpenCurrentReceiptButton.Enabled = !IsOperationBusy && !_editor.RequiresPostOutcomeReload;
        SaveButton.Visible = PostButton.Visible = detail;
        TotalLabel.Visible = _totalCaption.Visible = detail;
        _compactTitle!.Visible = detail;
        _workspaceTitle.Text = detail ? "Goods receipt" : "Goods receipts";
        _savedState.Text = _editor.RequiresPostOutcomeReload ? "Reload required" : _editor.IsBusy ? "Working…" : _editor.IsDirty || _inline.HasPendingDataEdit ? "Unsaved changes" : _editor.Id.HasValue ? "Saved" : "Not saved";
        _lineCount.Text = _editor.Lines.Count + (_editor.Lines.Count == 1 ? " line" : " lines");
        _workspaceCommands.PerformLayout();
        _layoutReceipt();
    }

    private Control CreateLayout()
    {
        Padding = new Padding(16);
        AutoScroll = true;
        AutoScrollMinSize = new Size(480, 0);
        BackToReceiptsButton = CreateButton("Back to receipts", () => TryShowReceiptList());
        OpenCurrentReceiptButton = CreateButton("Open current receipt", () => TryShowReceiptDetail());
        _workspaceTitle = new Label { Name = "CompactHeading", AutoSize = true, Text = "Goods receipts" };
        _compactTitle = new Label { Name = "CompactSecondary", AutoSize = true };
        _workspaceCommands = new FlowLayoutPanel { Name = "ReceiptDocumentCommands", AutoSize = true, Dock = DockStyle.Top, WrapContents = true, TabIndex = 0, Padding = new Padding(0, 0, 0, 8) };
        _workspaceCommands.Controls.AddRange(new Control[] { BackToReceiptsButton, OpenCurrentReceiptButton, _workspaceTitle, _compactTitle, NewButton, RefreshButton, SaveButton, PostButton });
        BackToReceiptsButton.Outline = OpenCurrentReceiptButton.Outline = NewButton.Outline = RefreshButton.Outline = SaveButton.Outline = true;
        PostButton.Outline = false;
        BackToReceiptsButton.TabIndex = 0; OpenCurrentReceiptButton.TabIndex = 1;
        NewButton.TabIndex = 2; RefreshButton.TabIndex = 3; SaveButton.TabIndex = 4; PostButton.TabIndex = 5;

        var master = new Panel { Dock = DockStyle.Fill };
        var masterBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 0, 8, 8), TabIndex = 0 };
        SearchBox.Dock = WarehouseFilterLookup.Dock = StatusFilter.Dock = DockStyle.None;
        SearchBox.Width = WarehouseFilterLookup.Width = 220; StatusFilter.Width = 110;
        SearchBox.TabIndex = 0; WarehouseFilterLookup.TabIndex = 1; StatusFilter.TabIndex = 2;
        masterBar.Controls.AddRange(new Control[] { SearchBox, WarehouseFilterLookup, StatusFilter });
        ReceiptGrid.TabIndex = 1;
        var paging = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, TabIndex = 2 };
        paging.Controls.AddRange(new Control[] { PreviousPageButton, PageLabel, NextPageButton });
        master.Controls.Add(ReceiptGrid); master.Controls.Add(paging); master.Controls.Add(masterBar);

        var detail = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var content = new Panel { Dock = DockStyle.Fill };
        var metadata = new TableLayoutPanel { Name = "ReceiptMetadata", Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
        var fields = new[] { CompactField("Receipt date", ReceiptDateEditor, 0), CompactField("Warehouse", WarehouseLookup, 1), CompactField("Employee", EmployeeLookup, 2), CompactField("Note", NoteEditor, 3) };
        foreach (var field in fields) metadata.Controls.Add(field);
        _savedState = new Label { Name = "CompactSecondary", AutoSize = true };
        StatusLabel.BorderStyle = BorderStyle.FixedSingle;
        StatusLabel.Padding = new Padding(6, 3, 6, 3);
        var state = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 4, 0, 8), TabStop = false };
        state.Controls.AddRange(new Control[] { StatusLabel, _savedState });
        // Keep the opaque token synchronized for concurrency, outside the visible presentation.
        VersionLabel.Visible = false;
        var header = new Panel { Dock = DockStyle.Top, TabIndex = 0 };
        header.Controls.Add(metadata); header.Controls.Add(state);
        _lineCount = new Label { Name = "CompactSecondary", AutoSize = true, Margin = new Padding(12, 8, 12, 0) };
        var lineHeader = new FlowLayoutPanel { Name = "ReceiptLineCommands", Dock = DockStyle.Top, AutoSize = true, WrapContents = true, TabIndex = 2 };
        lineHeader.Controls.AddRange(new Control[] { new Label { Name = "CompactUser", Text = "Receipt lines", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, _lineCount, AddLineButton, RemoveLineButton });
        AddLineButton.Outline = RemoveLineButton.Outline = true;
        AddLineButton.TabIndex = 0; RemoveLineButton.TabIndex = 1;
        var totals = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        TotalLabel.Name = "CompactHeading";
        _totalCaption = new Label { Text = "Total", AutoSize = true, Margin = new Padding(8, 8, 8, 0) };
        totals.Controls.AddRange(new Control[] { TotalLabel, _totalCaption, BusyIndicator });
        var footer = _workspaceFooter = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 1, TabIndex = 3, Padding = new Padding(0, 8, 0, 0) };
        OperationStatusLabel.Name = "CompactSecondary";
        OperationStatusLabel.Margin = new Padding(0, 4, 0, 0);
        footer.Controls.Add(totals); footer.Controls.Add(OperationStatusLabel);
        LineGrid.TabIndex = 1;
        content.Controls.Add(LineGrid); content.Controls.Add(lineHeader); content.Controls.Add(header);
        detail.Controls.Add(content);
        var arranging = false;
        _layoutReceipt = () =>
        {
            if (arranging || detail.IsDisposed) return;
            arranging = true;
            try
            {
                var scale = new Func<int, int>(logical => BusinessCompactAppearance.Scale(this, logical));
                var available = detail.ClientSize.Width;
                var minimumHeight = 0;
                for (var pass = 0; pass < 3; pass++)
                {
                    var width = Math.Max(scale(400), available);
                    var columns = width >= scale(900) ? 3 : width >= scale(560) ? 2 : 1;
                    metadata.SuspendLayout();
                    metadata.ColumnCount = columns;
                    metadata.ColumnStyles.Clear();
                    for (var column = 0; column < columns; column++) metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
                    for (var index = 0; index < fields.Length; index++)
                    {
                        metadata.SetColumnSpan(fields[index], 1);
                        metadata.SetCellPosition(fields[index], new TableLayoutPanelCellPosition(index == 3 ? 0 : index % columns, index == 3 ? (int)Math.Ceiling(3d / columns) : index / columns));
                        fields[index].MinimumSize = new Size(0, scale(60));
                    }
                    metadata.SetColumnSpan(fields[3], columns);
                    metadata.ResumeLayout(true);
                    header.Width = metadata.Width = width;
                    header.Height = metadata.PreferredSize.Height + state.PreferredSize.Height;
                    OperationStatusLabel.MaximumSize = new Size(Math.Max(1, ClientSize.Width - Padding.Horizontal - scale(16)), 0);
                    minimumHeight = header.Height + lineHeader.PreferredSize.Height + scale(200);
                    available = detail.ClientSize.Width - (minimumHeight > detail.ClientSize.Height ? SystemInformation.VerticalScrollBarWidth : 0);
                }
                content.MinimumSize = new Size(scale(400), minimumHeight);
                detail.AutoScrollMinSize = content.MinimumSize;
                detail.PerformLayout();
            }
            finally { arranging = false; }
        };
        detail.SizeChanged += (_, __) => _layoutReceipt();
        header.FontChanged += (_, __) => _layoutReceipt();
        OperationStatusLabel.TextChanged += (_, __) => _layoutReceipt();
        _workspaceSplit = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1200, 600), SplitterDistance = 260, TabIndex = 1 };
        _workspaceSplit.Panel1.Controls.Add(master); _workspaceSplit.Panel2.Controls.Add(detail);
        _workspaceSplit.SizeChanged += (_, __) => _layoutReceipt();
        return _workspaceSplit;
    }
}
