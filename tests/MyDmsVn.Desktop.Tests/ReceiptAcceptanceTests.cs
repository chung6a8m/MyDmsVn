using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class ReceiptAcceptanceTests
{
    [Fact]
    public void Rowversion_conflict_is_inline_and_keeps_dirty_header_line_and_opaque_version()
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture();
            using var control = fixture.CreateControl();
            Load(control, token);
            control.NoteEditor.Text = "operator correction";
            control.QuantityEditor.Value = 3.1250m;
            control.UnitCostEditor.Value = 9.4321m;
            fixture.Api.UpdateError = new ApiError(ApiStatusCode.Conflict,
                "GoodsReceipt.ConcurrencyConflict", "Receipt changed. Reload before saving.");
            var readsBefore = fixture.Api.GetCalls;

            control.SaveButton.PerformClick();
            Drain(control.LastOperation, token);

            Assert.Equal("Receipt changed. Reload before saving.", control.OperationStatusLabel.Text);
            Assert.Equal("operator correction", control.NoteEditor.Text);
            Assert.Equal(3.1250m, control.QuantityEditor.Value);
            Assert.Equal(9.4321m, control.UnitCostEditor.Value);
            Assert.Equal("AQIDBAUGBwg=", control.VersionLabel.Text);
            Assert.Equal("AQIDBAUGBwg=", Assert.Single(fixture.Api.Updates).ExpectedVersion);
            Assert.True(fixture.Editor.IsDirty);
            Assert.True(control.SaveButton.Enabled);
            Assert.False(control.PostButton.Enabled);
            Assert.Equal(readsBefore, fixture.Api.GetCalls);
            Assert.Empty(fixture.Api.Posts);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Pending_post_disables_every_mutating_control_and_repeated_clicks_issue_one_command()
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture();
            using var control = fixture.CreateControl();
            Load(control, token);
            fixture.Api.PendingPost = new TaskCompletionSource<ApiResponse<PostGoodsReceiptResponse>>();

            control.PostButton.PerformClick();
            var operation = control.LastOperation;
            Assert.False(operation.IsCompleted);
            Assert.False(control.PostButton.Enabled);
            Assert.False(control.SaveButton.Enabled);
            Assert.False(control.NewButton.Enabled);
            Assert.False(control.RefreshButton.Enabled);
            Assert.False(control.ReceiptGrid.Enabled);
            Assert.False(control.QuantityEditor.Enabled);
            Assert.False(control.NoteEditor.Enabled);
            control.PostButton.PerformClick();
            control.SaveButton.PerformClick();
            Assert.Single(fixture.Api.Posts);
            Assert.Empty(fixture.Api.Updates);

            fixture.Api.Current = Receipt("Posted");
            fixture.Api.PendingPost.SetResult(ApiResponse<PostGoodsReceiptResponse>.Success(
                new PostGoodsReceiptResponse(41, "Posted", PostedAt)));
            Drain(operation, token);

            Assert.Equal("Posted", control.StatusLabel.Text);
            Assert.False(control.PostButton.Enabled);
            Assert.False(control.SaveButton.Enabled);
            Assert.Single(fixture.Api.Posts);
            Assert.Equal("AQIDBAUGBwg=", fixture.Api.Posts[0].ExpectedVersion);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Uncertain_post_requires_read_reload_before_another_mutating_command(bool apiFailure, bool committed)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture();
            using var control = fixture.CreateControl();
            Load(control, token);
            fixture.Api.CommitThenLosePostResponse = true;
            fixture.Api.ReturnPostFailure = apiFailure;
            fixture.Api.CommitBeforeResponseLoss = committed;

            control.PostButton.PerformClick();
            Drain(control.LastOperation, token);

            Assert.Single(fixture.Api.Posts);
            Assert.NotEqual("Ready", control.OperationStatusLabel.Text);
            Assert.False(control.PostButton.Enabled);
            Assert.False(control.SaveButton.Enabled);
            Assert.True(control.RefreshButton.Enabled);
            control.PostButton.PerformClick();
            Assert.Single(fixture.Api.Posts);

            fixture.Api.ReadError = new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Read temporarily denied.");
            control.RefreshButton.PerformClick();
            Drain(control.LastOperation, token);
            Assert.True(fixture.Editor.RequiresPostOutcomeReload);
            Assert.Null(fixture.Editor.Id);
            Assert.Empty(fixture.Editor.Lines);
            Assert.False(control.SaveButton.Enabled);
            Assert.False(control.PostButton.Enabled);
            Assert.Single(fixture.Api.Posts);

            fixture.Api.ReadError = null;
            control.RefreshButton.PerformClick();
            Drain(control.LastOperation, token);

            Assert.Equal(committed ? "Posted" : "Draft", control.StatusLabel.Text);
            Assert.Equal(!committed, control.SaveButton.Enabled);
            Assert.Equal(!committed, control.PostButton.Enabled);
            Assert.Single(fixture.Api.Posts);
            if (!committed)
            {
                fixture.Api.CommitThenLosePostResponse = false;
                control.PostButton.PerformClick();
                Drain(control.LastOperation, token);
                Assert.Equal(2, fixture.Api.Posts.Count);
                Assert.Equal("Posted", control.StatusLabel.Text);
                Assert.False(control.PostButton.Enabled);
            }
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false, ApiStatusCode.Forbidden, "Auth.Forbidden")]
    [InlineData(true, ApiStatusCode.Forbidden, "Auth.Forbidden")]
    [InlineData(false, ApiStatusCode.Forbidden, "Auth.UserInactive")]
    [InlineData(true, ApiStatusCode.Forbidden, "Auth.UserInactive")]
    [InlineData(false, ApiStatusCode.Unauthorized, "Auth.SessionExpired")]
    [InlineData(true, ApiStatusCode.Unauthorized, "Auth.SessionExpired")]
    public void Rejected_save_or_post_renders_authorization_error_without_success_or_followup_reads(
        bool post, ApiStatusCode status, string code)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture();
            using var control = fixture.CreateControl();
            Load(control, token);
            var error = new ApiError(status, code, "Current identity cannot perform this action.");
            fixture.Api.UpdateError = error;
            fixture.Api.PostError = error;
            var readsBefore = fixture.Api.GetCalls;
            var listsBefore = fixture.Api.ListCalls;
            if (post)
            {
                control.PostButton.PerformClick();
            }
            else
            {
                control.NoteEditor.Text = "unsaved correction";
                control.SaveButton.PerformClick();
            }
            Drain(control.LastOperation, token);

            Assert.Equal("Current identity cannot perform this action.", control.OperationStatusLabel.Text);
            Assert.Equal("Draft", control.StatusLabel.Text);
            Assert.Equal("AQIDBAUGBwg=", control.VersionLabel.Text);
            Assert.Equal("received", fixture.Api.Current.Note);
            Assert.Equal(readsBefore, fixture.Api.GetCalls);
            Assert.Equal(listsBefore, fixture.Api.ListCalls);
            Assert.Equal(post ? 1 : 0, fixture.Api.Posts.Count);
            Assert.Equal(post ? 0 : 1, fixture.Api.Updates.Count);
            Assert.Equal(DesktopNotificationKind.Error, fixture.Notifications.LastNotification!.Kind);
            if (!post)
            {
                Assert.Equal("unsaved correction", control.NoteEditor.Text);
                Assert.True(fixture.Editor.IsDirty);
            }
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Post_only_actor_with_denied_read_cannot_post_an_unloaded_receipt()
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture();
            fixture.Api.ReadError = new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Read denied.");
            using var control = fixture.CreateControl();
            Drain(control.ActivateAsync(token), token);
            Drain(control.SelectReceiptAsync(41, token), token);

            Assert.Null(fixture.Editor.Id);
            Assert.Equal(1, control.ReceiptGrid.RowsCount);
            Assert.Equal(1, control.LineGrid.RowsCount);
            Assert.False(control.PostButton.Enabled);
            control.PostButton.PerformClick();
            Assert.Empty(fixture.Api.Posts);
            Assert.Empty(fixture.Api.Updates);
            Assert.Equal("Read denied.", control.OperationStatusLabel.Text);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Disposal_cancels_pending_post_and_late_success_cannot_publish_or_refresh()
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture();
            using var control = fixture.CreateControl();
            Load(control, token);
            fixture.Api.PendingPost = new TaskCompletionSource<ApiResponse<PostGoodsReceiptResponse>>();
            control.PostButton.PerformClick();
            var operation = control.LastOperation;
            var readsBefore = fixture.Api.GetCalls;
            var listsBefore = fixture.Api.ListCalls;
            var notification = fixture.Notifications.LastNotification;

            control.Dispose();
            Assert.True(fixture.Api.PostToken.IsCancellationRequested);
            fixture.Api.PendingPost.SetResult(ApiResponse<PostGoodsReceiptResponse>.Success(
                new PostGoodsReceiptResponse(41, "Posted", PostedAt)));
            Drain(operation, token);

            Assert.Equal(readsBefore, fixture.Api.GetCalls);
            Assert.Equal(listsBefore, fixture.Api.ListCalls);
            Assert.Same(notification, fixture.Notifications.LastNotification);
            Assert.Equal("Draft", fixture.Editor.Status);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Reopened_receipt_fetches_fresh_active_options_for_all_three_lookup_kinds()
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            var warehouses = new LookupSource(CatalogKind.Warehouse, 3);
            var employees = new LookupSource(CatalogKind.Employee, 5);
            var products = new LookupSource(CatalogKind.Product, 7);
            using (var fixture = new Fixture(warehouses, employees, products))
            using (var control = fixture.CreateControl())
            {
                Load(control, token);
                Assert.Single(Options(control.WarehouseLookup.DataSource));
                Assert.Single(Options(control.EmployeeLookup.DataSource));
                Assert.Single(Options(control.ProductLookup.DataSource));
            }

            // No messenger event: a newly opened workspace must obtain current catalog state.
            warehouses.Active = false;
            employees.Active = false;
            products.Active = false;
            using var reopened = new Fixture(warehouses, employees, products);
            using var reopenedControl = reopened.CreateControl();
            Drain(reopenedControl.ActivateAsync(token), token);

            Assert.Empty(Options(reopenedControl.WarehouseLookup.DataSource));
            Assert.Empty(Options(reopenedControl.EmployeeLookup.DataSource));
            Assert.Empty(Options(reopenedControl.ProductLookup.DataSource));
            Assert.Equal(2, warehouses.Requests);
            Assert.Equal(2, employees.Requests);
            Assert.Equal(2, products.Requests);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Session_switch_disposes_pending_receipt_save_and_old_completion_cannot_reach_next_users_workspace(
        bool signOutFirst, bool lateFailure)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            var notifications = new DesktopNotificationCenter();
            var session = new ReceiptTestSession();
            session.SetUser(new CurrentUserDto(42, "first", "First operator"));
            using var shell = new FoundationShellForm(
                new FoundationViewModel(new FoundationShellFormTests.ReadyApiClient(), notifications), session, notifications);
            using var oldFixture = new Fixture(notifications: notifications);
            using var oldControl = oldFixture.CreateControl();
            var oldVersion = shell.WorkspaceSessionVersion;
            Assert.NotNull(shell.OpenWorkspace("goods-receipts", "Goods receipts", oldControl, oldVersion));
            shell.Show();
            System.Windows.Forms.Application.DoEvents();
            Load(oldControl, token);
            oldControl.NoteEditor.Text = "private first-user draft";
            oldFixture.Api.PendingUpdate = new TaskCompletionSource<ApiResponse<GoodsReceiptDto>>();
            Assert.True(oldControl.SaveButton.Visible);
            Assert.True(oldControl.SaveButton.Enabled);
            oldControl.SaveButton.PerformClick();
            var oldSave = oldControl.LastOperation;
            Assert.False(oldSave.IsCompleted);
            Assert.False(oldControl.SaveButton.Enabled);
            Assert.Single(oldFixture.Api.Updates);
            Assert.False(oldFixture.Api.PendingUpdate.Task.IsCompleted);
            var oldReads = oldFixture.Api.GetCalls;
            var oldLists = oldFixture.Api.ListCalls;

            if (signOutFirst) shell.SignOut();
            session.SetUser(new CurrentUserDto(84, "second", "Second operator"));
            Assert.True(oldControl.IsDisposed);
            Assert.True(oldFixture.Api.UpdateToken.IsCancellationRequested);
            Assert.Empty(shell.Workspace.TabPages);
            Assert.Equal("Second operator", shell.CurrentUserText);

            using var nextFixture = new Fixture(notifications: notifications);
            using var nextControl = nextFixture.CreateControl();
            var nextPage = shell.OpenWorkspace("goods-receipts", "Goods receipts", nextControl, shell.WorkspaceSessionVersion);
            Assert.NotNull(nextPage);
            Load(nextControl, token);
            nextControl.NoteEditor.Text = "second-user unsaved input";
            shell.SetStatus("Second user's workspace ready.");
            var nextNotification = notifications.LastNotification;
            var nextReads = nextFixture.Api.GetCalls;
            var nextLists = nextFixture.Api.ListCalls;

            oldFixture.Api.PendingUpdate.SetResult(lateFailure
                ? ApiResponse<GoodsReceiptDto>.Failure(new ApiError(ApiStatusCode.InternalServerError,
                    "Receipt.LateFailure", "private first-user failure"))
                : ApiResponse<GoodsReceiptDto>.Success(Receipt(note: "private first-user saved result")));
            Drain(oldSave, token);

            Assert.Equal(oldReads, oldFixture.Api.GetCalls);
            Assert.Equal(oldLists, oldFixture.Api.ListCalls);
            Assert.Equal(nextReads, nextFixture.Api.GetCalls);
            Assert.Equal(nextLists, nextFixture.Api.ListCalls);
            Assert.Same(nextNotification, notifications.LastNotification);
            Assert.Equal("Second user's workspace ready.", shell.StatusText);
            Assert.Equal("Second operator", shell.CurrentUserText);
            Assert.Same(nextPage, Assert.Single(shell.Workspace.TabPages.Cast<TabPage>()));
            Assert.Same(nextControl, Assert.Single(nextPage!.Controls.Cast<Control>()));
            Assert.Equal("second-user unsaved input", nextControl.NoteEditor.Text);
            Assert.True(nextFixture.Editor.IsDirty);
            Assert.Empty(nextFixture.Api.Updates);
            Assert.False(nextControl.IsDisposed);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(944, BootstrapThemeMode.Light)]
    [InlineData(1680, BootstrapThemeMode.Light)]
    [InlineData(944, BootstrapThemeMode.Dark)]
    [InlineData(1680, BootstrapThemeMode.Dark)]
    public void Receipt_header_fields_and_commands_remain_visible_at_supported_workspace_widths(int width, BootstrapThemeMode mode)
    {
        StaTest.Run(token =>
        {
            var originalTheme = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                using var guard = new WinFormsTestGuard();
                using var fixture = new Fixture();
                using var control = fixture.CreateControl();
                using var host = new Form { ClientSize = new System.Drawing.Size(width, 900), AutoScaleMode = AutoScaleMode.None };
                host.Controls.Add(control);
                host.Show();
                Load(control, token);
                host.PerformLayout();
                System.Windows.Forms.Application.DoEvents();
                host.ClientSize = new System.Drawing.Size(width == 944 ? 1680 : 944, 900);
                System.Windows.Forms.Application.DoEvents();
                host.ClientSize = new System.Drawing.Size(width, 900);
                System.Windows.Forms.Application.DoEvents();
                var ancestry = string.Join(" / ", Parents(control.WarehouseLookup).Select(p =>
                    p.GetType().Name + " Bounds=" + p.Bounds + " Client=" + p.ClientSize +
                    (p is SplitContainer split ? " Distance=" + split.SplitterDistance + " Min1=" + split.Panel1MinSize : "")));
                Assert.True(control.WarehouseLookup.Width >= 240,
                    "Warehouse input too narrow: " + control.WarehouseLookup.Bounds + "; " + ancestry);
                foreach (var input in new Control[] { control.ReceiptDateEditor, control.WarehouseLookup,
                    control.EmployeeLookup, control.NoteEditor, control.SaveButton, control.PostButton })
                {
                    var expected = input.RectangleToScreen(input.ClientRectangle);
                    var visible = expected;
                    foreach (var parent in Parents(input))
                        visible.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
                    Assert.True(expected == visible, input.GetType().Name + " clipped: expected=" + expected + " visible=" + visible + "; " + ancestry);
                }
                Assert.Empty(guard.Exceptions);
            }
            finally { BootstrapThemeManager.CurrentTheme = originalTheme; }
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(944, BootstrapThemeMode.Light)]
    [InlineData(1680, BootstrapThemeMode.Light)]
    [InlineData(944, BootstrapThemeMode.Dark)]
    [InlineData(1680, BootstrapThemeMode.Dark)]
    public void Receipt_filters_remain_visible_and_search_usable_at_supported_workspace_widths(int width, BootstrapThemeMode mode)
    {
        StaTest.Run(token =>
        {
            var originalTheme = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                using var guard = new WinFormsTestGuard();
                using var fixture = new Fixture();
                using var control = fixture.CreateControl();
                using var host = new Form { ClientSize = new System.Drawing.Size(width, 900), AutoScaleMode = AutoScaleMode.None };
                host.Controls.Add(control);
                host.Show();
                Load(control, token);
                host.PerformLayout();
                System.Windows.Forms.Application.DoEvents();
                Assert.True(control.SearchBox.Width >= 100, "Receipt search width=" + control.SearchBox.Width);
                foreach (var input in new Control[] { control.SearchBox, control.WarehouseFilterLookup,
                    control.StatusFilter, control.RefreshButton, control.NewButton })
                {
                    var expected = input.RectangleToScreen(input.ClientRectangle);
                    var visible = expected;
                    foreach (var parent in Parents(input))
                        visible.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
                    Assert.True(expected == visible, input.GetType().Name + " clipped: expected=" + expected + " visible=" + visible);
                }
                Assert.Empty(guard.Exceptions);
            }
            finally { BootstrapThemeManager.CurrentTheme = originalTheme; }
        }, TimeSpan.FromSeconds(10));
    }

    private static IEnumerable<Control> Parents(Control control)
    {
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
            yield return parent;
    }

    private static IEnumerable<CatalogLookupOption> Options(object? source)
    {
        Assert.NotNull(source);
        return Assert.IsAssignableFrom<IEnumerable<CatalogLookupOption>>(source);
    }

    private static void Load(GoodsReceiptControl control, CancellationToken token)
    {
        Drain(control.ActivateAsync(token), token);
        Drain(control.SelectReceiptAsync(41, token), token);
    }

    private static void Drain(Task task, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(4))
        {
            token.ThrowIfCancellationRequested();
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }
        Assert.True(task.IsCompleted, "Controlled receipt operation failed to drain within four seconds.");
        task.GetAwaiter().GetResult();
    }

    private static readonly DateTime PostedAt = new DateTime(2026, 10, 19, 1, 2, 3, DateTimeKind.Utc);

    private static GoodsReceiptDto Receipt(string status = "Draft", string note = "received") => new GoodsReceiptDto(
        41, "GR0000000041", new DateTime(2026, 10, 19), 3, 5, status, note,
        status == "Posted" ? PostedAt : (DateTime?)null, "AQIDBAUGBwg=",
        new[] { new GoodsReceiptLineDto(91, 1, 7, 2.5m, 4m, "P7 - Product", true) },
        "W3 - Warehouse", true, "E5 - Employee", true);

    private sealed class Fixture : IDisposable
    {
        public Fixture(LookupSource? warehouses = null, LookupSource? employees = null, LookupSource? products = null,
            DesktopNotificationCenter? notifications = null)
        {
            Notifications = notifications ?? new DesktopNotificationCenter();
            var messenger = new WeakReferenceMessenger();
            var dispatcher = new ImmediateUiDispatcher();
            var delay = new ImmediateDelay();
            List = new GoodsReceiptListViewModel(Api, dispatcher, Notifications, delay);
            Editor = new GoodsReceiptEditorViewModel(Api,
                new CatalogLookupViewModel(warehouses ?? new LookupSource(CatalogKind.Warehouse, 3),
                    messenger, dispatcher, Notifications, delay),
                new CatalogLookupViewModel(employees ?? new LookupSource(CatalogKind.Employee, 5),
                    messenger, dispatcher, Notifications, delay),
                new GoodsReceiptProductOptionsViewModel(products ?? new LookupSource(CatalogKind.Product, 7),
                    messenger, dispatcher, Notifications, delay), dispatcher, Notifications);
        }

        public ReceiptApi Api { get; } = new ReceiptApi();
        public DesktopNotificationCenter Notifications { get; }
        public GoodsReceiptListViewModel List { get; }
        public GoodsReceiptEditorViewModel Editor { get; }
        public GoodsReceiptControl CreateControl() => new GoodsReceiptControl(List, Editor);
        public void Dispose()
        {
            List.Dispose();
            Editor.Dispose();
            Api.PendingPost?.TrySetCanceled();
            Api.PendingUpdate?.TrySetCanceled();
        }
    }

    private sealed class ImmediateDelay : IAsyncDelay
    {
        public Task DelayAsync(TimeSpan interval, CancellationToken token) => Task.CompletedTask;
    }

    private sealed class ReceiptTestSession : IDesktopSession
    {
        public event EventHandler? SessionChanged;
        public CurrentUserDto? CurrentUser { get; private set; }
        public bool IsAuthenticated => CurrentUser != null;
        public DesktopSessionState State => IsAuthenticated ? DesktopSessionState.Authenticated : DesktopSessionState.Unauthenticated;
        public long Version { get; private set; }
        public void SignOut() => SetUser(null);
        public void SetUser(CurrentUserDto? user)
        {
            CurrentUser = user;
            Version++;
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class LookupSource : ICatalogLookupSource
    {
        private readonly int _id;
        public LookupSource(CatalogKind kind, int id) { Kind = kind; _id = id; }
        public CatalogKind Kind { get; }
        public bool Active { get; set; } = true;
        public int Requests { get; private set; }
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token)
        {
            Requests++;
            IReadOnlyList<CatalogLookupDto> items = Active
                ? new[] { new CatalogLookupDto(_id, "C" + _id, "Choice", true) }
                : Array.Empty<CatalogLookupDto>();
            return Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(items));
        }
        public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken token) =>
            Task.FromResult(ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(id, "C" + id, "Choice", Active)));
    }

    private sealed class ReceiptApi : IGoodsReceiptApiClient
    {
        public GoodsReceiptDto Current { get; set; } = Receipt();
        public IList<UpdateGoodsReceiptDraftRequest> Updates { get; } = new List<UpdateGoodsReceiptDraftRequest>();
        public IList<PostGoodsReceiptRequest> Posts { get; } = new List<PostGoodsReceiptRequest>();
        public int GetCalls { get; private set; }
        public int ListCalls { get; private set; }
        public ApiError? UpdateError { get; set; }
        public ApiError? PostError { get; set; }
        public ApiError? ReadError { get; set; }
        public bool CommitThenLosePostResponse { get; set; }
        public bool CommitBeforeResponseLoss { get; set; } = true;
        public bool ReturnPostFailure { get; set; }
        public TaskCompletionSource<ApiResponse<PostGoodsReceiptResponse>>? PendingPost { get; set; }
        public TaskCompletionSource<ApiResponse<GoodsReceiptDto>>? PendingUpdate { get; set; }
        public CancellationToken PostToken { get; private set; }
        public CancellationToken UpdateToken { get; private set; }

        public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(GoodsReceiptListRequest request, CancellationToken token)
        {
            ListCalls++;
            return Task.FromResult(ReadError == null
                ? ApiResponse<PagedResult<GoodsReceiptDto>>.Success(new PagedResult<GoodsReceiptDto>(new[] { Current }, 1, 25, 1))
                : ApiResponse<PagedResult<GoodsReceiptDto>>.Failure(ReadError));
        }
        public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken token)
        {
            GetCalls++;
            return Task.FromResult(ReadError == null
                ? ApiResponse<GoodsReceiptDto>.Success(Current)
                : ApiResponse<GoodsReceiptDto>.Failure(ReadError));
        }
        public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken token) =>
            throw new NotSupportedException("This acceptance fixture only edits existing drafts.");
        public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(UpdateGoodsReceiptDraftRequest request, CancellationToken token)
        {
            Updates.Add(request);
            UpdateToken = token;
            if (PendingUpdate != null) return PendingUpdate.Task;
            return Task.FromResult(UpdateError == null
                ? ApiResponse<GoodsReceiptDto>.Success(Current)
                : ApiResponse<GoodsReceiptDto>.Failure(UpdateError));
        }
        public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(PostGoodsReceiptRequest request, CancellationToken token)
        {
            Posts.Add(request);
            PostToken = token;
            if (PendingPost != null) return PendingPost.Task;
            if (PostError != null) return Task.FromResult(ApiResponse<PostGoodsReceiptResponse>.Failure(PostError));
            if (CommitThenLosePostResponse)
            {
                if (CommitBeforeResponseLoss) Current = Receipt("Posted");
                if (ReturnPostFailure)
                    return Task.FromResult(ApiResponse<PostGoodsReceiptResponse>.Failure(
                        new ApiError(ApiStatusCode.InternalServerError, "Inventory.PostOutcomeUnknown", "Post outcome unknown. Reload receipt.")));
                return Task.FromException<ApiResponse<PostGoodsReceiptResponse>>(new TimeoutException("Response lost after dispatch."));
            }
            Current = Receipt("Posted");
            return Task.FromResult(ApiResponse<PostGoodsReceiptResponse>.Success(new PostGoodsReceiptResponse(41, "Posted", PostedAt)));
        }
    }
}
