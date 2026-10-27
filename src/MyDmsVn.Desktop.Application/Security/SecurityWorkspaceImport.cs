using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.Application.Security;

public sealed partial class SecurityWorkspaceViewModel
{
    private SecurityPermissionImportCoordinator? _importCoordinator;

    public PermissionAssignmentImportPreviewDto? ImportPreview { get; private set; }
    public string ImportStatus { get; private set; } = string.Empty;

    public void InvalidateImportPreview()
    {
        _importCoordinator?.InvalidatePreview();
        ImportPreview = null;
        ImportStatus = string.Empty;
    }

    public async Task<bool> PreviewImportAsync(
        PermissionImportTargetTypeDto targetType, string json, CancellationToken token)
    {
        InvalidateImportPreview();
        var digest = CatalogDigest;
        if (!CanNavigate || !TryImportTarget(targetType, out var targetId) ||
            !Catalog.IsCurrent(OwnerVersion) || string.IsNullOrEmpty(digest))
            return false;

        IsBusy = true;
        ImportStatus = "Đang kiểm tra JSON";
        Notify();
        try
        {
            var response = await ImportCoordinator.PreviewAsync(targetType, targetId, json,
                _generation, digest!, token);
            if (!IsCurrent || token.IsCancellationRequested || !TryImportTarget(targetType, out var currentId) || currentId != targetId)
                return false;
            if (response.IsSuccess && response.Data != null)
            {
                ImportPreview = response.Data;
                ImportStatus = "Xem trước thay đổi cho " + response.Data.TargetName;
                return true;
            }

            if (response.Error?.Status == MyDmsVn.Contracts.ApiStatusCode.Forbidden)
                RevokeTargetData(targetType == PermissionImportTargetTypeDto.User ?
                    SecurityAdministrationTargetDto.User : SecurityAdministrationTargetDto.Role);
            ImportStatus = "Không thể xem trước: " + response.Error?.Code;
            return false;
        }
        catch (OperationCanceledException)
        {
            ImportStatus = "Đã hủy xem trước";
            return false;
        }
        catch
        {
            ImportStatus = "Không tải được bản xem trước";
            return false;
        }
        finally
        {
            if (IsCurrent) { IsBusy = false; Notify(); }
        }
    }

    public async Task<bool> ApplyImportPreviewAsync(
        PermissionImportTargetTypeDto targetType, string originalJson, bool confirmed, CancellationToken token)
    {
        if (!confirmed) { InvalidateImportPreview(); Notify(); return false; }
        var digest = CatalogDigest;
        if (!IsCurrent || IsBusy || ImportPreview == null || !TryImportTarget(targetType, out var targetId) ||
            targetId != ImportPreview.Import.TargetId || !Catalog.IsCurrent(OwnerVersion) ||
            string.IsNullOrEmpty(digest))
            return false;

        IsBusy = true;
        ImportStatus = "Đang nhập quyền";
        Notify();
        try
        {
            var response = await ImportCoordinator.ApplyPreviewAsync(targetType, targetId, originalJson,
                _generation, digest!, true, token);
            ImportPreview = null;
            if (!IsCurrent || response == null) return false;
            if (!response.IsSuccess)
            {
                if (response.Error?.Status == MyDmsVn.Contracts.ApiStatusCode.Forbidden)
                    RevokeTargetData(targetType == PermissionImportTargetTypeDto.User ?
                        SecurityAdministrationTargetDto.User : SecurityAdministrationTargetDto.Role);
                ImportStatus = "Không thể nhập quyền: " + response.Error?.Code;
                return false;
            }

            await LoadCoreAsync(token);
            var refreshed = !token.IsCancellationRequested && Catalog.IsCurrent(OwnerVersion) &&
                (targetType == PermissionImportTargetTypeDto.User ? Users.IsCurrent(OwnerVersion) : Roles.IsCurrent(OwnerVersion));
            if (!refreshed)
                MarkRecovery(targetType == PermissionImportTargetTypeDto.User ?
                    SecurityAdministrationTargetDto.User : SecurityAdministrationTargetDto.Role);
            ImportStatus = refreshed ? "Đã nhập quyền" : "Đã nhập quyền, chưa tải lại được";
            return refreshed;
        }
        catch (OperationCanceledException)
        {
            ImportPreview = null;
            MarkRecovery(targetType == PermissionImportTargetTypeDto.User ?
                SecurityAdministrationTargetDto.User : SecurityAdministrationTargetDto.Role);
            ImportStatus = "Cần đối chiếu kết quả nhập quyền";
            return false;
        }
        catch
        {
            ImportPreview = null;
            MarkRecovery(targetType == PermissionImportTargetTypeDto.User ?
                SecurityAdministrationTargetDto.User : SecurityAdministrationTargetDto.Role);
            ImportStatus = "Cần đối chiếu kết quả nhập quyền";
            return false;
        }
        finally
        {
            if (IsCurrent) { IsBusy = false; Notify(); }
        }
    }

    private SecurityPermissionImportCoordinator ImportCoordinator =>
        _importCoordinator ??= new SecurityPermissionImportCoordinator(_administration, () => IsCurrent);

    private bool TryImportTarget(PermissionImportTargetTypeDto targetType, out int targetId)
    {
        targetId = 0;
        if (!IsCurrent || HasDirtyEditor || RequiresRecovery) return false;
        if (targetType == PermissionImportTargetTypeDto.User && Users.IsCurrent(OwnerVersion) &&
            SelectedUser is { IsActive: true, IsProtectedAccount: false } user)
        {
            targetId = user.UserId;
            return true;
        }
        if (targetType == PermissionImportTargetTypeDto.Role && Roles.IsCurrent(OwnerVersion) &&
            SelectedRole is { IsActive: true } role)
        {
            targetId = role.RoleId;
            return true;
        }
        return false;
    }
}
