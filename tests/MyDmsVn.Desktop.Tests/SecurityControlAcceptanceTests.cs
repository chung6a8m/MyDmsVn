using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.WinForms.Features.Security;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

/// <summary>Control binding evidence; fake dispatches do not establish server authorization.</summary>
public sealed class SecurityControlAcceptanceTests
{
    [Fact]
    public void Protected_bound_user_disables_edit_membership_and_import_without_dispatch()
    {
        Run(context =>
        {
            context.SelectUser(1);
            Assert.Equal(1, context.ViewModel.SelectedUserId);
            foreach (var field in new[] { "_username", "_displayName", "_email", "_password" })
                Assert.False(Field<TextBox>(context.Control, field).Enabled);
            Assert.False(Field<CheckedListBox>(context.Control, "_memberships").Enabled);
            foreach (var action in new[] { "Save changes", "Activate", "Deactivate", "Reset password" })
            {
                var button = context.Button(0, action);
                Assert.False(button.Enabled);
                button.PerformClick();
            }
            context.Tabs.SelectedIndex = 2;
            Assert.False(context.Control.ImportPermissionButton.Enabled);
            Assert.False(Field<ComboBox>(context.Control, "_decision").Enabled);
            context.SelectPermission("Inventory.Read");
            context.Button(2, "Apply assignment").PerformClick();
            Drain(context.Control.LastOperation);
            Assert.Empty(context.Api.Mutations);
            Assert.Contains("Protected superuser", context.Explanation.Text);
        });
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, null)]
    public void User_selection_and_direct_decision_buttons_dispatch_selected_identity_and_revision(int decisionIndex, bool? decision)
    {
        Run(context =>
        {
            context.SelectUser(17);
            Assert.Equal("operator", Field<TextBox>(context.Control, "_username").Text);
            Assert.Empty(context.Api.Mutations);
            context.Tabs.SelectedIndex = 2;
            context.SelectPermission("Inventory.Read");
            Assert.Contains("Direct Deny (overrides role grant)", context.Explanation.Text);
            Field<ComboBox>(context.Control, "_decision").SelectedIndex = decisionIndex;
            context.Button(2, "Apply assignment").PerformClick();
            Drain(context.Control.LastOperation);
            var request = Assert.Single(context.Api.Mutations);
            Assert.Equal(SecurityAdministrationTargetDto.User, request.Target);
            Assert.Equal(SecurityAdministrationOperationDto.SetPermission, request.Operation);
            Assert.Equal(17, request.TargetId);
            Assert.Equal(6, request.ExpectedRevision);
            Assert.Equal("Inventory.Read", request.PermissionKey);
            Assert.Equal(decision, request.Decision);
        });
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Role_selection_limits_catalog_to_active_permissions_and_dispatches_grant_or_remove(int decisionIndex, bool granted)
    {
        Run(context =>
        {
            context.Tabs.SelectedIndex = 1;
            SelectRow(context.Control.RolesGrid, "Name", "StockClerk");
            Assert.Equal(8, context.ViewModel.SelectedRoleId);
            Assert.Null(context.ViewModel.SelectedUserId);
            context.Tabs.SelectedIndex = 2;
            var decision = Field<ComboBox>(context.Control, "_decision");
            Assert.Equal(new[] { "Grant", "Remove" }, decision.Items.Cast<string>().ToArray());
            Assert.All(context.Control.PermissionsGrid.Rows.Cast<DataGridViewRow>(), row => Assert.Equal("Active", row.Cells["Status"].Value));
            context.SelectPermission("Inventory.Read");
            decision.SelectedIndex = decisionIndex;
            context.Button(2, "Apply assignment").PerformClick();
            Drain(context.Control.LastOperation);
            var request = Assert.Single(context.Api.Mutations);
            Assert.Equal(SecurityAdministrationTargetDto.Role, request.Target);
            Assert.Equal(8, request.TargetId);
            Assert.Equal(9, request.ExpectedRevision);
            Assert.Equal(granted, request.Decision);
            Assert.Equal("Inventory.Read", request.PermissionKey);
        });
    }

    [Fact]
    public void User_membership_checkbox_uses_both_assignment_revisions_once()
    {
        Run(context =>
        {
            context.SelectUser(17);
            var memberships = Field<CheckedListBox>(context.Control, "_memberships");
            Assert.True(memberships.GetItemChecked(0));
            Assert.Empty(context.Api.Mutations);
            memberships.SetItemChecked(0, false);
            Drain(context.Control.LastOperation);
            var request = Assert.Single(context.Api.Mutations);
            Assert.Equal(SecurityAdministrationOperationDto.SetMembership, request.Operation);
            Assert.Equal(17, request.TargetId);
            Assert.Equal(8, request.RelatedId);
            Assert.Equal(6, request.ExpectedRevision);
            Assert.Equal(9, request.ExpectedRelatedRevision);
            Assert.Equal(false, request.IsActive);
        });
    }

    [Fact]
    public void Revision_conflict_renders_safe_error_refreshes_rows_and_does_not_retry()
    {
        Run(context =>
        {
            context.SelectUser(17);
            context.Api.MutationResponse = ApiResponse<SecurityAdministrationResultDto>.Failure(
                new ApiError(ApiStatusCode.Conflict, "Security.Conflict", "Assignments changed. Refresh before retrying."));
            context.Api.Users = new[] { User(17, revision: 12) };
            var before = context.Api.UserReads;
            context.Button(0, "Save changes").PerformClick();
            Drain(context.Control.LastOperation);
            Assert.Single(context.Api.Mutations);
            Assert.Equal(6, context.Api.Mutations[0].ExpectedRevision);
            Assert.Equal(before + 1, context.Api.UserReads);
            Assert.Equal(12, context.ViewModel.SelectedUser!.AssignmentRevision);
            Assert.Contains("Assignments changed", context.Control.StatusText);
        });
    }

    [Fact]
    public void Forbidden_reads_disable_only_the_affected_management_tabs_and_show_server_error()
    {
        Run(context =>
        {
            context.SelectUser(17);
            context.Api.DenyManagementReads = true;
            Drain(context.Control.ActivateAsync(CancellationToken.None));
            Assert.False(context.Tabs.TabPages[0].Enabled);
            Assert.False(context.Tabs.TabPages[1].Enabled);
            Assert.True(context.Tabs.TabPages[2].Enabled);
            Assert.False(context.Control.ImportPermissionButton.Enabled);
            Assert.Contains("Management permission revoked", context.Control.StatusText);
            Assert.Empty(context.Api.Mutations);
        });
    }

    [Fact]
    public void New_user_required_field_validation_renders_without_a_request_or_modal()
    {
        Run(context =>
        {
            context.Button(0, "New user").PerformClick();
            context.Button(0, "Create").PerformClick();
            Drain(context.Control.LastOperation);
            Assert.Empty(context.Api.Mutations);
            Assert.True(context.ViewModel.HasErrors);
            Assert.Contains("temporary password", context.Control.StatusText);
            Assert.True(context.Control.IsPasswordMasked);
        });
    }

    [Fact]
    public void Session_revocation_discards_delayed_load_and_clears_control_rows()
    {
        Run(context =>
        {
            context.Api.PendingUsers = new TaskCompletionSource<ApiResponse<IReadOnlyList<SecurityUserDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = context.Control.ActivateAsync(CancellationToken.None);
            context.Session.SignOut();
            context.Api.PendingUsers.SetResult(ApiResponse<IReadOnlyList<SecurityUserDto>>.Success(new[] { User(25) }));
            Drain(operation);
            Pump(() => context.Control.UsersGrid.Rows.Count == 0);
            Assert.Empty(context.ViewModel.Users);
            Assert.Empty(context.ViewModel.CatalogItems);
            Assert.False(context.Control.ImportPermissionButton.Enabled);
            Assert.All(context.Tabs.TabPages.Cast<TabPage>(), tab => Assert.False(tab.Enabled));
            Assert.Empty(context.Api.Mutations);
        });
    }

    [Fact]
    public void Disposed_control_discards_delayed_background_load_and_unsubscribes_session()
    {
        Run(context =>
        {
            context.Api.PendingUsers = new TaskCompletionSource<ApiResponse<IReadOnlyList<SecurityUserDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = context.Control.ActivateAsync(CancellationToken.None);
            context.Control.Dispose();
            context.Api.PendingUsers.SetResult(ApiResponse<IReadOnlyList<SecurityUserDto>>.Success(new[] { User(25) }));
            Drain(operation);
            context.Session.SignOut();
            Assert.True(context.Control.IsDisposed);
            Assert.Empty(context.ViewModel.Users);
            Assert.Equal(0, context.Session.SubscriberCount);
            Assert.Empty(context.Api.Mutations);
        });
    }

    [Theory]
    [InlineData(BootstrapThemeMode.Light)]
    [InlineData(BootstrapThemeMode.Dark)]
    public void Security_tabs_keep_read_only_grids_accessible_inputs_and_keyboard_focus_at_supported_width(BootstrapThemeMode mode)
    {
        var original = BootstrapThemeManager.CurrentTheme;
        try
        {
            Run(context =>
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                context.Host.ClientSize = new Size(1280, 800);
                context.SelectUser(17);
                var username = Field<TextBox>(context.Control, "_username");
                Assert.True(username.Focus());
                Assert.True(context.Host.SelectNextControl(username, true, true, true, true));
                Assert.False(username.Focused);
                Assert.All(new[] { context.Control.UsersGrid, context.Control.RolesGrid, context.Control.PermissionsGrid }, grid =>
                {
                    Assert.True(grid.ReadOnly);
                    Assert.False(grid.AllowUserToAddRows);
                    Assert.All(grid.Columns.Cast<DataGridViewColumn>(), column => Assert.True(column.ReadOnly));
                });
                context.Tabs.SelectedIndex = 2;
                Assert.Equal("Filter permission catalog", Field<TextBox>(context.Control, "_search").AccessibleName);
                Field<TextBox>(context.Control, "_search").Text = "inventory.read";
                Assert.Single(context.Control.PermissionsGrid.Rows.Cast<DataGridViewRow>());
                foreach (var tab in context.Tabs.TabPages.Cast<TabPage>())
                {
                    context.Tabs.SelectedTab = tab;
                    context.Host.PerformLayout();
                    Assert.True(tab.ClientSize.Width > 0);
                    Assert.All(Descendants(tab).OfType<SplitContainer>(), split =>
                        Assert.InRange(split.SplitterDistance, split.Panel1MinSize, split.Width - split.SplitterWidth - split.Panel2MinSize));
                }
            });
        }
        finally { BootstrapThemeManager.CurrentTheme = original; }
    }

    [Fact]
    public void Import_button_previews_exact_json_confirms_additions_then_applies_preview_revision()
    {
        Run(context =>
        {
            context.SelectUser(17);
            context.Tabs.SelectedIndex = 2;
            context.Control.ImportPermissionButton.PerformClick();
            Drain(context.Control.LastOperation);
            Assert.Equal(1, context.JsonSelections);
            Assert.Equal(Context.ImportJson, context.Api.PreviewJson);
            var confirmation = Assert.Single(context.Confirmations);
            Assert.Contains("Operator", confirmation);
            Assert.Contains("Current revision: 6", confirmation);
            Assert.Contains("Inventory.Read", confirmation);
            var request = Assert.Single(context.Api.Imports);
            Assert.Equal(Context.ImportJson, request.Json);
            Assert.Equal(6, request.ExpectedTargetRevision);
            Assert.Equal("catalog-digest", request.ExpectedCatalogDigest);
            Assert.Equal(PermissionImportTargetTypeDto.User, context.Api.ImportTarget);
            Assert.Equal(17, context.Api.ImportTargetId);
            Assert.Null(context.ViewModel.ImportPreview);
        }, import: true);
    }

    [Fact]
    public void Declined_import_confirmation_does_not_apply_any_assignment()
    {
        Run(context =>
        {
            context.SelectUser(17);
            context.Tabs.SelectedIndex = 2;
            context.ConfirmImport = false;
            context.Control.ImportPermissionButton.PerformClick();
            Drain(context.Control.LastOperation);
            Assert.Single(context.Confirmations);
            Assert.Empty(context.Api.Imports);
            Assert.Empty(context.Api.Mutations);
        }, import: true);
    }

    [Fact]
    public void Import_revision_conflict_clears_preview_refreshes_and_renders_error_without_retry()
    {
        Run(context =>
        {
            context.SelectUser(17);
            context.Tabs.SelectedIndex = 2;
            context.Api.ImportResponse = ApiResponse<PermissionAssignmentImportResultDto>.Failure(
                new ApiError(ApiStatusCode.Conflict, "Import.StaleRevision", "Import preview is stale. Preview again."));
            context.Api.Users = new[] { User(17, revision: 12) };
            var reads = context.Api.UserReads;
            context.Control.ImportPermissionButton.PerformClick();
            Drain(context.Control.LastOperation);
            Assert.Single(context.Api.Imports);
            Assert.Equal(6, context.Api.Imports[0].ExpectedTargetRevision);
            Assert.Equal(reads + 1, context.Api.UserReads);
            Assert.Equal(12, context.ViewModel.SelectedUser!.AssignmentRevision);
            Assert.Null(context.ViewModel.ImportPreview);
            Assert.Contains("Import preview is stale", context.Control.StatusText);
        }, import: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Late_import_preview_after_revocation_or_disposal_cannot_confirm_or_apply(bool dispose)
    {
        Run(context =>
        {
            context.SelectUser(17);
            context.Tabs.SelectedIndex = 2;
            context.Api.PendingPreview = new TaskCompletionSource<ApiResponse<PermissionAssignmentImportPreviewDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Control.ImportPermissionButton.PerformClick();
            Assert.Equal(Context.ImportJson, context.Api.PreviewJson);
            var operation = context.Control.LastOperation;
            Assert.False(operation.IsCompleted);
            if (dispose) context.Control.Dispose();
            else context.Session.SignOut();
            context.Api.PendingPreview.SetResult(ApiResponse<PermissionAssignmentImportPreviewDto>.Success(Api.Preview()));
            Drain(operation);
            Assert.Empty(context.Confirmations);
            Assert.Empty(context.Api.Imports);
            Assert.Null(context.ViewModel.ImportPreview);
        }, import: true);
    }

    [Fact]
    public void Changing_selected_target_while_import_preview_is_pending_cannot_confirm_the_old_target()
    {
        Run(context =>
        {
            context.SelectUser(17);
            context.Tabs.SelectedIndex = 2;
            context.Api.PendingPreview = new TaskCompletionSource<ApiResponse<PermissionAssignmentImportPreviewDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Control.ImportPermissionButton.PerformClick();
            var operation = context.Control.LastOperation;
            Assert.False(operation.IsCompleted);
            context.SelectUser(1);
            context.Api.PendingPreview.SetResult(ApiResponse<PermissionAssignmentImportPreviewDto>.Success(Api.Preview()));
            Drain(operation);
            Assert.Empty(context.Confirmations);
            Assert.Empty(context.Api.Imports);
            Assert.Null(context.ViewModel.ImportPreview);
        }, import: true);
    }
    [Theory]
    [InlineData("Save changes", SecurityAdministrationOperationDto.Update)]
    [InlineData("Activate", SecurityAdministrationOperationDto.SetActive)]
    [InlineData("Deactivate", SecurityAdministrationOperationDto.SetActive)]
    [InlineData("Reset password", SecurityAdministrationOperationDto.SetPassword)]
    public void User_management_buttons_forward_editor_values_and_selected_revision(string action, SecurityAdministrationOperationDto operation)
    {
        Run(context =>
        {
            context.SelectUser(17);
            Field<TextBox>(context.Control, "_displayName").Text = "Edited operator";
            Field<TextBox>(context.Control, "_email").Text = "operator@example.invalid";
            Field<TextBox>(context.Control, "_password").Text = "test-only-temporary-value";
            context.Button(0, action).PerformClick();
            Drain(context.Control.LastOperation);
            var request = Assert.Single(context.Api.Mutations);
            Assert.Equal(operation, request.Operation);
            Assert.Equal(SecurityAdministrationTargetDto.User, request.Target);
            Assert.Equal(17, request.TargetId);
            Assert.Equal(6, request.ExpectedRevision);
            if (action == "Save changes")
            {
                Assert.Equal("Edited operator", request.DisplayName);
                Assert.Equal("operator@example.invalid", request.Email);
            }
            if (operation == SecurityAdministrationOperationDto.SetActive) Assert.Equal(action == "Activate", request.IsActive);
            if (operation == SecurityAdministrationOperationDto.SetPassword)
            {
                Assert.Equal("test-only-temporary-value", request.Password);
                Assert.Empty(Field<TextBox>(context.Control, "_password").Text);
            }
        });
    }

    [Fact]
    public void Create_user_button_forwards_new_editor_and_clears_temporary_password()
    {
        Run(context =>
        {
            context.Button(0, "New user").PerformClick();
            Field<TextBox>(context.Control, "_username").Text = "new.operator";
            Field<TextBox>(context.Control, "_displayName").Text = "New operator";
            Field<TextBox>(context.Control, "_password").Text = "test-only-temporary-value";
            context.Button(0, "Create").PerformClick();
            Drain(context.Control.LastOperation);
            var request = Assert.Single(context.Api.Mutations);
            Assert.Equal(SecurityAdministrationOperationDto.Create, request.Operation);
            Assert.Equal("new.operator", request.Name);
            Assert.Equal("New operator", request.DisplayName);
            Assert.Equal("test-only-temporary-value", request.Password);
            Assert.Empty(Field<TextBox>(context.Control, "_password").Text);
        });
    }

    [Theory]
    [InlineData("Save changes", SecurityAdministrationOperationDto.Update)]
    [InlineData("Activate", SecurityAdministrationOperationDto.SetActive)]
    [InlineData("Deactivate", SecurityAdministrationOperationDto.SetActive)]
    public void Role_management_buttons_forward_selected_role_revision(string action, SecurityAdministrationOperationDto operation)
    {
        Run(context =>
        {
            context.Tabs.SelectedIndex = 1;
            SelectRow(context.Control.RolesGrid, "Name", "StockClerk");
            Field<TextBox>(context.Control, "_roleName").Text = "EditedClerk";
            context.Button(1, action).PerformClick();
            Drain(context.Control.LastOperation);
            var request = Assert.Single(context.Api.Mutations);
            Assert.Equal(SecurityAdministrationTargetDto.Role, request.Target);
            Assert.Equal(operation, request.Operation);
            Assert.Equal(8, request.TargetId);
            Assert.Equal(9, request.ExpectedRevision);
            if (action == "Save changes") Assert.Equal("EditedClerk", request.Name);
            if (operation == SecurityAdministrationOperationDto.SetActive) Assert.Equal(action == "Activate", request.IsActive);
        });
    }

    [Fact]
    public void New_role_button_clears_selected_identity_before_create()
    {
        Run(context =>
        {
            context.Tabs.SelectedIndex = 1;
            SelectRow(context.Control.RolesGrid, "Name", "StockClerk");
            context.Button(1, "New role").PerformClick();
            Field<TextBox>(context.Control, "_roleName").Text = "NewClerk";
            context.Button(1, "Create").PerformClick();
            Drain(context.Control.LastOperation);
            var request = Assert.Single(context.Api.Mutations);
            Assert.Equal(SecurityAdministrationTargetDto.Role, request.Target);
            Assert.Equal(SecurityAdministrationOperationDto.Create, request.Operation);
            Assert.Equal("NewClerk", request.Name);
            Assert.Null(request.ExpectedRevision);
        });
    }

    [Fact]
    public void Deprecated_permission_is_visible_for_user_but_cannot_be_assigned()
    {
        Run(context =>
        {
            context.SelectUser(17);
            context.Tabs.SelectedIndex = 2;
            context.SelectPermission("Inventory.Legacy");
            Assert.Contains("not Active", context.Explanation.Text);
            Field<ComboBox>(context.Control, "_decision").SelectedIndex = 0;
            context.Button(2, "Apply assignment").PerformClick();
            Drain(context.Control.LastOperation);
            Assert.Empty(context.Api.Mutations);
        });
    }
    [Fact]
    public void Moving_between_columns_of_the_same_user_keeps_editor_and_preview()
    {
        Run(context =>
        {
            context.SelectUser(17);
            context.Tabs.SelectedIndex = 2;
            context.ConfirmImport = false;
            context.Control.ImportPermissionButton.PerformClick();
            Drain(context.Control.LastOperation);
            var preview = context.ViewModel.ImportPreview;
            Assert.NotNull(preview);
            context.Tabs.SelectedIndex = 0;
            Field<TextBox>(context.Control, "_displayName").Text = "Unsaved display name";
            Field<TextBox>(context.Control, "_password").Text = "test-only-unsaved-value";
            var row = context.Control.UsersGrid.CurrentRow!;
            Assert.Equal("operator", row.Cells["Username"].Value);
            context.Control.UsersGrid.CurrentCell = row.Cells["DisplayName"];
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(17, context.ViewModel.SelectedUserId);
            Assert.Equal("Unsaved display name", Field<TextBox>(context.Control, "_displayName").Text);
            Assert.Equal("test-only-unsaved-value", Field<TextBox>(context.Control, "_password").Text);
            Assert.Same(preview, context.ViewModel.ImportPreview);
            Assert.Empty(context.Api.Mutations);
            Assert.Empty(context.Api.Imports);
        }, import: true);
    }

    [Fact]
    public void Changing_target_mode_while_user_import_preview_is_pending_cannot_confirm_old_user()
    {
        Run(context =>
        {
            // Both grids have a current row; changing target mode must invalidate the user preview.
            context.Tabs.SelectedIndex = 1;
            SelectRow(context.Control.RolesGrid, "Name", "StockClerk");
            Assert.Equal(8, context.ViewModel.SelectedRoleId);
            context.SelectUser(17);
            Assert.Equal(17, context.ViewModel.SelectedUserId);
            context.Tabs.SelectedIndex = 2;
            context.Api.PendingPreview = new TaskCompletionSource<ApiResponse<PermissionAssignmentImportPreviewDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Control.ImportPermissionButton.PerformClick();
            var operation = context.Control.LastOperation;
            Assert.False(operation.IsCompleted);
            var target = Field<ComboBox>(context.Control, "_target");
            target.SelectedItem = "Role";
            Assert.Equal("Role", target.SelectedItem);
            context.Api.PendingPreview.SetResult(ApiResponse<PermissionAssignmentImportPreviewDto>.Success(Api.Preview()));
            Drain(operation);
            Assert.Empty(context.Confirmations);
            Assert.Empty(context.Api.Imports);
            Assert.Null(context.ViewModel.ImportPreview);
            Assert.Empty(context.Api.Mutations);
        }, import: true);
    }
    private static void Run(Action<Context> action, bool import = false) => StaTest.Run(_ =>
    {
        using var guard = new WinFormsTestGuard();
        using var context = new Context(import);
        guard.Attach(context.Control.UsersGrid);
        guard.Attach(context.Control.RolesGrid);
        guard.Attach(context.Control.PermissionsGrid);
        try { action(context); }
        finally
        {
            context.Api.PendingUsers?.TrySetResult(ApiResponse<IReadOnlyList<SecurityUserDto>>.Success(Array.Empty<SecurityUserDto>()));
            context.Api.PendingPreview?.TrySetResult(ApiResponse<PermissionAssignmentImportPreviewDto>.Success(Api.Preview()));
        }
        Assert.Empty(guard.DrainExceptions());
    }, TimeSpan.FromSeconds(20));

    private static T Field<T>(object owner, string name) where T : class =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    private static IEnumerable<Control> Descendants(Control owner)
    {
        foreach (Control control in owner.Controls)
        {
            yield return control;
            foreach (var nested in Descendants(control)) yield return nested;
        }
    }

    private static void SelectRow(DataGridView grid, string column, object value)
    {
        var row = Assert.Single(grid.Rows.Cast<DataGridViewRow>(), item => Equals(item.Cells[column].Value, value));
        grid.ClearSelection();
        grid.CurrentCell = null;
        grid.CurrentCell = row.Cells[0];
        row.Selected = true;
        System.Windows.Forms.Application.DoEvents();
    }

    private static void Drain(Task task)
    {
        Pump(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
        System.Windows.Forms.Application.DoEvents();
    }

    private static void Pump(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(5))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(5);
        }
        Assert.True(condition(), "Security UI operation did not complete within five seconds.");
    }

    private static SecurityUserDto User(int id, long revision = 6, bool isProtected = false) =>
        new(id, id == 1 ? "bound.owner" : "operator", "Operator", null, true, revision,
            new[] { 8 }, new[] { new SecurityDecisionDto("Inventory.Read", false) }, isProtected);

    private sealed class Context : IDisposable
    {
        public Context(bool import)
        {
            ViewModel = new SecurityAdministrationViewModel(Api, new Catalog(), Session, new DesktopNotificationCenter());
            if (import)
            {
                var constructor = typeof(SecurityAdministrationControl).GetConstructor(new[]
                {
                    typeof(SecurityAdministrationViewModel), typeof(Func<string>), typeof(Func<string, bool>),
                });
                Assert.NotNull(constructor);
                Func<string?> selectJson = () => { JsonSelections++; return ImportJson; };
                Func<string, bool> confirm = summary => { Confirmations.Add(summary); return ConfirmImport; };
                Control = (SecurityAdministrationControl)constructor!.Invoke(new object[] { ViewModel, selectJson, confirm });
            }
            else Control = new SecurityAdministrationControl(ViewModel);
            Host = new Form { ClientSize = new Size(1280, 800), ShowInTaskbar = false };
            Host.Controls.Add(Control);
            Host.Show();
            Drain(Control.ActivateAsync(CancellationToken.None));
        }
        public const string ImportJson = "{\"schemaVersion\":1,\"targetType\":\"User\",\"targetId\":17,\"entries\":[{\"permissionKey\":\"Inventory.Read\",\"decision\":\"Allow\"}]}";
        public int JsonSelections { get; private set; }
        public bool ConfirmImport { get; set; } = true;
        public List<string> Confirmations { get; } = new();
        public Api Api { get; } = new();
        public Session Session { get; } = new();
        public SecurityAdministrationViewModel ViewModel { get; }
        public SecurityAdministrationControl Control { get; }
        public Form Host { get; }
        public TabControl Tabs => Field<TabControl>(Control, "_tabs");
        public Label Explanation => Descendants(Tabs.TabPages[2]).OfType<Label>().Single(label => label.Name == "PermissionExplanation");
        public Button Button(int tab, string text) => Descendants(Tabs.TabPages[tab]).OfType<Button>().Single(button => button.Text == text);
        public void SelectUser(int id)
        {
            Tabs.SelectedIndex = 0;
            var username = Api.Users.Single(user => user.UserId == id).Username;
            SelectRow(Control.UsersGrid, "Username", username);
        }
        public void SelectPermission(string key) => SelectRow(Control.PermissionsGrid, "Key", key);
        public void Dispose() { Host.Close(); Host.Dispose(); Control.Dispose(); }
    }

    private sealed class Api : ISecurityAdministrationApiClient
    {
        public IReadOnlyList<SecurityUserDto> Users { get; set; } = new[] { User(1, isProtected: true), User(17) };
        public List<SecurityAdministrationMutationRequest> Mutations { get; } = new();
        public string? PreviewJson { get; private set; }
        public List<PermissionAssignmentImportRequest> Imports { get; } = new();
        public PermissionImportTargetTypeDto ImportTarget { get; private set; }
        public int ImportTargetId { get; private set; }
        public TaskCompletionSource<ApiResponse<PermissionAssignmentImportPreviewDto>>? PendingPreview { get; set; }
        public ApiResponse<PermissionAssignmentImportResultDto> ImportResponse { get; set; } =
            ApiResponse<PermissionAssignmentImportResultDto>.Success(new PermissionAssignmentImportResultDto(7, 1, true));
        public static PermissionAssignmentImportPreviewDto Preview() => new(
            new ParsedPermissionAssignmentImportDto(1, PermissionImportTargetTypeDto.User, 17,
                new[] { new PermissionAssignmentImportEntryDto("Inventory.Read", true) }),
            "Operator", 6, "catalog-digest", new[] { new PermissionAssignmentImportEntryDto("Inventory.Read", true) });
        public int UserReads { get; private set; }
        public bool DenyManagementReads { get; set; }
        public TaskCompletionSource<ApiResponse<IReadOnlyList<SecurityUserDto>>>? PendingUsers { get; set; }
        public ApiResponse<SecurityAdministrationResultDto> MutationResponse { get; set; } =
            ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(7, true));
        public Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ListUsersAsync(CancellationToken cancellationToken)
        {
            UserReads++;
            return PendingUsers?.Task ?? Task.FromResult(DenyManagementReads
                ? ApiResponse<IReadOnlyList<SecurityUserDto>>.Failure(Denied())
                : ApiResponse<IReadOnlyList<SecurityUserDto>>.Success(Users));
        }
        public Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(DenyManagementReads ? ApiResponse<IReadOnlyList<SecurityRoleDto>>.Failure(Denied()) :
                ApiResponse<IReadOnlyList<SecurityRoleDto>>.Success(new[] { new SecurityRoleDto(8, "StockClerk", true, 9, new[] { 17 }, new[] { "Inventory.Read" }) }));
        public Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(SecurityAdministrationMutationRequest request, CancellationToken cancellationToken)
        {
            Mutations.Add(request);
            return Task.FromResult(MutationResponse);
        }
        public Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewPermissionImportAsync(PermissionImportTargetTypeDto targetType, int targetId, string json, CancellationToken cancellationToken)
        {
            PreviewJson = json;
            return PendingPreview?.Task ?? Task.FromResult(ApiResponse<PermissionAssignmentImportPreviewDto>.Success(Preview()));
        }
        public Task<ApiResponse<PermissionAssignmentImportResultDto>> ApplyPermissionImportAsync(PermissionImportTargetTypeDto targetType, int targetId, PermissionAssignmentImportRequest request, CancellationToken cancellationToken)
        {
            Imports.Add(request);
            ImportTarget = targetType;
            ImportTargetId = targetId;
            return Task.FromResult(ImportResponse);
        }
        private static ApiError Denied() => new(ApiStatusCode.Forbidden, "Security.Forbidden", "Management permission revoked.");
    }

    private sealed class Catalog : IPermissionCatalogApiClient
    {
        public Task<ApiResponse<PermissionCatalogDto>> GetCatalogAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<PermissionCatalogDto>.Success(new PermissionCatalogDto("1.0", "catalog-digest", new[]
            {
                new PermissionCatalogItemDto("Inventory.Read", "Inventory", "Read inventory", "Active"),
                new PermissionCatalogItemDto("Inventory.Legacy", "Inventory", "Legacy permission", "Deprecated"),
            })));
    }

    private sealed class Session : IDesktopSession
    {
        private EventHandler? _changed;
        public event EventHandler? SessionChanged { add => _changed += value; remove => _changed -= value; }
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public bool IsAuthenticated { get; private set; } = true;
        public DesktopSessionState State => IsAuthenticated ? DesktopSessionState.Authenticated : DesktopSessionState.LoggingOut;
        public CurrentUserDto? CurrentUser { get; private set; } = new(7, "admin", "Administrator");
        public long Version { get; private set; } = 1;
        public void SignOut() { IsAuthenticated = false; CurrentUser = null; Version++; _changed?.Invoke(this, EventArgs.Empty); }
    }
}
