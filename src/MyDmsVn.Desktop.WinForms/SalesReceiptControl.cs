using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms.Features.Security;
using Grid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.WinForms;

public interface ISalesReceiptControlFactory { Control Create(); }

public sealed class SalesReceiptControl : UserControl, IActivatableDesktopControl, ISalesCommandBufferAdmission
{
    private readonly IDesktopSession _session;
    private readonly long _sessionVersion;
    private readonly IUiDispatcher _ui;
    private readonly IPermissionApiClient _permissions;
    private readonly IProductApiClient _productApi;
    private readonly Dictionary<Guid, long> _productSelections = new Dictionary<Guid, long>();
    private CancellationTokenSource _documentCancellation = new CancellationTokenSource();
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly SalesReceiptListViewModel _list;
    private readonly SalesReceiptLinesGridAdapter _inline;
    private readonly List<LookupBinding> _lookups = new List<LookupBinding>();
    private readonly CatalogLookupViewModel _products;
    private readonly Panel _master = new Panel { Dock = DockStyle.Fill };
    private readonly Panel _detail = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Label _title = new Label { Name = "CompactHeading", AutoSize = true, Text = "Sales receipts" };
    private readonly Label _state = new Label { Name = "CompactSecondary", AutoSize = true };
    private readonly ErrorProvider _errors;
    private readonly int _uiThread;
    private bool _applying;
    private bool _disposed;
    private bool _hasDetail;
    private bool _write;
    private bool _post;
    private bool _read;
    private bool _criteriaDirty;
    private string? _message;
    private long _boundary;
    private long _activation;
    private IReadOnlyList<SalesReceiptListRow>? _projectedItems;
    private IReadOnlyList<long> _saleIds = Array.Empty<long>();

    public SalesReceiptControl(ISalesReceiptApiClient api, IDesktopSession session, IUiDispatcher ui,
        ISalesReceiptPreviewCalculator calculator, Func<CatalogKind, CatalogLookupViewModel> lookupFactory, IPermissionApiClient permissions, IProductApiClient productApi)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session)); _sessionVersion = session.Version;
        _ui = ui ?? throw new ArgumentNullException(nameof(ui)); _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _productApi = productApi ?? throw new ArgumentNullException(nameof(productApi));
        if (lookupFactory == null) throw new ArgumentNullException(nameof(lookupFactory));
        _uiThread = Thread.CurrentThread.ManagedThreadId;
        Editor = new SalesReceiptEditorViewModel(api, session, ui, calculator, this);
        _list = new SalesReceiptListViewModel(api, session, ui);
        _products = lookupFactory(CatalogKind.Product);
        Dock = DockStyle.Fill; AutoScaleMode = AutoScaleMode.Dpi; Padding = new Padding(16);
        AutoScroll = true; AutoScrollMinSize = new Size(480, 0);
        CustomerLookup = BindLookup("Customer", lookupFactory(CatalogKind.Customer), false, option => Editor.SetCustomer(option.Id, option.DisplayName, option.IsAvailableForNewSelection));
        WarehouseLookup = BindLookup("Warehouse", lookupFactory(CatalogKind.Warehouse), false, option => Editor.SetWarehouse(option.Id, option.DisplayName, option.IsAvailableForNewSelection));
        EmployeeLookup = BindLookup("Employee", lookupFactory(CatalogKind.Employee), false, option => Editor.SetEmployee(option.Id, option.DisplayName, option.IsAvailableForNewSelection));
        CustomerFilterLookup = BindLookup("Filter by customer", lookupFactory(CatalogKind.Customer), true, null);
        WarehouseFilterLookup = BindLookup("Filter by warehouse", lookupFactory(CatalogKind.Warehouse), true, null);
        EmployeeFilterLookup = BindLookup("Filter by employee", lookupFactory(CatalogKind.Employee), true, null);
        ProductFilterLookup = BindLookup("Filter by product", lookupFactory(CatalogKind.Product), true, null);
        SaleDateEditor = new BootstrapDatePicker { AccessibleName = "Sale date", Dock = DockStyle.Top };
        NoteEditor = new BootstrapTextBox { AccessibleName = "Note", Dock = DockStyle.Top };
        SearchBox = new BootstrapTextBox { AccessibleName = "Search sales receipts", Width = 220 };
        FromDateEditor = new DateTimePicker { AccessibleName = "From date", ShowCheckBox = true, Checked = false, Format = DateTimePickerFormat.Short, Width = 130 };
        ToDateEditor = new DateTimePicker { AccessibleName = "To date", ShowCheckBox = true, Checked = false, Format = DateTimePickerFormat.Short, Width = 130 };
        StatusFilter = new ComboBox { AccessibleName = "Filter by status", DropDownStyle = ComboBoxStyle.DropDownList, Width = 105 };
        StatusFilter.Items.AddRange(new object[] { "All", "Draft", "Posted" }); StatusFilter.SelectedIndex = 0;
        PageSizeEditor = new NumericUpDown { AccessibleName = "Page size", Minimum = 1, Maximum = 200, Value = 50, Width = 65 };
        ReceiptGrid = new ReceiptResultsGrid { AccessibleName = "Sales receipt results", Dock = DockStyle.Fill, CausesValidation = false };
        ((ReceiptResultsGrid)ReceiptGrid).ConfigureRowActions(row => row > 0 && row <= _saleIds.Count ? (object)_saleIds[row - 1] : null,
            row => row > 0 && row <= _saleIds.Count ? Convert.ToString(ReceiptGrid[row, 0].Value) ?? "" : "",
            row => { if (row > 0 && row <= _saleIds.Count) SetOperation(SelectReceiptAsync(_saleIds[row - 1], CancellationToken.None)); });
        ((ReceiptResultsGrid)ReceiptGrid).AdmitPointerNavigation = CanLeaveDetail;
        ReceiptGrid.SizeChanged += (_, __) => SizeListColumns();
        ReceiptGrid.FontChanged += (_, __) => SizeListColumns();
        ReceiptGrid.DoubleClick += (_, __) => OpenSelected();
        ReceiptGrid.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = true; } };
        LineGrid = new SalesReceiptLinesGrid { Dock = DockStyle.Fill, AccessibleName = "Sales receipt lines", CausesValidation = false };
        _inline = new SalesReceiptLinesGridAdapter((SalesReceiptLinesGrid)LineGrid, Editor, _products, () => WorkspaceMode == ReceiptWorkspaceMode.Detail && !Busy);
        LineGrid.Selection.FocusRowEntered += (_, __) => { if (!_applying && Current) { Editor.SelectLine(_inline.SelectedLine); ApplyState(); } };
        _inline.EditStateChanged += InlineChanged; _inline.LookupOperationStarted += SetOperation; _inline.ProductCommitted += ProductCommitted;
        NewButton = Button("New", BeginNew);
        SaveButton = Button("Save", () => SetOperation(SaveAsync()));
        PostButton = Button("Post", () => SetOperation(PostAsync())); PostButton.Outline = false;
        ReloadButton = Button("Reload", () => SetOperation(ReloadAsync()));
        RefreshButton = Button("Refresh", () => SetOperation(RefreshAsync()));
        BackToReceiptsButton = Button("Back to sales", () => TryShowReceiptList());
        OpenCurrentReceiptButton = Button("Open current sale", () => TryShowReceiptDetail());
        AddLineButton = Button("Add line", () => { if (Editor.CanEdit && !Busy && _inline.TryFinishEdit(ReceiptEditIntent.AddLine)) Editor.AddLine(); });
        RemoveLineButton = Button("Remove line", () => { var row = _inline.SelectedLine; if (Editor.CanEdit && !Busy && row != null && _inline.TryFinishEdit(ReceiptEditIntent.RemoveLine)) Editor.RemoveLine(row); });
        FirstPageButton = Button("First", () => SetOperation(_list.FirstAsync(_lifetime.Token)));
        PreviousPageButton = Button("Previous", () => SetOperation(_list.PreviousAsync(_lifetime.Token)));
        NextPageButton = Button("Next", () => SetOperation(_list.NextAsync(_lifetime.Token)));
        ApplyFiltersButton = Button("Apply filters", () => SetOperation(ApplyFiltersAsync()));
        OutcomeSaleIdEditor = new TextBox { AccessibleName = "Reviewed saved sale ID", Width = 120 };
        ReviewCreateButton = Button("Review saved sale", () => { if (long.TryParse(OutcomeSaleIdEditor.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0) SetOperation(Editor.ResolveCreateOutcomeAsync(id, _lifetime.Token)); });
        TotalLabel = Summary("Sales total"); CostLabel = Summary("Posted total cost"); ProfitLabel = Summary("Posted gross profit");
        LineCostLabel = Summary("Selected line posted cost"); StockLabel = Summary("Stock availability");
        OperationStatusLabel = new Label { Name = "CompactSecondary", AutoSize = true, AccessibleName = "Sales operation status" };
        AppliedCriteriaLabel = new Label { AutoSize = true, AccessibleName = "Applied sales filters" };
        _errors = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };
        CreateLayout(); BusinessCompactAppearance.Apply(this);
        SaleDateEditor.ValueChanged += (_, __) => { if (!_applying && CanEditHeader) Editor.SaleDate = SaleDateEditor.Value; };
        NoteEditor.TextChanged += (_, __) => { if (!_applying && CanEditHeader) Editor.Note = NoteEditor.Text; };
        SearchBox.TextChanged += PendingFilterChanged; FromDateEditor.ValueChanged += PendingFilterChanged;
        ToDateEditor.ValueChanged += PendingFilterChanged; StatusFilter.SelectedValueChanged += PendingFilterChanged; PageSizeEditor.ValueChanged += PendingFilterChanged;
        Editor.PropertyChanged += StateChanged; _list.PropertyChanged += StateChanged; _session.SessionChanged += SessionChanged;
        ApplyState();
    }

    public SalesReceiptEditorViewModel Editor { get; }
    public Grid LineGrid { get; }
    public Grid ReceiptGrid { get; }
    public BootstrapLookupBox CustomerLookup { get; }
    public BootstrapLookupBox WarehouseLookup { get; }
    public BootstrapLookupBox EmployeeLookup { get; }
    public BootstrapLookupBox ProductLookup => _inline.ProductControl;
    public BootstrapFormattedTextBox QuantityEditor => _inline.QuantityControl;
    public BootstrapFormattedTextBox UnitPriceEditor => _inline.PriceControl;
    public BootstrapLookupBox CustomerFilterLookup { get; }
    public BootstrapLookupBox WarehouseFilterLookup { get; }
    public BootstrapLookupBox EmployeeFilterLookup { get; }
    public BootstrapLookupBox ProductFilterLookup { get; }
    public BootstrapDatePicker SaleDateEditor { get; }
    public BootstrapTextBox NoteEditor { get; }
    public BootstrapTextBox SearchBox { get; }
    public DateTimePicker FromDateEditor { get; }
    public DateTimePicker ToDateEditor { get; }
    public ComboBox StatusFilter { get; }
    public NumericUpDown PageSizeEditor { get; }
    public BootstrapButton NewButton { get; }
    public BootstrapButton SaveButton { get; }
    public BootstrapButton PostButton { get; }
    public BootstrapButton ReloadButton { get; }
    public BootstrapButton RefreshButton { get; }
    public BootstrapButton BackToReceiptsButton { get; }
    public BootstrapButton OpenCurrentReceiptButton { get; }
    public BootstrapButton AddLineButton { get; }
    public BootstrapButton RemoveLineButton { get; }
    public BootstrapButton FirstPageButton { get; }
    public BootstrapButton PreviousPageButton { get; }
    public BootstrapButton NextPageButton { get; }
    public BootstrapButton ApplyFiltersButton { get; }
    public TextBox OutcomeSaleIdEditor { get; }
    public BootstrapButton ReviewCreateButton { get; }
    public Label TotalLabel { get; }
    public Label CostLabel { get; }
    public Label ProfitLabel { get; }
    public Label LineCostLabel { get; }
    public Label StockLabel { get; }
    public Label OperationStatusLabel { get; }
    public Label AppliedCriteriaLabel { get; }
    public Task LastOperation { get; private set; } = Task.CompletedTask;
    internal ReceiptWorkspaceMode WorkspaceMode { get; private set; } = ReceiptWorkspaceMode.List;
    private bool Current => !_disposed && _session.IsAuthenticated && _session.Version == _sessionVersion;
    private bool Busy => !Current || Editor.IsBusy || _list.IsBusy;
    private bool CanEditHeader => Current && !Busy && WorkspaceMode == ReceiptWorkspaceMode.Detail && Editor.CanEdit;

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        if (!Current || Busy) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var generation = _boundary; var activation = ++_activation;
        var read = await _permissions.CheckAsync("Sales.Receipts.Read", linked.Token);
        var write = await _permissions.CheckAsync("Sales.Receipts.Write", linked.Token);
        var post = await _permissions.CheckAsync("Sales.Receipts.Post", linked.Token);
        if (!Current || linked.IsCancellationRequested || generation != _boundary || activation != _activation) return;
        _read = read.IsSuccess && read.Data!.IsAllowed; _write = write.IsSuccess && write.Data!.IsAllowed; _post = post.IsSuccess && post.Data!.IsAllowed;
        if (!_read) { DenyProtectedState(); return; }
        Editor.SetAccess(_read, _write, _post); _list.SetAccess(true); ApplyState();
        // Existing Detail activation refreshes permissions without admitting a document/query boundary.
        if (WorkspaceMode == ReceiptWorkspaceMode.Detail) return;
        await Task.WhenAll(_lookups.Select(binding => binding.ViewModel.ActivateAsync(linked.Token)).Concat(new[] { _products.ActivateAsync(linked.Token), _list.RefreshAsync(linked.Token) }));
    }
    public async Task SelectReceiptAsync(long id, CancellationToken token)
    {
        if (!_read || Busy || !CanLeaveDetail()) return;
        CancelBoundary(); _list.Select(id); await Editor.LoadAsync(id, token);
        if (!Current || Editor.SaleId != id || Editor.IsAccessDenied) return;
        _hasDetail = true; WorkspaceMode = ReceiptWorkspaceMode.Detail; ApplyState();
    }
    public Task SaveAsync() => Current && !Busy && _read && !Editor.IsAccessDenied ? Editor.SaveAsync(_lifetime.Token) : Task.CompletedTask;
    public Task PostAsync() => Current && !Busy && _read && !Editor.IsAccessDenied ? Editor.PostAsync(_lifetime.Token) : Task.CompletedTask;
    private Task ReloadAsync()
    {
        if (Busy || Editor.RequiresCreateOutcomeReview) return Task.CompletedTask;
        if (!Editor.RequiresPostOutcomeReload && !CanLeaveDetail()) return Task.CompletedTask;
        CancelBoundary(); return Editor.ReloadOutcomeAsync(_lifetime.Token);
    }
    private Task RefreshAsync() => WorkspaceMode == ReceiptWorkspaceMode.Detail ? ReloadAsync() : _list.RefreshAsync(_lifetime.Token);
    public Task<bool> TryCommitPendingEditsAsync(SalesCommandIntent intent, CancellationToken token)
    {
        var result = false;
        return CommitOnUi();
        async Task<bool> CommitOnUi()
        {
            await _ui.InvokeAsync(() =>
            {
                if (!Current || Busy || !_read || Editor.IsAccessDenied || WorkspaceMode != ReceiptWorkspaceMode.Detail || token.IsCancellationRequested) return;
                var pending = _lookups.FirstOrDefault(binding => !binding.Filter && binding.Control.HasPendingText);
                if (pending != null) { _message = "Choose a current header lookup result or cancel the pending query."; pending.Control.Focus(); ApplyState(); return; }
                result = _inline.TryFinishEdit(intent == SalesCommandIntent.Save ? ReceiptEditIntent.Save : ReceiptEditIntent.Post);
                // The adapter owns inline validation and clears it on cancel/commit.
                // Do not overwrite independent operation feedback with a second error copy.
                if (!result) _inline.FocusActiveEdit();
                if (result) InvalidateProductRequests();
                ApplyState();
            }, token);
            return result;
        }
    }
    private bool CanLeaveDetail()
    {
        if (Busy || Editor.RequiresPostOutcomeReload || Editor.RequiresCreateOutcomeReview) return false;
        if (WorkspaceMode == ReceiptWorkspaceMode.List) return true;
        if (Editor.IsDirty || _inline.HasPendingDataEdit)
        { _message = "Save or cancel changes before leaving the sale."; ApplyState(); return false; }
        return _inline.TryFinishEdit(ReceiptEditIntent.ReturnToList);
    }
    internal bool TryShowReceiptList()
    {
        if (!CanLeaveDetail()) return false;
        CancelBoundary(); WorkspaceMode = ReceiptWorkspaceMode.List; ApplyState(); return true;
    }
    internal bool TryShowReceiptDetail()
    {
        if (Busy || !_read || !_hasDetail || Editor.IsAccessDenied) return false;
        WorkspaceMode = ReceiptWorkspaceMode.Detail; ApplyState(); return true;
    }
    private void BeginNew()
    {
        if (!_write || !CanLeaveDetail()) return;
        CancelBoundary(); if (!Editor.NewDraft()) return;
        _hasDetail = true; WorkspaceMode = ReceiptWorkspaceMode.Detail; ApplyState();
    }
    private void CancelBoundary()
    {
        ++_boundary; InvalidateProductRequests();
        _inline.CancelActiveEdit(); _products.Cancel();
        foreach (var binding in _lookups) { binding.ViewModel.Cancel(); binding.Control.CancelPendingEdit(); }
        _message = null;
    }
    private Task ApplyFiltersAsync()
    {
        if (Busy || !_read || _lookups.Any(binding => binding.Filter && binding.Control.HasPendingText)) return Task.CompletedTask;
        _criteriaDirty = false;
        return _list.SetCriteriaAsync(new SalesReceiptListRequest(FromDateEditor.Checked ? FromDateEditor.Value.Date : (DateTime?)null,
            ToDateEditor.Checked ? ToDateEditor.Value.Date : (DateTime?)null, Selected(CustomerFilterLookup), Selected(WarehouseFilterLookup), Selected(EmployeeFilterLookup),
            StatusFilter.SelectedIndex == 0 ? null : (string)StatusFilter.SelectedItem, Selected(ProductFilterLookup), SearchBox.Text, (int)PageSizeEditor.Value), _lifetime.Token);
    }
    private static int? Selected(BootstrapLookupBox lookup) => lookup.SelectedValue is int id && id > 0 ? id : (int?)null;
    private void OpenSelected()
    {
        var row = ReceiptGrid.Selection.ActivePosition.Row;
        if (row > 0 && row <= _saleIds.Count) SetOperation(SelectReceiptAsync(_saleIds[row - 1], CancellationToken.None));
    }
    private BootstrapLookupBox BindLookup(string name, CatalogLookupViewModel vm, bool filter, Action<CatalogLookupOption>? commit)
    {
        var lookup = new AccessibleLookupBox { AccessibleName = name, DisplayMember = nameof(CatalogLookupOption.DisplayName), ValueMember = nameof(CatalogLookupOption.Id), Width = 220, Dock = DockStyle.Top, SearchDebounceMilliseconds = 0 };
        lookup.SearchMembers.Add(nameof(CatalogLookupOption.DisplayName));
        var binding = new LookupBinding(lookup, vm, filter); _lookups.Add(binding);
        lookup.TextChanged += (_, __) => { if (!_applying && Current && !Busy && lookup.Enabled && (filter || CanEditHeader) && lookup.HasPendingText) SetOperation(vm.SetSearch(lookup.Text)); };
        lookup.SelectedValueChanged += (_, __) =>
        {
            if (_applying || !Current || Busy || !lookup.Enabled || lookup.HasPendingText) return;
            if (!filter && CanEditHeader && lookup.SelectedItem is CatalogLookupOption option && (option.IsAvailableForNewSelection || option.Id == vm.SelectedId))
            { commit!(option); vm.SetSelection(option.Id, option.DisplayName, option.IsAvailableForNewSelection); }
            else if (filter) { _criteriaDirty = true; ApplyState(); }
        };
        vm.PropertyChanged += StateChanged;
        return lookup;
    }
    private void SynchronizeLookup(BootstrapLookupBox lookup, int id, string? label, bool active)
    {
        var binding = _lookups.Single(item => item.Control == lookup);
        if (!binding.HasSnapshot || binding.BoundId != id || binding.BoundLabel != label || binding.BoundActive != active)
        {
            binding.HasSnapshot = true; binding.BoundId = id; binding.BoundLabel = label; binding.BoundActive = active;
            binding.ViewModel.SetSelection(id > 0 ? (int?)id : null, label, active);
        }
        lookup.DataSource = binding.ViewModel.Items.ToArray();
        var selection = id > 0 ? (int?)id : null;
        if (!Equals(lookup.SelectedValue, selection)) lookup.SelectValue(selection);
    }
    private void ApplyState()
    {
        if (_disposed || _applying) return;
        _applying = true;
        try
        {
            var detail = WorkspaceMode == ReceiptWorkspaceMode.Detail;
            _master.Visible = !detail; _detail.Visible = detail; _state.Visible = detail;
            _title.Text = detail ? (Editor.SaleId > 0 ? Editor.SaleNumber : "New sale") : "Sales receipts";
            _state.Text = Editor.RequiresPostOutcomeReload ? "Reload required" : Editor.RequiresCreateOutcomeReview ? "Review Create outcome" : Busy ? "Working…" : Editor.IsDirty || _inline.HasPendingDataEdit ? "Unsaved changes" : Editor.SaleId > 0 ? "Saved · " + Editor.Status : "Not saved";
            BackToReceiptsButton.Visible = detail; OpenCurrentReceiptButton.Visible = !detail && _hasDetail;
            SaveButton.Visible = PostButton.Visible = ReloadButton.Visible = detail;
            SaveButton.Enabled = Editor.CanEdit && !Busy; PostButton.Enabled = _post && _read && !Editor.IsAccessDenied && !Busy && Editor.Status == "Draft" && !Editor.RequiresPostOutcomeReload && !Editor.RequiresCreateOutcomeReview;
            ReloadButton.Enabled = _read && !Busy && !Editor.RequiresCreateOutcomeReview;
            NewButton.Enabled = _write && _read && !Editor.IsAccessDenied && !Busy && !Editor.RequiresPostOutcomeReload && !Editor.RequiresCreateOutcomeReview;
            RefreshButton.Enabled = _read && !Busy;
            AddLineButton.Enabled = RemoveLineButton.Enabled = Editor.CanEdit && !Busy;
            foreach (var lookup in new[] { CustomerLookup, WarehouseLookup, EmployeeLookup }) lookup.Enabled = CanEditHeader;
            SaleDateEditor.Enabled = NoteEditor.Enabled = CanEditHeader;
            SearchBox.Enabled = FromDateEditor.Enabled = ToDateEditor.Enabled = StatusFilter.Enabled = PageSizeEditor.Enabled = _read && !Busy;
            OutcomeSaleIdEditor.Enabled = _read && !Busy;
            OpenCurrentReceiptButton.Enabled = _read && !Busy && _hasDetail && !Editor.IsAccessDenied;
            SaleDateEditor.Value = Editor.SaleDate; NoteEditor.Text = Editor.Note ?? "";
            SynchronizeLookup(CustomerLookup, Editor.CustomerId, Editor.CustomerDisplayName, Editor.CustomerIsActive);
            SynchronizeLookup(WarehouseLookup, Editor.WarehouseId, Editor.WarehouseDisplayName, Editor.WarehouseIsActive);
            SynchronizeLookup(EmployeeLookup, Editor.EmployeeId, Editor.EmployeeDisplayName, Editor.EmployeeIsActive);
            foreach (var binding in _lookups.Where(item => item.Filter)) { binding.Control.Enabled = _read && !Busy; binding.Control.DataSource = binding.ViewModel.Items.Where(item => item.IsAvailableForNewSelection).ToArray(); }
            _inline.RefreshEditability();
            TotalLabel.Text = "Total: " + (Editor.TotalAmount.HasValue ? Money(Editor.TotalAmount) : "Invalid amount"); CostLabel.Text = "Posted cost: " + Money(Editor.TotalCost); ProfitLabel.Text = "Gross profit: " + Money(Editor.GrossProfit);
            var selected = _inline.SelectedLine;
            LineCostLabel.Text = "Selected line cost: " + Money(selected?.CostAmount) + " · Unit cost: " + (selected?.PostedUnitCost?.ToString("0.########") ?? "Not posted");
            StockLabel.Text = "Stock availability is checked at Post.";
            OutcomeSaleIdEditor.Visible = ReviewCreateButton.Visible = Editor.RequiresCreateOutcomeReview;
            ReviewCreateButton.Enabled = _read && !Busy;
            OperationStatusLabel.Text = _inline.EditError ?? Editor.Error?.Message ?? _list.Error?.Message ?? _message ?? "Ready";
            _errors.SetError(LineGrid, _inline.EditError ?? "");
            AppliedCriteriaLabel.Text = _list.AppliedCriteria == null ? "No applied results" : !_criteriaDirty && ReferenceEquals(_list.AppliedCriteria, _list.PendingCriteria) ? "Applied filters · " + _list.Items.Count + " sales" : "Pending criteria · previous results retained";
            FirstPageButton.Enabled = _read && !Busy; PreviousPageButton.Enabled = !_criteriaDirty && _list.CanPrevious && !Busy; NextPageButton.Enabled = !_criteriaDirty && _list.CanNext && !Busy;
            ApplyFiltersButton.Enabled = _read && !Busy;
            if (!Current || Editor.IsAccessDenied)
            {
                foreach (var binding in _lookups) { binding.Control.DataSource = Array.Empty<CatalogLookupOption>(); binding.Control.CancelPendingEdit(); }
            }
            ProjectList();
        }
        finally { _applying = false; }
    }
    private static string Money(decimal? amount) => amount?.ToString("N4", CultureInfo.CurrentCulture) ?? "Not posted";
    private void ProjectList()
    {
        if (!_read || !Current || Editor.IsAccessDenied)
        {
            _projectedItems = null; _saleIds = Array.Empty<long>(); ReceiptGrid.Redim(0, 0); return;
        }
        if (ReferenceEquals(_projectedItems, _list.Items)) return;
        _projectedItems = _list.Items;
        var ids = _list.Items.Select(row => row.SaleId).ToArray();
        _saleIds = ids;
        ReceiptGrid.Redim(ids.Length + 1, 8); ReceiptGrid.FixedRows = 1;
        var headers = new[] { "Sale", "Date", "Customer", "Warehouse", "Employee", "Status", "Lines", "Total" };
        for (var c = 0; c < headers.Length; c++) { ReceiptGrid[0, c] = new SourceGrid.Cells.ColumnHeader(headers[c]); }
        SizeListColumns();
        for (var r = 0; r < ids.Length; r++)
        {
            var item = _list.Items[r]; var values = new object[] { item.SaleNumber, item.SaleDate.ToShortDateString(), item.CustomerDisplayName, item.WarehouseDisplayName, item.EmployeeDisplayName, item.Status, item.LineCount, item.TotalAmount };
            for (var c = 0; c < values.Length; c++) ReceiptGrid[r + 1, c] = new SourceGrid.Cells.Cell(values[c]);
        }
        var selected = Array.FindIndex(ids, id => id == _list.SelectedSaleId);
        if (selected >= 0) ((ReceiptResultsGrid)ReceiptGrid).SynchronizeRow(selected + 1, 0);
    }
    private void SizeListColumns()
    {
        if (_disposed || ReceiptGrid.ColumnsCount != 8) return;
        int Scale(int value) => BusinessCompactAppearance.Scale(ReceiptGrid, value);
        var totalWidth = Math.Max(Scale(150), TextRenderer.MeasureText(SalesContractLimits.MaximumAmount.ToString("0.0000", CultureInfo.CurrentCulture), ReceiptGrid.Font).Width + Scale(12));
        var widths = new[] { Scale(120), Scale(85), Scale(125), Scale(95), Scale(95), Scale(65), Scale(57), totalWidth };
        widths[2] = Math.Max(widths[2], ReceiptGrid.ClientSize.Width - widths.Where((width, index) => index != 2).Sum() - SystemInformation.VerticalScrollBarWidth);
        for (var column = 0; column < widths.Length; column++) ReceiptGrid.Columns[column].Width = widths[column];
    }
    private void CreateLayout()
    {
        var commands = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        commands.Controls.AddRange(new Control[] { BackToReceiptsButton, OpenCurrentReceiptButton, _title, NewButton, RefreshButton, SaveButton, PostButton, ReloadButton, _state, OutcomeSaleIdEditor, ReviewCreateButton });
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        foreach (var field in new[] { Field("Search", SearchBox), Field("Customer", CustomerFilterLookup), Field("Warehouse", WarehouseFilterLookup), Field("Employee", EmployeeFilterLookup), Field("Product", ProductFilterLookup), Field("From", FromDateEditor), Field("To", ToDateEditor), Field("Status", StatusFilter), Field("Page size", PageSizeEditor) }) filters.Controls.Add(field);
        filters.Controls.Add(ApplyFiltersButton);
        var paging = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        paging.Controls.AddRange(new Control[] { FirstPageButton, PreviousPageButton, NextPageButton, AppliedCriteriaLabel });
        _master.Controls.Add(ReceiptGrid); _master.Controls.Add(paging); _master.Controls.Add(filters);
        var metadata = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        metadata.Controls.AddRange(new Control[] { Field("Sale date", SaleDateEditor), Field("Customer", CustomerLookup), Field("Warehouse", WarehouseLookup), Field("Employee", EmployeeLookup), Field("Note", NoteEditor) });
        var lineCommands = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true }; lineCommands.Controls.AddRange(new Control[] { AddLineButton, RemoveLineButton });
        var summary = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true }; summary.Controls.AddRange(new Control[] { TotalLabel, CostLabel, ProfitLabel, LineCostLabel, StockLabel });
        _detail.Controls.Add(LineGrid); _detail.Controls.Add(lineCommands); _detail.Controls.Add(metadata); _detail.Controls.Add(summary);
        var status = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true }; status.Controls.Add(OperationStatusLabel);
        Controls.Add(_master); Controls.Add(_detail); Controls.Add(commands); Controls.Add(status);
    }
    private static Control Field(string label, Control editor)
    {
        var field = new Panel { Width = editor.Width > 150 ? editor.Width + 8 : 180, Height = 65, Margin = new Padding(0, 4, 12, 4) };
        editor.Dock = DockStyle.Top;
        var caption = new Label { AutoSize = true, Text = label, Dock = DockStyle.Top };
        field.Controls.Add(editor); field.Controls.Add(caption); return field;
    }
    private static Label Summary(string name) => new Label { AutoSize = true, AccessibleName = name, Margin = new Padding(4, 8, 12, 8) };
    private ReceiptCommandButton Button(string text, Action action)
    {
        var button = new ReceiptCommandButton { Text = text, AccessibleName = text, AutoSize = true, CausesValidation = false, Outline = true };
        button.RetainEditorFocus = () => _inline.HasPendingEdit || _lookups.Any(binding => !binding.Filter && binding.Control.HasPendingText);
        button.Click += (_, __) => { if (Current && button.Enabled) action(); }; return button;
    }
    private void InvalidateProductRequests()
    {
        _documentCancellation.Cancel(); _documentCancellation.Dispose();
        _documentCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _productSelections.Clear();
    }
    private void ProductCommitted(SalesReceiptLineViewModel line)
    {
        _productSelections.TryGetValue(line.ClientKey, out var generation);
        _productSelections[line.ClientKey] = ++generation;
        SetOperation(ResolveProductAsync(line, line.ProductId, generation, _boundary, Editor.SaleId, Editor.Version, _documentCancellation.Token));
    }
    private async Task ResolveProductAsync(SalesReceiptLineViewModel line, int productId, long generation, long boundary, long saleId, string version, CancellationToken token)
    {
        var response = await _productApi.GetAsync(productId, token).ConfigureAwait(false);
        await _ui.InvokeAsync(() =>
        {
            if (!Current || token.IsCancellationRequested || boundary != _boundary || saleId != Editor.SaleId || version != Editor.Version || !CanEditHeader ||
                !Editor.Lines.Contains(line) || line.ProductId != productId ||
                !_productSelections.TryGetValue(line.ClientKey, out var current) || current != generation) return;
            if (response.IsSuccess && response.Data!.Id == productId)
            {
                var product = response.Data;
                line.SetProduct(product.Id, product.Code + " - " + product.Name, product.IsActive, product.Unit);
            }
            else { _message = "Product Unit could not be loaded. Save reloads the authoritative Unit snapshot."; ApplyState(); }
        }, CancellationToken.None).ConfigureAwait(false);
    }
    private void PendingFilterChanged(object? sender, EventArgs args) { if (!_applying && Current && _read && !Busy) { _criteriaDirty = true; ApplyState(); } }
    private void InlineChanged(object? sender, EventArgs e) => ApplyState();
    private void StateChanged(object? sender, PropertyChangedEventArgs e) => Dispatch(() =>
    {
        if (ReferenceEquals(sender, _list) && _list.IsAccessDenied && _read) DenyProtectedState();
        else ApplyState();
    });
    private void DenyProtectedState()
    {
        // Flip admission before notifications to avoid recursive denial propagation.
        _read = _write = _post = false;
        CancelBoundary(); _hasDetail = false;
        Editor.SetAccess(false, false, false); _list.SetAccess(false); ApplyState();
    }
    private void Dispatch(Action action)
    {
        if (_disposed) return;
        if (Thread.CurrentThread.ManagedThreadId != _uiThread)
        { if (IsHandleCreated) BeginInvoke(new Action(() => { if (!_disposed) action(); })); }
        else action();
    }
    private void SessionChanged(object? sender, EventArgs e)
    {
        _lifetime.Cancel();
        Dispatch(() => { CancelBoundary(); _read = _write = _post = false; ApplyState(); });
    }
    private void SetOperation(Task task) { LastOperation = Task.WhenAll(LastOperation, Observe(task)); }
    private async Task Observe(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception) { if (Current) { _message = "The operation failed. Reload before continuing."; ApplyState(); } }
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S)) { SetOperation(SaveAsync()); return true; }
        if (keyData == (Keys.Alt | Keys.Left)) { TryShowReceiptList(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true; _lifetime.Cancel(); _documentCancellation.Cancel(); ++_boundary;
            _session.SessionChanged -= SessionChanged; Editor.PropertyChanged -= StateChanged; _list.PropertyChanged -= StateChanged;
            _inline.EditStateChanged -= InlineChanged; _inline.LookupOperationStarted -= SetOperation; _inline.ProductCommitted -= ProductCommitted; _inline.Dispose(); _products.Dispose();
            foreach (var binding in _lookups) { binding.ViewModel.PropertyChanged -= StateChanged; binding.ViewModel.Dispose(); }
            _errors.Dispose(); Editor.Dispose(); _list.Dispose(); _lifetime.Dispose(); _documentCancellation.Dispose();
        }
        base.Dispose(disposing);
    }
    private sealed class LookupBinding
    {
        internal LookupBinding(BootstrapLookupBox control, CatalogLookupViewModel vm, bool filter) { Control = control; ViewModel = vm; Filter = filter; }
        internal readonly BootstrapLookupBox Control; internal readonly CatalogLookupViewModel ViewModel; internal readonly bool Filter;
        internal bool HasSnapshot; internal int BoundId; internal string? BoundLabel; internal bool BoundActive;
    }
}
