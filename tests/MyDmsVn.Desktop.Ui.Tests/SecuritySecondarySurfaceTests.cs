using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms;
using Xunit;
using static MyDmsVn.Desktop.Tests.SecurityWorkspaceControlTests;

namespace MyDmsVn.Desktop.Tests;

public sealed class SecuritySecondarySurfaceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Membership_selector_is_themed_and_native_confirmation_is_single(bool dark, bool confirm) =>
        Run(SecurityScenarioId.SEC_L04, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 1;
            WithDialog("Gán vai trò — ", () => Button(control.UserDetail, "Gán vai trò…").PerformClick(), dialog =>
            {
                Assert.Contains(control.ViewModel.SelectedUser!.DisplayName, dialog.Text);
                var roles = Assert.IsType<BootstrapComboBox>(Assert.Single(Children(dialog).OfType<ComboBox>()));
                Assert.IsType<BootstrapButton>(dialog.AcceptButton);
                Assert.IsType<BootstrapButton>(dialog.CancelButton);
                roles.SelectedIndex = 1;
                var prior = BootstrapThemeManager.CurrentTheme;
                try
                {
                    BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
                    Assert.Same(roles, Assert.Single(Children(dialog).OfType<ComboBox>()));
                    Assert.True(roles.Font.Height > 0);
                    roles.Focus();
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(confirm ? Keys.Enter : Keys.Escape);
                }
                finally { BootstrapThemeManager.CurrentTheme = prior; }
            });
            Pump(control.LastOperation);
            var writes = scene.Journal.Entries.Where(entry => entry.Operation == "Security.Mutate").ToArray();
            Assert.Equal(confirm ? 1 : 0, writes.Length);
            if (confirm)
            {
                var request = Assert.IsType<SecurityMutationJournalEntry>(writes[0].Request);
                Assert.Equal(101, request.TargetId);
                Assert.Equal(202, request.RelatedId);
                Assert.Equal(11, request.ExpectedRevision);
                Assert.Equal(22, request.ExpectedRelatedRevision);
            }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Membership_confirmation_never_retargets_after_selection_or_session_change(bool replaceSession) =>
        Run(SecurityScenarioId.SEC_L04, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 1;
            WithDialog("Gán vai trò — ", () => Button(control.UserDetail, "Gán vai trò…").PerformClick(), dialog =>
            {
                Assert.Single(Children(dialog).OfType<ComboBox>()).SelectedIndex = 1;
                if (replaceSession) scene.Session.Replace();
                else control.ViewModel.SelectUser(102);
                if (dialog.DialogResult == DialogResult.None) ((Button)dialog.AcceptButton!).PerformClick();
            });
            Pump(control.LastOperation);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Theory]
    [InlineData("user", false)]
    [InlineData("user", true)]
    [InlineData("role", false)]
    [InlineData("role", true)]
    [InlineData("remove", false)]
    [InlineData("remove", true)]
    public void Context_confirmation_names_targets_and_native_choice_owns_one_write(string operation, bool confirm) =>
        Run(SecurityScenarioId.SEC_L04, (control, scene) =>
        {
            var role = operation == "role";
            if (role) control.Tabs.SelectedIndex = 1;
            if (operation == "remove") { control.UserDetail.Tabs.SelectedIndex = 1; control.UserDetail.Memberships.SelectedIndex = 0; }
            var detail = role ? (Control)control.RoleDetail : control.UserDetail;
            var userName = control.ViewModel.SelectedUser!.DisplayName;
            var roleName = control.ViewModel.SelectedRole!.Name;
            WithDialog(operation == "remove" ? "Gỡ vai trò" : "Đổi trạng thái", () => Button(detail, operation == "remove" ? "Gỡ vai trò" : "Đổi trạng thái…").PerformClick(), dialog =>
            {
                var copy = string.Join("\n", Children(dialog).OfType<Label>().Select(label => label.Text));
                Assert.Contains(role ? roleName : userName, copy);
                if (role) { Assert.Contains("thành viên", copy); Assert.Contains("quyền", copy); }
                if (operation == "remove") Assert.Contains(roleName, copy);
                Assert.IsType<BootstrapButton>(dialog.AcceptButton);
                Assert.IsType<BootstrapButton>(dialog.CancelButton);
                ((Control)dialog.AcceptButton!).Focus();
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(confirm ? Keys.Enter : Keys.Escape);
            });
            Pump(control.LastOperation);
            var writes = scene.Journal.Entries.Where(entry => entry.Operation == "Security.Mutate").ToArray();
            Assert.Equal(confirm ? 1 : 0, writes.Length);
            if (confirm)
            {
                var request = Assert.IsType<SecurityMutationJournalEntry>(writes[0].Request);
                Assert.Equal(role ? 201 : 101, request.TargetId);
                Assert.Equal(operation == "remove" ? SecurityAdministrationOperationDto.SetMembership : SecurityAdministrationOperationDto.SetActive, request.Operation);
            }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Activation_confirmation_cannot_dispatch_to_a_changed_target(bool role) =>
        Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            if (role) control.Tabs.SelectedIndex = 1;
            WithDialog("Đổi trạng thái", () => Button(role ? (Control)control.RoleDetail : control.UserDetail, "Đổi trạng thái…").PerformClick(), dialog =>
            {
                if (role) control.ViewModel.SelectRole(202); else control.ViewModel.SelectUser(102);
                ((Button)dialog.AcceptButton!).PerformClick();
            });
            Pump(control.LastOperation);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Import_preview_contains_exact_revision_digest_and_uses_themed_context_action() =>
        Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.ViewModel.SelectRole(202);
            Assert.IsType<BootstrapButton>(control.RoleImportButton);
            control.SetImportDialogs(() => "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}", summary =>
            {
                Assert.Contains("#202", summary);
                Assert.Contains("Revision: " + control.ViewModel.ImportPreview!.TargetRevision, summary);
                Assert.Contains("Catalog digest: " + control.ViewModel.ImportPreview.CatalogDigest, summary);
                Assert.Contains("Sales.Receipts.Read", summary);
                return false;
            });
            control.RoleImportButton.PerformClick();
            Pump(control.LastOperation);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.ImportApply");
        });

    [Fact]
    public void Direct_role_import_feedback_uses_preview_owner_instead_of_visible_user_or_stale_picker_target() =>
        Run(SecurityScenarioId.SEC_L01, (control, _) =>
        {
            var userJson = "{\"schemaVersion\":1,\"targetType\":\"User\",\"targetId\":101,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";
            control.SetImportDialogs(() => userJson, _ => false);
            control.UserImportButton.PerformClick();
            Pump(control.LastOperation);

            control.Tabs.SelectedIndex = 0;
            control.ViewModel.SelectRole(202);
            var roleJson = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";
            Pump(control.ViewModel.PreviewImportAsync(PermissionImportTargetTypeDto.Role, roleJson, default));

            var statuses = Children(control).OfType<Label>().ToArray();
            var userStatus = Assert.Single(statuses, label => label.AccessibleName == "Trạng thái nhập quyền riêng");
            var roleStatus = Assert.Single(statuses, label => label.AccessibleName == "Trạng thái nhập quyền vai trò");
            Assert.Equal(control.ViewModel.ImportStatus, roleStatus.Text);
            control.Tabs.SelectedIndex = 1;
            System.Windows.Forms.Application.DoEvents();
            Assert.True(roleStatus.Visible);
            Assert.Empty(userStatus.Text);
            Assert.False(userStatus.Visible);
        });

    [Fact]
    public void Lab_import_review_separates_developer_faults_from_confirmation()
    {
        Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var dialog = new SecurityImportReviewDialog("target #202\nRevision: 22\nCatalog digest: fixture", () => { }, () => { });
            dialog.Show();
            Assert.IsType<BootstrapButton>(dialog.ConfirmButton);
            Assert.IsType<BootstrapButton>(dialog.CancelButton);
            Assert.Contains(Children(dialog).OfType<BootstrapLabel>(), label => label.Text.Contains("Công cụ phát triển"));
            var preview = Assert.Single(Children(dialog).OfType<BootstrapLabel>(), label => label.Text.Contains("target #202"));
            Assert.Equal(preview.Text, preview.AccessibilityObject.Description);
            dialog.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Product_import_preview_uses_owned_themed_modal_and_native_choice(bool confirm) =>
        Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.ViewModel.SelectRole(202);
            // Preserve the real product confirmation; replace only OS file-system IO.
            var confirmation = (Func<string, bool>)typeof(MyDmsVn.Desktop.WinForms.Features.Security.SecurityWorkspaceControl)
                .GetField("_confirmImport", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(control)!;
            control.SetImportDialogs(() => "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}", confirmation);
            WithDialog("Xem trước nhập quyền", () => { control.RoleImportButton.PerformClick(); Pump(control.LastOperation); }, dialog =>
            {
                var label = Assert.Single(Children(dialog).OfType<BootstrapLabel>(), item => item.Text.Contains("Catalog digest:"));
                Assert.Contains("#202", label.Text);
                Assert.Contains("Revision: 22", label.Text);
                Assert.Contains("Sales.Receipts.Read", label.Text);
                Assert.Equal(label.Text, label.AccessibilityObject.Description);
                Assert.IsType<BootstrapButton>(dialog.AcceptButton);
                Assert.IsType<BootstrapButton>(dialog.CancelButton);
                var prior = BootstrapThemeManager.CurrentTheme;
                try
                {
                    BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(BootstrapThemeMode.Dark);
                    BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(BootstrapThemeMode.Light);
                    Assert.True(label.Font.Height > 0);
                    ((Control)dialog.AcceptButton!).Focus();
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(confirm ? Keys.Enter : Keys.Escape);
                }
                finally { BootstrapThemeManager.CurrentTheme = prior; }
            });
            Assert.Equal(confirm ? 1 : 0, scene.Journal.Entries.Count(entry => entry.Operation == "Security.ImportApply"));
        });

    [Fact]
    public void Dirty_navigation_escape_preserves_draft_without_write() =>
        Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.ViewModel.BeginDirectPermission("Sales.Receipts.Read");
            control.ViewModel.SetDirectDecision(true);
            var revision = control.ViewModel.EditorRevision;
            WithDialog("Thay đổi chưa lưu", () => Pump(control.AdmitNavigationAsync()), dialog =>
            {
                Assert.Equal(3, Children(dialog).OfType<BootstrapButton>().Count());
                Assert.Equal("Ở lại", ((Control)dialog.CancelButton!).Text);
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
            });
            Assert.Equal(revision, control.ViewModel.EditorRevision);
            Assert.True(control.ViewModel.HasDirtyEditor);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Dirty_navigation_decision_does_not_discard_a_newer_edit() =>
        Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.ViewModel.BeginDirectPermission("Sales.Receipts.Read");
            control.ViewModel.SetDirectDecision(true);
            WithDialog("Thay đổi chưa lưu", () => Pump(control.AdmitNavigationAsync()), dialog =>
            {
                control.ViewModel.SetDirectDecision(false);
                Button(dialog, "Bỏ thay đổi").PerformClick();
            });
            Assert.True(control.ViewModel.HasDirtyEditor);
            Assert.Equal(false, control.ViewModel.DirectDecision);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Recovery_confirmation_escape_keeps_one_persistent_explanation() =>
        Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            scene.Administration.MutationErrors.Enqueue(new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.Conflict, "Security.RevisionConflict", "fixture"));
            WithDialog("Đổi trạng thái", () => Button(control.UserDetail, "Đổi trạng thái…").PerformClick(), dialog => ((Button)dialog.AcceptButton!).PerformClick());
            Pump(control.LastOperation);
            Assert.True(control.ViewModel.RequiresRecovery);
            Assert.False(control.UserDetail.ActionFeedback.Visible);
            var feedback = Assert.Single(Children(control.UserDetail).OfType<Label>(), label => label.Visible && label.Text == control.ViewModel.LastMutationFeedback!.Message);
            Assert.IsType<BootstrapLabel>(feedback);
            Pump(control.ViewModel.ReloadForComparisonAsync(default));
            var review = Assert.IsType<BootstrapButton>(Button(control.UserDetail, "Đã đối chiếu — lập diff mới"));
            WithDialog("Đối chiếu", review.PerformClick, dialog => NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape));
            Assert.True(control.ViewModel.RequiresRecovery);
            Assert.True(feedback.Visible);
            Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Removing_membership_after_saving_same_user_draft_uses_refreshed_revision() =>
        Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 1;
            control.UserDetail.Memberships.SelectedIndex = 0;
            control.ViewModel.BeginDirectPermission("Sales.Receipts.Post");
            control.ViewModel.SetDirectDecision(true);
            control.NavigationResolutionOverride = () => MyDmsVn.Desktop.Application.Security.SecurityDraftResolution.Save;
            WithDialog("Gỡ vai trò", () => { Button(control.UserDetail, "Gỡ vai trò").PerformClick(); Pump(control.LastOperation); }, dialog => ((Button)dialog.AcceptButton!).PerformClick());
            var requests = scene.Journal.Entries.Where(entry => entry.Operation == "Security.Mutate").Select(entry => Assert.IsType<SecurityMutationJournalEntry>(entry.Request)).ToArray();
            Assert.Equal(2, requests.Length);
            Assert.Equal(SecurityAdministrationOperationDto.SetMembership, requests[1].Operation);
            Assert.Equal(101, requests[1].TargetId);
            Assert.Equal(201, requests[1].RelatedId);
            Assert.Equal(12, requests[1].ExpectedRevision);
        });

    [Fact]
    public void Assignment_recovery_does_not_reuse_an_earlier_activation_error() =>
        Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            var error = new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.Conflict, "Security.RevisionConflict", "fixture");
            scene.Administration.MutationErrors.Enqueue(error);
            WithDialog("Đổi trạng thái", () => Button(control.UserDetail, "Đổi trạng thái…").PerformClick(), dialog => ((Button)dialog.AcceptButton!).PerformClick());
            Pump(control.LastOperation);
            Pump(control.ViewModel.ReloadForComparisonAsync(default));
            Assert.True(control.ViewModel.ReviewRecoveryAndRebase());
            control.UserDetail.Tabs.SelectedIndex = 3;
            Assert.True(control.ViewModel.BeginDirectPermission("Sales.Receipts.Post"));
            control.ViewModel.SetDirectDecision(true);
            scene.Administration.MutationErrors.Enqueue(error);
            Pump(control.ViewModel.SaveEditorAsync(default));
            Assert.True(control.ViewModel.RequiresRecovery);
            Assert.Contains(Children(control.UserDetail).OfType<Label>(), label => label.Visible && label.Text == control.ViewModel.EditingStatus);
            Assert.DoesNotContain(Children(control.UserDetail).OfType<Label>(), label => label.Visible && label.Text == control.ViewModel.LastMutationFeedback!.Message);
        });

    internal static System.Collections.Generic.IEnumerable<Control> Children(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }

    static Button Button(Control root, string text) => Assert.Single(Children(root).OfType<Button>(), button => button.Text == text);
    static void Pump(System.Threading.Tasks.Task task)
    {
        var clock = Stopwatch.StartNew();
        while (!task.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(6)) { System.Windows.Forms.Application.DoEvents(); Thread.Yield(); }
        Assert.True(task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    static void WithDialog(string title, Action open, Action<Form> inspect)
    {
        Exception? failure = null;
        var observed = false;
        var clock = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text.StartsWith(title));
            if (dialog == null)
            {
                if (clock.Elapsed < TimeSpan.FromSeconds(1)) return;
                timer.Stop();
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                return;
            }
            timer.Stop();
            observed = true;
            try { inspect(dialog); }
            catch (Exception error) { failure = error; }
            finally { if (!dialog.IsDisposed && dialog.DialogResult == DialogResult.None) dialog.Close(); }
        };
        timer.Start();
        open();
        if (failure != null) throw failure;
        Assert.True(observed, "The themed, owner-bound decision dialog was not opened: " + title);
    }
}
