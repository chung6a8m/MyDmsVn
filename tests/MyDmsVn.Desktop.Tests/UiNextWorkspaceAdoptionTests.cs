using System;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class UiNextWorkspaceAdoptionTests
{
    [Fact]
    public void Factory_stock_scope_rejects_previous_session_after_user_replacement()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            var session = new TestSession();
            session.SetCurrentUser(new CurrentUserDto(42, "operator", "Operator"));
            using var balance = Assert.IsType<StockBalanceControl>(
                new InventoryControlFactory(new InventoryControlTests.InventoryApiClient(),
                    new ImmediateUiDispatcher(), new DesktopNotificationCenter(),
                    new InventoryControlTests.SilentReporter(), new InventoryControlTests.ImmediateDelay(), session)
                    .CreateStockBalances());
            var previous = balance.FeedbackScope.BeginOperation("old-query", WorkspaceOperationKind.Query);
            Assert.True(balance.FeedbackScope.IsCurrent(previous));

            session.SetCurrentUser(new CurrentUserDto(84, "manager", "Manager"));

            Assert.False(balance.FeedbackScope.IsCurrent(previous));
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Shell_uses_factory_stock_scope_for_local_query_failure()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            var api = new InventoryControlTests.InventoryApiClient
            {
                BalanceError = new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Stock unavailable.")
            };
            var balance = Assert.IsType<StockBalanceControl>(Factory(api).CreateStockBalances());
            using var shell = new FoundationShellForm(
                new FoundationViewModel(new FoundationShellFormTests.ReadyApiClient()));
            shell.Show();
            var page = shell.OpenWorkspace("stock-balance", "Stock Balance", balance, shell.WorkspaceSessionVersion)!;
            var host = Assert.IsType<WorkspaceFeedbackHost>(page.Controls[0]);
            balance.ActivateAsync(CancellationToken.None).GetAwaiter().GetResult();
            System.Windows.Forms.Application.DoEvents();

            Assert.True(host.Alert.Visible);
            Assert.Contains("Stock unavailable.", host.Alert.Text);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Hidden_stock_failure_stays_with_its_instance_and_is_retired_on_close()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            var apiA = new InventoryControlTests.InventoryApiClient();
            var apiB = new InventoryControlTests.InventoryApiClient();
            var factoryA = Factory(apiA);
            var factoryB = Factory(apiB);
            var cardA = Assert.IsType<StockCardControl>(factoryA.CreateStockCard());
            var cardB = Assert.IsType<StockCardControl>(factoryB.CreateStockCard());
            Assert.NotEqual(cardA.FeedbackScope.WorkspaceId, cardB.FeedbackScope.WorkspaceId);
            using var hostA = new WorkspaceFeedbackHost(cardA, cardA.FeedbackScope) { Dock = DockStyle.Fill };
            using var hostB = new WorkspaceFeedbackHost(cardB, cardB.FeedbackScope) { Dock = DockStyle.Fill };
            using var tabs = new TabControl { Dock = DockStyle.Fill };
            var pageA = new TabPage("A"); var pageB = new TabPage("B");
            pageA.Controls.Add(hostA); pageB.Controls.Add(hostB);
            tabs.TabPages.Add(pageA); tabs.TabPages.Add(pageB);
            using var form = new Form { Width = 900, Height = 600 };
            form.Controls.Add(tabs); form.Show();
            tabs.SelectedTab = pageB; System.Windows.Forms.Application.DoEvents();
            tabs.SelectedTab = pageA; System.Windows.Forms.Application.DoEvents();

            cardA.WarehouseFilter.Value = 2; cardA.ProductFilter.Value = 7;
            cardA.LastOperation.GetAwaiter().GetResult();
            cardB.WarehouseFilter.Value = 2; cardB.ProductFilter.Value = 7;
            cardB.LastOperation.GetAwaiter().GetResult();
            apiB.CardError = new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "B denied.");
            cardB.ActivateAsync(CancellationToken.None).GetAwaiter().GetResult();
            System.Windows.Forms.Application.DoEvents();

            Assert.False(hostA.Alert.Visible);
            Assert.True(hostB.HasAttention);
            tabs.SelectedTab = pageB; System.Windows.Forms.Application.DoEvents();
            Assert.Contains("B denied.", hostB.Alert.Text);
            Assert.False(hostB.HasAttention);

            var old = cardB.FeedbackScope.BeginOperation("late", WorkspaceOperationKind.Query);
            tabs.TabPages.Remove(pageB); pageB.Dispose();
            Assert.False(cardB.FeedbackScope.IsCurrent(old));
            Assert.False(hostA.Alert.Visible);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(20));
    }

    private static InventoryControlFactory Factory(InventoryControlTests.InventoryApiClient api) =>
        new InventoryControlFactory(api, new ImmediateUiDispatcher(), new DesktopNotificationCenter(),
            new InventoryControlTests.SilentReporter(), new InventoryControlTests.ImmediateDelay());

    private sealed class TestSession : IDesktopSession
    {
        public event EventHandler? SessionChanged;
        public bool IsAuthenticated => CurrentUser != null;
        public DesktopSessionState State => IsAuthenticated ? DesktopSessionState.Authenticated : DesktopSessionState.Unauthenticated;
        public CurrentUserDto? CurrentUser { get; private set; }
        public long Version { get; private set; }
        public void SignOut() => SetCurrentUser(null);
        public void SetCurrentUser(CurrentUserDto? user)
        {
            CurrentUser = user;
            Version++;
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
