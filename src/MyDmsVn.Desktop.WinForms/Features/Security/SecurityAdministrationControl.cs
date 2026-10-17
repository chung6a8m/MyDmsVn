using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

public interface ISecurityAdministrationControlFactory
{
    Control Create();
}

public interface IActivatableDesktopControl
{
    Task ActivateAsync(CancellationToken cancellationToken);
}

public sealed class SecurityAdministrationControl : UserControl, IActivatableDesktopControl
{
    private readonly SecurityAdministrationViewModel _viewModel;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _usersGrid = CreateGrid();
    private readonly DataGridView _rolesGrid = CreateGrid();
    private readonly DataGridView _permissionsGrid = CreateGrid();
    private readonly TextBox _username = new();
    private readonly TextBox _displayName = new();
    private readonly TextBox _email = new();
    private readonly TextBox _password = new();
    private readonly TextBox _roleName = new();
    private readonly TextBox _search = new();
    private readonly CheckedListBox _memberships = new() { CheckOnClick = true };
    private readonly ComboBox _decision = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _target = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _status = new() { AutoSize = true };
    private readonly Button _importButton = new() { Text = "Import JSON…", AutoSize = true };
    private readonly int _uiThreadId;
    private bool _applying;
    private bool _disposed;

    public SecurityAdministrationControl(SecurityAdministrationViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _uiThreadId = Thread.CurrentThread.ManagedThreadId;
        Dock = DockStyle.Fill;
        _password.UseSystemPasswordChar = true;
        UpdateDecisionOptions(isRoleTarget: false);
        _target.Items.AddRange(new object[] { "User", "Role" });
        _target.SelectedIndex = 0;

        var usersTab = new TabPage("Users");
        var rolesTab = new TabPage("Roles");
        var permissionsTab = new TabPage("Permissions");
        usersTab.Controls.Add(CreateUsersPage());
        rolesTab.Controls.Add(CreateRolesPage());
        permissionsTab.Controls.Add(CreatePermissionsPage());
        _tabs.TabPages.Add(usersTab);
        _tabs.TabPages.Add(rolesTab);
        _tabs.TabPages.Add(permissionsTab);

        var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(8, 4, 8, 2) };
        var cancelButton = new Button { Text = "Cancel operation", Dock = DockStyle.Right, AutoSize = true };
        cancelButton.Click += (_, _) => _viewModel.CancelCommand.Execute(null);
        statusBar.Controls.Add(_status);
        statusBar.Controls.Add(cancelButton);
        Controls.Add(_tabs);
        Controls.Add(statusBar);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ErrorsChanged += OnErrorsChanged;
        _usersGrid.DataError += OnDataError;
        _rolesGrid.DataError += OnDataError;
        _permissionsGrid.DataError += OnDataError;
        _search.TextChanged += (_, _) => _viewModel.SearchText = _search.Text;
        _username.TextChanged += (_, _) => _viewModel.Username = _username.Text;
        _displayName.TextChanged += (_, _) => _viewModel.DisplayName = _displayName.Text;
        _email.TextChanged += (_, _) => _viewModel.Email = _email.Text;
        _password.TextChanged += (_, _) => _viewModel.Password = _password.Text;
        _roleName.TextChanged += (_, _) => _viewModel.RoleName = _roleName.Text;
        _target.SelectedIndexChanged += (_, _) =>
        {
            UpdateDecisionOptions(string.Equals(_target.SelectedItem as string, "Role", StringComparison.Ordinal));
            RefreshPermissionRows();
            ApplyState();
        };
        _viewModel.SelectUser(null);
        ApplyState();
    }

    public DataGridView UsersGrid => _usersGrid;
    public DataGridView RolesGrid => _rolesGrid;
    public DataGridView PermissionsGrid => _permissionsGrid;
    public Button ImportPermissionButton => _importButton;
    public string StatusText => _status.Text;
    public bool IsPasswordMasked => _password.UseSystemPasswordChar;
    public Task LastOperation { get; private set; } = Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken)
    {
        return _viewModel.LoadAsync(cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.ErrorsChanged -= OnErrorsChanged;
            _usersGrid.DataError -= OnDataError;
            _rolesGrid.DataError -= OnDataError;
            _permissionsGrid.DataError -= OnDataError;
            _viewModel.Dispose();
        }

        base.Dispose(disposing);
    }

    private Control CreateUsersPage()
    {
        ConfigureColumns(_usersGrid,
            ("Username", "User name"), ("DisplayName", "Display name"), ("Email", "Email"),
            ("IsActive", "Active"), ("IsProtectedAccount", "Protected"));
        _usersGrid.SelectionChanged += (_, _) =>
        {
            if (!_applying && _usersGrid.CurrentRow?.DataBoundItem is SecurityUserDto user)
            {
                _viewModel.SelectUser(user.UserId);
                _target.SelectedIndex = 0;
                RefreshMemberships();
            }
        };
        _memberships.ItemCheck += (_, eventArgs) =>
        {
            if (_applying || _viewModel.SelectedUser == null || eventArgs.Index < 0 || eventArgs.Index >= _viewModel.Roles.Count)
            {
                return;
            }

            var role = _viewModel.Roles[eventArgs.Index];
            var isMember = eventArgs.NewValue == CheckState.Checked;
            BeginOperation(_viewModel.SetMembershipAsync(role.RoleId, isMember, CancellationToken.None));
        };

        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2, Padding = new Padding(8) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(fields, "Username", _username);
        AddField(fields, "Display name", _displayName);
        AddField(fields, "Email", _email);
        _password.AccessibleName = "Temporary password";
        AddField(fields, "Temporary password", _password);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.Add(CreateButton("New user", (_, _) => ClearUserEditor()));
        buttons.Controls.Add(CreateButton("Create", (_, _) => BeginOperation(_viewModel.CreateUserAsync(CancellationToken.None))));
        buttons.Controls.Add(CreateButton("Save changes", (_, _) => BeginOperation(_viewModel.UpdateUserAsync(CancellationToken.None))));
        buttons.Controls.Add(CreateButton("Activate", (_, _) => BeginOperation(_viewModel.SetUserActiveAsync(true, CancellationToken.None))));
        buttons.Controls.Add(CreateButton("Deactivate", (_, _) => BeginOperation(_viewModel.SetUserActiveAsync(false, CancellationToken.None))));
        buttons.Controls.Add(CreateButton("Reset password", (_, _) => BeginOperation(_viewModel.ResetUserPasswordAsync(CancellationToken.None))));
        AddField(fields, "Actions", buttons);
        AddField(fields, "Role membership", _memberships);

        var split = new SplitContainer { Dock = DockStyle.Fill };
        split.SizeChanged += (_, _) => SetUserEditorSplitDistance(split);
        split.Panel1.Controls.Add(_usersGrid);
        split.Panel2.Controls.Add(fields);
        var top = CreateSearchBar("Filter users");
        var layout = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(split);
        layout.Controls.Add(top);
        return layout;
    }

    private static void SetUserEditorSplitDistance(SplitContainer split)
    {
        var availableWidth = split.ClientSize.Width - split.SplitterWidth;
        var minimumDistance = split.Panel1MinSize;
        var maximumDistance = availableWidth - split.Panel2MinSize;
        if (maximumDistance < minimumDistance)
        {
            return;
        }

        var distance = (int)Math.Round(availableWidth * 0.55d);
        distance = Math.Max(minimumDistance, Math.Min(distance, maximumDistance));
        if (split.SplitterDistance != distance)
        {
            split.SplitterDistance = distance;
        }
    }

    private Control CreateRolesPage()
    {
        ConfigureColumns(_rolesGrid,
            ("Name", "Role"), ("IsActive", "Active"), ("AssignmentRevision", "Revision"));
        _rolesGrid.SelectionChanged += (_, _) =>
        {
            if (!_applying && _rolesGrid.CurrentRow?.DataBoundItem is SecurityRoleDto role)
            {
                _viewModel.SelectRole(role.RoleId);
                _target.SelectedIndex = 1;
            }
        };
        var editor = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        _roleName.Width = 240;
        _roleName.AccessibleName = "Role name";
        editor.Controls.Add(new Label { Text = "Role name", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        editor.Controls.Add(_roleName);
        editor.Controls.Add(CreateButton("New role", (_, _) => { _viewModel.SelectRole(null); _roleName.Clear(); }));
        editor.Controls.Add(CreateButton("Create", (_, _) => BeginOperation(_viewModel.CreateRoleAsync(CancellationToken.None))));
        editor.Controls.Add(CreateButton("Save changes", (_, _) => BeginOperation(_viewModel.UpdateRoleAsync(CancellationToken.None))));
        editor.Controls.Add(CreateButton("Activate", (_, _) => BeginOperation(_viewModel.SetRoleActiveAsync(true, CancellationToken.None))));
        editor.Controls.Add(CreateButton("Deactivate", (_, _) => BeginOperation(_viewModel.SetRoleActiveAsync(false, CancellationToken.None))));
        var layout = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(_rolesGrid);
        layout.Controls.Add(editor);
        return layout;
    }

    private Control CreatePermissionsPage()
    {
        ConfigureColumns(_permissionsGrid,
            ("Key", "Permission"), ("Module", "Module"), ("Status", "Lifecycle"),
            ("Change", "Catalog change"), ("AssignmentStatus", "Assignment"));
        _permissionsGrid.SelectionChanged += (_, _) => UpdatePermissionExplanation();
        _permissionsGrid.CellDoubleClick += (_, _) => ApplyPermissionSelection();
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        _target.Width = 100;
        _search.Width = 260;
        _search.AccessibleName = "Filter permission catalog";
        toolbar.Controls.Add(new Label { Text = "Target", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        toolbar.Controls.Add(_target);
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(_decision);
        toolbar.Controls.Add(CreateButton("Apply assignment", (_, _) => ApplyPermissionSelection()));
        _importButton.Click += async (_, _) => await ImportPermissionFileAsync();
        toolbar.Controls.Add(_importButton);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(8) };
        var explanation = new Label { Dock = DockStyle.Fill, AutoSize = false, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
        explanation.Name = "PermissionExplanation";
        footer.Controls.Add(explanation);
        var layout = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(_permissionsGrid);
        layout.Controls.Add(toolbar);
        layout.Controls.Add(footer);
        return layout;
    }

    private Control CreateSearchBar(string accessibleName)
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8) };
        var text = new TextBox { Dock = DockStyle.Fill, AccessibleName = accessibleName };
        text.TextChanged += (_, _) => FilterGrid(_usersGrid, text.Text);
        bar.Controls.Add(text);
        return bar;
    }

    private void ApplyState()
    {
        if (_disposed || IsDisposed)
        {
            return;
        }

        _applying = true;
        try
        {
            _usersGrid.DataSource = null;
            _usersGrid.DataSource = _viewModel.Users.ToList();
            _rolesGrid.DataSource = null;
            _rolesGrid.DataSource = _viewModel.Roles.ToList();
            _username.Text = _viewModel.Username;
            _displayName.Text = _viewModel.DisplayName;
            _email.Text = _viewModel.Email;
            _roleName.Text = _viewModel.RoleName;
            _password.Text = _viewModel.Password;
            var protectedUser = _viewModel.IsSelectedUserProtected;
            _tabs.TabPages[0].Enabled = _viewModel.CanManageUsers;
            _tabs.TabPages[1].Enabled = _viewModel.CanManageRoles;
            _tabs.TabPages[2].Enabled = _viewModel.CanReadPermissionCatalog;
            _username.Enabled = !protectedUser;
            _displayName.Enabled = !protectedUser;
            _email.Enabled = !protectedUser;
            _password.Enabled = !protectedUser;
            _decision.Enabled = _viewModel.CanReadPermissionCatalog && !protectedUser;
            _importButton.Enabled = CanImportSelectedTarget();
            foreach (Control control in Descendants(_tabs.TabPages[0]))
            {
                if (control is Button button && button.Text is "Save changes" or "Activate" or "Deactivate" or "Reset password")
                {
                    button.Enabled = !protectedUser;
                }
            }

            RefreshMemberships();
            RefreshPermissionRows();
            _status.Text = _viewModel.ErrorMessage ?? (_viewModel.IsBusy ? "Working…" : "Ready");
        }
        finally
        {
            _applying = false;
        }
    }

    private void RefreshMemberships()
    {
        _memberships.Items.Clear();
        var user = _viewModel.SelectedUser;
        foreach (var role in _viewModel.Roles)
        {
            var index = _memberships.Items.Add(role.Name + (role.IsActive ? string.Empty : " (inactive)"));
            _memberships.SetItemChecked(index, user?.RoleIds.Contains(role.RoleId) == true);
        }

        _memberships.Enabled = user != null && !user.IsProtectedAccount;
    }

    private void RefreshPermissionRows()
    {
        _permissionsGrid.DataSource = null;
        IEnumerable<SecurityPermissionCatalogItemViewModel> rows = _viewModel.FilteredCatalogItems;
        if (string.Equals(_target.SelectedItem as string, "Role", StringComparison.Ordinal))
        {
            rows = rows.Where(row => string.Equals(row.Current?.Status, "Active", StringComparison.Ordinal));
        }

        _permissionsGrid.DataSource = rows.Select(row => new PermissionRow(
            row.Key,
            row.Current?.Module ?? row.Previous?.Module ?? string.Empty,
            row.Current?.Status ?? "Removed",
            row.ChangeKind.ToString(),
            row.AssignmentStatus)).ToList();
        UpdatePermissionExplanation();
    }

    private void UpdatePermissionExplanation()
    {
        if (_permissionsGrid.CurrentRow?.DataBoundItem is not PermissionRow row)
        {
            return;
        }

        var label = _tabs.TabPages[2].Controls.OfType<Panel>()
            .SelectMany(panel => panel.Controls.OfType<Label>())
            .FirstOrDefault(item => item.Name == "PermissionExplanation");
        if (label != null)
        {
            label.Text = _viewModel.GetPermissionExplanation(row.Key);
        }
    }

    private void ApplyPermissionSelection()
    {
        if (_permissionsGrid.CurrentRow?.DataBoundItem is not PermissionRow row)
        {
            return;
        }

        if (string.Equals(_target.SelectedItem as string, "Role", StringComparison.Ordinal))
        {
            var isGranted = _decision.SelectedIndex == 0;
            BeginOperation(_viewModel.SetRolePermissionAsync(row.Key, isGranted, CancellationToken.None));
            return;
        }

        bool? decision = _decision.SelectedIndex switch
        {
            0 => true,
            1 => false,
            _ => null,
        };
        BeginOperation(_viewModel.SetDirectDecisionAsync(row.Key, decision, CancellationToken.None));
    }

    private bool CanImportSelectedTarget()
    {
        if (string.Equals(_target.SelectedItem as string, "User", StringComparison.Ordinal))
        {
            return _viewModel.CanManageUsers && _viewModel.SelectedUser != null &&
                   _viewModel.SelectedUser.IsActive && !_viewModel.SelectedUser.IsProtectedAccount;
        }

        return _viewModel.CanManageRoles && _viewModel.SelectedRole != null && _viewModel.SelectedRole.IsActive;
    }

    private async Task ImportPermissionFileAsync()
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Permission assignment JSON (*.json)|*.json|All files (*.*)|*.*",
            Title = "Select a permission assignment import",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var json = ReadBoundedUtf8File(dialog.FileName);
            var targetType = string.Equals(_target.SelectedItem as string, "Role", StringComparison.Ordinal)
                ? PermissionImportTargetTypeDto.Role
                : PermissionImportTargetTypeDto.User;
            var targetId = targetType == PermissionImportTargetTypeDto.User
                ? _viewModel.SelectedUser?.UserId
                : _viewModel.SelectedRole?.RoleId;
            if (!targetId.HasValue)
            {
                return;
            }

            await _viewModel.PreviewPermissionImportAsync(targetType, targetId.Value, json, CancellationToken.None);
            var preview = _viewModel.ImportPreview;
            if (preview == null || _disposed || IsDisposed) return;

            var summary = new StringBuilder()
                .Append("Target: ").Append(preview.Import.TargetType).Append(" ")
                .Append(preview.TargetName).Append(" (#").Append(preview.Import.TargetId).AppendLine(")")
                .Append("New assignments: ").Append(preview.Additions.Count).AppendLine()
                .Append("Current revision: ").Append(preview.TargetRevision).AppendLine()
                .AppendLine()
                .AppendLine("Only these additive decisions will be applied:");
            foreach (var entry in preview.Additions.Take(20))
            {
                summary.Append("• ").Append(entry.PermissionKey).Append(" — ")
                    .AppendLine(entry.Granted ? "Allow" : "Deny");
            }

            if (preview.Additions.Count > 20)
                summary.Append("… and ").Append(preview.Additions.Count - 20).AppendLine(" more");
            if (preview.Additions.Count == 0)
                summary.AppendLine("No new assignments; applying will be a no-op.");
            summary.AppendLine().Append("Continue with this exact preview?");
            if (MessageBox.Show(this, summary.ToString(), "Confirm permission import",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                await _viewModel.ApplyPermissionImportAsync(json, CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                         exception is DecoderFallbackException || exception is InvalidDataException)
        {
            _status.Text = exception is InvalidDataException
                ? exception.Message
                : "The import file could not be read.";
        }
    }

    private static string ReadBoundedUtf8File(string path)
    {
        const int limit = PermissionAssignmentImportLimits.MaximumUtf8Bytes;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[limit + 1];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = stream.Read(buffer, count, buffer.Length - count);
            if (read == 0) break;
            count += read;
        }

        if (count > limit)
            throw new InvalidDataException("The import file exceeds the 1 MiB limit.");
        return new UTF8Encoding(false, true).GetString(buffer, 0, count);
    }

    private void UpdateDecisionOptions(bool isRoleTarget)
    {
        var selectedIndex = _decision.SelectedIndex;
        _decision.Items.Clear();
        if (isRoleTarget)
        {
            _decision.Items.AddRange(new object[] { "Grant", "Remove" });
            _decision.SelectedIndex = selectedIndex == 0 ? 0 : 1;
        }
        else
        {
            _decision.Items.AddRange(new object[] { "Allow", "Deny", "Inherit" });
            _decision.SelectedIndex = selectedIndex is 0 or 1 ? selectedIndex : 2;
        }
    }

    private void ClearUserEditor()
    {
        _viewModel.SelectUser(null);
        _username.Clear();
        _displayName.Clear();
        _email.Clear();
        _password.Clear();
    }

    private void BeginOperation(Task operation)
    {
        LastOperation = operation;
        _ = ObserveOperationAsync(operation);
    }

    private async Task ObserveOperationAsync(Task operation)
    {
        try
        {
            await operation;
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Operation canceled.";
        }
        catch (Exception)
        {
            _status.Text = "The operation could not be completed.";
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (Thread.CurrentThread.ManagedThreadId == _uiThreadId)
        {
            ApplyState();
        }
        else if (IsHandleCreated && !IsDisposed)
        {
            try
            {
                BeginInvoke((Action)ApplyState);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs eventArgs) => OnViewModelPropertyChanged(sender, new PropertyChangedEventArgs(nameof(SecurityAdministrationViewModel.ErrorMessage)));

    private void OnDataError(object? sender, DataGridViewDataErrorEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        eventArgs.ThrowException = false;
        _status.Text = "The selected value is invalid.";
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        ReadOnly = true,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
    };

    private static void ConfigureColumns(DataGridView grid, params (string Property, string Title)[] columns)
    {
        foreach (var column in columns)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = column.Property,
                HeaderText = column.Title,
                Name = column.Property,
                ReadOnly = true,
            });
        }
    }

    private static void AddField(TableLayoutPanel layout, string title, Control control)
    {
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (layout.ColumnStyles.Count == 0)
        {
            layout.ColumnCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        }

        layout.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) }, 0, layout.RowCount - 1);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, layout.RowCount - 1);
    }

    private static Button CreateButton(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += handler;
        return button;
    }

    private static void FilterGrid(DataGridView grid, string filter)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            row.Visible = string.IsNullOrWhiteSpace(filter) || row.Cells.Cast<DataGridViewCell>().Any(cell =>
                Convert.ToString(cell.Value)?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class PermissionRow
    {
        public PermissionRow(string key, string module, string status, string change, string assignmentStatus)
        {
            Key = key;
            Module = module;
            Status = status;
            Change = change;
            AssignmentStatus = assignmentStatus;
        }

        public string Key { get; }
        public string Module { get; }
        public string Status { get; }
        public string Change { get; }
        public string AssignmentStatus { get; }
    }
}
