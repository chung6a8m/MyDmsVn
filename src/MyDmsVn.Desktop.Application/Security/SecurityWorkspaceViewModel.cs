using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.Application.Security;
public sealed partial class SecurityWorkspaceViewModel : IDisposable
{
    readonly ISecurityAdministrationApiClient _administration;
    readonly IPermissionCatalogApiClient _catalog;
    readonly IDesktopSession _session;
    readonly IUiDispatcher _dispatcher;
    bool _disposed;
    int _generation;
    public SecurityWorkspaceViewModel(ISecurityAdministrationApiClient administration, IPermissionCatalogApiClient catalog, IDesktopSession session, IUiDispatcher dispatcher, long capturedVersion)
    {
        _administration = administration;
        _catalog = catalog;
        _session = session;
        _dispatcher = dispatcher;
        OwnerVersion = capturedVersion;
        Users = Empty<SecurityUserDto>();
        Roles = Empty<SecurityRoleDto>();
        Catalog = Empty<PermissionCatalogItemDto>();
        session.SessionChanged += SessionChanged;
    }

    public event EventHandler? Changed;
    public string? CatalogDigest { get; private set; }
    public long OwnerVersion { get; }
    public bool IsCurrent => !_disposed && _session.IsAuthenticated && _session.Version == OwnerVersion;
    public SecurityDataset<SecurityUserDto> Users { get; private set; }
    public SecurityDataset<SecurityRoleDto> Roles { get; private set; }
    public SecurityDataset<PermissionCatalogItemDto> Catalog { get; private set; }
    public int? SelectedUserId { get; private set; }
    public int? SelectedRoleId { get; private set; }
    public SecurityUserDto? SelectedUser => Users.Items.FirstOrDefault(u => u.UserId == SelectedUserId);
    public SecurityRoleDto? SelectedRole => Roles.Items.FirstOrDefault(r => r.RoleId == SelectedRoleId);
    public SecurityProjectionSnapshot Snapshot => new(SelectedUser, Users, Roles, Catalog, OwnerVersion);

    public void SelectUser(int id)
    {
        if (SelectedUserId != id && !CanNavigate) return;
        if (IsCurrent && Users.IsCurrent(OwnerVersion))
        {
            if (SelectedUserId != id)
            {
                InvalidateImportPreview();
                ClearEditorForLifetime();
                LastMutationFeedback = null;
            }
            SelectedUserId = id;
            Notify();
        }
    }

    public void SelectRole(int id)
    {
        if (SelectedRoleId != id && !CanNavigate) return;
        if (IsCurrent && Roles.IsCurrent(OwnerVersion))
        {
            if (SelectedRoleId != id)
            {
                InvalidateImportPreview();
                ClearEditorForLifetime();
                LastMutationFeedback = null;
            }
            SelectedRoleId = id;
            Notify();
        }
    }

    public Task LoadAsync(CancellationToken token) => !CanNavigate ? Task.CompletedTask : LoadCoreAsync(token);

    async Task LoadCoreAsync(CancellationToken token, bool requiredSaveRefresh = false)
    {
        if (!IsCurrent)
            return;
        InvalidateImportPreview();
        var generation = ++_generation;
        var previousCatalogDigest = CatalogDigest;
        Users = Empty<SecurityUserDto>(SecurityDatasetState.Loading);
        Roles = Empty<SecurityRoleDto>(SecurityDatasetState.Loading);
        CatalogDigest = null;
        Catalog = Empty<PermissionCatalogItemDto>(SecurityDatasetState.Loading);
        Notify();
        await Task.WhenAll(ReadUsers(), ReadRoles(), ReadCatalog());
        async Task Publish(Action action)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                if (IsCurrent && generation == _generation && !token.IsCancellationRequested)
                {
                    action();
                    Notify();
                }
            }, CancellationToken.None);
        }

        async Task ReadUsers()
        {
            try
            {
                var r = await _administration.ListUsersAsync(token);
                await Publish(() =>
                {
                    Users = Dataset(r);
                    if (Users.State == SecurityDatasetState.Forbidden)
                        RevokeTargetData(SecurityAdministrationTargetDto.User, requiredSaveRefresh);
                    else
                        InvalidateCleanEditorForRead(SecurityAdministrationTargetDto.User);
                    if (SelectedUser == null)
                        SelectedUserId = Users.Items.FirstOrDefault()?.UserId;
                });
            }
            catch (OperationCanceledException)when (token.IsCancellationRequested)
            {
            }
            catch
            {
                await Publish(() => { Users = Empty<SecurityUserDto>(SecurityDatasetState.Failed); InvalidateCleanEditorForRead(SecurityAdministrationTargetDto.User); });
            }
        }

        async Task ReadRoles()
        {
            try
            {
                var r = await _administration.ListRolesAsync(token);
                await Publish(() =>
                {
                    Roles = Dataset(r);
                    if (Roles.State == SecurityDatasetState.Forbidden)
                        RevokeTargetData(SecurityAdministrationTargetDto.Role, requiredSaveRefresh);
                    else
                        InvalidateCleanEditorForRead(SecurityAdministrationTargetDto.Role);
                    if (SelectedRole == null)
                        SelectedRoleId = Roles.Items.FirstOrDefault()?.RoleId;
                });
            }
            catch (OperationCanceledException)when (token.IsCancellationRequested)
            {
            }
            catch
            {
                await Publish(() => { Roles = Empty<SecurityRoleDto>(SecurityDatasetState.Failed); InvalidateCleanEditorForRead(SecurityAdministrationTargetDto.Role); });
            }
        }

        async Task ReadCatalog()
        {
            try
            {
                var r = await _catalog.GetCatalogAsync(token);
                await Publish(() =>
                {
                    CatalogDigest = r.IsSuccess ? r.Data!.CatalogDigest : null;
                    Catalog = r.IsSuccess ? new SecurityDataset<PermissionCatalogItemDto>(SecurityDatasetState.Ready, OwnerVersion, true, r.Data!.Items) : Empty<PermissionCatalogItemDto>(r.Error?.Status == ApiStatusCode.Forbidden ? SecurityDatasetState.Forbidden : SecurityDatasetState.Failed);
                    if (!IsBusy && !RequiresRecovery && (RoleDraft != null || DirectPermissionKey != null) &&
                        (!Catalog.IsCurrent(OwnerVersion) || !string.Equals(previousCatalogDigest, CatalogDigest, StringComparison.Ordinal)))
                        ClearEditorForLifetime();
                });
            }
            catch (OperationCanceledException)when (token.IsCancellationRequested)
            {
            }
            catch
            {
                await Publish(() => { CatalogDigest = null; Catalog = Empty<PermissionCatalogItemDto>(SecurityDatasetState.Failed); if (!IsBusy && !RequiresRecovery) ClearEditorForLifetime(); });
            }
        }
    }

    SecurityDataset<T> Dataset<T>(ApiResponse<System.Collections.Generic.IReadOnlyList<T>> response) => response.IsSuccess ? new SecurityDataset<T>(SecurityDatasetState.Ready, OwnerVersion, true, response.Data!) : Empty<T>(response.Error?.Status == ApiStatusCode.Forbidden ? SecurityDatasetState.Forbidden : SecurityDatasetState.Failed);
    SecurityDataset<T> Empty<T>(SecurityDatasetState state = SecurityDatasetState.NotLoaded) => new(state, OwnerVersion, false);
    void Notify() => Changed?.Invoke(this, EventArgs.Empty);
    void SessionChanged(object? sender, EventArgs e)
    {
        if (IsCurrent)
            return;
        ++_generation;
        InvalidateImportPreview();
        CatalogDigest = null;
        LastMutationFeedback = null;
        ClearEditorForLifetime();
        Users = Empty<SecurityUserDto>();
        Roles = Empty<SecurityRoleDto>();
        Catalog = Empty<PermissionCatalogItemDto>();
        SelectedUserId = null;
        SelectedRoleId = null;
        Notify();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        InvalidateImportPreview();
        CatalogDigest = null;
        LastMutationFeedback = null;
        ClearEditorForLifetime();
        ++_generation;
        _session.SessionChanged -= SessionChanged;
    }
}
