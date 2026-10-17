using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.WinForms.Features.Security;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SecurityAdministrationFormTests
{
    [Fact]
    public void Administration_control_loads_and_disposes_on_serialized_sta_without_modal_errors()
    {
        StaTest.Run(
            _ =>
            {
                using var guard = new WinFormsTestGuard();
                var session = new TestSession();
                var viewModel = new SecurityAdministrationViewModel(
                    new FakeAdministrationClient(),
                    new FakeCatalogClient(),
                    session,
                    new DesktopNotificationCenter());
                using (var control = new SecurityAdministrationControl(viewModel))
                {
                    control.ActivateAsync(CancellationToken.None).GetAwaiter().GetResult();
                    Assert.NotNull(control.UsersGrid);
                    Assert.NotNull(control.RolesGrid);
                    Assert.NotNull(control.PermissionsGrid);
                    Assert.Equal("Import JSON…", control.ImportPermissionButton.Text);
                    Assert.True(control.IsPasswordMasked);
                    Assert.True(control.ImportPermissionButton.Enabled);
                    Assert.True(guard.FatalErrorDialogsSuppressed);
                    guard.Attach(control.PermissionsGrid);
                    var expected = new InvalidOperationException("invalid permission row");
                    typeof(DataGridView).GetMethod("OnDataError", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(control.PermissionsGrid, new object[]
                        {
                            false,
                            new DataGridViewDataErrorEventArgs(expected, 0, 0, DataGridViewDataErrorContexts.Commit),
                        });
                    Assert.Equal("The selected value is invalid.", control.StatusText);
                    Assert.Same(expected, Assert.Single(guard.DrainExceptions()));
                    Assert.Empty(guard.DrainExceptions());
                }

                session.SignOut();
            },
            TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Security_workspace_is_session_scoped_and_closes_on_sign_out()
    {
        StaTest.Run(
            _ =>
            {
                using var guard = new WinFormsTestGuard();
                var session = new TestSession();
                var notifications = new DesktopNotificationCenter();
                var viewModel = new FoundationViewModel(new FakeFoundationClient(), notifications);
                var securityFactory = new SecurityControlFactory(session, notifications);
                using var shell = new FoundationShellForm(
                    viewModel,
                    session,
                    notifications,
                    new EmptyCatalogFactory(),
                    new EmptyGoodsReceiptFactory(),
                    new EmptyInventoryFactory(),
                    securityFactory);

                Assert.NotNull(shell.OpenSecurityAdministration());
                Assert.Single(shell.Workspace.TabPages);
                shell.SignOut();
                Assert.Empty(shell.Workspace.TabPages);
                Assert.Null(shell.OpenSecurityAdministration());
                Assert.Empty(guard.DrainExceptions());
            },
            TimeSpan.FromSeconds(20));
    }

    private sealed class FakeAdministrationClient : ISecurityAdministrationApiClient
    {
        public Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ListUsersAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<IReadOnlyList<SecurityUserDto>>.Success(new[]
            {
                new SecurityUserDto(4, "operator", "Operator", null, true, 1, Array.Empty<int>(), Array.Empty<SecurityDecisionDto>()),
            }));

        public Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<IReadOnlyList<SecurityRoleDto>>.Success(new[]
            {
                new SecurityRoleDto(8, "StockClerk", true, 1, Array.Empty<int>(), new[] { "Inventory.Read" }),
            }));

        public Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(
            SecurityAdministrationMutationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<SecurityAdministrationResultDto>.Success(
                new SecurityAdministrationResultDto(request.ExpectedRevision ?? 1, true)));

        public Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewPermissionImportAsync(
            PermissionImportTargetTypeDto targetType,
            int targetId,
            string json,
            CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<PermissionAssignmentImportPreviewDto>.Failure(
                new ApiError(ApiStatusCode.BadRequest, "Import.Unexpected", "Import UI is not exercised here.")));

        public Task<ApiResponse<PermissionAssignmentImportResultDto>> ApplyPermissionImportAsync(
            PermissionImportTargetTypeDto targetType,
            int targetId,
            PermissionAssignmentImportRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<PermissionAssignmentImportResultDto>.Failure(
                new ApiError(ApiStatusCode.BadRequest, "Import.Unexpected", "Import UI is not exercised here.")));
    }

    private sealed class FakeCatalogClient : IPermissionCatalogApiClient
    {
        public Task<ApiResponse<PermissionCatalogDto>> GetCatalogAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<PermissionCatalogDto>.Success(new PermissionCatalogDto(
                "1.0.0.0",
                "digest",
                new[] { new PermissionCatalogItemDto("Inventory.Read", "Inventory", "Read inventory", "Active") })));
    }

    private sealed class FakeFoundationClient : IFoundationApiClient
    {
        public Task<ApiResponse<FoundationStatus>> GetStatusAsync(
            FoundationStatusRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<FoundationStatus>.Success(new FoundationStatus(true, "test")));
    }

    private sealed class SecurityControlFactory : ISecurityAdministrationControlFactory
    {
        private readonly TestSession _session;
        private readonly IDesktopNotificationService _notifications;

        public SecurityControlFactory(TestSession session, IDesktopNotificationService notifications)
        {
            _session = session;
            _notifications = notifications;
        }

        public Control Create() => new SecurityAdministrationControl(new SecurityAdministrationViewModel(
            new FakeAdministrationClient(),
            new FakeCatalogClient(),
            _session,
            _notifications));
    }

    private sealed class EmptyCatalogFactory : ICatalogControlFactory
    {
        public Control Create(CatalogKind kind) => new Panel();
    }

    private sealed class EmptyGoodsReceiptFactory : IGoodsReceiptControlFactory
    {
        public Control Create() => new Panel();
    }

    private sealed class EmptyInventoryFactory : IInventoryControlFactory
    {
        public Control CreateStockBalances() => new Panel();
        public Control CreateStockCard() => new Panel();
    }

    private sealed class TestSession : IDesktopSession
    {
        public event EventHandler? SessionChanged;
        public bool IsAuthenticated { get; private set; } = true;
        public DesktopSessionState State => IsAuthenticated ? DesktopSessionState.Authenticated : DesktopSessionState.LoggingOut;
        public CurrentUserDto? CurrentUser { get; private set; } = new(1, "admin", "Administrator");
        public long Version { get; private set; } = 1;

        public void SignOut()
        {
            IsAuthenticated = false;
            CurrentUser = null;
            Version++;
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
