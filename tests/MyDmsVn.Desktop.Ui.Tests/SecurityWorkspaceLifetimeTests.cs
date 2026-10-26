using System;
using System.Linq;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.UiLab.Scenarios;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SecurityWorkspaceLifetimeTests
{
    [Fact]
    public async Task Clean_target_switch_discards_the_old_editor_identity()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        vm.BeginRolePermissions();
        Assert.Equal(201, vm.RoleDraft!.RoleId);
        vm.SelectRole(202);
        Assert.Null(vm.RoleDraft);
        Assert.Equal(202, vm.SelectedRoleId);
        Assert.True(vm.BeginRolePermissions());
        Assert.Equal(202, vm.RoleDraft!.RoleId);

        vm.DiscardEditor();
        Assert.True(vm.BeginDirectPermission("Sales.Receipts.Post"));
        Assert.Equal(101, vm.SelectedUserId);
        vm.SelectUser(102);
        Assert.Null(vm.DirectPermissionKey);
        Assert.Equal(102, vm.SelectedUserId);
    }

    [Fact]
    public async Task Rejected_direct_permission_save_reports_known_rejection()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L06);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        Assert.True(vm.BeginDirectPermission("Sales.Receipts.Read"));
        vm.SetDirectDecision(false);
        scene.Administration.MutationErrors.Enqueue(new ApiError(ApiStatusCode.BadRequest,
            "ValidationError", "Rejected by validation."));

        await vm.SaveEditorAsync(default);

        Assert.Equal(SecuritySaveStopReason.Rejected, vm.LastSaveOutcome!.StopReason);
        Assert.True(vm.RequiresRecovery);
        Assert.Equal("Sales.Receipts.Read", vm.DirectPermissionKey);
        Assert.Equal(false, vm.DirectDecision);
        Assert.Contains("Không lưu", vm.EditingStatus);
        Assert.DoesNotContain("cần đối chiếu kết quả", vm.EditingStatus);
        Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    }
    [Fact]
    public async Task Recovery_cannot_be_discarded_before_comparison()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        vm.BeginRolePermissions();
        vm.SetRoleGranted("Sales.Receipts.Post", true);
        scene.Administration.MutationErrors.Enqueue(new ApiError(ApiStatusCode.Conflict,
            "Security.RevisionConflict", "Changed elsewhere."));
        await vm.SaveEditorAsync(default);
        Assert.True(vm.RequiresRecovery);

        vm.DiscardEditor();

        Assert.True(vm.RequiresRecovery);
        Assert.False(vm.CanNavigate);
        Assert.NotNull(vm.RoleDraft);
    }

    [Fact]
    public async Task Rebase_preserves_unrelated_grant_added_by_another_actor()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        vm.BeginRolePermissions();
        vm.SetRoleGranted("Sales.Receipts.Read", false);
        scene.Administration.MutationErrors.Enqueue(new ApiError(ApiStatusCode.Conflict,
            "Security.RevisionConflict", "Changed elsewhere."));
        await vm.SaveEditorAsync(default);
        var oldRole = scene.Data.Roles[0];
        scene.Data.Roles[0] = new SecurityRoleDto(oldRole.RoleId, oldRole.Name, oldRole.IsActive,
            oldRole.AssignmentRevision + 1, oldRole.MemberUserIds,
            oldRole.PermissionKeys.Concat(new[] { "Sales.Receipts.Post" }).ToArray());

        await vm.ReloadForComparisonAsync(default);
        Assert.True(vm.ReviewRecoveryAndRebase());

        Assert.Contains("Sales.Receipts.Post", vm.RoleDraft!.DesiredKeys);
        Assert.Equal(new[] { "Sales.Receipts.Read" }, vm.RoleDraft.Changes.Select(change => change.PermissionKey));
    }

    [Fact]
    public async Task Forbidden_mutation_revokes_only_the_affected_dataset()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        scene.Administration.MutationErrors.Enqueue(new ApiError(ApiStatusCode.Forbidden,
            "Auth.Forbidden", "Access changed."));
        var role = vm.SelectedRole!;

        Assert.False(await vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(
            SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetActive,
            role.RoleId, isActive: false, expectedRevision: role.AssignmentRevision), default));

        Assert.Equal(SecurityDatasetState.Forbidden, vm.Roles.State);
        Assert.Empty(vm.Roles.Items);
        Assert.Equal(SecurityDatasetState.Ready, vm.Users.State);
    }

    [Fact]
    public async Task Forbidden_role_refresh_clears_a_clean_grant_editor()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        Assert.True(vm.BeginRolePermissions());
        Assert.NotNull(vm.RoleDraft);
        scene.Administration.RolesError = new ApiError(ApiStatusCode.Forbidden,
            "Auth.Forbidden", "Role access revoked.");

        await vm.LoadAsync(default);

        Assert.Equal(SecurityDatasetState.Forbidden, vm.Roles.State);
        Assert.Null(vm.RoleDraft);
        Assert.Equal(SecurityDatasetState.Ready, vm.Users.State);
    }

    [Fact]
    public async Task Draftless_recovery_requires_the_affected_dataset_to_reload()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        var role = vm.SelectedRole!;
        scene.Administration.RolesError = new ApiError(ApiStatusCode.InternalServerError,
            "Failed", "Roles unavailable.");

        Assert.False(await vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(
            SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetActive,
            role.RoleId, isActive: false, expectedRevision: role.AssignmentRevision), default));
        Assert.True(vm.RequiresRecovery);
        Assert.False(vm.ReviewRecoveryAndRebase());
        scene.Administration.RolesError = null;
        await vm.ReloadForComparisonAsync(default);
        Assert.True(vm.ReviewRecoveryAndRebase());
        Assert.False(vm.RequiresRecovery);
    }

    [Fact]
    public void Direct_conflict_recovery_actions_remain_reachable_in_user_tab() => SecurityWorkspaceControlTests.Run(
        SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 3;
            Assert.True(control.ViewModel.BeginDirectPermission("Sales.Receipts.Post"));
            control.ViewModel.SetDirectDecision(true);
            scene.Administration.MutationErrors.Enqueue(new ApiError(ApiStatusCode.Conflict,
                "Security.RevisionConflict", "Changed elsewhere."));
            Pump(control.ViewModel.SaveEditorAsync(default));
            Assert.True(control.ViewModel.RequiresRecovery);
            control.RecoveryConfirmationOverride = () => true;

            var review = Find(control).Single(button => button.Text == "Đã đối chiếu — lập diff mới");
            Assert.Same(control.UserDetail, review.Parent!.Parent);
            Assert.True(review.Parent.Visible);
            Assert.InRange(review.Parent.Bottom, 1, control.UserDetail.ClientSize.Height);
            review.PerformClick();
            Pump(control.LastOperation);
            Assert.True(control.ViewModel.RequiresRecovery);
            var reload = Find(control).Single(button => button.Text == "Tải lại để đối chiếu");
            reload.PerformClick();
            Pump(control.LastOperation);
            review.PerformClick();
            Pump(control.LastOperation);

            Assert.False(control.ViewModel.RequiresRecovery);
            Assert.False(review.Parent!.Visible);
            Assert.True(control.ViewModel.HasDirtyEditor);
            Assert.Equal(3, control.UserDetail.Tabs.SelectedIndex);
        });

    [Fact]
    public void Role_recovery_is_reachable_from_members_tab() => SecurityWorkspaceControlTests.Run(
        SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 1;
            var role = control.ViewModel.SelectedRole!;
            scene.Administration.RolesError = new ApiError(ApiStatusCode.InternalServerError, "Failed", "Roles unavailable.");
            Pump(control.ViewModel.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetActive,
                role.RoleId, isActive: false, expectedRevision: role.AssignmentRevision), default));
            Assert.True(control.ViewModel.RequiresRecovery);
            var reload = Find(control).Single(button => button.Text == "Tải lại để đối chiếu");
            Assert.Same(control.RoleDetail, reload.Parent!.Parent);
            Assert.True(reload.Visible);
            Assert.InRange(reload.Parent.Bottom, 1, control.RoleDetail.ClientSize.Height);
            Assert.Equal(1, control.RoleDetail.Tabs.SelectedIndex);
        });

    static void Pump(Task task)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5))
            System.Windows.Forms.Application.DoEvents();
        Assert.True(task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Dirty_navigation_and_close_preserve_the_same_editor_revision()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        Assert.True(vm.BeginRolePermissions());
        vm.SetRoleGranted("Sales.Receipts.Post", true);
        var draft = vm.RoleDraft;
        vm.SelectRole(202);
        await vm.LoadAsync(default);
        Assert.Same(draft, vm.RoleDraft);
        Assert.Equal(201, vm.SelectedRoleId);
        Assert.False(await vm.ResolveDraftAsync(SecurityDraftResolution.Stay, default));
        Assert.Same(draft, vm.RoleDraft);
        Assert.True(await vm.ResolveDraftAsync(SecurityDraftResolution.Discard, default));
        Assert.True(vm.CanNavigate);
        Assert.DoesNotContain(scene.Journal.Entries, e => e.Operation == "Security.Mutate");
    }

    [Fact]
    public async Task In_flight_write_blocks_ordinary_navigation_and_close()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        vm.BeginRolePermissions();
        vm.SetRoleGranted("Sales.Receipts.Post", true);
        var gate = scene.Administration.HoldNextMutation(true);
        var saving = vm.SaveEditorAsync(default);
        Assert.True(vm.IsBusy);
        vm.SelectRole(202);
        Assert.Equal(201, vm.SelectedRoleId);
        Assert.False(await vm.ResolveDraftAsync(SecurityDraftResolution.Discard, default));
        gate.Complete(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(22, true)));
        await saving;
        Assert.True(vm.CanNavigate);
        Assert.Contains("Sales.Receipts.Post", vm.SelectedRole!.PermissionKeys);
    }

    [Fact]
    public async Task Forced_session_replacement_stops_later_items_and_discards_old_results()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        vm.BeginRolePermissions();
        vm.SetRoleGranted("Sales.Receipts.Post", true);
        vm.SetRoleGranted("Sales.Receipts.Read", false);
        var gate = scene.Administration.HoldNextMutation(true);
        var saving = vm.SaveEditorAsync(default);
        scene.Session.Replace();
        gate.Complete(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(22, true)));
        await saving;
        Assert.Null(vm.RoleDraft);
        Assert.Null(vm.LastSaveOutcome);
        Assert.Empty(vm.Users.Items);
        Assert.Empty(vm.Roles.Items);
        Assert.Single(scene.Journal.Entries, e => e.Operation == "Security.Mutate");
    }

    [Fact]
    public async Task Confirmed_save_with_failed_refresh_keeps_close_open()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        vm.BeginRolePermissions();
        vm.SetRoleGranted("Sales.Receipts.Post", true);
        scene.Administration.RolesError = new ApiError(ApiStatusCode.InternalServerError, "Failed", "Unavailable");
        await vm.SaveEditorAsync(default);
        Assert.True(vm.RequiresRecovery);
        Assert.NotNull(vm.RoleDraft);
        Assert.False(vm.CanNavigate);
        Assert.Contains("Đã lưu", vm.EditingStatus);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Confirmed_save_with_forbidden_required_refresh_scrubs_target_and_keeps_close_blocked(bool role)
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        if (role)
        {
            Assert.True(vm.BeginRolePermissions());
            vm.SetRoleGranted("Sales.Receipts.Post", true);
            scene.Administration.RolesError = new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Denied");
        }
        else
        {
            Assert.True(vm.BeginDirectPermission("Sales.Receipts.Post"));
            vm.SetDirectDecision(true);
            scene.Administration.UsersError = new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Denied");
        }

        await vm.SaveEditorAsync(default);

        Assert.Equal(SecuritySaveCompletion.Complete, vm.LastSaveOutcome?.Completion);
        Assert.Equal(SecurityRefreshOutcome.Failed, vm.LastSaveOutcome?.RefreshOutcome);
        Assert.True(vm.RequiresRecovery);
        Assert.False(vm.CanNavigate);
        Assert.False(await vm.ResolveDraftAsync(SecurityDraftResolution.Discard, default));
        Assert.Contains("Đã lưu, chưa tải lại được", vm.EditingStatus);
        Assert.Null(vm.RoleDraft);
        Assert.Null(vm.DirectPermissionKey);
        Assert.Empty(role ? vm.Roles.Items : vm.Users.Items);
        Assert.Equal(SecurityDatasetState.Forbidden, role ? vm.Roles.State : vm.Users.State);
        Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        scene.Administration.RolesError = null;
        scene.Administration.UsersError = null;
        Assert.True(await vm.ReloadForComparisonAsync(default));
        Assert.True(vm.ReviewRecoveryAndRebase());
        Assert.True(vm.CanNavigate);
        Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    }

    [Fact]
    public async Task Confirmed_profile_mutation_with_forbidden_refresh_keeps_recovery_after_clean_editor_scrub()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        Assert.True(vm.BeginRolePermissions());
        var role = vm.SelectedRole!;
        scene.Administration.RolesError = new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Denied");

        Assert.False(await vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(
            SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.Update,
            role.RoleId, name: role.Name + " changed", expectedRevision: role.AssignmentRevision), default));

        Assert.Null(vm.RoleDraft);
        Assert.Empty(vm.Roles.Items);
        Assert.True(vm.RequiresRecovery);
        Assert.False(vm.CanNavigate);
        Assert.Contains("Đã lưu, chưa tải lại được", vm.LastMutationFeedback!.Message);
        Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    }

    [Fact]
    public async Task Direct_tri_state_does_not_change_committed_projection_before_save()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        Assert.True(vm.BeginDirectPermission("Sales.Receipts.Post"));
        vm.SetDirectDecision(null);
        Assert.False(vm.SelectedUser!.DirectDecisions.Single(d => d.PermissionKey == "Sales.Receipts.Post").Decision);
        Assert.True(vm.HasDirtyEditor);
        await vm.SaveEditorAsync(default);
        Assert.DoesNotContain(vm.SelectedUser!.DirectDecisions, d => d.PermissionKey == "Sales.Receipts.Post");
        Assert.True(vm.CanNavigate);
    }

    static SecurityWorkspaceViewModel Model(SecurityScenario scene) => new(scene.Administration, scene.Catalog, scene.Session, new ImmediateUiDispatcher(), scene.Session.Version);

    [Fact]
    public async Task Context_actions_and_refresh_resolve_dirty_owner_without_overwrite()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        vm.BeginRolePermissions(); vm.SetRoleGranted("Sales.Receipts.Post", true);
        var draft = vm.RoleDraft;
        Assert.False(await vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.Role,
            SecurityAdministrationOperationDto.Update, 201, name: "Changed", expectedRevision: 21), default));
        await vm.LoadAsync(default);
        Assert.Same(draft, vm.RoleDraft);
        Assert.True(await vm.ResolveDraftAsync(SecurityDraftResolution.Save, default));
        Assert.True(vm.CanNavigate);
    }

    [Fact]
    public async Task Membership_uses_both_loaded_revisions_and_refreshes_both_entities()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        Assert.True(await vm.SetMembershipAsync(202, true, default));
        Assert.Contains(202, vm.SelectedUser!.RoleIds);
        Assert.Contains(101, vm.Roles.Items.Single(r => r.RoleId == 202).MemberUserIds);
        Assert.Equal(12, vm.SelectedUser.AssignmentRevision);
        Assert.Equal(23, vm.Roles.Items.Single(r => r.RoleId == 202).AssignmentRevision);
    }

    [Fact]
    public async Task Conflict_recovery_retains_intent_until_explicit_rebase_without_retry()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L08);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        vm.BeginRolePermissions();
        vm.SetRoleGranted("Sales.Receipts.Post", true);
        vm.SetRoleGranted("Sales.Receipts.Read", false);
        await vm.SaveEditorAsync(default);
        Assert.True(vm.RequiresRecovery);
        var outcome = vm.LastSaveOutcome;
        var calls = scene.Journal.Entries.Count(e => e.Operation == "Security.Mutate");
        await vm.ReloadForComparisonAsync(default);
        Assert.Equal(calls, scene.Journal.Entries.Count(e => e.Operation == "Security.Mutate"));
        Assert.True(vm.RequiresRecovery);
        Assert.True(vm.ReviewRecoveryAndRebase());
        Assert.Same(outcome, vm.LastSaveOutcome);
        Assert.False(vm.RequiresRecovery);
        Assert.Single(vm.RoleDraft!.Changes);
    }

    [Fact]
    public async Task Partial_conflict_status_shows_confirmed_rejected_and_unsent_counts()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L08);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        vm.BeginRolePermissions();
        vm.SetRoleGranted("Sales.Receipts.Post", true);
        vm.SetRoleGranted("Sales.Receipts.Read", false);
        vm.SetRoleGranted("Sales.Receipts.Write", false);

        await vm.SaveEditorAsync(default);

        Assert.Equal(SecuritySaveCompletion.Partial, vm.LastSaveOutcome!.Completion);
        Assert.Single(vm.LastSaveOutcome.ConfirmedKeys);
        Assert.Single(vm.LastSaveOutcome.RejectedKeys);
        Assert.Single(vm.LastSaveOutcome.UnattemptedKeys);
        Assert.Contains("Đã lưu 1", vm.EditingStatus);
        Assert.Contains("chưa gửi 1", vm.EditingStatus);
        Assert.Contains("xung đột", vm.EditingStatus, StringComparison.CurrentCultureIgnoreCase);
    }

    [Fact]
    public void Close_save_preserves_editor_identity_and_submitted_revision() => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, _) =>
    {
        control.ViewModel.BeginRolePermissions();
        control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
        var before = control.CaptureCloseState();
        var close = new MyDmsVn.Desktop.WinForms.WorkspaceCloseCoordinator((_, _) => Task.FromResult(MyDmsVn.Desktop.WinForms.WorkspaceCloseChoice.Save));
        var task = close.RequestCloseAsync(control, default);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5)) System.Windows.Forms.Application.DoEvents();
        Assert.True(task.IsCompleted);
        Assert.Equal(MyDmsVn.Desktop.WinForms.WorkspaceCloseDecision.Admitted, task.GetAwaiter().GetResult());
        Assert.Equal(before.EditorIdentity, control.CaptureCloseState().EditorIdentity);
        Assert.Equal(before.Revision, control.CaptureCloseState().Revision);
    });

    [Fact]
    public async Task Profile_create_rename_activation_and_password_use_explicit_commands()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        Assert.True(await vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.Create,
            name: "new.user", displayName: "New User", password: "temporary-password"), default));
        Assert.Equal("new.user", vm.SelectedUser!.Username);
        var user = vm.SelectedUser;
        Assert.True(await vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.Update,
            user.UserId, name: user.Username, displayName: "Updated User", expectedRevision: user.AssignmentRevision), default));
        user = vm.SelectedUser!;
        Assert.Equal("Updated User", user.DisplayName);
        Assert.True(await vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetPassword,
            user.UserId, password: "replacement-password", expectedRevision: user.AssignmentRevision), default));
        user = vm.SelectedUser!;
        Assert.True(await vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetActive,
            user.UserId, isActive: false, expectedRevision: user.AssignmentRevision), default));
        Assert.False(vm.SelectedUser!.IsActive);
        Assert.DoesNotContain("replacement-password", string.Join(" ", scene.Journal.Entries));
    }

    [Fact]
    public async Task Canceled_in_flight_response_is_unknown_and_reload_never_replays_it()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var vm = Model(scene);
        await vm.LoadAsync(default);
        vm.BeginRolePermissions(); vm.SetRoleGranted("Sales.Receipts.Post", true);
        var gate = scene.Administration.HoldNextMutation(true);
        var saving = vm.SaveEditorAsync(default);
        gate.Cancel();
        await saving;
        Assert.Equal(SecuritySaveStopReason.OutcomeUnknown, vm.LastSaveOutcome!.StopReason);
        Assert.Single(vm.LastSaveOutcome.UncertainKeys);
        var count = scene.Journal.Entries.Count(e => e.Operation == "Security.Mutate");
        await vm.ReloadForComparisonAsync(default);
        Assert.Equal(count, scene.Journal.Entries.Count(e => e.Operation == "Security.Mutate"));
        Assert.Equal(SecuritySaveStopReason.OutcomeUnknown, vm.LastSaveOutcome.StopReason);
        Assert.True(vm.ReviewRecoveryAndRebase());
        Assert.Equal(SecuritySaveStopReason.OutcomeUnknown, vm.LastSaveOutcome.StopReason);
        Assert.True(vm.HasDirtyEditor);
    }

    [Fact]
    public void Editor_button_tracks_held_mutation_until_refresh_finishes() => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.Tabs.SelectedIndex = 1;
        control.ViewModel.BeginRolePermissions();
        control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
        var gate = scene.Administration.HoldNextMutation(true);
        var save = Find(control).Single(b => b.Text == "Lưu quyền vai trò");
        save.PerformClick();
        Assert.True(control.ViewModel.IsBusy);
        Assert.False(control.LastOperation.IsCompleted);
        gate.Complete(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(22, true)));
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!control.LastOperation.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5)) System.Windows.Forms.Application.DoEvents();
        Assert.True(control.LastOperation.IsCompleted);
        control.LastOperation.GetAwaiter().GetResult();
        Assert.True(control.ViewModel.CanNavigate);
    });

    static System.Collections.Generic.IEnumerable<System.Windows.Forms.Button> Find(System.Windows.Forms.Control parent)
    {
        foreach (System.Windows.Forms.Control child in parent.Controls)
        {
            if (child is System.Windows.Forms.Button button) yield return button;
            foreach (var nested in Find(child)) yield return nested;
        }
    }
}
