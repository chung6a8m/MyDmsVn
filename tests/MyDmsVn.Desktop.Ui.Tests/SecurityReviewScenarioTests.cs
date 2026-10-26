using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.Tests;
using Xunit;

namespace MyDmsVn.Desktop.Ui.Tests;

public sealed class SecurityReviewScenarioTests
{
    [Fact]
    public void Host_noop_grant_response_chains_unchanged_revision_to_next_key()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L07, (_, _) =>
        {
            using var host = new UiLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            host.ModuleSelector.SelectedIndex = 1;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.ScenarioSelector.SelectedItem = SecurityScenarioId.SEC_L07;
            Until(() => host.CurrentSecurityScenarioData?.Id == SecurityScenarioId.SEC_L07 && !host.IsResetting);

            var scene = host.CurrentSecurityScenarioData!;
            var vm = host.CurrentSecurityControl!.ViewModel;
            Assert.True(vm.BeginRolePermissions());
            vm.SetRoleGranted("Inventory.Stock.Read", true);
            vm.SetRoleGranted("Sales.Receipts.Post", true);
            host.SecurityReviewToggleButton.PerformClick();
            host.SecurityMutationOutcomeSelector.SelectedItem = "No-op grant";
            host.SecurityArmMutationButton.PerformClick();

            var save = vm.SaveEditorAsync(default);
            Until(() => save.IsCompleted);
            save.GetAwaiter().GetResult();

            var requests = scene.Journal.Entries.Where(entry => entry.Operation == "Security.Mutate")
                .Select(entry => Assert.IsType<SecurityMutationJournalEntry>(entry.Request)).ToArray();
            Assert.Equal(new[] { "Inventory.Stock.Read", "Sales.Receipts.Post" }, requests.Select(request => request.PermissionKey));
            Assert.Equal(new long?[] { 21, 21 }, requests.Select(request => request.ExpectedRevision));
            var responses = scene.Journal.Entries.Where(entry => entry.Operation == "Security.Mutate.Result")
                .Select(entry => Assert.IsType<SecurityResponseJournalEntry>(entry.Request)).ToArray();
            Assert.Equal(new long?[] { 21, 22 }, responses.Select(response => response.AssignmentRevision));
            Assert.False(vm.HasDirtyEditor);
            Assert.All(new[] { "Inventory.Stock.Read", "Sales.Receipts.Post" },
                key => Assert.Contains(key, scene.Data.Roles.Single(role => role.RoleId == 201).PermissionKeys));
        });

    [Fact]
    public void Review_reload_reports_dirty_editor_block_without_new_reads()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L10, (_, _) =>
        {
            using var host = new UiLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            host.ModuleSelector.SelectedIndex = 1;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.ScenarioSelector.SelectedItem = SecurityScenarioId.SEC_L10;
            Until(() => host.CurrentSecurityScenarioData?.Id == SecurityScenarioId.SEC_L10 && !host.IsResetting);
            var scene = host.CurrentSecurityScenarioData!;
            var vm = host.CurrentSecurityControl!.ViewModel;
            Assert.True(vm.BeginDirectPermission("Sales.Receipts.Post"));
            vm.SetDirectDecision(true);
            Assert.True(vm.HasDirtyEditor);
            var reads = scene.Journal.Entries.Count;

            host.SecurityReviewToggleButton.PerformClick();
            host.SecurityReloadButton.PerformClick();
            Until(() => host.StatusText.IndexOf("blocked", StringComparison.OrdinalIgnoreCase) >= 0);

            Assert.Equal(reads, scene.Journal.Entries.Count);
            Assert.True(vm.HasDirtyEditor);
            Assert.Equal(true, vm.DirectDecision);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Host_membership_revision_fault_controls_reject_the_stale_loaded_revision(bool relatedRole)
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L04, (_, _) =>
        {
            using var host = new UiLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            host.ModuleSelector.SelectedIndex = 1;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.ScenarioSelector.SelectedItem = SecurityScenarioId.SEC_L04;
            Until(() => host.CurrentSecurityScenarioData?.Id == SecurityScenarioId.SEC_L04 && !host.IsResetting);
            host.SecurityReviewToggleButton.PerformClick();

            var scene = host.CurrentSecurityScenarioData!;
            var vm = host.CurrentSecurityControl!.ViewModel;
            Assert.Equal(101, vm.SelectedUserId);
            if (relatedRole) host.SecurityAdvanceMembershipRoleRevisionButton.PerformClick();
            else host.SecurityAdvanceMembershipUserRevisionButton.PerformClick();

            Assert.Equal(relatedRole ? 11 : 12, scene.Data.Users.Single(user => user.UserId == 101).AssignmentRevision);
            Assert.Equal(relatedRole ? 23 : 22, scene.Data.Roles.Single(role => role.RoleId == 202).AssignmentRevision);
            var pending = vm.SetMembershipAsync(202, true, default);
            Until(() => pending.IsCompleted);
            Assert.False(pending.GetAwaiter().GetResult());
            var request = Assert.IsType<SecurityMutationJournalEntry>(Assert.Single(scene.Journal.Entries,
                entry => entry.Operation == "Security.Mutate").Request);
            Assert.Equal(SecurityAdministrationOperationDto.SetMembership, request.Operation);
            Assert.Equal(101, request.TargetId);
            Assert.Equal(202, request.RelatedId);
            Assert.Equal(11, request.ExpectedRevision);
            Assert.Equal(22, request.ExpectedRelatedRevision);
            Assert.Contains(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate.Result" &&
                Assert.IsType<SecurityResponseJournalEntry>(entry.Request).Outcome == "Conflict");
            Assert.DoesNotContain(202, scene.Data.Users.Single(user => user.UserId == 101).RoleIds);
            Assert.DoesNotContain(101, scene.Data.Roles.Single(role => role.RoleId == 202).MemberUserIds);
        });

    [Fact]
    public void Host_persistent_membership_denial_keeps_role_management_independent()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L04, (_, _) =>
        {
            using var host = new UiLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            host.ModuleSelector.SelectedIndex = 1;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.ScenarioSelector.SelectedItem = SecurityScenarioId.SEC_L04;
            Until(() => host.CurrentSecurityScenarioData?.Id == SecurityScenarioId.SEC_L04 && !host.IsResetting);
            host.SecurityReviewToggleButton.PerformClick();
            host.SecurityDenyMembershipCheckBox.Checked = true;

            var scene = host.CurrentSecurityScenarioData!;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var response = scene.Administration.MutateAsync(new SecurityAdministrationMutationRequest(
                    SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetMembership,
                    101, relatedId: 202, isActive: true, expectedRevision: 11, expectedRelatedRevision: 22), default).GetAwaiter().GetResult();
                Assert.Equal(ApiStatusCode.Forbidden, response.Error!.Status);
            }
            Assert.DoesNotContain(202, scene.Data.Users.Single(user => user.UserId == 101).RoleIds);
            Assert.DoesNotContain(101, scene.Data.Roles.Single(role => role.RoleId == 202).MemberUserIds);

            var grant = scene.Administration.MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetPermission,
                201, permissionKey: "Sales.Receipts.Post", decision: true, expectedRevision: 21), default).GetAwaiter().GetResult();
            Assert.True(grant.IsSuccess);
            Assert.Equal(22, grant.Data!.AssignmentRevision);
            Assert.Contains("Sales.Receipts.Post", scene.Data.Roles.Single(role => role.RoleId == 201).PermissionKeys);
            Assert.Equal(2, scene.Journal.Entries.Count(entry => entry.Operation == "Security.Mutate.Result" &&
                Assert.IsType<SecurityResponseJournalEntry>(entry.Request).Outcome == "Forbidden"));
        });

    [Fact]
    public void Import_review_dialog_can_change_fixture_after_preview_before_confirm()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L09, (_, scene) =>
        {
            const string json = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";
            var preview = scene.Administration.PreviewPermissionImportAsync(
                PermissionImportTargetTypeDto.Role, 202, json, default).GetAwaiter().GetResult();
            Assert.True(preview.IsSuccess);

            using var dialog = new SecurityImportReviewDialog("Preview #202",
                () => scene.AdvanceImportTargetRevision(PermissionImportTargetTypeDto.Role, 202),
                () => scene.RotateCatalogDigest());
            dialog.Show();
            dialog.AdvanceTargetRevisionButton.PerformClick();
            dialog.ConfirmButton.PerformClick();

            Assert.Equal(DialogResult.OK, dialog.DialogResult);
            var apply = scene.Administration.ApplyPermissionImportAsync(PermissionImportTargetTypeDto.Role, 202,
                new PermissionAssignmentImportRequest(json, preview.Data!.TargetRevision, preview.Data.CatalogDigest), default).GetAwaiter().GetResult();
            Assert.Equal(ApiStatusCode.Conflict, apply.Error!.Status);
            Assert.DoesNotContain("Sales.Receipts.Read", scene.Data.Roles.Single(role => role.RoleId == 202).PermissionKeys);
        });

    [Fact]
    public void Import_review_dialog_catalog_change_rejects_apply_without_assignment()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L09, (_, scene) =>
        {
            const string json = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";
            var preview = scene.Administration.PreviewPermissionImportAsync(
                PermissionImportTargetTypeDto.Role, 202, json, default).GetAwaiter().GetResult();
            Assert.True(preview.IsSuccess);

            using var dialog = new SecurityImportReviewDialog("Preview #202",
                () => scene.AdvanceImportTargetRevision(PermissionImportTargetTypeDto.Role, 202),
                scene.RotateCatalogDigest);
            dialog.Show();
            dialog.RotateCatalogDigestButton.PerformClick();
            dialog.ConfirmButton.PerformClick();

            Assert.Equal(DialogResult.OK, dialog.DialogResult);
            var apply = scene.Administration.ApplyPermissionImportAsync(PermissionImportTargetTypeDto.Role, 202,
                new PermissionAssignmentImportRequest(json, preview.Data!.TargetRevision, preview.Data.CatalogDigest), default).GetAwaiter().GetResult();
            Assert.Equal("Security.ImportCatalogChanged", apply.Error!.Code);
            Assert.DoesNotContain("Sales.Receipts.Read", scene.Data.Roles.Single(role => role.RoleId == 202).PermissionKeys);
        });

    [Fact]
    public void Import_review_dialog_target_switch_sends_no_apply_to_new_role()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            const string json = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";
            control.ViewModel.SelectRole(202);
            Assert.True(control.ViewModel.PreviewImportAsync(PermissionImportTargetTypeDto.Role, json, default).GetAwaiter().GetResult());

            using var dialog = new SecurityImportReviewDialog("Preview #202",
                () => scene.AdvanceImportTargetRevision(PermissionImportTargetTypeDto.Role, 202),
                scene.RotateCatalogDigest,
                () => control.ViewModel.SelectRole(201));
            dialog.Show();
            dialog.SwitchTargetButton.PerformClick();
            dialog.ConfirmButton.PerformClick();

            Assert.Equal(DialogResult.OK, dialog.DialogResult);
            Assert.False(control.ViewModel.ApplyImportPreviewAsync(PermissionImportTargetTypeDto.Role,
                json, true, default).GetAwaiter().GetResult());
            Assert.Equal(201, control.ViewModel.SelectedRoleId);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.ImportApply");
        });

    [Fact]
    public void Host_review_can_release_a_late_import_preview_without_republishing_it()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            using var host = new UiLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            host.ModuleSelector.SelectedIndex = 1;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.ScenarioSelector.SelectedItem = SecurityScenarioId.SEC_L09;
            Until(() => host.CurrentSecurityScenarioData?.Id == SecurityScenarioId.SEC_L09 && !host.IsResetting);
            host.SecurityReviewToggleButton.PerformClick();
            host.SecurityHoldImportPreviewButton.PerformClick();

            var scene = host.CurrentSecurityScenarioData!;
            var viewModel = host.CurrentSecurityControl!.ViewModel;
            viewModel.SelectRole(202);
            const string json = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";
            var pending = viewModel.PreviewImportAsync(PermissionImportTargetTypeDto.Role, json, default);
            Assert.False(pending.IsCompleted);
            scene.Session.Replace();
            host.SecurityReleaseImportPreviewButton.PerformClick();
            Until(() => pending.IsCompleted);

            Assert.False(pending.GetAwaiter().GetResult());
            Assert.Null(viewModel.ImportPreview);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.ImportApply");
            host.Close();
        });

    [Fact]
    public void Explicit_review_mutation_replaces_scenario_preset()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            using var host = new UiLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            host.ModuleSelector.SelectedIndex = 1;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.ScenarioSelector.SelectedItem = SecurityScenarioId.SEC_L08;
            Until(() => host.CurrentSecurityScenarioData?.Id == SecurityScenarioId.SEC_L08 && !host.IsResetting);
            host.SecurityReviewToggleButton.PerformClick();
            host.SecurityMutationOutcomeSelector.SelectedItem = "Forbidden";
            host.SecurityArmMutationButton.PerformClick();

            var scene = host.CurrentSecurityScenarioData!;
            var response = scene.Administration.MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetPermission,
                201, permissionKey: "Sales.Receipts.Post", decision: true, expectedRevision: 21), default).GetAwaiter().GetResult();

            Assert.Equal(ApiStatusCode.Forbidden, response.Error!.Status);
            Assert.DoesNotContain("Sales.Receipts.Post", scene.Data.Roles[0].PermissionKeys);
            host.CloseWorkspaceButton.PerformClick();
            Until(() => host.CurrentSecurityControl == null);
        });

    [Fact]
    public void Host_review_gate_can_restore_denied_users_then_hold_an_empty_users_read()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            using var host = new UiLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            host.ModuleSelector.SelectedIndex = 1;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.ScenarioSelector.SelectedItem = SecurityScenarioId.SEC_L02;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting &&
                host.CurrentSecurityControl.ViewModel.Users.State == SecurityDatasetState.Forbidden);

            host.SecurityReviewToggleButton.PerformClick();
            host.SecurityReadSourceSelector.SelectedItem = "Users";
            host.SecurityReadOutcomeSelector.SelectedItem = "Success";
            host.SecurityArmReadButton.PerformClick();
            host.SecurityReloadButton.PerformClick();
            Until(() => host.CurrentSecurityControl!.ViewModel.Users.State == SecurityDatasetState.Ready);
            Assert.Equal(host.CurrentSecurityScenarioData!.Data.Users.Count,
                host.CurrentSecurityControl!.ViewModel.Users.Items.Count);

            host.SecurityReadOutcomeSelector.SelectedItem = "Empty";
            host.SecurityHoldReadCheckBox.Checked = true;
            host.SecurityArmReadButton.PerformClick();
            host.SecurityReloadButton.PerformClick();
            Until(() => host.CurrentSecurityControl!.ViewModel.Users.State == SecurityDatasetState.Loading);
            Assert.Equal(SecurityDatasetState.Ready, host.CurrentSecurityControl!.ViewModel.Roles.State);
            host.SecurityReleaseReadButton.PerformClick();
            Until(() => host.CurrentSecurityControl!.ViewModel.Users.State == SecurityDatasetState.Ready);
            Assert.Empty(host.CurrentSecurityControl!.ViewModel.Users.Items);
            Assert.Contains("Security.Users.Result outcome=Success count=0", host.JournalText);
            host.CloseWorkspaceButton.PerformClick();
            Until(() => host.CurrentSecurityControl == null);
        });

    [Fact]
    public async Task Held_read_response_is_consumed_once_so_the_next_review_reload_uses_the_fixture()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        var gate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>>();
        scene.Administration.UsersGate = gate;

        var held = scene.Administration.ListUsersAsync(default);
        Assert.False(held.IsCompleted);
        gate.Complete(ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>.Success(Array.Empty<SecurityUserDto>()));
        Assert.Empty((await held).Data!);

        var restored = await scene.Administration.ListUsersAsync(default);
        Assert.True(restored.IsSuccess);
        Assert.Equal(scene.Data.Users.Count, restored.Data!.Count);
    }

    [Fact]
    public async Task Mutation_journal_records_sanitized_request_and_returned_revision_without_password()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        var response = await scene.Administration.MutateAsync(new SecurityAdministrationMutationRequest(
            SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetPassword,
            101, password: "fixture-secret", expectedRevision: 11), default);

        Assert.True(response.IsSuccess);
        var call = Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        Assert.IsType<SecurityMutationJournalEntry>(call.Request);
        Assert.DoesNotContain("fixture-secret", string.Join("\n", scene.Journal.Entries.Select(entry => entry.Request?.ToString())));
        Assert.Contains(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate.Result" &&
            entry.Request is SecurityResponseJournalEntry result && result.AssignmentRevision == response.Data!.AssignmentRevision);
    }

    [Fact]
    public async Task Direct_permission_journal_distinguishes_allow_deny_and_inherit()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        long revision = 11;
        foreach (bool? decision in new bool?[] { true, false, null })
        {
            var response = await scene.Administration.MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetPermission,
                101, permissionKey: "Sales.Receipts.Post", decision: decision,
                expectedRevision: revision), default);
            Assert.True(response.IsSuccess);
            revision = response.Data!.AssignmentRevision;
        }

        var calls = scene.Journal.Entries.Where(entry => entry.Operation == "Security.Mutate")
            .Select(entry => Assert.IsType<SecurityMutationJournalEntry>(entry.Request)).ToArray();
        Assert.Equal(new bool?[] { true, false, null }, calls.Select(call => call.Decision));
        Assert.Contains("decision=true", calls[0].ToString());
        Assert.Contains("decision=false", calls[1].ToString());
        Assert.Contains("decision=null", calls[2].ToString());
    }

    [Fact]
    public void Host_uncertain_release_remains_available_when_ignore_owner_release_was_selected()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            using var host = new UiLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            host.ModuleSelector.SelectedIndex = 1;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.SecurityReviewToggleButton.PerformClick();
            host.SecurityMutationOutcomeSelector.SelectedItem = "Uncertain";
            host.SecurityIgnoreOwnerReleaseCheckBox.Checked = true;
            host.SecurityArmMutationButton.PerformClick();
            var pending = host.CurrentSecurityScenarioData!.Administration.MutateAsync(
                new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User,
                    SecurityAdministrationOperationDto.SetPassword, 101,
                    password: "fixture-secret", expectedRevision: 11), default);
            Assert.False(pending.IsCompleted);

            host.SecurityReleaseMutationButton.PerformClick();
            Until(() => pending.IsCompleted);
            Assert.ThrowsAny<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            Until(() => host.JournalText.Contains("Security.Mutate.Result outcome=OutcomeUnknown"));
            Assert.DoesNotContain("fixture-secret", host.JournalText);
            host.CloseWorkspaceButton.PerformClick();
            Until(() => host.CurrentSecurityControl == null);
        });

    [Fact]
    public void Poisoned_reset_keeps_failure_visible_and_can_release_an_older_held_read()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            using var host = new UiLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            host.ModuleSelector.SelectedIndex = 1;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.SecurityReviewToggleButton.PerformClick();
            host.SecurityHoldReadCheckBox.Checked = true;
            host.SecurityIgnoreCancellationCheckBox.Checked = true;
            host.SecurityIgnoreOwnerReleaseCheckBox.Checked = true;
            host.SecurityArmReadButton.PerformClick();
            var scene = host.CurrentSecurityScenarioData!;
            host.SecurityReloadButton.PerformClick();
            Until(() => scene.Administration.OutstandingOperations.Any(task => !task.IsCompleted));
            host.SecurityReloadButton.PerformClick();
            var reset = host.ResetForTestAsync();
            Until(() => reset.IsCompleted, 8);
            reset.GetAwaiter().GetResult();

            Assert.True(host.IsPoisoned);
            Assert.Contains("did not drain", host.StatusText);
            Assert.Same(scene, host.CurrentSecurityScenarioData);
            Assert.True(host.SecurityReleaseReadButton.Enabled);
            var failure = host.StatusText;
            host.SecurityReleaseReadButton.PerformClick();
            Until(() => scene.Administration.OutstandingOperations.All(task => task.IsCompleted));
            Assert.Equal(failure, host.StatusText);
            host.Close();
            Until(() => host.IsDisposed, 8);
        });

    private static void Until(Func<bool> predicate, int seconds = 5)
    {
        var watch = Stopwatch.StartNew();
        while (!predicate())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(seconds)) throw new TimeoutException("Security review host did not reach expected state.");
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }
    }
}
