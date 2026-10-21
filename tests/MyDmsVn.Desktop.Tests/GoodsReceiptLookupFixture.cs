using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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

namespace MyDmsVn.Desktop.Tests;

internal sealed class GoodsReceiptLookupFixture : IDisposable
{
    private readonly CancellationToken _token;
    private readonly BootstrapTheme _previousTheme = BootstrapThemeManager.CurrentTheme;
    private readonly SynchronizationContext? _previousContext = SynchronizationContext.Current;
    private readonly int _uiThread = Thread.CurrentThread.ManagedThreadId;
    private readonly List<int> _mutationThreads = new List<int>();
    private WindowsFormsSynchronizationContext? _context;
    private readonly List<Task> _operations = new List<Task>();
    private readonly ControlledDelay _delay = new ControlledDelay();
    private readonly ControlledSource _warehouse = new ControlledSource(CatalogKind.Warehouse, 3);
    private readonly ControlledSource _employee = new ControlledSource(CatalogKind.Employee, 5);
    private readonly ControlledSource _product = new ControlledSource(CatalogKind.Product, 7);
    internal GoodsReceiptLookupFixture(string target, CancellationToken token)
    {
        _token = token;
        Target = target;
        try
        {
            var theme = Environment.GetEnvironmentVariable("MYDMSVN_BCF_THEME");
            if (theme != null)
            {
                Assert.True(theme == "Light" || theme == "Dark", "Unknown BCF lookup theme.");
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(
                    theme == "Dark" ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
            }
            Form = new Form { ClientSize = new Size(1500, 850), StartPosition = FormStartPosition.CenterScreen };
            Form.Show();
            ContextBeforeInstallation = SynchronizationContext.Current?.GetType().FullName ?? "null";
            // xUnit's context can flow into a newly created STA. Form.Show does not
            // necessarily replace it; explicitly own the UI context for async continuations.
            _context = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
            var dispatcher = new SynchronizationContextUiDispatcher(_context);
            var notifications = new DesktopNotificationCenter();
            var messenger = new WeakReferenceMessenger();
            Api = new ReceiptClient();
            List = new GoodsReceiptListViewModel(Api, dispatcher, notifications, _delay);
            Editor = new GoodsReceiptEditorViewModel(Api,
                new CatalogLookupViewModel(_warehouse, messenger, dispatcher, notifications, _delay),
                new CatalogLookupViewModel(_employee, messenger, dispatcher, notifications, _delay),
                new GoodsReceiptProductOptionsViewModel(_product, messenger, dispatcher, notifications, _delay),
                dispatcher, notifications);
            Editor.Lines.CollectionChanged += (_, __) =>
            {
                lock (_mutationThreads) _mutationThreads.Add(Thread.CurrentThread.ManagedThreadId);
            };
            Control = new GoodsReceiptControl(List, Editor) { Dock = DockStyle.Fill };
            Form.Controls.Add(Control);
            Wait(Control.ActivateAsync(token));
            Wait(Control.SelectReceiptAsync(41, token));
            Assert.Null(Control.WarehouseFilterLookup.SelectedValue);
            Assert.Equal(3, Control.WarehouseLookup.SelectedValue);
            Assert.Equal(5, Control.EmployeeLookup.SelectedValue);
            Assert.False(Editor.IsDirty);
            Assert.False(Control.WarehouseFilterLookup.HasPendingText);
            Assert.False(Control.WarehouseLookup.HasPendingText);
            Assert.False(Control.EmployeeLookup.HasPendingText);
            if (target == "WarehouseFilterLookup") Assert.True(Control.TryShowReceiptList());
            Lookup = target == "WarehouseFilterLookup" ? Control.WarehouseFilterLookup :
                target == "WarehouseLookup" ? Control.WarehouseLookup : Control.EmployeeLookup;
            Source = target == "EmployeeLookup" ? _employee : _warehouse;
            _warehouse.Hold = _employee.Hold = true;
            Driver = new NativeLookupTypingDriver(Lookup);
            InitialText = Driver.Text;
            // Selection is setup only; every query character is sent separately to native EDIT.
            Driver.SelectRange(0, Driver.Text.Length);
        }
        catch
        {
            // Failed focus/setup is never RED evidence and must not leave a hosted window.
            Dispose();
            throw;
        }
    }
    internal string Target { get; }
    internal string ContextBeforeInstallation { get; }
    internal string InitialText { get; }
    internal Form Form { get; }
    internal GoodsReceiptControl Control { get; }
    internal GoodsReceiptListViewModel List { get; }
    internal GoodsReceiptEditorViewModel Editor { get; }
    internal ReceiptClient Api { get; }
    internal BootstrapLookupBox Lookup { get; }
    internal NativeLookupTypingDriver Driver { get; private set; }
    internal void Refocus() => Driver = new NativeLookupTypingDriver(Lookup);
    internal ControlledSource Source { get; }
    internal ControlledSource WarehouseSource => _warehouse;
    internal ControlledSource EmployeeSource => _employee;
    internal ControlledSource ProductSource => _product;
    internal CatalogLookupViewModel LookupViewModel => Target == "EmployeeLookup" ? Editor.EmployeeLookup : Editor.WarehouseLookup;
    internal CancellationToken Token => _token;
    internal int DelayCount => _delay.Count;
    internal void ReleaseDelays() => _delay.ReleaseAll();
    internal Task Type(char character)
        => Type(Driver, character);
    internal Task Type(NativeLookupTypingDriver driver, char character)
    {
        AssertUiOwner();
        driver.TypeCharacter(character);
        return Track(Control.LastOperation);
    }
    internal Task Track(Task operation)
    {
        _operations.Add(operation);
        return operation;
    }
    internal void TypeText(string text)
    {
        foreach (var character in text) Type(character);
    }
    internal bool DelayWasCancelled(int index) => _delay.At(index).Token.IsCancellationRequested;
    internal void CompleteDelay(int index) => _delay.At(index).Completion.TrySetResult(true);
    internal void CompleteResponse(Task operation, int delayIndex)
    {
        var responseIndex = AdmitSearch(delayIndex);
        Source.Complete(responseIndex);
        Wait(operation); // Includes CompleteOperationAsync's final ApplyState, not only Items.
    }
    internal int AdmitSearch(int delayIndex)
    {
        var responseIndex = Source.Count;
        CompleteDelay(delayIndex);
        PumpUntil(() => Source.Count > responseIndex, "lookup source admission");
        return responseIndex;
    }
    internal void RebindOptions()
    {
        Source.Hold = false;
        try { Wait(Track(LookupViewModel.RefreshAsync(_token))); }
        finally { Source.Hold = true; }
    }
    internal void Wait(Task operation)
    {
        if (!_operations.Contains(operation)) Track(operation);
        AssertUiOwner();
        PumpUntil(() => operation.IsCompleted, "operation completion");
        operation.GetAwaiter().GetResult();
        System.Windows.Forms.Application.DoEvents();
        AssertUiOwner();
    }
    private void AssertUiOwner()
    {
        Assert.Equal(_uiThread, Thread.CurrentThread.ManagedThreadId);
        Assert.IsType<WindowsFormsSynchronizationContext>(SynchronizationContext.Current);
        lock (_mutationThreads) Assert.All(_mutationThreads, thread => Assert.Equal(_uiThread, thread));
    }
    internal void PumpUntil(Func<bool> condition, string boundary, bool cleanup = false)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (!cleanup) _token.ThrowIfCancellationRequested();
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }
        Assert.True(condition(), "Fixture boundary timed out: " + boundary);
    }
    public void Dispose()
    {
        try
        {
            if (Control?.IsDisposed != true)
            {
                Editor?.WarehouseLookup.Cancel();
                Editor?.EmployeeLookup.Cancel();
            }
            Api?.ReleaseList();
            _delay.ReleaseAll();
            _warehouse.ReleaseAll();
            _employee.ReleaseAll();
            _product.ReleaseAll();
            PumpUntil(() =>
            {
                // An activation can admit its next catalog only after the preceding
                // source drains. Release that staged work within the same finite budget.
                _delay.ReleaseAll();
                _warehouse.ReleaseAll();
                _employee.ReleaseAll();
                _product.ReleaseAll();
                Api?.ReleaseList();
                return _operations.All(task => task.IsCompleted);
            }, "owned operations drain", cleanup: true);
            foreach (var task in _operations) task.GetAwaiter().GetResult();
            if (_context != null) AssertUiOwner();
            Assert.All(_delay.Entries, entry => Assert.True(entry.Completion.Task.IsCompleted));
            foreach (var source in new[] { _warehouse, _employee, _product })
                Assert.All(source.PendingTasks, task => Assert.True(task.IsCompleted));
        }
        catch (Exception failure)
        {
            if (_operations.Any(task => !task.IsCompleted)) GuiTestSafety.Poison(failure);
            throw;
        }
        finally
        {
            Control?.Dispose();
            Editor?.Dispose();
            List?.Dispose();
            Form?.Dispose();
            SynchronizationContext.SetSynchronizationContext(_previousContext);
            _context?.Dispose();
            BootstrapThemeManager.CurrentTheme = _previousTheme;
        }
    }
    private sealed class ControlledDelay : IAsyncDelay
    {
        private readonly List<Entry> _entries = new List<Entry>();
        internal Entry[] Entries { get { lock (_entries) return _entries.ToArray(); } }
        internal int Count { get { lock (_entries) return _entries.Count; } }
        internal Entry At(int index) { lock (_entries) return _entries[index]; }
        public Task DelayAsync(TimeSpan interval, CancellationToken token)
        {
            // Deliberately acknowledge cancellation only when released by the test. This
            // controls the old operation continuation independently of the latest search.
            var entry = new Entry(token);
            lock (_entries) _entries.Add(entry);
            return entry.Completion.Task;
        }
        internal void ReleaseAll() { foreach (var entry in Entries) entry.Completion.TrySetResult(true); }
        internal sealed class Entry
        {
            internal Entry(CancellationToken token) => Token = token;
            internal CancellationToken Token { get; }
            internal TaskCompletionSource<bool> Completion { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
    internal sealed class ControlledSource : ICatalogLookupSource
    {
        private readonly int _id;
        private int _lookupCalls;
        private readonly List<TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>> _pending = new List<TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>>();
        internal ControlledSource(CatalogKind kind, int id) { Kind = kind; _id = id; }
        public CatalogKind Kind { get; }
        internal bool Hold { get; set; }
        internal bool SelectedIsActive { get; set; } = true;
        internal int Count { get { lock (_pending) return _pending.Count; } }
        internal int LookupCalls => Volatile.Read(ref _lookupCalls);
        internal Task[] PendingTasks { get { lock (_pending) return _pending.Select(item => (Task)item.Task).ToArray(); } }
        private CatalogLookupDto[] Options => new[] { new CatalogLookupDto(_id, "C" + _id, "Guide", SelectedIsActive), new CatalogLookupDto(_id + 10, "G", "Guitar", true) };
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token)
        {
            Interlocked.Increment(ref _lookupCalls);
            if (!Hold) return Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(Options));
            var completion = new TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pending) _pending.Add(completion);
            return completion.Task;
        }
        public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken token) =>
            Task.FromResult(ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(id, "C" + id, "Guide", SelectedIsActive)));
        internal void Complete(int index, params CatalogLookupDto[] options)
        {
            lock (_pending) _pending[index].TrySetResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(options.Length == 0 ? Options : options));
        }
        internal void ReleaseAll() { lock (_pending) foreach (var item in _pending) item.TrySetResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(Options)); }
    }
    internal sealed class ReceiptClient : IGoodsReceiptApiClient
    {
        private readonly List<TaskCompletionSource<ApiResponse<PagedResult<GoodsReceiptDto>>>> _listCompletions = new List<TaskCompletionSource<ApiResponse<PagedResult<GoodsReceiptDto>>>>();
        internal List<GoodsReceiptListRequest> Requests { get; } = new List<GoodsReceiptListRequest>();
        internal GoodsReceiptDto Current { get; set; } = Receipt();
        internal bool DenyList { get; set; }
        internal bool HoldList { get; set; }
        internal int GetCalls { get; private set; }
        internal int PostCalls { get; private set; }
        internal UpdateGoodsReceiptDraftRequest? LastUpdate { get; private set; }
        internal static GoodsReceiptDto Receipt(long id = 41, string status = "Draft", bool active = true, bool line = false) =>
            new GoodsReceiptDto(id, "GR" + id.ToString("D10"), new DateTime(2026, 10, 21), 3, 5, status, "", new DateTime(2026, 10, 21), "AQIDBAUGBwg=", line ? new[] { new GoodsReceiptLineDto(91, 1, 7, 1m, 2m, "P7 - Product", true) } : Array.Empty<GoodsReceiptLineDto>(), active ? "C3 - Guide" : "Historic warehouse", active, active ? "C5 - Guide" : "Historic employee", active);
        public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(GoodsReceiptListRequest request, CancellationToken token)
        {
            Requests.Add(request);
            if (HoldList)
            {
                var completion = new TaskCompletionSource<ApiResponse<PagedResult<GoodsReceiptDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
                _listCompletions.Add(completion);
                return completion.Task;
            }
            return Task.FromResult(ListResponse());
        }
        private ApiResponse<PagedResult<GoodsReceiptDto>> ListResponse() => DenyList
            ? ApiResponse<PagedResult<GoodsReceiptDto>>.Failure(new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Denied."))
            : ApiResponse<PagedResult<GoodsReceiptDto>>.Success(new PagedResult<GoodsReceiptDto>(new[] { Current }, 1, 25, 1));
        internal void ReleaseList() { foreach (var completion in _listCompletions) completion.TrySetResult(ListResponse()); }
        public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken token)
        {
            GetCalls++;
            return Task.FromResult(ApiResponse<GoodsReceiptDto>.Success(Current));
        }
        public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(UpdateGoodsReceiptDraftRequest request, CancellationToken token)
        {
            LastUpdate = request;
            return Task.FromResult(ApiResponse<GoodsReceiptDto>.Failure(new ApiError(ApiStatusCode.Conflict, "Concurrency.Conflict", "Synthetic save rejection.")));
        }
        public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(PostGoodsReceiptRequest request, CancellationToken token)
        {
            PostCalls++;
            throw new InvalidOperationException("Synthetic uncertain Post transport failure.");
        }
    }
}
