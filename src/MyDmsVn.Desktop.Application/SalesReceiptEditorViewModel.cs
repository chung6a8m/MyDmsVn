using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application;

public sealed class SalesReceiptEditorViewModel : ObservableObject, IDisposable
{
    private readonly object _sync = new object();
    private readonly ISalesReceiptApiClient _api;
    private readonly IDesktopSession _session;
    private readonly IUiDispatcher _ui;
    private readonly ISalesReceiptPreviewCalculator _calculator;
    private readonly ISalesCommandBufferAdmission _buffers;
    private readonly ObservableCollection<SalesReceiptLineViewModel> _lines = new ObservableCollection<SalesReceiptLineViewModel>();
    private static readonly Regex LineFieldPattern = new Regex(@"^(?:request\.)?lines\[([0-9]+)\](?:\.(productId|qty|unitPrice|saleLineId))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly long _sessionVersion;
    private CancellationTokenSource? _active;
    private long _operation;
    private long _document;
    private Guid _editorIdentity = Guid.NewGuid();
    private long _editRevision;
    private bool _mutatingDraft;
    private WorkspaceSaveStatus _lastSaveStatus = WorkspaceSaveStatus.CanceledBeforeMutation;
    private long _lastSubmittedRevision;
    private bool _disposed;
    private bool _admitting;
    private bool _read;
    private bool _write;
    private bool _post;
    private bool _denied;
    private int _busy;
    private DateTime _saleDate = DateTime.Today;
    private int _customerId;
    private int _warehouseId;
    private int _employeeId;
    private string? _note;
    private PostRecovery? _postRecovery;

    public SalesReceiptEditorViewModel(ISalesReceiptApiClient api, IDesktopSession session, IUiDispatcher ui,
        ISalesReceiptPreviewCalculator calculator, ISalesCommandBufferAdmission buffers)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _sessionVersion = session.Version;
        _api = (api ?? throw new ArgumentNullException(nameof(api))).ForSession(_sessionVersion);
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _buffers = buffers ?? throw new ArgumentNullException(nameof(buffers));
        Lines = new ReadOnlyObservableCollection<SalesReceiptLineViewModel>(_lines);
        _session.SessionChanged += SessionChanged;
    }
    public ReadOnlyObservableCollection<SalesReceiptLineViewModel> Lines { get; }
    public SalesReceiptLineViewModel? SelectedLine { get; private set; }
    public long SaleId { get; private set; }
    public string SaleNumber { get; private set; } = "";
    public string Version { get; private set; } = "";
    public string Status { get; private set; } = "Draft";
    public DateTime? PostedAtUtc { get; private set; }
    public string? CustomerDisplayName { get; private set; }
    public string? WarehouseDisplayName { get; private set; }
    public string? EmployeeDisplayName { get; private set; }
    public bool CustomerIsActive { get; private set; } = true;
    public bool WarehouseIsActive { get; private set; } = true;
    public bool EmployeeIsActive { get; private set; } = true;
    public DateTime SaleDate { get => _saleDate; set { if (CanEdit && _saleDate != value.Date) { AdvanceDraftRevision(); _saleDate = value.Date; OnPropertyChanged(); Dirty(revisionAdvanced: true); } } }
    public int CustomerId { get => _customerId; set { if (CanEdit && _customerId != value) { AdvanceDraftRevision(); _customerId = value; CustomerDisplayName = null; CustomerIsActive = true; Dirty(revisionAdvanced: true); } } }
    public int WarehouseId { get => _warehouseId; set { if (CanEdit && _warehouseId != value) { AdvanceDraftRevision(); _warehouseId = value; WarehouseDisplayName = null; WarehouseIsActive = true; Dirty(revisionAdvanced: true); } } }
    public int EmployeeId { get => _employeeId; set { if (CanEdit && _employeeId != value) { AdvanceDraftRevision(); _employeeId = value; EmployeeDisplayName = null; EmployeeIsActive = true; Dirty(revisionAdvanced: true); } } }
    public string? Note { get => _note; set { if (CanEdit && _note != value) { AdvanceDraftRevision(); _note = value; OnPropertyChanged(); Dirty(revisionAdvanced: true); } } }
    public bool IsDirty { get; private set; }
    public Guid EditorIdentity => _editorIdentity;
    public long EditRevision => _editRevision;
    public bool IsMutationBusy => _busy == 2 || _admitting;
    public bool IsBusy => _busy != 0;
    public bool IsAccessDenied => !_read || _denied || !SessionCurrent;
    public bool RequiresCreateOutcomeReview { get; private set; }
    public bool RequiresPostOutcomeReload { get; private set; }
    private bool CanUseDraft => SessionCurrent && _read && !_denied && !IsBusy && Status == "Draft" &&
        !RequiresCreateOutcomeReview && !RequiresPostOutcomeReload;
    public bool CanEdit => CanUseDraft && _write && !_mutatingDraft;
    private bool CanCommand(SalesCommandIntent intent) => CanUseDraft && (intent == SalesCommandIntent.Save ? _write : _post);
    public decimal? TotalAmount
    {
        get
        {
            if (Status == "Posted") return _postedAmount;
            try { return _calculator.SumAmounts(_lines.Select(row => row.LineAmount ?? throw new ArgumentException("Invalid line amount."))); }
            catch (ArgumentException) { return null; }
            catch (OverflowException) { return null; }
        }
    }
    private decimal? _postedAmount;
    public decimal? TotalCost { get; private set; }
    public decimal? GrossProfit { get; private set; }
    public ApiError? Error { get; private set; }
    public System.Collections.Generic.IReadOnlyList<SalesReceiptValidationIssue> ValidationIssues { get; private set; } =
        Array.Empty<SalesReceiptValidationIssue>();
    private bool SessionCurrent => !_disposed && _session.IsAuthenticated && _session.Version == _sessionVersion;
    private bool CanRead => SessionCurrent && _read && !_denied;

    public bool SetCustomer(int id, string? displayName, bool isActive)
    {
        if (!CanEdit) return false;
        if (_customerId == id && CustomerDisplayName == displayName && CustomerIsActive == isActive) return true;
        AdvanceDraftRevision(); _customerId = id; CustomerDisplayName = displayName; CustomerIsActive = isActive; Dirty(revisionAdvanced: true); return true;
    }
    public bool SetWarehouse(int id, string? displayName, bool isActive)
    {
        if (!CanEdit) return false;
        if (_warehouseId == id && WarehouseDisplayName == displayName && WarehouseIsActive == isActive) return true;
        AdvanceDraftRevision(); _warehouseId = id; WarehouseDisplayName = displayName; WarehouseIsActive = isActive; Dirty(revisionAdvanced: true); return true;
    }
    public bool SetEmployee(int id, string? displayName, bool isActive)
    {
        if (!CanEdit) return false;
        if (_employeeId == id && EmployeeDisplayName == displayName && EmployeeIsActive == isActive) return true;
        AdvanceDraftRevision(); _employeeId = id; EmployeeDisplayName = displayName; EmployeeIsActive = isActive; Dirty(revisionAdvanced: true); return true;
    }

    public void SetAccess(bool read, bool write, bool post)
    {
        lock (_sync)
        {
            if (!SessionCurrent) return;
            _read = read; _write = write; _post = post; _denied = !read;
            if (!read) { Invalidate(); Scrub(); }
            Notify();
        }
    }
    public SalesReceiptLineViewModel? AddLine()
    {
        lock (_sync)
        {
            if (!CanEdit || _lines.Count >= SalesContractLimits.MaximumLines) return null;
            var row = CreateRow(); AdvanceDraftRevision(); _lines.Add(row); SelectedLine = row; Dirty(revisionAdvanced: true); return row;
        }
    }
    private SalesReceiptLineViewModel CreateRow()
    {
        SalesReceiptLineViewModel? row = null;
        row = new SalesReceiptLineViewModel(() => CanEdit && _lines.Contains(row!),
            AdvanceDraftRevision, field => Dirty(row, field, revisionAdvanced: true), _calculator);
        return row;
    }
    public bool RemoveLine(SalesReceiptLineViewModel line)
    {
        lock (_sync)
        {
            if (!CanEdit || !_lines.Contains(line)) return false;
            AdvanceDraftRevision(); _lines.Remove(line);
            SetValidationIssues(ValidationIssues.Where(issue => !ReferenceEquals(issue.Line, line)).ToArray());
            if (ReferenceEquals(SelectedLine, line)) SelectedLine = null;
            Dirty(revisionAdvanced: true); return true;
        }
    }
    public bool MoveLine(SalesReceiptLineViewModel line, int index)
    {
        lock (_sync)
        {
            if (!CanEdit || !_lines.Contains(line) || index < 0 || index >= _lines.Count) return false;
            AdvanceDraftRevision(); _lines.Move(_lines.IndexOf(line), index); Dirty(revisionAdvanced: true); return true;
        }
    }
    public void SelectLine(SalesReceiptLineViewModel? line)
    {
        if (CanRead && (line == null || _lines.Contains(line))) { SelectedLine = line; OnPropertyChanged(nameof(SelectedLine)); }
    }
    public bool NewDraft()
    {
        lock (_sync)
        {
            if (!SessionCurrent || !_read || !_write || _denied || _admitting || _busy == 2 || RequiresCreateOutcomeReview || RequiresPostOutcomeReload) return false;
            Invalidate(); ++_document; _editorIdentity = Guid.NewGuid(); ++_editRevision; Scrub(); Notify(); return true;
        }
    }
    public Task LoadAsync(long id, CancellationToken token = default)
    {
        lock (_sync)
        {
            if (!CanRead || _admitting || _busy == 2 || RequiresCreateOutcomeReview || RequiresPostOutcomeReload || id <= 0) return Task.CompletedTask;
            ++_document; _editorIdentity = Guid.NewGuid(); ++_editRevision;
            SetValidationIssues(Array.Empty<SalesReceiptValidationIssue>());
            return ReadAsync(id, false, false, token);
        }
    }
    public Task ReloadOutcomeAsync(CancellationToken token = default)
    {
        lock (_sync)
        {
            var recovery = _postRecovery;
            var id = RequiresPostOutcomeReload
                ? recovery != null && recovery.Document == _document && recovery.SessionVersion == _sessionVersion ? recovery.SaleId : 0
                : SaleId;
            return CanRead && !_admitting && !IsBusy && id > 0 && !RequiresCreateOutcomeReview
                ? ReadAsync(id, true, false, token) : Task.CompletedTask;
        }
    }
    public Task ResolveCreateOutcomeAsync(long reviewedId, CancellationToken token = default)
    {
        lock (_sync)
            return CanRead && !_admitting && !IsBusy && RequiresCreateOutcomeReview && reviewedId > 0
                ? ReadAsync(reviewedId, false, true, token) : Task.CompletedTask;
    }

    private async Task ReadAsync(long id, bool resolvePost, bool resolveCreate, CancellationToken token)
    {
        var operation = Begin(token, 1);
        try
        {
            var result = await _api.GetAsync(id, operation.Cancellation.Token).ConfigureAwait(false);
            await Publish(operation, () =>
            {
                if (result.IsSuccess && result.Data!.SaleId == id &&
                    (!resolvePost || _postRecovery?.KnownPosted == null || result.Data.Status == "Posted"))
                {
                    Apply(result.Data, null);
                    if (resolvePost) { RequiresPostOutcomeReload = false; _postRecovery = null; }
                    if (resolveCreate) RequiresCreateOutcomeReview = false;
                    Error = null;
                }
                else ErrorResult(result.Error ?? InvalidResponse());
                Notify();
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested) { }
        catch (Exception) { await Publish(operation, () => { Error = Unexpected(); Notify(); }).ConfigureAwait(false); }
        finally { await End(operation).ConfigureAwait(false); }
    }
    public Task SaveAsync(CancellationToken token = default, IWorkspaceFeedbackSink? feedback = null) => CommandAsync(SalesCommandIntent.Save, token, feedback);
    public Task PostAsync(CancellationToken token = default, IWorkspaceFeedbackSink? feedback = null) => CommandAsync(SalesCommandIntent.Post, token, feedback);
    public async Task<WorkspaceSaveOutcome> SaveForCloseAsync(CancellationToken token = default, IWorkspaceFeedbackSink? feedback = null)
    {
        var identity = EditorIdentity;
        var revision = EditRevision;
        _lastSaveStatus = WorkspaceSaveStatus.CanceledBeforeMutation;
        _lastSubmittedRevision = revision;
        await CommandAsync(SalesCommandIntent.Save, token, feedback).ConfigureAwait(false);
        return new WorkspaceSaveOutcome(_lastSaveStatus, identity, _lastSubmittedRevision);
    }

    private async Task CommandAsync(SalesCommandIntent intent, CancellationToken token, IWorkspaceFeedbackSink? feedback)
    {
        long document;
        long admissionOperation;
        lock (_sync)
        {
            if (_admitting || !CanCommand(intent)) { if (intent == SalesCommandIntent.Save) _lastSaveStatus = WorkspaceSaveStatus.Rejected; return; }
            _admitting = true; document = _document; admissionOperation = _operation;
        }
        Operation? operation = null;
        try
        {
            // Keep model mutation available while the control commits its raw buffers.
            token.ThrowIfCancellationRequested();
            if (!await _buffers.TryCommitPendingEditsAsync(intent, token).ConfigureAwait(false)) return;
            token.ThrowIfCancellationRequested();
            Task? work = null;
            await _ui.InvokeAsync(() =>
            {
                lock (_sync)
                {
                    token.ThrowIfCancellationRequested();
                    if (!CanCommand(intent) || _document != document || _operation != admissionOperation) return;
                    if (intent == SalesCommandIntent.Post && (SaleId <= 0 || IsDirty))
                    { Error = new ApiError(ApiStatusCode.Conflict, "SalesReceipt.SaveRequired", "Save pending changes before posting."); PublishFailure(feedback, Error); Notify(); return; }
                    if (intent == SalesCommandIntent.Save && SaleId > 0 && !IsDirty) return;
                    if (!ValidDraft()) { if (intent == SalesCommandIntent.Save) _lastSaveStatus = WorkspaceSaveStatus.Rejected; Error = new ApiError(ApiStatusCode.BadRequest, "SalesReceipt.InvalidDraft", "Resolve the sales header and line validation errors."); PublishFailure(feedback, Error); Notify(); return; }
                    operation = Begin(token, 2);
                    work = intent == SalesCommandIntent.Save ? SaveCore(operation, feedback) : PostCore(operation, feedback);
                }
            }, CancellationToken.None).ConfigureAwait(false);
            if (work != null) await work.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (operation != null) await Publish(operation, () => { Error = Unexpected(); PublishFailure(feedback, Error); Notify(); }).ConfigureAwait(false);
        }
        catch (Exception)
        {
            if (operation != null) await Publish(operation, () => { Error = Unexpected(); PublishFailure(feedback, Error); Notify(); }).ConfigureAwait(false);
            else await _ui.InvokeAsync(() =>
            {
                lock (_sync)
                    if (SessionCurrent && _document == document && _operation == admissionOperation) { Error = Unexpected(); PublishFailure(feedback, Error); Notify(); }
            }, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            if (operation != null) await End(operation).ConfigureAwait(false);
            lock (_sync) { _admitting = false; }
        }
    }
    private bool ValidDraft() => CustomerId > 0 && WarehouseId > 0 && EmployeeId > 0 && CustomerIsActive && WarehouseIsActive && EmployeeIsActive &&
        (Note?.Length ?? 0) <= SalesContractLimits.MaximumNoteLength && _lines.Count >= 1 && _lines.Count <= SalesContractLimits.MaximumLines &&
        _lines.All(row => row.ProductId > 0 && row.ProductIsActive && row.LineAmount.HasValue) && TotalAmount.HasValue;

    private async Task SaveCore(Operation operation, IWorkspaceFeedbackSink? feedback)
    {
        var rows = _lines.ToArray();
        var requestLines = rows.Select(row => new SaveSalesReceiptLineRequest(row.SaleLineId, row.ProductId, row.Quantity, row.UnitPrice, row.IsGift)).ToArray();
        var create = SaleId == 0;
        // Set before dispatch: canceled/failed/ambiguous Create cannot safely be replayed.
        if (create) RequiresCreateOutcomeReview = true;
        _lastSubmittedRevision = EditRevision;
        _lastSaveStatus = WorkspaceSaveStatus.Unknown;
        Notify();
        var result = create
            ? await _api.CreateDraftAsync(new SaveSalesReceiptRequest(SaleDate, CustomerId, WarehouseId, EmployeeId, Note, requestLines), operation.Cancellation.Token).ConfigureAwait(false)
            : await _api.UpdateDraftAsync(new UpdateSalesReceiptDraftRequest(SaleId, Version, SaleDate, CustomerId, WarehouseId, EmployeeId, Note, requestLines), operation.Cancellation.Token).ConfigureAwait(false);
        await Publish(operation, () =>
        {
            if (result.IsSuccess)
            {
                var dto = result.Data!;
                if (dto.Status != "Draft" || (!create && dto.SaleId != SaleId) || !Matches(dto, requestLines))
                { Error = InvalidResponse(); PublishFailure(feedback, Error); return; }
                Apply(dto, rows); RequiresCreateOutcomeReview = false; Error = null;
                _lastSaveStatus = WorkspaceSaveStatus.Saved;
                feedback?.Publish(new DesktopNotification(DesktopNotificationKind.Success, create ? "Sale created." : "Sale saved."));
            }
            else
            {
                _lastSaveStatus = KnownRejection(result.Error!) ? WorkspaceSaveStatus.Rejected : WorkspaceSaveStatus.Unknown;
                if (create && KnownRejection(result.Error!)) RequiresCreateOutcomeReview = false;
                if (!create && result.Error!.Status == ApiStatusCode.Conflict) BeginPostRecovery(SaleId);
                ErrorResult(result.Error!, rows);
                PublishFailure(feedback, Error);
            }
            Notify();
        }).ConfigureAwait(false);
    }
    private static bool Matches(SalesReceiptDto dto, SaveSalesReceiptLineRequest[] submitted)
    {
        var returned = dto.Lines.OrderBy(row => row.LineNo).ToArray();
        if (dto.SaleId <= 0 || string.IsNullOrWhiteSpace(dto.Version) || returned.Length != submitted.Length ||
            returned.Any(row => row.SaleLineId <= 0) || returned.Select(row => row.SaleLineId).Distinct().Count() != returned.Length) return false;
        return returned.Select((row, i) => row.LineNo == i + 1 && (submitted[i].SaleLineId == 0 || submitted[i].SaleLineId == row.SaleLineId) &&
            row.ProductId == submitted[i].ProductId && row.Qty == submitted[i].Qty && row.UnitPrice == submitted[i].UnitPrice && row.IsGift == submitted[i].IsGift).All(valid => valid);
    }
    private async Task PostCore(Operation operation, IWorkspaceFeedbackSink? feedback)
    {
        var id = SaleId;
        var version = Version;
        var rows = _lines.ToArray();
        BeginPostRecovery(id); Notify();
        var result = await _api.PostAsync(new PostSalesReceiptRequest(id, version), operation.Cancellation.Token).ConfigureAwait(false);
        var knownPosted = false;
        await Publish(operation, () =>
        {
            if (result.IsSuccess)
            {
                var dto = result.Data!;
                if (dto.SaleId != id || dto.Status != "Posted" || string.IsNullOrWhiteSpace(dto.Version)) { Error = InvalidResponse(); PublishFailure(feedback, Error); return; }
                Status = dto.Status; Version = dto.Version; PostedAtUtc = dto.PostedAtUtc;
                _postRecovery!.KnownPosted = dto;
                IsDirty = false; TotalCost = null; GrossProfit = null; _postedAmount = _calculator.SumAmounts(_lines.Select(row => row.LineAmount!.Value));
                knownPosted = true; Error = null;
                feedback?.Publish(new DesktopNotification(DesktopNotificationKind.Success, "Sale posted."));
            }
            else
            {
                if (KnownRejection(result.Error!) && result.Error!.Status != ApiStatusCode.Conflict)
                { RequiresPostOutcomeReload = false; _postRecovery = null; }
                ErrorResult(result.Error!, rows);
                PublishFailure(feedback, Error);
            }
            Notify();
        }).ConfigureAwait(false);
        if (!knownPosted || !Current(operation)) return;
        // Only this same document/session may issue a follow-on detail read.
        var detail = await _api.GetAsync(id, operation.Cancellation.Token).ConfigureAwait(false);
        await Publish(operation, () =>
        {
            if (detail.IsSuccess && detail.Data!.SaleId == id && detail.Data.Status == "Posted")
            { Apply(detail.Data, null); RequiresPostOutcomeReload = false; _postRecovery = null; Error = null; }
            else { ErrorResult(detail.Error ?? InvalidResponse()); PublishFailure(feedback, Error); }
            Notify();
        }).ConfigureAwait(false);
    }
    private static bool KnownRejection(ApiError error) => error.Status == ApiStatusCode.BadRequest || error.Status == ApiStatusCode.Forbidden ||
        error.Status == ApiStatusCode.NotFound || error.Status == ApiStatusCode.Conflict ||
        (error.Status == ApiStatusCode.Unauthorized && error.Code != "Auth.SessionChanged");
    private static void PublishFailure(IWorkspaceFeedbackSink? feedback, ApiError? error)
    {
        if (error != null) feedback?.Publish(new DesktopNotification(DesktopNotificationKind.Error, error.Message));
    }
    private void ErrorResult(ApiError error, SalesReceiptLineViewModel[]? submitted = null)
    {
        if (SalesReceiptListViewModel.IsAuthorization(error)) { _denied = true; Scrub(); }
        else SetValidationIssues(ProjectValidationIssues(error, submitted));
        Error = error;
    }
    private static IReadOnlyList<SalesReceiptValidationIssue> ProjectValidationIssues(
        ApiError error, SalesReceiptLineViewModel[]? submitted)
    {
        return error.Details.Select(detail =>
        {
            var field = detail.Field ?? string.Empty;
            var match = LineFieldPattern.Match(field);
            if (submitted != null && match.Success &&
                int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                index < submitted.Length)
            {
                var member = match.Groups[2].Value;
                var canonical = member.Equals("qty", StringComparison.OrdinalIgnoreCase) ? "qty" :
                    member.Equals("unitPrice", StringComparison.OrdinalIgnoreCase) ? "unitPrice" :
                    member.Equals("productId", StringComparison.OrdinalIgnoreCase) ? "productId" : "line";
                return new SalesReceiptValidationIssue(submitted[index], canonical, detail.Code, detail.Message, "server");
            }
            return new SalesReceiptValidationIssue(null, field, detail.Code, detail.Message, "server");
        }).ToArray();
    }
    private void SetValidationIssues(IReadOnlyList<SalesReceiptValidationIssue> issues)
    {
        if (ValidationIssues.SequenceEqual(issues)) return;
        ValidationIssues = issues;
        OnPropertyChanged(nameof(ValidationIssues));
    }
    private void BeginPostRecovery(long id)
    {
        RequiresPostOutcomeReload = true;
        _postRecovery = new PostRecovery(id, _document, _sessionVersion);
    }
    private void Apply(SalesReceiptDto dto, SalesReceiptLineViewModel[]? submitted)
    {
        var previous = _lines.ToDictionary(row => row.SaleLineId > 0 ? row.SaleLineId : -_lines.IndexOf(row) - 1);
        var selectedKey = SelectedLine?.ClientKey;
        var ordered = dto.Lines.OrderBy(row => row.LineNo).ToArray();
        var rows = ordered.Select((line, i) => submitted != null ? submitted[i] :
            previous.TryGetValue(line.SaleLineId, out var retained) ? retained : CreateRow()).ToArray();
        for (var i = 0; i < rows.Length; ++i) rows[i].Apply(ordered[i]);
        SaleId = dto.SaleId; SaleNumber = dto.SaleNumber; Version = dto.Version; Status = dto.Status; PostedAtUtc = dto.PostedAtUtc;
        _saleDate = dto.SaleDate; _customerId = dto.CustomerId; _warehouseId = dto.WarehouseId; _employeeId = dto.EmployeeId; _note = dto.Note;
        CustomerDisplayName = dto.CustomerDisplayName; WarehouseDisplayName = dto.WarehouseDisplayName; EmployeeDisplayName = dto.EmployeeDisplayName;
        CustomerIsActive = dto.CustomerIsActive; WarehouseIsActive = dto.WarehouseIsActive; EmployeeIsActive = dto.EmployeeIsActive;
        TotalCost = dto.TotalCost; GrossProfit = dto.GrossProfit; _postedAmount = dto.Status == "Posted" ? dto.TotalAmount : null;
        IsDirty = false;
        SetValidationIssues(Array.Empty<SalesReceiptValidationIssue>());
        if (!_lines.SequenceEqual(rows)) { _lines.Clear(); foreach (var row in rows) _lines.Add(row); }
        SelectedLine = rows.FirstOrDefault(row => row.ClientKey == selectedKey);
        foreach (var row in rows) row.Notify();
        Notify();
    }
    public NumericDraftSnapshot<SalesReceiptLineViewModel> CaptureNumericDraft()
    {
        lock (_sync) return new NumericDraftSnapshot<SalesReceiptLineViewModel>(
            _editorIdentity, _document, _editRevision, _lines.ToArray());
    }

    public bool TryApplyNumericPatch(NumericDraftPatch<SalesReceiptLineViewModel> patch, out string error)
    {
        if (patch == null) throw new ArgumentNullException(nameof(patch));
        lock (_sync)
        {
            error = "The numeric draft changed; select the cells again.";
            if (!CanEdit || _mutatingDraft || patch.Snapshot.EditorIdentity != _editorIdentity ||
                patch.Snapshot.DocumentGeneration != _document || patch.Snapshot.Revision != _editRevision ||
                patch.Snapshot.Lines.Count != _lines.Count ||
                patch.Snapshot.Lines.Where((line, index) => !ReferenceEquals(line, _lines[index])).Any() ||
                patch.Changes.Count == 0) return false;

            var proposed = new Dictionary<SalesReceiptLineViewModel, (decimal Quantity, decimal Price)>();
            var changed = new Dictionary<SalesReceiptLineViewModel, (bool Quantity, bool Price)>();
            foreach (var line in _lines) proposed.Add(line, (line.Quantity, line.UnitPrice));
            var seen = new HashSet<(SalesReceiptLineViewModel Line, NumericDraftField Field)>();
            foreach (var change in patch.Changes)
            {
                if (!proposed.TryGetValue(change.Line, out var values) || !seen.Add((change.Line, change.Field)) ||
                    decimal.Round(change.Value, GoodsReceiptContractLimits.DecimalPlaces) != change.Value)
                { error = "The numeric patch contains an unavailable, duplicate or over-scale cell."; return false; }
                if (change.Field == NumericDraftField.Quantity)
                {
                    if (change.Value <= 0m || change.Value > SalesContractLimits.MaximumQuantity)
                    { error = "Quantity is outside the receipt bounds."; return false; }
                    values.Quantity = change.Value;
                }
                else if (change.Field == NumericDraftField.UnitPrice)
                {
                    if (change.Value < 0m || change.Value > SalesContractLimits.MaximumUnitPrice)
                    { error = "Unit price is outside the receipt bounds."; return false; }
                    values.Price = change.Value;
                }
                else { error = "This field does not belong to a sale."; return false; }
                proposed[change.Line] = values;
            }
            try
            {
                _calculator.SumAmounts(_lines.Select(line =>
                {
                    var values = proposed[line];
                    return _calculator.CalculateRevenue(values.Quantity, values.Price, line.IsGift);
                }));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            { error = "The numeric patch would exceed the receipt amount bounds."; return false; }

            foreach (var line in _lines)
            {
                var values = proposed[line];
                if (line.Quantity != values.Quantity || line.UnitPrice != values.Price)
                    changed.Add(line, (line.Quantity != values.Quantity, line.UnitPrice != values.Price));
            }
            if (changed.Count == 0) { error = string.Empty; return true; }
            _mutatingDraft = true;
            try
            {
                AdvanceDraftRevision();
                foreach (var item in changed)
                {
                    var values = proposed[item.Key];
                    item.Key.ApplyNumericSilently(values.Quantity, values.Price);
                }
                Error = null;
                SetValidationIssues(ValidationIssues.Where(issue => issue.Line == null ||
                    !changed.TryGetValue(issue.Line, out var fields) ||
                    !(fields.Quantity && string.Equals(issue.Field, "qty", StringComparison.Ordinal)) &&
                    !(fields.Price && string.Equals(issue.Field, "unitPrice", StringComparison.Ordinal))).ToArray());
                foreach (var item in changed) item.Key.NotifyNumeric(item.Value.Quantity, item.Value.Price);
            }
            finally { _mutatingDraft = false; }
            Notify();
            error = string.Empty;
            return true;
        }
    }

    private void AdvanceDraftRevision() { ++_editRevision; IsDirty = true; }
    private void Dirty(SalesReceiptLineViewModel? line = null, string? field = null, bool revisionAdvanced = false)
    {
        if (line != null)
            SetValidationIssues(ValidationIssues.Where(issue => !ReferenceEquals(issue.Line, line) ||
                !string.Equals(issue.Field, field, StringComparison.Ordinal)).ToArray());
        if (!revisionAdvanced) AdvanceDraftRevision();
        Error = null; Notify();
    }
    private Operation Begin(CancellationToken token, int busy)
    {
        _active?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        _active = cancellation; _busy = busy;
        var op = new Operation(++_operation, _document, cancellation); Error = null; Notify(); return op;
    }
    private bool Current(Operation operation) => SessionCurrent && _read && !_denied && !operation.Cancellation.IsCancellationRequested && _operation == operation.Id && _document == operation.Document;
    private Task Publish(Operation operation, Action action) => _ui.InvokeAsync(() =>
    { lock (_sync) { if (Current(operation)) action(); } }, CancellationToken.None);
    private async Task End(Operation operation)
    {
        await _ui.InvokeAsync(() =>
        {
            lock (_sync)
            {
                if (!SessionCurrent || _operation != operation.Id || _document != operation.Document) return;
                _busy = 0; _active = null; Notify();
            }
        }, CancellationToken.None).ConfigureAwait(false);
        operation.Cancellation.Dispose();
    }
    private void Invalidate() { ++_operation; _active?.Cancel(); _active = null; _busy = 0; }
    private void SessionChanged(object? sender, EventArgs args)
    {
        lock (_sync) { Invalidate(); ++_document; _editorIdentity = Guid.NewGuid(); ++_editRevision; _denied = true; _postRecovery = null;
            SetValidationIssues(Array.Empty<SalesReceiptValidationIssue>()); }
        _ = _ui.InvokeAsync(() => { lock (_sync) { Scrub(); Notify(); } }, CancellationToken.None);
    }
    private void Scrub()
    {
        _lines.Clear(); SelectedLine = null; SaleId = 0; SaleNumber = ""; Version = ""; Status = "Draft"; PostedAtUtc = null;
        _saleDate = DateTime.Today; _customerId = _warehouseId = _employeeId = 0; _note = null;
        CustomerDisplayName = WarehouseDisplayName = EmployeeDisplayName = null;
        CustomerIsActive = WarehouseIsActive = EmployeeIsActive = true;
        TotalCost = GrossProfit = _postedAmount = null; IsDirty = false; Error = null;
        SetValidationIssues(Array.Empty<SalesReceiptValidationIssue>());
    }
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true; Invalidate(); ++_document; _editorIdentity = Guid.NewGuid(); ++_editRevision; _postRecovery = null; _session.SessionChanged -= SessionChanged; Scrub(); Notify();
        }
    }
    private static ApiError Unexpected() => new ApiError(ApiStatusCode.InternalServerError, "Desktop.UnexpectedError", "The operation outcome may require review.");
    private static ApiError InvalidResponse() => new ApiError(ApiStatusCode.InternalServerError, "SalesReceipt.InvalidResponse", "Reload the authoritative sale before continuing.");
    private void Notify()
    {
        foreach (var name in new[] { nameof(SaleId), nameof(SaleNumber), nameof(Version), nameof(Status), nameof(PostedAtUtc), nameof(SaleDate), nameof(CustomerId), nameof(WarehouseId), nameof(EmployeeId), nameof(Note), nameof(IsDirty), nameof(IsBusy), nameof(IsAccessDenied), nameof(RequiresCreateOutcomeReview), nameof(RequiresPostOutcomeReload), nameof(CanEdit), nameof(TotalAmount), nameof(TotalCost), nameof(GrossProfit), nameof(Error), nameof(SelectedLine), nameof(ValidationIssues) })
            OnPropertyChanged(name);
        foreach (var name in new[] { nameof(CustomerDisplayName), nameof(WarehouseDisplayName), nameof(EmployeeDisplayName), nameof(CustomerIsActive), nameof(WarehouseIsActive), nameof(EmployeeIsActive) })
            OnPropertyChanged(name);
    }
    private sealed class Operation
    {
        public Operation(long id, long document, CancellationTokenSource cancellation) { Id = id; Document = document; Cancellation = cancellation; }
        public long Id { get; }
        public long Document { get; }
        public CancellationTokenSource Cancellation { get; }
    }
    private sealed class PostRecovery
    {
        public PostRecovery(long saleId, long document, long sessionVersion)
        { SaleId = saleId; Document = document; SessionVersion = sessionVersion; }
        public long SaleId { get; }
        public long Document { get; }
        public long SessionVersion { get; }
        public PostSalesReceiptResponse? KnownPosted { get; set; }
    }
}
