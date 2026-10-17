using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SecurityAdministrationViewModelTests
{
    [Fact]
    public async Task Loads_complete_catalog_and_keeps_unassigned_distinct_from_new_keys()
    {
        var api = new FakeSecurityAdministrationApiClient();
        api.Users = new[] { User(7, "operator", roleIds: new[] { 3 }, decisions: Array.Empty<SecurityDecisionDto>()) };
        api.Roles = new[] { Role(3, "StockClerk", permissions: new[] { "Inventory.Read" }) };
        var catalog = new FakePermissionCatalogClient(Catalog(
            Item("Inventory.Read"), Item("Inventory.Write"), Item("Security.Users.Manage")));
        using var viewModel = CreateViewModel(api, catalog, new TestSession());

        await viewModel.LoadAsync(CancellationToken.None);

        Assert.Equal(3, viewModel.CatalogItems.Count);
        Assert.Equal("Inventory.Read", viewModel.CatalogItems[0].Key);
        Assert.True(viewModel.CatalogItems[0].IsAssigned);
        Assert.Equal("Assigned", viewModel.CatalogItems[0].AssignmentStatus);
        Assert.False(viewModel.CatalogItems[1].IsAssigned);
        Assert.Equal("Unassigned", viewModel.CatalogItems[1].AssignmentStatus);
        Assert.Equal(PermissionCatalogChangeKind.NotCompared, viewModel.CatalogItems[1].ChangeKind);
    }

    [Fact]
    public async Task Filters_full_catalog_and_shows_direct_deny_precedence_and_inactive_role()
    {
        var api = new FakeSecurityAdministrationApiClient();
        api.Users = new[] { User(7, "operator", roleIds: new[] { 3 }, decisions: new[] { new SecurityDecisionDto("Inventory.Read", false) }) };
        api.Roles = new[] { Role(3, "StockClerk", isActive: false, permissions: new[] { "Inventory.Read" }) };
        using var viewModel = CreateViewModel(
            api,
            new FakePermissionCatalogClient(Catalog(Item("Inventory.Read"), Item("Sales.Read"))),
            new TestSession());

        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SelectUser(7);
        viewModel.SearchText = "sales";

        Assert.Single(viewModel.FilteredCatalogItems);
        Assert.Equal("Sales.Read", System.Linq.Enumerable.First(viewModel.FilteredCatalogItems).Key);
        viewModel.SearchText = "inventory.read";
        Assert.StartsWith("Direct Deny", viewModel.GetPermissionExplanation("Inventory.Read"));
        Assert.Contains("inactive", viewModel.GetPermissionExplanation("Inventory.Read"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Direct_inherit_mutation_uses_selected_assignment_revision_and_clears_password()
    {
        var api = new FakeSecurityAdministrationApiClient();
        api.Users = new[] { User(7, "operator", roleIds: Array.Empty<int>(), decisions: new[] { new SecurityDecisionDto("Inventory.Read", true) }, revision: 12) };
        using var viewModel = CreateViewModel(
            api,
            new FakePermissionCatalogClient(Catalog(Item("Inventory.Read"))),
            new TestSession());
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SelectUser(7);
        await viewModel.SetDirectDecisionAsync("Inventory.Read", null, CancellationToken.None);

        Assert.Equal(12, api.LastMutation!.ExpectedRevision);
        Assert.Equal(SecurityAdministrationOperationDto.SetPermission, api.LastMutation.Operation);
        Assert.Null(api.LastMutation.Decision);
        viewModel.Password = "temporary-pass";
        await viewModel.ResetUserPasswordAsync(CancellationToken.None);
        Assert.Equal(SecurityAdministrationOperationDto.SetPassword, api.LastMutation!.Operation);
        Assert.Equal(string.Empty, viewModel.Password);
    }

    [Fact]
    public async Task Revision_conflict_is_reported_and_refreshes_the_current_assignment_without_retrying()
    {
        var api = new FakeSecurityAdministrationApiClient();
        api.Users = new[] { User(7, "operator", roleIds: Array.Empty<int>(), decisions: Array.Empty<SecurityDecisionDto>(), revision: 4) };
        api.MutationHandler = _ =>
        {
            api.Users = new[] { User(7, "operator", roleIds: Array.Empty<int>(), decisions: Array.Empty<SecurityDecisionDto>(), revision: 5) };
            return ApiResponse<SecurityAdministrationResultDto>.Failure(
                new ApiError(ApiStatusCode.Conflict, "Security.RevisionConflict", "The assignment revision changed."));
        };
        using var viewModel = CreateViewModel(
            api,
            new FakePermissionCatalogClient(Catalog(Item("Inventory.Read"))),
            new TestSession());
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SelectUser(7);

        await viewModel.SetDirectDecisionAsync("Inventory.Read", true, CancellationToken.None);

        Assert.Equal(4, api.LastMutation!.ExpectedRevision);
        Assert.Equal(5, viewModel.SelectedUser!.AssignmentRevision);
        Assert.Contains("revision changed", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, api.MutationCount);
    }

    [Fact]
    public async Task Session_change_discards_delayed_catalog_result_and_prevents_later_mutation()
    {
        var api = new FakeSecurityAdministrationApiClient();
        api.Users = new[] { User(7, "operator", roleIds: Array.Empty<int>(), decisions: Array.Empty<SecurityDecisionDto>()) };
        var session = new TestSession();
        var catalog = new FakePermissionCatalogClient(Catalog(Item("Inventory.Read"))) { Hold = true };
        using var viewModel = CreateViewModel(api, catalog, session);
        var load = viewModel.LoadAsync(CancellationToken.None);
        session.SignOut();
        catalog.Release();
        await load;

        Assert.Empty(viewModel.CatalogItems);
        await viewModel.SetDirectDecisionAsync("Inventory.Read", true, CancellationToken.None);
        Assert.Null(api.LastMutation);
    }

    [Fact]
    public async Task Dispose_during_load_cancels_work_clears_password_and_discards_late_results()
    {
        var api = new FakeSecurityAdministrationApiClient();
        api.Users = new[] { User(7, "operator", roleIds: Array.Empty<int>(), decisions: Array.Empty<SecurityDecisionDto>()) };
        var catalog = new FakePermissionCatalogClient(Catalog(Item("Inventory.Read"))) { Hold = true };
        var viewModel = CreateViewModel(api, catalog, new TestSession());
        viewModel.Password = "temporary-pass";
        var load = viewModel.LoadAsync(CancellationToken.None);

        viewModel.Dispose();
        catalog.Release();
        await load;

        Assert.Empty(viewModel.Users);
        Assert.Empty(viewModel.CatalogItems);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.False(viewModel.CanManageUsers);
        Assert.False(viewModel.CanReadPermissionCatalog);
    }

    [Fact]
    public async Task Superuser_status_is_not_inferred_from_role_name()
    {
        var api = new FakeSecurityAdministrationApiClient();
        api.Users = new[] { User(7, "operator", roleIds: new[] { 3 }, decisions: Array.Empty<SecurityDecisionDto>()) };
        api.Roles = new[] { Role(3, "Administrator", permissions: new[] { "Inventory.Read" }) };
        using var viewModel = CreateViewModel(
            api,
            new FakePermissionCatalogClient(Catalog(Item("Inventory.Read"))),
            new TestSession());
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SelectUser(7);

        Assert.False(viewModel.IsSelectedUserProtected);
        Assert.Equal("Role Grant", viewModel.GetPermissionExplanation("Inventory.Read"));
        await viewModel.UpdateUserAsync(CancellationToken.None);
        Assert.NotNull(api.LastMutation);
    }

    [Fact]
    public async Task Bound_protected_account_cannot_be_edited_from_the_view_model()
    {
        var api = new FakeSecurityAdministrationApiClient();
        api.Users = new[] { User(7, "root.admin", roleIds: Array.Empty<int>(), decisions: Array.Empty<SecurityDecisionDto>(), isProtectedAccount: true) };
        using var viewModel = CreateViewModel(
            api,
            new FakePermissionCatalogClient(Catalog(Item("Inventory.Read"))),
            new TestSession());
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SelectUser(7);

        Assert.True(viewModel.IsSelectedUserProtected);
        Assert.Contains("Protected superuser", viewModel.GetPermissionExplanation("Inventory.Read"));
        await viewModel.UpdateUserAsync(CancellationToken.None);
        Assert.Null(api.LastMutation);
    }

    [Fact]
    public async Task Local_administration_client_uses_server_authorization_for_reads_and_mutations()
    {
        var services = new ServiceCollection();
        services.AddServerApplication("root.admin");
        services.AddLocalDesktopAdapter();
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ISecurityAdministrationApiClient>();

        var users = await client.ListUsersAsync(CancellationToken.None);
        var mutation = await client.MutateAsync(
            new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.User,
                SecurityAdministrationOperationDto.Create,
                name: "operator",
                displayName: "Operator",
                password: "a-safe-temporary-password"),
            CancellationToken.None);

        Assert.False(users.IsSuccess);
        Assert.Equal("Auth.Unauthorized", users.Error!.Code);
        Assert.False(mutation.IsSuccess);
        Assert.Equal("Auth.Unauthorized", mutation.Error!.Code);
    }

    [Fact]
    public async Task Create_user_validation_rejects_missing_required_fields_before_dispatch()
    {
        var api = new FakeSecurityAdministrationApiClient();
        using var viewModel = CreateViewModel(
            api,
            new FakePermissionCatalogClient(Catalog()),
            new TestSession());

        await viewModel.CreateUserAsync(CancellationToken.None);

        Assert.Null(api.LastMutation);
        Assert.True(viewModel.HasErrors);
        Assert.NotEmpty((IEnumerable<string>)viewModel.GetErrors(nameof(SecurityAdministrationViewModel.Username)));
        Assert.NotEmpty((IEnumerable<string>)viewModel.GetErrors(nameof(SecurityAdministrationViewModel.Password)));
    }

    [Fact]
    public async Task Delayed_mutation_from_previous_session_cannot_update_or_submit_after_relogin()
    {
        var api = new FakeSecurityAdministrationApiClient { HoldMutation = true };
        api.Users = new[] { User(7, "operator", roleIds: Array.Empty<int>(), decisions: Array.Empty<SecurityDecisionDto>()) };
        var session = new TestSession();
        using var viewModel = CreateViewModel(
            api,
            new FakePermissionCatalogClient(Catalog(Item("Inventory.Read"))),
            session);
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SelectUser(7);
        var mutation = viewModel.SetDirectDecisionAsync("Inventory.Read", true, CancellationToken.None);
        Assert.Equal(1, api.MutationCount);

        session.SignOut();
        session.SignIn(99);
        api.ReleaseMutation();
        await mutation;

        Assert.Empty(viewModel.Users);
        await viewModel.SetDirectDecisionAsync("Inventory.Read", false, CancellationToken.None);
        Assert.Equal(1, api.MutationCount);
    }

    private static SecurityAdministrationViewModel CreateViewModel(
        FakeSecurityAdministrationApiClient api,
        FakePermissionCatalogClient catalog,
        TestSession session) =>
        new(api, catalog, session, new DesktopNotificationCenter());

    private static PermissionCatalogDto Catalog(params PermissionCatalogItemDto[] items) =>
        new("1.0.0.0", "digest", items);

    private static PermissionCatalogItemDto Item(string key) =>
        new(key, key.Split('.')[0], key + " description", "Active");

    private static SecurityUserDto User(
        int id,
        string username,
        int[] roleIds,
        SecurityDecisionDto[] decisions,
        long revision = 1,
        bool isProtectedAccount = false) =>
        new(id, username, username, null, true, revision, roleIds, decisions, isProtectedAccount);

    private static SecurityRoleDto Role(
        int id,
        string name,
        string[] permissions,
        bool isActive = true) =>
        new(id, name, isActive, 1, Array.Empty<int>(), permissions);

    private sealed class FakeSecurityAdministrationApiClient : ISecurityAdministrationApiClient
    {
        public IReadOnlyList<SecurityUserDto> Users { get; set; } = Array.Empty<SecurityUserDto>();
        public IReadOnlyList<SecurityRoleDto> Roles { get; set; } = Array.Empty<SecurityRoleDto>();
        public SecurityAdministrationMutationRequest? LastMutation { get; private set; }
        public bool HoldMutation { get; set; }
        public int MutationCount { get; private set; }
        public Func<SecurityAdministrationMutationRequest, ApiResponse<SecurityAdministrationResultDto>>? MutationHandler { get; set; }
        private readonly TaskCompletionSource<ApiResponse<SecurityAdministrationResultDto>> _pendingMutation = new();

        public Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ListUsersAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<IReadOnlyList<SecurityUserDto>>.Success(Users));

        public Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<IReadOnlyList<SecurityRoleDto>>.Success(Roles));

        public Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(
            SecurityAdministrationMutationRequest request,
            CancellationToken cancellationToken)
        {
            LastMutation = request;
            MutationCount++;
            if (HoldMutation)
            {
                return _pendingMutation.Task;
            }

            if (MutationHandler != null)
            {
                return Task.FromResult(MutationHandler(request));
            }

            return Task.FromResult(ApiResponse<SecurityAdministrationResultDto>.Success(
                new SecurityAdministrationResultDto(request.ExpectedRevision ?? 0, true)));
        }

        public void ReleaseMutation() => _pendingMutation.TrySetResult(
            ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(1, true)));
    }

    private sealed class FakePermissionCatalogClient : IPermissionCatalogApiClient
    {
        private readonly PermissionCatalogDto _catalog;
        private readonly TaskCompletionSource<ApiResponse<PermissionCatalogDto>> _pending = new();

        public FakePermissionCatalogClient(PermissionCatalogDto catalog)
        {
            _catalog = catalog;
        }

        public bool Hold { get; set; }

        public Task<ApiResponse<PermissionCatalogDto>> GetCatalogAsync(CancellationToken cancellationToken) =>
            Hold
                ? _pending.Task
                : Task.FromResult(ApiResponse<PermissionCatalogDto>.Success(_catalog));

        public void Release() => _pending.TrySetResult(ApiResponse<PermissionCatalogDto>.Success(_catalog));
    }

    private sealed class TestSession : IDesktopSession
    {
        public event EventHandler? SessionChanged;
        public bool IsAuthenticated { get; private set; } = true;
        public DesktopSessionState State => IsAuthenticated ? DesktopSessionState.Authenticated : DesktopSessionState.LoggingOut;
        public CurrentUserDto? CurrentUser { get; private set; } = new(42, "admin", "Admin");
        public long Version { get; private set; } = 1;

        public void SignOut()
        {
            IsAuthenticated = false;
            CurrentUser = null;
            Version++;
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SignIn(int userId)
        {
            IsAuthenticated = true;
            CurrentUser = new CurrentUserDto(userId, "next-session", "Next session");
            Version++;
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
