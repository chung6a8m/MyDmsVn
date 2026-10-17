using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.Application.Security;

public sealed class SecurityPermissionCatalogItemViewModel
{
    internal SecurityPermissionCatalogItemViewModel(
        PermissionCatalogComparisonItem comparison,
        bool isAssigned)
    {
        Key = comparison.Key;
        ChangeKind = comparison.ChangeKind;
        Current = comparison.Current;
        Previous = comparison.Previous;
        IsAssigned = isAssigned;
    }

    public string Key { get; }
    public PermissionCatalogChangeKind ChangeKind { get; }
    public PermissionCatalogItemDto? Current { get; }
    public PermissionCatalogItemDto? Previous { get; }
    public bool IsAssigned { get; }
    public string AssignmentStatus => IsAssigned ? "Assigned" : "Unassigned";
}

public sealed class SecurityAdministrationViewModel : DesktopViewModelBase, IDisposable
{
    private readonly ISecurityAdministrationApiClient _administration;
    private readonly IPermissionCatalogApiClient _catalog;
    private readonly IDesktopSession _session;
    private readonly ObservableCollection<SecurityUserDto> _users = new();
    private readonly ObservableCollection<SecurityRoleDto> _roles = new();
    private readonly ObservableCollection<SecurityPermissionCatalogItemViewModel> _catalogItems = new();
    private PermissionCatalogDto? _previousCatalog;
    private string _searchText = string.Empty;
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string _email = string.Empty;
    private string _roleName = string.Empty;
    private string _password = string.Empty;
    private int? _selectedUserId;
    private int? _selectedRoleId;
    private bool _disposed;
    private bool _canManageUsers;
    private bool _canManageRoles;
    private bool _canReadPermissionCatalog;

    public SecurityAdministrationViewModel(
        ISecurityAdministrationApiClient administration,
        IPermissionCatalogApiClient catalog,
        IDesktopSession session,
        IDesktopNotificationService notifications,
        IDesktopExceptionReporter? exceptionReporter = null)
        : base(notifications, exceptionReporter)
    {
        _administration = administration ?? throw new ArgumentNullException(nameof(administration));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _session.SessionChanged += OnSessionChanged;
        LoadCommand = new AsyncRelayCommand(LoadFromCommandAsync);
        CreateUserCommand = new AsyncRelayCommand(CreateUserFromCommandAsync);
        CreateRoleCommand = new AsyncRelayCommand(CreateRoleFromCommandAsync);
        CancelCommand = new RelayCommand(CancelCurrentOperation);
    }

    public IReadOnlyList<SecurityUserDto> Users => _users;
    public IReadOnlyList<SecurityRoleDto> Roles => _roles;
    public IReadOnlyList<SecurityPermissionCatalogItemViewModel> CatalogItems => _catalogItems;
    public IEnumerable<SecurityPermissionCatalogItemViewModel> FilteredCatalogItems =>
        string.IsNullOrWhiteSpace(SearchText)
            ? _catalogItems
            : _catalogItems.Where(item => item.Key.IndexOf(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          (item.Current?.Module.IndexOf(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                                          (item.Current?.Description.IndexOf(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(FilteredCatalogItems));
            }
        }
    }

    public string Username { get => _username; set => SetProperty(ref _username, value ?? string.Empty); }
    public string DisplayName { get => _displayName; set => SetProperty(ref _displayName, value ?? string.Empty); }
    public string Email { get => _email; set => SetProperty(ref _email, value ?? string.Empty); }
    public string RoleName { get => _roleName; set => SetProperty(ref _roleName, value ?? string.Empty); }
    public string Password { get => _password; set => SetProperty(ref _password, value ?? string.Empty); }

    public int? SelectedUserId
    {
        get => _selectedUserId;
        private set
        {
            if (SetProperty(ref _selectedUserId, value))
            {
                OnPropertyChanged(nameof(SelectedUser));
                OnPropertyChanged(nameof(IsSelectedUserProtected));
                RebuildCatalogItems();
            }
        }
    }

    public int? SelectedRoleId
    {
        get => _selectedRoleId;
        private set
        {
            if (SetProperty(ref _selectedRoleId, value))
            {
                OnPropertyChanged(nameof(SelectedRole));
                RebuildCatalogItems();
            }
        }
    }

    public SecurityUserDto? SelectedUser => _users.FirstOrDefault(user => user.UserId == SelectedUserId);
    public SecurityRoleDto? SelectedRole => _roles.FirstOrDefault(role => role.RoleId == SelectedRoleId);
    public bool IsSelectedUserProtected => SelectedUser?.IsProtectedAccount == true;
    public bool CanManageUsers { get => _canManageUsers; private set => SetProperty(ref _canManageUsers, value); }
    public bool CanManageRoles { get => _canManageRoles; private set => SetProperty(ref _canManageRoles, value); }
    public bool CanReadPermissionCatalog { get => _canReadPermissionCatalog; private set => SetProperty(ref _canReadPermissionCatalog, value); }
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand CreateUserCommand { get; }
    public IAsyncRelayCommand CreateRoleCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public void SetPreviousCatalogSnapshot(PermissionCatalogDto? snapshot)
    {
        _previousCatalog = snapshot;
        RebuildCatalogItems();
    }

    public void SelectUser(int? userId)
    {
        SelectedRoleId = null;
        SelectedUserId = userId;
        if (SelectedUser != null)
        {
            Username = SelectedUser.Username;
            DisplayName = SelectedUser.DisplayName;
            Email = SelectedUser.Email ?? string.Empty;
            Password = string.Empty;
        }
    }

    public void SelectRole(int? roleId)
    {
        SelectedUserId = null;
        SelectedRoleId = roleId;
        if (SelectedRole != null)
        {
            RoleName = SelectedRole.Name;
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (!TryCaptureSession(out var sessionVersion))
        {
            return;
        }

        await ExecuteBusyAsync(
            async token =>
            {
                try
                {
                    var usersTask = _administration.ListUsersAsync(token);
                    var rolesTask = _administration.ListRolesAsync(token);
                    var catalogTask = _catalog.GetCatalogAsync(token);
                    await Task.WhenAll(usersTask, rolesTask, catalogTask);
                    if (!IsCurrentSession(sessionVersion, token))
                    {
                        return;
                    }

                    var users = await usersTask;
                    var roles = await rolesTask;
                    var catalog = await catalogTask;
                    CanManageUsers = users.IsSuccess;
                    CanManageRoles = roles.IsSuccess;
                    CanReadPermissionCatalog = catalog.IsSuccess;
                    if (users.IsSuccess)
                    {
                        Replace(_users, users.Data!);
                    }

                    if (roles.IsSuccess)
                    {
                        Replace(_roles, roles.Data!);
                    }

                    if (catalog.IsSuccess)
                    {
                        _loadedCatalog = catalog.Data!;
                    }

                    if (!SelectedUserId.HasValue && !SelectedRoleId.HasValue && _users.Count > 0)
                    {
                        SelectUser(_users[0].UserId);
                    }

                    RebuildCatalogItems();
                    IsEmpty = _users.Count == 0 && _roles.Count == 0;
                    if (!users.IsSuccess)
                    {
                        ApplyApiError(users.Error!);
                    }
                    else if (!roles.IsSuccess)
                    {
                        ApplyApiError(roles.Error!);
                    }
                    else if (!catalog.IsSuccess)
                    {
                        ApplyApiError(catalog.Error!);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    if (IsCurrentSession(sessionVersion, token))
                    {
                        ApplyUnexpectedError(exception, "Security administration could not be loaded.");
                    }
                }
            },
            cancellationToken,
            publishCancellation: () => IsCurrentSession(sessionVersion, CancellationToken.None));
    }

    private PermissionCatalogDto? _loadedCatalog;

    public string GetPermissionExplanation(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return "Select a permission.";
        }

        if (SelectedUser is { } user)
        {
            var catalogEntry = _loadedCatalog?.Items.FirstOrDefault(item =>
                string.Equals(item.Key, permissionKey, StringComparison.Ordinal));
            if (catalogEntry == null || !string.Equals(catalogEntry.Status, "Active", StringComparison.Ordinal))
            {
                return "Permission is not Active; it cannot be granted.";
            }

            if (user.IsProtectedAccount)
            {
                return "Protected superuser account: Active permissions are allowed by the bound identity.";
            }

            if (!user.IsActive)
            {
                return "Inactive account: sign-in is blocked regardless of its assignments.";
            }

            var direct = user.DirectDecisions.FirstOrDefault(decision => decision.Decision.HasValue &&
                string.Equals(decision.PermissionKey, permissionKey, StringComparison.Ordinal));
            if (direct != null)
            {
                var matchingRole = _roles.Any(role => user.RoleIds.Contains(role.RoleId) &&
                    role.PermissionKeys.Contains(permissionKey, StringComparer.Ordinal));
                var inactiveMatchingRole = _roles.Any(role => !role.IsActive && user.RoleIds.Contains(role.RoleId) &&
                    role.PermissionKeys.Contains(permissionKey, StringComparer.Ordinal));
                return direct.Decision == true
                    ? matchingRole ? "Direct Allow (overrides role grant)." : "Direct Allow"
                    : inactiveMatchingRole
                        ? "Direct Deny (overrides inactive role grant)."
                        : matchingRole ? "Direct Deny (overrides role grant)." : "Direct Deny";
            }

            var memberRoles = _roles.Where(role => user.RoleIds.Contains(role.RoleId)).ToArray();
            if (memberRoles.Any(role => role.IsActive && role.PermissionKeys.Contains(permissionKey, StringComparer.Ordinal)))
            {
                return "Role Grant";
            }

            if (memberRoles.Any(role => !role.IsActive && role.PermissionKeys.Contains(permissionKey, StringComparer.Ordinal)))
            {
                return "No effective grant: matching role is inactive.";
            }

            return "No assignment: default deny.";
        }

        if (SelectedRole is { } role)
        {
            if (!role.IsActive)
            {
                return "Inactive role: its grants do not apply to members.";
            }

            return role.PermissionKeys.Contains(permissionKey, StringComparer.Ordinal)
                ? "Role Grant"
                : "No assignment: default deny.";
        }

        return "Select a user or role.";
    }

    public Task CreateUserAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrEmpty(Password))
        {
            ApplyValidationError("Enter a user name, display name and temporary password.",
                (nameof(Username), Username), (nameof(DisplayName), DisplayName), (nameof(Password), Password));
            return Task.CompletedTask;
        }

        return MutateAsync(
            new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.User,
                SecurityAdministrationOperationDto.Create,
                name: Username,
                displayName: DisplayName,
                email: EmptyToNull(Email),
                password: Password),
            cancellationToken,
            clearPassword: true);
    }

    public Task UpdateUserAsync(CancellationToken cancellationToken)
    {
        var user = SelectedUser;
        if (user == null || user.IsProtectedAccount)
        {
            return Task.CompletedTask;
        }

        return MutateAsync(new SecurityAdministrationMutationRequest(
            SecurityAdministrationTargetDto.User,
            SecurityAdministrationOperationDto.Update,
            targetId: user.UserId,
            name: Username,
            displayName: DisplayName,
            email: EmptyToNull(Email),
            expectedRevision: user.AssignmentRevision), cancellationToken);
    }

    public Task SetUserActiveAsync(bool isActive, CancellationToken cancellationToken)
    {
        var user = SelectedUser;
        return user == null || user.IsProtectedAccount
            ? Task.CompletedTask
            : MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.User,
                SecurityAdministrationOperationDto.SetActive,
                targetId: user.UserId,
                isActive: isActive,
                expectedRevision: user.AssignmentRevision), cancellationToken);
    }

    public Task ResetUserPasswordAsync(CancellationToken cancellationToken)
    {
        var user = SelectedUser;
        return user == null || user.IsProtectedAccount || string.IsNullOrEmpty(Password)
            ? Task.CompletedTask
            : MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.User,
                SecurityAdministrationOperationDto.SetPassword,
                targetId: user.UserId,
                password: Password,
                expectedRevision: user.AssignmentRevision), cancellationToken, clearPassword: true);
    }

    public Task CreateRoleAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RoleName))
        {
            ApplyValidationError("Enter a role name.", (nameof(RoleName), RoleName));
            return Task.CompletedTask;
        }

        return MutateAsync(new SecurityAdministrationMutationRequest(
            SecurityAdministrationTargetDto.Role,
            SecurityAdministrationOperationDto.Create,
            name: RoleName), cancellationToken);
    }

    public Task UpdateRoleAsync(CancellationToken cancellationToken)
    {
        var role = SelectedRole;
        return role == null
            ? Task.CompletedTask
            : MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.Role,
                SecurityAdministrationOperationDto.Update,
                targetId: role.RoleId,
                name: RoleName,
                expectedRevision: role.AssignmentRevision), cancellationToken);
    }

    public Task SetRoleActiveAsync(bool isActive, CancellationToken cancellationToken)
    {
        var role = SelectedRole;
        return role == null
            ? Task.CompletedTask
            : MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.Role,
                SecurityAdministrationOperationDto.SetActive,
                targetId: role.RoleId,
                isActive: isActive,
                expectedRevision: role.AssignmentRevision), cancellationToken);
    }

    public Task SetMembershipAsync(int roleId, bool isMember, CancellationToken cancellationToken)
    {
        var user = SelectedUser;
        var role = _roles.FirstOrDefault(item => item.RoleId == roleId);
        return user == null || user.IsProtectedAccount || role == null
            ? Task.CompletedTask
            : MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.User,
                SecurityAdministrationOperationDto.SetMembership,
                targetId: user.UserId,
                relatedId: role.RoleId,
                isActive: isMember,
                expectedRevision: user.AssignmentRevision,
                expectedRelatedRevision: role.AssignmentRevision), cancellationToken);
    }

    public Task SetDirectDecisionAsync(string permissionKey, bool? decision, CancellationToken cancellationToken)
    {
        var user = SelectedUser;
        return user == null || user.IsProtectedAccount || !IsKnownActivePermission(permissionKey)
            ? Task.CompletedTask
            : MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.User,
                SecurityAdministrationOperationDto.SetPermission,
                targetId: user.UserId,
                permissionKey: permissionKey,
                decision: decision,
                expectedRevision: user.AssignmentRevision), cancellationToken);
    }

    public Task SetRolePermissionAsync(string permissionKey, bool isGranted, CancellationToken cancellationToken)
    {
        var role = SelectedRole;
        return role == null || !IsKnownActivePermission(permissionKey)
            ? Task.CompletedTask
            : MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.Role,
                SecurityAdministrationOperationDto.SetPermission,
                targetId: role.RoleId,
                permissionKey: permissionKey,
                decision: isGranted,
                expectedRevision: role.AssignmentRevision), cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.SessionChanged -= OnSessionChanged;
        CancelCurrentOperation();
        ClearSessionData();
    }

    private async Task MutateAsync(
        SecurityAdministrationMutationRequest request,
        CancellationToken cancellationToken,
        bool clearPassword = false)
    {
        ThrowIfDisposed();
        if (!TryCaptureSession(out var sessionVersion))
        {
            Password = string.Empty;
            return;
        }

        await ExecuteBusyAsync(
            async token =>
            {
                if (!IsCurrentSession(sessionVersion, token))
                {
                    return;
                }

                try
                {
                    var response = await _administration.MutateAsync(request, token);
                    if (!IsCurrentSession(sessionVersion, token))
                    {
                        return;
                    }

                    if (!response.IsSuccess)
                    {
                        ApplyApiError(response.Error!);
                        if (response.Error!.Status == ApiStatusCode.Conflict)
                        {
                            await RefreshDataAsync(token, sessionVersion);
                        }

                        return;
                    }

                    PublishNotification(
                        DesktopNotificationKind.Success,
                        response.Data!.Changed ? "Security changes saved." : "No changes were needed.");
                    await RefreshDataAsync(token, sessionVersion);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    if (IsCurrentSession(sessionVersion, token))
                    {
                        ApplyUnexpectedError(exception, "Security changes could not be saved.");
                    }
                }
                finally
                {
                    if (clearPassword)
                    {
                        Password = string.Empty;
                    }
                }
            },
            cancellationToken,
            publishCancellation: () => IsCurrentSession(sessionVersion, CancellationToken.None));
    }

    private async Task RefreshDataAsync(CancellationToken token, long sessionVersion)
    {
        var usersTask = _administration.ListUsersAsync(token);
        var rolesTask = _administration.ListRolesAsync(token);
        var catalogTask = _catalog.GetCatalogAsync(token);
        await Task.WhenAll(usersTask, rolesTask, catalogTask);
        if (!IsCurrentSession(sessionVersion, token))
        {
            return;
        }

        var users = await usersTask;
        var roles = await rolesTask;
        var catalog = await catalogTask;
        CanManageUsers = users.IsSuccess;
        CanManageRoles = roles.IsSuccess;
        CanReadPermissionCatalog = catalog.IsSuccess;
        if (users.IsSuccess)
        {
            Replace(_users, users.Data!);
        }

        if (roles.IsSuccess)
        {
            Replace(_roles, roles.Data!);
        }

        if (catalog.IsSuccess)
        {
            _loadedCatalog = catalog.Data!;
        }

        if (!users.IsSuccess || !roles.IsSuccess || !catalog.IsSuccess)
        {
            ApplyApiError(!users.IsSuccess ? users.Error! : !roles.IsSuccess ? roles.Error! : catalog.Error!);
        }
        if (SelectedUserId.HasValue && SelectedUser == null)
        {
            SelectedUserId = null;
        }

        if (SelectedRoleId.HasValue && SelectedRole == null)
        {
            SelectedRoleId = null;
        }

        RebuildCatalogItems();
    }

    private void RebuildCatalogItems()
    {
        _catalogItems.Clear();
        if (_loadedCatalog == null)
        {
            OnPropertyChanged(nameof(CatalogItems));
            OnPropertyChanged(nameof(FilteredCatalogItems));
            return;
        }

        var comparison = PermissionCatalogSnapshotComparer.Compare(_loadedCatalog, _previousCatalog);
        foreach (var item in comparison.Items)
        {
            var assigned = SelectedUser is { } user
                ? user.DirectDecisions.Any(decision => string.Equals(decision.PermissionKey, item.Key, StringComparison.Ordinal)) ||
                  user.RoleIds.Any(roleId => _roles.Any(role => role.RoleId == roleId && role.IsActive && role.PermissionKeys.Contains(item.Key, StringComparer.Ordinal)))
                : SelectedRole?.PermissionKeys.Contains(item.Key, StringComparer.Ordinal) == true;
            _catalogItems.Add(new SecurityPermissionCatalogItemViewModel(item, assigned));
        }

        OnPropertyChanged(nameof(CatalogItems));
        OnPropertyChanged(nameof(FilteredCatalogItems));
    }

    private bool IsKnownActivePermission(string permissionKey) =>
        _loadedCatalog?.Items.Any(item => string.Equals(item.Key, permissionKey, StringComparison.Ordinal) &&
            string.Equals(item.Status, "Active", StringComparison.Ordinal)) == true;

    private bool TryCaptureSession(out long version)
    {
        version = _session.Version;
        return !_disposed && _session.IsAuthenticated && _session.CurrentUser != null;
    }

    private bool IsCurrentSession(long version, CancellationToken token) =>
        !token.IsCancellationRequested && !_disposed && _session.IsAuthenticated &&
        _session.Version == version && _session.CurrentUser != null;

    private void OnSessionChanged(object? sender, EventArgs eventArgs)
    {
        if (!_session.IsAuthenticated)
        {
            CancelCurrentOperation();
            ClearSessionData();
        }
    }

    private void ClearSessionData()
    {
        _users.Clear();
        _roles.Clear();
        _catalogItems.Clear();
        _loadedCatalog = null;
        CanManageUsers = false;
        CanManageRoles = false;
        CanReadPermissionCatalog = false;
        SelectedUserId = null;
        SelectedRoleId = null;
        Password = string.Empty;
        OnPropertyChanged(nameof(Users));
        OnPropertyChanged(nameof(Roles));
        OnPropertyChanged(nameof(CatalogItems));
        OnPropertyChanged(nameof(FilteredCatalogItems));
    }

    private async Task LoadFromCommandAsync(CancellationToken token) => await LoadAsync(token);
    private async Task CreateUserFromCommandAsync(CancellationToken token) => await CreateUserAsync(token);
    private async Task CreateRoleFromCommandAsync(CancellationToken token) => await CreateRoleAsync(token);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SecurityAdministrationViewModel));
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ApplyValidationError(string message, params (string Name, string Value)[] fields)
    {
        var details = fields
            .Where(field => string.IsNullOrWhiteSpace(field.Value))
            .Select(field => new ApiErrorDetail("Validation.Required", message, field.Name))
            .ToArray();
        ApplyApiError(new ApiError(ApiStatusCode.BadRequest, "Validation.Failed", message, details));
    }
}
