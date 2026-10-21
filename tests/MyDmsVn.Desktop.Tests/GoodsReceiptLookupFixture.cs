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
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

internal sealed class GoodsReceiptLookupFixture : IDisposable
{
    private readonly CancellationToken _token;
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
    internal NativeLookupTypingDriver Driver { get; }
    internal ControlledSource Source { get; }
    internal int DelayCount => _delay.Count;
    internal Task Type(char character)
    {
        AssertUiOwner();
        Driver.TypeCharacter(character);
        var operation = Control.LastOperation;
        _operations.Add(operation);
        return operation;
    }
    internal bool DelayWasCancelled(int index) => _delay.At(index).Token.IsCancellationRequested;
    internal void CompleteDelay(int index) => _delay.At(index).Completion.TrySetResult(true);
    internal void CompleteResponse(Task operation, int delayIndex)
    {
        var responseIndex = Source.Count;
        CompleteDelay(delayIndex);
        PumpUntil(() => Source.Count > responseIndex, "lookup source admission");
        Source.Complete(responseIndex);
        Wait(operation); // Includes CompleteOperationAsync's final ApplyState, not only Items.
    }
    internal void Wait(Task operation)
    {
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
    private void PumpUntil(Func<bool> condition, string boundary, bool cleanup = false)
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
            Editor?.WarehouseLookup.Cancel();
            Editor?.EmployeeLookup.Cancel();
            _delay.ReleaseAll();
            _warehouse.ReleaseAll();
            _employee.ReleaseAll();
            _product.ReleaseAll();
            PumpUntil(() => _operations.All(task => task.IsCompleted), "owned operations drain", cleanup: true);
            foreach (var task in _operations) task.GetAwaiter().GetResult();
            if (_context != null) AssertUiOwner();
            Assert.All(_delay.Entries, entry => Assert.True(entry.Completion.Task.IsCompleted));
            foreach (var source in new[] { _warehouse, _employee, _product })
                Assert.All(source.PendingTasks, task => Assert.True(task.IsCompleted));
        }
        finally
        {
            Control?.Dispose();
            Editor?.Dispose();
            List?.Dispose();
            Form?.Dispose();
            SynchronizationContext.SetSynchronizationContext(_previousContext);
            _context?.Dispose();
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
        private readonly List<TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>> _pending = new List<TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>>();
        internal ControlledSource(CatalogKind kind, int id) { Kind = kind; _id = id; }
        public CatalogKind Kind { get; }
        internal bool Hold { get; set; }
        internal int Count { get { lock (_pending) return _pending.Count; } }
        internal Task[] PendingTasks { get { lock (_pending) return _pending.Select(item => (Task)item.Task).ToArray(); } }
        private CatalogLookupDto[] Options => new[] { new CatalogLookupDto(_id, "C" + _id, "Guide", true), new CatalogLookupDto(_id + 10, "G", "Guitar", true) };
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token)
        {
            if (!Hold) return Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(Options));
            var completion = new TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pending) _pending.Add(completion);
            return completion.Task;
        }
        public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken token) =>
            Task.FromResult(ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(id, "C" + id, "Guide", true)));
        internal void Complete(int index)
        {
            lock (_pending) _pending[index].TrySetResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(Options));
        }
        internal void ReleaseAll() { lock (_pending) foreach (var item in _pending) item.TrySetResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(Options)); }
    }
    internal sealed class ReceiptClient : IGoodsReceiptApiClient
    {
        internal List<GoodsReceiptListRequest> Requests { get; } = new List<GoodsReceiptListRequest>();
        private static GoodsReceiptDto Receipt => new GoodsReceiptDto(41, "GR0000000041", new DateTime(2026, 10, 21), 3, 5, "Draft", "", new DateTime(2026, 10, 21), "AQIDBAUGBwg=", Array.Empty<GoodsReceiptLineDto>(), "C3 - Guide", true, "C5 - Guide", true);
        public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(GoodsReceiptListRequest request, CancellationToken token)
        {
            Requests.Add(request);
            return Task.FromResult(ApiResponse<PagedResult<GoodsReceiptDto>>.Success(new PagedResult<GoodsReceiptDto>(new[] { Receipt }, request.PageNumber, request.PageSize, 1)));
        }
        public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken token) => Task.FromResult(ApiResponse<GoodsReceiptDto>.Success(Receipt));
        public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(UpdateGoodsReceiptDraftRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(PostGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
    }
}
