using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.UiLab.Scenarios;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SecurityPaneStateTests
{
    [Theory]
    [InlineData(ApiStatusCode.Forbidden, SecurityDatasetState.Forbidden)]
    [InlineData(ApiStatusCode.InternalServerError, SecurityDatasetState.Failed)]
    public void Revoked_or_failed_users_clear_old_profiles_without_clearing_authorized_roles(ApiStatusCode status, SecurityDatasetState expected) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            Assert.NotEmpty(control.Users.Items);
            scene.Administration.UsersError = new ApiError(status, "PaneUnavailable", "Cannot load this pane.");
            Wait(control.ActivateAsync(default));
            Assert.Equal(expected, control.ViewModel.Users.State);
            Assert.Empty(control.Users.Items);
            Assert.Null(control.ViewModel.SelectedUser);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Roles.State);
            Assert.Equal(3, control.Roles.Items.Count);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Catalog.State);
            Assert.Equal(4, control.PermissionCatalog.Keys.Rows.Count);
            Assert.False(control.ViewModel.BeginDirectPermission("Sales.Receipts.Read"));
            scene.Administration.UsersError = null;
            Wait(control.ActivateAsync(default));
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Users.State);
            Assert.Equal(3, control.Users.Items.Count);
        });

    [Theory]
    [InlineData(ApiStatusCode.Forbidden, SecurityDatasetState.Forbidden)]
    [InlineData(ApiStatusCode.InternalServerError, SecurityDatasetState.Failed)]
    public void Revoked_or_failed_roles_clear_old_role_details_without_clearing_users(ApiStatusCode status, SecurityDatasetState expected) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            Assert.NotEmpty(control.Roles.Items);
            scene.Administration.RolesError = new ApiError(status, "PaneUnavailable", "Cannot load this pane.");
            Wait(control.ActivateAsync(default));
            Assert.Equal(expected, control.ViewModel.Roles.State);
            Assert.Empty(control.Roles.Items);
            Assert.Null(control.ViewModel.SelectedRole);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Users.State);
            Assert.Equal(3, control.Users.Items.Count);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Catalog.State);
            scene.Administration.RolesError = null;
            Wait(control.ActivateAsync(default));
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Roles.State);
            Assert.Equal(3, control.Roles.Items.Count);
        });

    [Theory]
    [InlineData(ApiStatusCode.Forbidden, SecurityDatasetState.Forbidden)]
    [InlineData(ApiStatusCode.InternalServerError, SecurityDatasetState.Failed)]
    public void Revoked_or_failed_catalog_clears_old_keys_without_clearing_accounts(ApiStatusCode status, SecurityDatasetState expected) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            Assert.NotEmpty(control.PermissionCatalog.Keys.Rows);
            scene.Catalog.Error = new ApiError(status, "PaneUnavailable", "Cannot load this pane.");
            Wait(control.ActivateAsync(default));
            Assert.Equal(expected, control.ViewModel.Catalog.State);
            Assert.Empty(control.PermissionCatalog.Keys.Rows);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Users.State);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Roles.State);
            Assert.Equal(3, control.Users.Items.Count);
            Assert.Equal(3, control.Roles.Items.Count);
            scene.Catalog.Error = null;
            Wait(control.ActivateAsync(default));
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Catalog.State);
            Assert.Equal(4, control.PermissionCatalog.Keys.Rows.Count);
        });

    [Fact]
    public void Held_read_exposes_loading_then_successful_empty_without_inventing_permission_denial() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            var gate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>> { IgnoreCancellation = true };
            scene.Administration.UsersGate = gate;
            var loading = control.ActivateAsync(default);
            Assert.False(loading.IsCompleted);
            Assert.Equal(SecurityDatasetState.Loading, control.ViewModel.Users.State);
            Assert.Empty(control.Users.Items);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Roles.State);
            gate.Complete(ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>.Success(Array.Empty<SecurityUserDto>()));
            Wait(loading);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Users.State);
            Assert.Empty(control.Users.Items);
            Assert.Null(control.ViewModel.SelectedUser);
            Assert.Equal(3, control.Roles.Items.Count);
        });

    [Fact]
    public void Held_role_read_exposes_loading_then_successful_empty_independently() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            var gate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityRoleDto>>> { IgnoreCancellation = true };
            scene.Administration.RolesGate = gate;
            var loading = control.ActivateAsync(default);
            Assert.False(loading.IsCompleted);
            Assert.Equal(SecurityDatasetState.Loading, control.ViewModel.Roles.State);
            Assert.Empty(control.Roles.Items);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Users.State);
            gate.Complete(ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityRoleDto>>.Success(Array.Empty<SecurityRoleDto>()));
            Wait(loading);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Roles.State);
            Assert.Empty(control.Roles.Items);
            Assert.Null(control.ViewModel.SelectedRole);
            Assert.Equal(3, control.Users.Items.Count);
        });

    [Fact]
    public void Held_catalog_read_exposes_loading_then_successful_empty_independently() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            var gate = new ScenarioGate<ApiResponse<PermissionCatalogDto>> { IgnoreCancellation = true };
            scene.Catalog.Gate = gate;
            var loading = control.ActivateAsync(default);
            Assert.False(loading.IsCompleted);
            Assert.Equal(SecurityDatasetState.Loading, control.ViewModel.Catalog.State);
            Assert.Empty(control.PermissionCatalog.Keys.Rows);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Users.State);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Roles.State);
            gate.Complete(ApiResponse<PermissionCatalogDto>.Success(new PermissionCatalogDto("test", "empty-catalog", Array.Empty<PermissionCatalogItemDto>())));
            Wait(loading);
            Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Catalog.State);
            Assert.Empty(control.PermissionCatalog.Keys.Rows);
            Assert.Equal(3, control.Users.Items.Count);
            Assert.Equal(3, control.Roles.Items.Count);
        });

    [Fact]
    public void Inactive_ordinary_targets_keep_direct_and_role_editing_but_protected_user_does_not() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.ViewModel.SelectUser(102);
            Assert.True(control.ViewModel.BeginDirectPermission("Sales.Receipts.Read"));
            control.ViewModel.SetDirectDecision(true);
            Assert.True(control.ViewModel.HasDirtyEditor);
            control.ViewModel.DiscardEditor();
            control.ViewModel.SelectRole(203);
            Assert.True(control.ViewModel.BeginRolePermissions());
            control.ViewModel.SetRoleGranted("Sales.Receipts.Read", true);
            Assert.True(control.ViewModel.HasDirtyEditor);
            control.ViewModel.DiscardEditor();
            control.ViewModel.SelectUser(103);
            Assert.False(control.ViewModel.BeginDirectPermission("Sales.Receipts.Read"));
            Assert.DoesNotContain(scene.Journal.Entries, e => e.Operation.Contains("Mutate"));
        });

    static void Wait(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }
        Assert.True(task.IsCompleted);
        task.GetAwaiter().GetResult();
    }
}
