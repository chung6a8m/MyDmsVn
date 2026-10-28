using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms.Features.Security;
using Xunit;
using Xunit.Abstractions;
using static MyDmsVn.Desktop.Tests.SecurityWorkspaceControlTests;

namespace MyDmsVn.Desktop.Tests;

[Trait("Module", "Security")]
public sealed class SecurityNativeAndResetTests
{
    readonly ITestOutputHelper output;

    public SecurityNativeAndResetTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Role_catalog_native_filter_retains_caret_range_key_and_enter_never_writes(bool dark) =>
        Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            var prior = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = MyDmsVn.Desktop.WinForms.BusinessCompactAppearance.CreateTheme(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
                control.Tabs.SelectedIndex = 2;
                NativeLookupTypingDriver.Activate(control.FindForm()!);
                var catalog = control.PermissionCatalog;
                catalog.Keys.CurrentCell = catalog.Keys.Rows.Cast<DataGridViewRow>().Single(row => (string)row.Cells[1].Value == "Sales.Receipts.Post").Cells[0];
                var search = Assert.IsType<BootstrapTextBox>((object)catalog.Search);
                search.Focus();
                var editor = GetFocus();
                var text = "";
                foreach (var character in "Sales.")
                {
                    SendMessage(editor, 0x0102, new IntPtr(character), IntPtr.Zero);
                    text += character;
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal(text, search.Text);
                    AssertNativeSelection(editor, text.Length, 0);
                    Assert.Equal(editor, GetFocus());
                    Assert.Equal("Sales.Receipts.Post", catalog.Keys.CurrentRow!.Cells[1].Value);
                }
                SendMessage(editor, 0x00B1, new IntPtr(0), new IntPtr(5));
                control.ViewModel.SelectRole(201);
                AssertNativeSelection(editor, 0, 5);
                Assert.Equal(editor, GetFocus());
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                Assert.Equal("Sales.Receipts.Post", catalog.Keys.CurrentRow!.Cells[1].Value);
                Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            }
            finally { BootstrapThemeManager.CurrentTheme = prior; }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void User_detail_effective_native_filter_retains_caret_key_and_owner_without_write(bool dark) =>
        Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            var prior = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = MyDmsVn.Desktop.WinForms.BusinessCompactAppearance.CreateTheme(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
                control.UserDetail.Tabs.SelectedIndex = 2;
                NativeLookupTypingDriver.Activate(control.FindForm()!);
                var detail = control.UserDetail;
                var row = Assert.Single(detail.Effective.Rows.Cast<DataGridViewRow>(), item => Equals(item.Tag, "Sales.Receipts.Post"));
                detail.Effective.CurrentCell = row.Cells[0];
                var search = Assert.IsType<BootstrapTextBox>((object)detail.Search);
                search.Focus();
                Assert.True(search.ContainsFocus);
                var editor = GetFocus();
                var text = "";
                foreach (var character in "Sales.")
                {
                    SendMessage(editor, 0x0102, new IntPtr(character), IntPtr.Zero);
                    text += character;
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal(text, search.Text);
                    AssertNativeSelection(editor, text.Length, 0);
                    Assert.Equal(editor, GetFocus());
                    Assert.Equal("Sales.Receipts.Post", detail.Effective.CurrentRow!.Tag);
                    Assert.Equal(101, control.ViewModel.SelectedUserId);
                }
                SendMessage(editor, 0x00B1, new IntPtr(0), new IntPtr(5));
                control.ViewModel.SelectUser(101);
                AssertNativeSelection(editor, 0, 5);
                Assert.Equal(editor, GetFocus());
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                Assert.Contains("Quyền riêng: Từ chối", detail.Explanation.Text);
                Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            }
            finally { BootstrapThemeManager.CurrentTheme = prior; }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_tab_moves_from_search_through_filter_to_master_list(bool roles) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, _) =>
        {
            if (roles) control.Tabs.SelectedIndex = 1;
            var form = control.FindForm()!;
            NativeLookupTypingDriver.Activate(form);
            var search = roles ? control.RoleSearch : control.UserSearch;
            Assert.IsType<BootstrapTextBox>((object)search);
            var activity = roles ? control.RoleActivity : control.UserActivity;
            var list = roles ? control.Roles : control.Users;
            search.Focus();
            Assert.True(search.ContainsFocus);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.True(activity.ContainsFocus, "Tab from search should focus the activity filter.");
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.True(list.ContainsFocus, "Tab from activity filter should focus the master list.");
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Down);
            Assert.True(Assert.Single(list.SelectedIndices.Cast<int>()) == 1,
                "Native Down: " + string.Join(";", list.Items.Cast<ListViewItem>().Select(item => item.Index + "=" + item.Bounds)) +
                "; focused=" + list.FocusedItem?.Index + "; HWND=" + GetFocus() + "; list=" + list.Handle);
            Assert.Equal(roles ? 202 : 102, roles ? control.ViewModel.SelectedRoleId : control.ViewModel.SelectedUserId);
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Native_master_down_keeps_selected_owner_after_reload_or_filter_rebind(bool roles, bool reload) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            if (roles) control.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var list = roles ? control.Roles : control.Users;
            var search = roles ? control.RoleSearch : control.UserSearch;
            var activity = roles ? control.RoleActivity : control.UserActivity;
            var bounds = list.Items[1].GetBounds(ItemBoundsPortion.Label);
            NativeLookupTypingDriver.Click(list, new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
            Assert.Equal(1, Assert.Single(list.SelectedIndices.Cast<int>()));
            Assert.Equal(roles ? 202 : 102, roles ? control.ViewModel.SelectedRoleId : control.ViewModel.SelectedUserId);
            search.Focus();
            Assert.True(search.ContainsFocus);
            var editor = GetFocus();
            if (reload)
            {
                // Fresh DTO instances retain domain IDs, as on a successful server reload.
                for (var i = 0; i < scene.Data.Users.Count; i++)
                {
                    var user = scene.Data.Users[i];
                    scene.Data.Users[i] = new SecurityUserDto(user.UserId, user.Username, user.DisplayName, user.Email,
                        user.IsActive, user.AssignmentRevision, user.RoleIds, user.DirectDecisions, user.IsProtectedAccount);
                }
                for (var i = 0; i < scene.Data.Roles.Count; i++)
                {
                    var role = scene.Data.Roles[i];
                    scene.Data.Roles[i] = new SecurityRoleDto(role.RoleId, role.Name, role.IsActive,
                        role.AssignmentRevision, role.MemberUserIds, role.PermissionKeys);
                }
                var operation = control.ActivateAsync(default);
                Until(() => operation.IsCompleted);
                operation.GetAwaiter().GetResult();
            }
            else
            {
                activity.SelectedIndex = roles ? 1 : 2;
                activity.SelectedIndex = 0;
            }
            Assert.True(search.ContainsFocus);
            Assert.Equal(editor, GetFocus());
            AssertNativeSelection(editor, 0, 0);
            Assert.Equal(roles ? 202 : 102, roles ? control.ViewModel.SelectedRoleId : control.ViewModel.SelectedUserId);
            Assert.Equal(1, Assert.Single(list.SelectedIndices.Cast<int>()));
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.True(activity.ContainsFocus);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.True(list.ContainsFocus);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Down);
            Assert.Equal(2, Assert.Single(list.SelectedIndices.Cast<int>()));
            Assert.Equal(roles ? 203 : 103, roles ? control.ViewModel.SelectedRoleId : control.ViewModel.SelectedUserId);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_master_down_continues_from_programmatic_domain_selection(bool roles) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            if (roles) control.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var list = roles ? control.Roles : control.Users;
            var search = roles ? control.RoleSearch : control.UserSearch;

            list.Items[0].Selected = true;
            list.Items[0].Focused = true;
            Assert.True(list.Focus());
            Assert.Equal(0, Assert.Single(list.SelectedIndices.Cast<int>()));
            Assert.Equal(0, list.FocusedItem?.Index);

            search.Focus();
            Assert.True(search.ContainsFocus);
            if (roles) control.ViewModel.SelectRole(202);
            else control.ViewModel.SelectUser(102);

            Assert.True(search.ContainsFocus, "Domain selection must not move keyboard focus to the master list.");
            Assert.Equal(1, Assert.Single(list.SelectedIndices.Cast<int>()));
            Assert.True(list.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Down);

            Assert.Equal(2, Assert.Single(list.SelectedIndices.Cast<int>()));
            Assert.Equal(roles ? 203 : 103, roles ? control.ViewModel.SelectedRoleId : control.ViewModel.SelectedUserId);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_role_member_open_aligns_selected_and_focused_user_after_hidden_target_filter(bool activityFilter) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var users = control.Users;
            users.Items[0].Selected = true;
            users.Items[0].Focused = true;
            Assert.True(users.Focus());
            Assert.Equal(101, ((SecurityUserDto)Assert.Single(users.SelectedItems.Cast<ListViewItem>()).Tag).UserId);
            Assert.Equal(101, ((SecurityUserDto)users.FocusedItem!.Tag).UserId);

            if (activityFilter) control.UserActivity.SelectedIndex = 1;
            else control.UserSearch.Text = "lan";
            Assert.DoesNotContain(users.Items.Cast<ListViewItem>(), item => ((SecurityUserDto)item.Tag).UserId == 102);

            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 1;
            control.RoleDetail.Members.SelectedIndex = 1;
            Assert.True(control.RoleDetail.OpenUser.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Until(() => control.LastOperation.IsCompleted);
            control.LastOperation.GetAwaiter().GetResult();

            Assert.Equal(0, control.Tabs.SelectedIndex);
            Assert.Equal(102, control.ViewModel.SelectedUserId);
            Assert.Equal(102, ((SecurityUserDto)Assert.Single(users.SelectedItems.Cast<ListViewItem>()).Tag).UserId);
            Assert.Equal(102, ((SecurityUserDto)users.FocusedItem!.Tag).UserId);
            Assert.True(users.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Down);
            Assert.Equal(103, ((SecurityUserDto)Assert.Single(users.SelectedItems.Cast<ListViewItem>()).Tag).UserId);
            Assert.Equal(103, control.ViewModel.SelectedUserId);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_enter_activates_explicit_create_without_mutating_on_cancel(bool roles) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            if (roles) control.Tabs.SelectedIndex = 1;
            var form = control.FindForm()!;
            NativeLookupTypingDriver.Activate(form);
            var detail = roles ? (Control)control.RoleDetail : control.UserDetail;
            var button = Descendants(detail).OfType<Button>()
                .Single(item => item.Text == (roles ? "Tạo vai trò…" : "Tạo người dùng…"));
            var opened = false;
            Exception? dialogError = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(item => item.Text == (roles ? "Tạo vai trò" : "Tạo người dùng"));
                if (dialog == null) return;
                timer.Stop();
                try
                {
                    opened = true;
                    var field = SecurityWorkspaceControlTests.DialogInputs(dialog).First();
                    field.Focus();
                    Assert.True(field.ContainsFocus);
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    Assert.Equal(DialogResult.Cancel, dialog.DialogResult);
                }
                catch (Exception error) { dialogError = error; dialog.Close(); }
            };
            Assert.True(button.Focus());
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not activate the focused create action.");
            if (dialogError != null) throw dialogError;
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_escape_cancels_identity_edit_without_mutation(bool roles) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L10, (control, scene) =>
        {
            if (roles) control.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var detail = roles ? (Control)control.RoleDetail : control.UserDetail;
            var button = Descendants(detail).OfType<Button>()
                .Single(item => item.Text == (roles ? "Sửa tên…" : "Sửa thông tin…"));
            var opened = false;
            Exception? dialogError = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(item => item.Text.StartsWith("Sửa — "));
                if (dialog == null) return;
                timer.Stop();
                try
                {
                    opened = true;
                    Assert.Equal("Hủy", Assert.IsType<BootstrapButton>(dialog.CancelButton).Text);
                    SecurityWorkspaceControlTests.DialogInputs(dialog).First().Focus();
                    Assert.True(SecurityWorkspaceControlTests.DialogInputs(dialog).First().ContainsFocus);
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    Assert.Equal(DialogResult.Cancel, dialog.DialogResult);
                }
                catch (Exception error) { dialogError = error; dialog.Close(); }
            };
            Assert.True(button.Focus());
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not activate the focused identity edit action.");
            if (dialogError != null) throw dialogError;
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_enter_opens_status_confirmation_and_escape_cancels_without_dispatch(bool roles) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            if (roles) control.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var detail = roles ? (Control)control.RoleDetail : control.UserDetail;
            var button = Assert.Single(Descendants(detail).OfType<Button>(), item => item.Text == "Đổi trạng thái…");
            var opened = false;
            Exception? dialogError = null;
            var elapsed = Stopwatch.StartNew();
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var title = new StringBuilder(80);
                GetWindowText(GetForegroundWindow(), title, title.Capacity);
                if (title.ToString() != "Đổi trạng thái")
                {
                    if (elapsed.Elapsed < TimeSpan.FromSeconds(4)) return;
                    timer.Stop();
                    dialogError = new TimeoutException("Native Enter did not open the status confirmation.");
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    return;
                }
                timer.Stop();
                try
                {
                    opened = true;
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                }
                catch (Exception error) { dialogError = error; }
            };
            Assert.True(button.Focus());
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not activate the focused status action.");
            if (dialogError != null) throw dialogError;
            Assert.True(button.ContainsFocus);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_enter_confirms_status_change_once_for_selected_owner(bool roles) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            if (roles) control.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var detail = roles ? (Control)control.RoleDetail : control.UserDetail;
            var button = Assert.Single(Descendants(detail).OfType<Button>(), item => item.Text == "Đổi trạng thái…");
            var opened = false;
            Exception? dialogError = null;
            var elapsed = Stopwatch.StartNew();
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var title = new StringBuilder(80);
                GetWindowText(GetForegroundWindow(), title, title.Capacity);
                if (title.ToString() != "Đổi trạng thái")
                {
                    if (elapsed.Elapsed < TimeSpan.FromSeconds(4)) return;
                    timer.Stop();
                    dialogError = new TimeoutException("Native Enter did not open the status confirmation.");
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    return;
                }
                timer.Stop();
                try
                {
                    opened = true;
                    Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                }
                catch (Exception error) { dialogError = error; NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape); }
            };
            Assert.True(button.Focus());
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not activate the focused status action.");
            if (dialogError != null) throw dialogError;
            Until(() => scene.Journal.Entries.Count(entry => entry.Operation == "Security.Catalog.Result") == 2);
            var request = Assert.IsType<SecurityMutationJournalEntry>(Assert.Single(scene.Journal.Entries,
                entry => entry.Operation == "Security.Mutate").Request);
            Assert.Equal(roles ? SecurityAdministrationTargetDto.Role : SecurityAdministrationTargetDto.User, request.Target);
            Assert.Equal(SecurityAdministrationOperationDto.SetActive, request.Operation);
            Assert.Equal(roles ? 201 : 101, request.TargetId);
            Assert.Equal(roles ? 21 : 11, request.ExpectedRevision);
            var result = Assert.IsType<SecurityResponseJournalEntry>(Assert.Single(scene.Journal.Entries,
                entry => entry.Operation == "Security.Mutate.Result").Request);
            Assert.Equal("Success", result.Outcome);
            Assert.Equal(roles ? 22 : 12, result.AssignmentRevision);
            Assert.False(roles ? control.ViewModel.SelectedRole!.IsActive : control.ViewModel.SelectedUser!.IsActive);
        });

    [Fact]
    public void Native_membership_dialog_names_role_and_escape_cancels_without_dispatch() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L04, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var button = Descendants(control.UserDetail).OfType<Button>().Single(item => item.Text == "Gán vai trò…");
            var opened = false;
            Exception? dialogError = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(item => item.Text.StartsWith("Gán vai trò — "));
                if (dialog == null) return;
                timer.Stop();
                try
                {
                    opened = true;
                    Assert.Contains(Descendants(dialog).OfType<Label>(), item => item.Visible && item.Text == "Vai trò");
                    var roles = Assert.Single(Descendants(dialog).OfType<ComboBox>());
                    Assert.Equal("Vai trò", roles.AccessibleName);
                    Assert.Equal("Hủy", Assert.IsType<BootstrapButton>(dialog.CancelButton).Text);
                    Assert.True(roles.Focus());
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    Assert.Equal(DialogResult.Cancel, dialog.DialogResult);
                }
                catch (Exception error) { dialogError = error; dialog.Close(); }
            };
            Assert.True(button.Focus());
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not open the membership dialog.");
            if (dialogError != null) throw dialogError;
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_membership_dialog_enter_confirms_selected_role_once() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L04, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var button = Descendants(control.UserDetail).OfType<Button>().Single(item => item.Text == "Gán vai trò…");
            var opened = false;
            Exception? dialogError = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(item => item.Text.StartsWith("Gán vai trò — "));
                if (dialog == null) return;
                timer.Stop();
                try
                {
                    opened = true;
                    var roles = Assert.Single(Descendants(dialog).OfType<ComboBox>());
                    roles.SelectedIndex = 1;
                    Assert.True(roles.Focus());
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                    Assert.Equal(DialogResult.OK, dialog.DialogResult);
                }
                catch (Exception error) { dialogError = error; dialog.Close(); }
            };
            Assert.True(button.Focus());
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not open the membership dialog.");
            if (dialogError != null) throw dialogError;
            var elapsed = Stopwatch.StartNew();
            while (!control.LastOperation.IsCompleted && elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Yield();
            }
            Assert.True(control.LastOperation.IsCompleted);
            Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate" &&
                entry.Request is SecurityMutationJournalEntry mutation &&
                mutation.Target == SecurityAdministrationTargetDto.User &&
                mutation.Operation == SecurityAdministrationOperationDto.SetMembership);
        });

    [Fact]
    public void Native_enter_opens_remove_role_confirmation_and_escape_keeps_membership() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L04, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var memberships = control.UserDetail.Memberships;
            var remove = Assert.Single(Descendants(control.UserDetail.Tabs.TabPages[1]).OfType<Button>(),
                item => item.Text == "Gỡ vai trò");
            Assert.False(remove.Enabled);
            Assert.True(memberships.Focus());
            // Details ListView may select its first item on focus; Home names this fixture's intended role.
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Home);
            Assert.Equal(0, memberships.SelectedIndex);
            Assert.True(remove.Enabled);
            for (var i = 0; i < 3 && !remove.ContainsFocus; i++)
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.True(remove.ContainsFocus, "Native Tab should reach Gỡ vai trò from the selected role.");
            var opened = false;
            Exception? dialogError = null;
            var elapsed = Stopwatch.StartNew();
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var title = new StringBuilder(80);
                GetWindowText(GetForegroundWindow(), title, title.Capacity);
                if (title.ToString() != "Gỡ vai trò")
                {
                    if (elapsed.Elapsed < TimeSpan.FromSeconds(4)) return;
                    timer.Stop();
                    dialogError = new TimeoutException("Native Enter did not open remove-role confirmation.");
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    return;
                }
                timer.Stop();
                try
                {
                    opened = true;
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                }
                catch (Exception error) { dialogError = error; }
            };
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not activate Gỡ vai trò.");
            if (dialogError != null) throw dialogError;
            Assert.True(remove.ContainsFocus, "Escape should return focus to Gỡ vai trò.");
            Assert.Equal(2, control.ViewModel.SelectedUser!.RoleIds.Count);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_enter_confirms_remove_role_once_for_selected_user() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L04, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var memberships = control.UserDetail.Memberships;
            var remove = Assert.Single(Descendants(control.UserDetail.Tabs.TabPages[1]).OfType<Button>(),
                item => item.Text == "Gỡ vai trò");
            Assert.True(memberships.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Home);
            Assert.Equal(0, memberships.SelectedIndex);
            for (var i = 0; i < 3 && !remove.ContainsFocus; i++)
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.True(remove.ContainsFocus);
            var opened = false;
            Exception? dialogError = null;
            var elapsed = Stopwatch.StartNew();
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var title = new StringBuilder(80);
                GetWindowText(GetForegroundWindow(), title, title.Capacity);
                if (title.ToString() != "Gỡ vai trò")
                {
                    if (elapsed.Elapsed < TimeSpan.FromSeconds(4)) return;
                    timer.Stop();
                    dialogError = new TimeoutException("Native Enter did not open remove-role confirmation.");
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    return;
                }
                timer.Stop();
                try
                {
                    opened = true;
                    Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                }
                catch (Exception error) { dialogError = error; NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape); }
            };
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not activate Gỡ vai trò.");
            if (dialogError != null) throw dialogError;
            Until(() => scene.Journal.Entries.Count(entry => entry.Operation == "Security.Catalog.Result") == 2);
            var request = Assert.IsType<SecurityMutationJournalEntry>(Assert.Single(scene.Journal.Entries,
                entry => entry.Operation == "Security.Mutate").Request);
            Assert.Equal(SecurityAdministrationTargetDto.User, request.Target);
            Assert.Equal(SecurityAdministrationOperationDto.SetMembership, request.Operation);
            Assert.Equal(101, request.TargetId);
            Assert.Equal(201, request.RelatedId);
            Assert.Equal(11, request.ExpectedRevision);
            Assert.Equal(21, request.ExpectedRelatedRevision);
            var result = Assert.IsType<SecurityResponseJournalEntry>(Assert.Single(scene.Journal.Entries,
                entry => entry.Operation == "Security.Mutate.Result").Request);
            Assert.Equal("Success", result.Outcome);
            Assert.Equal(12, result.AssignmentRevision);
            Assert.Equal(22, result.RelatedRevision);
            Assert.Single(control.ViewModel.SelectedUser!.RoleIds);
            Assert.Equal(101, control.ViewModel.SelectedUserId);
        });

    [Fact]
    public void Native_tab_enter_opens_selected_role_member_without_mutation() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L04, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var members = control.RoleDetail.Members;
            var open = control.RoleDetail.OpenUser;
            Assert.Equal(-1, members.SelectedIndex);
            Assert.False(open.Enabled);
            Assert.True(members.Focus());
            // Native Details ListView may select its first row on focus; name the target explicitly.
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Home);
            Assert.Equal(0, members.SelectedIndex);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Down);
            Assert.Equal(1, members.SelectedIndex);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Up);
            Assert.Equal(0, members.SelectedIndex);
            Assert.True(open.Enabled);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.True(open.ContainsFocus, "Tab from the selected member should focus Mở người dùng.");
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Until(() => control.ViewModel.SelectedUserId == 101 && control.Tabs.SelectedIndex == 0 &&
                control.UserDetail.Tabs.SelectedIndex == 1);
            Assert.Equal(6, scene.Journal.Entries.Count);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Native_role_space_autorepeat_toggles_once_until_keyup(int column) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L07, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            Assert.True(control.ViewModel.BeginRolePermissions());
            var grants = Assert.Single(Descendants(control.RoleDetail).OfType<BootstrapDataGridView>());
            var index = GrantIndex(grants, "Sales.Receipts.Post");
            grants.CurrentCell = grants.Rows[index].Cells[column];
            Assert.True(grants.Focus());
            Assert.Equal(grants.Handle, GetFocus());
            var draft = control.ViewModel.RoleDraft!;

            Assert.True(PostMessage(grants.Handle, 0x0100, new IntPtr((int)Keys.Space), new IntPtr(1)));
            System.Windows.Forms.Application.DoEvents();
            Assert.True(Granted(grants, index));
            Assert.Equal(1, draft.LocalEditRevision);
            for (var repeat = 0; repeat < 3; repeat++)
            {
                Assert.True(PostMessage(grants.Handle, 0x0100, new IntPtr((int)Keys.Space), new IntPtr((1L << 30) | 1)));
                System.Windows.Forms.Application.DoEvents();
                Assert.True(Granted(grants, index));
                Assert.Equal(1, draft.LocalEditRevision);
            }
            Assert.True(PostMessage(grants.Handle, 0x0101, new IntPtr((int)Keys.Space), new IntPtr(unchecked((int)0xC0000001))));
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(1, draft.LocalEditRevision);
            Assert.Collection(draft.Changes, change =>
            {
                Assert.Equal("Sales.Receipts.Post", change.PermissionKey);
                Assert.True(change.Granted);
            });
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.False(Granted(grants, index));
            Assert.Equal(2, draft.LocalEditRevision);
            Assert.Empty(draft.Changes);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_space_on_permission_name_toggles_only_the_selected_canonical_key() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L07, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            Assert.True(control.ViewModel.BeginRolePermissions());
            var grants = Assert.Single(Descendants(control.RoleDetail).OfType<BootstrapDataGridView>());
            var index = GrantIndex(grants, "Sales.Receipts.Post");
            grants.CurrentCell = grants.Rows[index].Cells[1];
            Assert.True(grants.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Collection(control.ViewModel.RoleDraft!.Changes, change =>
            {
                Assert.Equal("Sales.Receipts.Post", change.PermissionKey);
                Assert.True(change.Granted);
            });
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Empty(control.ViewModel.RoleDraft.Changes);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_role_search_keeps_native_text_caret_focus_and_enter_does_not_save() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L07, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            Assert.True(control.ViewModel.BeginRolePermissions());
            control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
            var search = Assert.Single(Descendants(control.RoleDetail).OfType<BootstrapTextBox>(), item => item.AccessibleName == "Tìm quyền vai trò");
            search.Focus();
            Assert.True(search.ContainsFocus);
            var editor = GetFocus();
            Assert.NotEqual(IntPtr.Zero, editor);
            var text = "";
            foreach (var character in "Read")
            {
                SendMessage(editor, 0x0102, new IntPtr(character), IntPtr.Zero);
                System.Windows.Forms.Application.DoEvents();
                text += character;
                Assert.Equal(text, search.Text);
                Assert.Equal(editor, GetFocus());
                var selection = SendMessage(editor, 0x00B0, IntPtr.Zero, IntPtr.Zero).ToInt64();
                Assert.Equal(text.Length, (int)(selection & 0xFFFF));
                Assert.Equal(text.Length, (int)((selection >> 16) & 0xFFFF));
            }
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(control.ViewModel.HasDirtyEditor);
            Assert.Contains("Sales.Receipts.Post", control.ViewModel.RoleDraft!.DesiredKeys);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_tab_preserves_staged_role_grant_after_mouse_toggle() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L07, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 0;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var edit = Assert.Single(Descendants(control.RoleDetail).OfType<Button>(), item => item.Text == "Sửa quyền");
            edit.PerformClick();
            var grants = Assert.Single(Descendants(control.RoleDetail).OfType<BootstrapDataGridView>());
            var index = GrantIndex(grants, "Sales.Receipts.Post");
            Assert.True(index >= 0);
            var item = grants.GetCellDisplayRectangle(0, index, true);
            NativeLookupTypingDriver.Click(grants, new Point(item.Left + 20, item.Top + item.Height / 2));
            Assert.True(Granted(grants, index));
            Assert.Contains("Sales.Receipts.Post", control.ViewModel.RoleDraft!.DesiredKeys);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.False(grants.ContainsFocus);
            Assert.Contains("Sales.Receipts.Post", control.ViewModel.RoleDraft.DesiredKeys);
            Assert.True(Granted(grants, index));
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_enter_starts_and_discards_role_grant_draft_without_dispatch() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 0;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var buttons = Descendants(control.RoleDetail).OfType<Button>().ToArray();
            var edit = Assert.Single(buttons, item => item.Text == "Sửa quyền");
            var cancel = Assert.Single(buttons, item => item.Text == "Hủy thay đổi");
            var grants = Assert.Single(Descendants(control.RoleDetail).OfType<BootstrapDataGridView>());
            grants.CurrentCell = grants.Rows[0].Cells[0];
            var before = Granted(grants, 0);
            Assert.True(grants.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Equal(before, Granted(grants, 0));
            Assert.Null(control.ViewModel.RoleDraft);
            Assert.True(edit.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.NotNull(control.ViewModel.RoleDraft);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            grants.CurrentCell = grants.Rows[0].Cells[0];
            Assert.True(grants.Focus());
            var item = grants.GetCellDisplayRectangle(0, 0, true);
            NativeLookupTypingDriver.Click(grants, new Point(item.Left + 20, item.Top + item.Height / 2));
            Assert.Equal(!before, Granted(grants, 0));
            Assert.Equal(0, grants.CurrentCell.RowIndex);
            Assert.True(grants.ContainsFocus);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Equal(before, Granted(grants, 0));
            Assert.False(control.ViewModel.HasDirtyEditor);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            Assert.True(cancel.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.Null(control.ViewModel.RoleDraft);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_enter_saves_one_role_grant_and_refreshes_confirmed_state() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L07, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 0;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var edit = Assert.Single(Descendants(control.RoleDetail).OfType<Button>(), item => item.Text == "Sửa quyền");
            Assert.True(edit.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            var grants = Assert.Single(Descendants(control.RoleDetail).OfType<BootstrapDataGridView>());
            var index = GrantIndex(grants, "Sales.Receipts.Post");
            Assert.True(index >= 0);
            Assert.False(Granted(grants, index));
            grants.CurrentCell = grants.Rows[index].Cells[0];
            Assert.True(grants.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.True(Granted(grants, index));
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            var save = Assert.Single(Descendants(control.RoleDetail).OfType<Button>(), item => item.Text == "Lưu quyền vai trò");
            Assert.True(save.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Until(() => scene.Journal.Entries.Count(entry => entry.Operation == "Security.Catalog.Result") == 2);
            var request = Assert.IsType<SecurityMutationJournalEntry>(Assert.Single(scene.Journal.Entries,
                entry => entry.Operation == "Security.Mutate").Request);
            Assert.Equal(SecurityAdministrationTargetDto.Role, request.Target);
            Assert.Equal(SecurityAdministrationOperationDto.SetPermission, request.Operation);
            Assert.Equal(201, request.TargetId);
            Assert.Equal("Sales.Receipts.Post", request.PermissionKey);
            Assert.True(request.Decision);
            Assert.Equal(21, request.ExpectedRevision);
            var result = Assert.IsType<SecurityResponseJournalEntry>(Assert.Single(scene.Journal.Entries,
                entry => entry.Operation == "Security.Mutate.Result").Request);
            Assert.Equal("Success", result.Outcome);
            Assert.Equal(22, result.AssignmentRevision);
            Assert.True(Granted(grants, index));
            Assert.False(control.ViewModel.HasDirtyEditor);
        });

    [Fact]
    public void Native_enter_during_held_role_save_does_not_dispatch_a_second_mutation() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L07, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 0;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var edit = Assert.Single(Descendants(control.RoleDetail).OfType<Button>(), item => item.Text == "Sửa quyền");
            Assert.True(edit.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            var grants = Assert.Single(Descendants(control.RoleDetail).OfType<BootstrapDataGridView>());
            var index = GrantIndex(grants, "Sales.Receipts.Post");
            Assert.True(index >= 0);
            grants.CurrentCell = grants.Rows[index].Cells[0];
            Assert.True(grants.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.True(Granted(grants, index));
            var save = Assert.Single(Descendants(control.RoleDetail).OfType<Button>(), item => item.Text == "Lưu quyền vai trò");
            var gate = scene.Administration.HoldNextMutation(true);
            Assert.True(save.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(control.ViewModel.IsBusy);
            Assert.False(save.Enabled);
            Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");

            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            gate.Complete(MyDmsVn.Contracts.ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(22, true)));
            Until(() => scene.Journal.Entries.Count(entry => entry.Operation == "Security.Catalog.Result") == 2);
            Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate.Result");
            Assert.True(Granted(grants, index));
            Assert.False(control.ViewModel.HasDirtyEditor);
        });

    [Fact]
    public void Native_tab_leaves_role_grant_list_and_save_has_real_focus() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L07, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 0;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var edit = Assert.Single(Descendants(control.RoleDetail).OfType<Button>(), item => item.Text == "Sửa quyền");
            Assert.True(edit.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            var grants = Assert.Single(Descendants(control.RoleDetail).OfType<BootstrapDataGridView>());
            var index = GrantIndex(grants, "Sales.Receipts.Post");
            Assert.True(index >= 0);
            grants.CurrentCell = grants.Rows[index].Cells[0];
            Assert.True(grants.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.True(Granted(grants, index));
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.False(grants.ContainsFocus, "Native Tab must leave the grant list while retaining its selected key.");
            Assert.Equal(index, grants.CurrentCell.RowIndex);
            var search = Assert.Single(Descendants(control.RoleDetail).OfType<BootstrapTextBox>(), item => item.AccessibleName == "Tìm quyền vai trò");
            search.Focus();
            Assert.True(search.ContainsFocus);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Shift | Keys.Tab);
            var save = Assert.Single(Descendants(control.RoleDetail).OfType<Button>(), item => item.Text == "Lưu quyền vai trò");
            Assert.Equal(save.Handle, GetFocus());
            Assert.True(save.ContainsFocus);
            Assert.True((save.AccessibilityObject.State & AccessibleStates.Focused) != 0,
                "The focused Save button must also expose focus through accessibility.");
            Assert.True((grants.AccessibilityObject.State & AccessibleStates.Focused) == 0,
                "The selected grant row must not claim focus after the keyboard moves to Save.");
            Assert.True(control.ViewModel.HasDirtyEditor);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_keyboard_selects_direct_key_and_tri_state_without_implicit_write() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 0;
            control.UserDetail.Tabs.SelectedIndex = 3;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var controls = Descendants(control.UserDetail).ToArray();
            var key = Assert.Single(controls.OfType<ComboBox>(), item => item.AccessibleName == "Quyền riêng");
            Assert.True(key.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Down);
            Assert.Equal("Sales.Receipts.Read", control.ViewModel.DirectPermissionKey);
            var deny = Assert.Single(controls.OfType<RadioButton>(), item => item.Text == "Từ chối");
            Assert.True(deny.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Equal(false, control.ViewModel.DirectDecision);
            Assert.True(control.ViewModel.HasDirtyEditor);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            var allow = Assert.Single(controls.OfType<RadioButton>(), item => item.Text == "Cho phép");
            Assert.True(allow.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Equal(true, control.ViewModel.DirectDecision);
            var inherit = Assert.Single(controls.OfType<RadioButton>(), item => item.Text == "Theo vai trò");
            Assert.True(inherit.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Null(control.ViewModel.DirectDecision);
            Assert.False(control.ViewModel.HasDirtyEditor);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_enter_saves_one_direct_decision_and_refreshes_confirmed_state() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L06, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 3;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            control.UserDetail.DirectDecisions.SelectedIndex = 0;
            Until(() => control.ViewModel.DirectPermissionKey == "Sales.Receipts.Post");
            var buttons = Descendants(control.UserDetail).OfType<Button>().ToArray();
            var save = Assert.Single(buttons, item => item.Text == "Lưu quyền này");
            var allow = Assert.Single(Descendants(control.UserDetail).OfType<RadioButton>(), item => item.Text == "Cho phép");
            Assert.True(allow.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Equal(true, control.ViewModel.DirectDecision);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            Assert.True(save.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Until(() => scene.Journal.Entries.Count(entry => entry.Operation == "Security.Catalog.Result") == 2);
            var request = Assert.IsType<SecurityMutationJournalEntry>(Assert.Single(scene.Journal.Entries,
                entry => entry.Operation == "Security.Mutate").Request);
            Assert.Equal(SecurityAdministrationTargetDto.User, request.Target);
            Assert.Equal(SecurityAdministrationOperationDto.SetPermission, request.Operation);
            Assert.Equal(101, request.TargetId);
            Assert.Equal("Sales.Receipts.Post", request.PermissionKey);
            Assert.True(request.Decision);
            Assert.Equal(11, request.ExpectedRevision);
            var result = Assert.IsType<SecurityResponseJournalEntry>(Assert.Single(scene.Journal.Entries,
                entry => entry.Operation == "Security.Mutate.Result").Request);
            Assert.Equal("Success", result.Outcome);
            Assert.Equal(12, result.AssignmentRevision);
            Assert.Contains(control.UserDetail.DirectDecisions.Items.Cast<object>(), item =>
                item.ToString()!.Contains("Sales.Receipts.Post") && item.ToString()!.Contains("Cho phép"));
            Assert.False(control.ViewModel.HasDirtyEditor);
        });

    [Fact]
    public void Native_catalog_navigation_never_dispatches_an_assignment() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 2;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var grid = control.PermissionCatalog.Keys;
            grid.CurrentCell = grid.Rows[0].Cells[0];
            Assert.True(grid.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Down);
            Assert.Contains("Sales.Receipts.Write", control.PermissionCatalog.Details.Text);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    [Theory]
    [InlineData(900, 600, false)]
    [InlineData(900, 600, true)]
    [InlineData(1200, 760, false)]
    [InlineData(1200, 760, true)]
    public void Native_search_preserves_caret_focus_and_committed_owner(int width, int height, bool dark) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            var priorTheme = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
                var form = control.FindForm()!;
                form.ClientSize = new Size(width, height);
                NativeLookupTypingDriver.Activate(form);
                ObserveNativeScale(form, control);
                var owner = control.ViewModel.SelectedUserId;
                var search = control.UserSearch;
                Assert.IsType<BootstrapTextBox>((object)search);
                search.Focus();
                Assert.True(search.ContainsFocus);
                var handle = GetFocus();
                Assert.NotEqual(search.Handle, handle);
                var text = "";
                foreach (var character in "inactive")
                {
                    SendMessage(handle, 0x0102, new IntPtr(character), IntPtr.Zero);
                    text += character;
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal(text, search.Text);
                    AssertNativeSelection(handle, text.Length, 0);
                    Assert.Equal(handle, GetFocus());
                    Assert.Equal(owner, control.ViewModel.SelectedUserId);
                }
                Assert.Single(control.Users.Items);
                Assert.Equal(102, ((SecurityUserDto)control.Users.Items[0].Tag).UserId);
                SendMessage(handle, 0x00B1, new IntPtr(2), new IntPtr(4));
                AssertNativeSelection(handle, 2, 2);
                SendMessage(handle, 0x0102, new IntPtr('X'), IntPtr.Zero);
                System.Windows.Forms.Application.DoEvents();
                Assert.Equal("inXtive", search.Text);
                AssertNativeSelection(handle, 3, 0);
                Assert.Empty(control.Users.Items);
                Assert.Equal(handle, GetFocus());
                Assert.Equal(owner, control.ViewModel.SelectedUserId);
                Assert.True(search.RectangleToScreen(search.ClientRectangle).Width > 0);
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                Assert.True(search.ContainsFocus);
                Assert.Equal(owner, control.ViewModel.SelectedUserId);
                Assert.DoesNotContain(scene.Journal.Entries, e => e.Operation.Contains("Mutate"));
            }
            finally { BootstrapThemeManager.CurrentTheme = priorTheme; }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Security_reset_drains_rearmed_held_reads_before_replacement(bool ignoresCancellation) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            using var form = new SecurityLabForm();
            form.Show();
            Until(() => form.CurrentSecurityControl != null && !form.IsResetting);
            for (var round = 0; round < 2; round++)
            {
                var old = form.CurrentSecurityControl!;
                var data = form.CurrentSecurityScenarioData!;
                var gate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>> { IgnoreCancellation = ignoresCancellation };
                data.Administration.UsersGate = gate;
                var reload = old.ActivateAsync(default);
                Until(() => data.Administration.OutstandingOperations.Any(t => !t.IsCompleted));
                var reset = form.ResetForTestAsync();
                Until(() => reset.IsCompleted);
                reset.GetAwaiter().GetResult();
                Assert.True(reload.IsCompleted);
                Assert.True(old.IsDisposed);
                Assert.True(gate.IsCompleted);
                Assert.False(form.IsPoisoned);
                Assert.NotSame(old, form.CurrentSecurityControl);
                Assert.NotSame(data, form.CurrentSecurityScenarioData);
                Assert.All(data.Administration.OutstandingOperations, t => Assert.True(t.IsCompleted));
                Assert.False(gate.Complete(ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>.Success(data.Data.Users)));
                Assert.Equal(3, form.CurrentSecurityControl!.Users.Items.Count);
            }
            form.CloseWorkspaceButton.PerformClick();
            Until(() => form.CurrentSecurityControl == null);
        });

    [Fact]
    public void Security_reset_timeout_poisons_host_until_old_response_is_released() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            using var form = new SecurityLabForm();
            form.Show();
            Until(() => form.CurrentSecurityControl != null && !form.IsResetting);
            var old = form.CurrentSecurityControl!;
            var data = form.CurrentSecurityScenarioData!;
            var gate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>> { IgnoreCancellation = true, IgnoreOwnerRelease = true };
            data.Administration.UsersGate = gate;
            var reload = old.ActivateAsync(default);
            Until(() => data.Administration.OutstandingOperations.Any(t => !t.IsCompleted));
            var reset = form.ResetForTestAsync();
            Until(() => reset.IsCompleted);
            reset.GetAwaiter().GetResult();
            Assert.True(form.IsPoisoned);
            Assert.Contains("did not drain", form.StatusText);
            Assert.Same(old, form.CurrentSecurityControl);
            Assert.Same(data, form.CurrentSecurityScenarioData);
            Assert.False(form.ScenarioSelector.Enabled);
            form.ResetForTestAsync().GetAwaiter().GetResult();
            Assert.Same(old, form.CurrentSecurityControl);
            Assert.True(gate.Complete(ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>.Success(data.Data.Users)));
            Until(() => reload.IsCompleted);
            form.Close();
            Until(() => form.IsDisposed);
        });

    static void AssertNativeSelection(IntPtr editor, int start, int length)
    {
        var range = unchecked((uint)SendMessage(editor, 0x00B0, IntPtr.Zero, IntPtr.Zero).ToInt64());
        Assert.Equal(start, (int)(range & 0xffff));
        Assert.Equal(length, (int)(range >> 16) - start);
    }

    static void Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(7))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }
        Assert.True(condition(), "Security native/Reset operation did not complete within its bounded pump.");
    }

    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr GetFocus();
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("shcore.dll")] static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scale);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

    void ObserveNativeScale(Form form, SecurityWorkspaceControl control)
    {
        var monitor = MonitorFromWindow(form.Handle, 2);
        Assert.NotEqual(IntPtr.Zero, monitor);
        Assert.Equal(0, GetScaleFactorForMonitor(monitor, out var scale));
        Assert.Equal(0, GetDpiForMonitor(monitor, 0, out var dpiX, out var dpiY));
        var expected = Environment.GetEnvironmentVariable("MYDMSVN_UIN_EXPECTED_SCALE");
        if (!string.IsNullOrWhiteSpace(expected))
            Assert.Equal(int.Parse(expected), scale);
        output.WriteLine("PHYSICAL_SCALE={0};MONITOR_EFFECTIVE_DPI={1}x{2};WINDOW_DPI={3};CLIENT={4}x{5};WORKSPACE={6};LIST={7};DETAIL={8};TABS={9}",
            scale, dpiX, dpiY, GetDpiForWindow(form.Handle), form.ClientSize.Width, form.ClientSize.Height,
            control.Bounds, control.Users.Bounds, control.UserDetail.Bounds, control.Tabs.Bounds);
    }
}
