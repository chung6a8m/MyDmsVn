using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.Application.Security;

public enum SecurityDraftResolution { Save, Discard, Stay }

public sealed class SecurityMutationFeedback
{
    public SecurityMutationFeedback(ApiStatusCode status, string message, string? field = null)
    {
        Status = status;
        Message = message;
        Field = field;
    }

    public ApiStatusCode Status { get; }
    public string Message { get; }
    public string? Field { get; }

    public static SecurityMutationFeedback From(ApiError? error)
    {
        var field = error?.Details.Select(detail => detail.Field).FirstOrDefault(IsKnownField);
        var label = field switch
        {
            "name" => "tên",
            "displayName" => "tên hiển thị",
            "email" => "email",
            "password" => "mật khẩu",
            _ => null
        };
        var status = error?.Status ?? ApiStatusCode.InternalServerError;
        var message = status switch
        {
            ApiStatusCode.BadRequest => label == null ? "Dữ liệu không hợp lệ. Kiểm tra các trường đã nhập." :
                "Dữ liệu không hợp lệ. Kiểm tra " + label + ".",
            ApiStatusCode.Conflict => "Xung đột: dữ liệu đã thay đổi. Tải lại để đối chiếu trước khi nhập lại.",
            ApiStatusCode.Forbidden => "Không đủ quyền thực hiện thao tác.",
            ApiStatusCode.Unauthorized => "Phiên đăng nhập đã thay đổi. Đóng hộp thoại và đăng nhập lại.",
            ApiStatusCode.NotFound => "Đối tượng không còn khả dụng. Tải lại dữ liệu.",
            _ => "Không lưu được thay đổi. Tải lại dữ liệu trước khi thử tiếp."
        };
        return new SecurityMutationFeedback(status, message, field);
    }

    static bool IsKnownField(string? field) => field is "name" or "displayName" or "email" or "password";
}

public sealed partial class SecurityWorkspaceViewModel
{
    public SecurityRolePermissionDraft? RoleDraft { get; private set; }
    public string? DirectPermissionKey { get; private set; }
    public bool? DirectDecision { get; private set; }
    bool? _originalDirectDecision;
    int _directUserId;
    long _directRevision;
    Guid _editorId;
    long _directEditRevision;
    long _completedEditRevision;
    SecurityAdministrationTargetDto? _recoveryTarget;
    bool _recoveryNeedsRelatedRoles;
    int _recoveryMarkedGeneration;
    public Guid EditorIdentity => _editorId;
    public long EditorRevision => RoleDraft?.LocalEditRevision ?? (DirectPermissionKey != null ? _directEditRevision : _completedEditRevision);
    public bool IsBusy { get; private set; }
    public bool RequiresRecovery { get; private set; }
    public SecurityAdministrationTargetDto? RecoveryTarget => _recoveryTarget;
    public bool CanReviewRecovery => IsCurrent && !IsBusy && RequiresRecovery &&
        _generation > _recoveryMarkedGeneration && Catalog.IsCurrent(OwnerVersion) &&
        _recoveryTarget != null &&
        (_recoveryTarget != SecurityAdministrationTargetDto.User || Users.IsCurrent(OwnerVersion)) &&
        (_recoveryTarget != SecurityAdministrationTargetDto.Role || Roles.IsCurrent(OwnerVersion)) &&
        (!_recoveryNeedsRelatedRoles || Roles.IsCurrent(OwnerVersion)) &&
        (RoleDraft == null || (Roles.IsCurrent(OwnerVersion) && Roles.Items.Any(role => role.RoleId == RoleDraft.RoleId))) &&
        (DirectPermissionKey == null || (Users.IsCurrent(OwnerVersion) &&
            Users.Items.Any(user => user.UserId == _directUserId && !user.IsProtectedAccount)));
    public bool HasDirtyEditor => RoleDraft?.IsDirty == true || (DirectPermissionKey != null && DirectDecision != _originalDirectDecision);
    public bool CanNavigate => !IsBusy && !HasDirtyEditor && !RequiresRecovery;
    public SecuritySaveOutcome? LastSaveOutcome { get; private set; }
    public SecurityAdministrationTargetDto? LastSaveTarget { get; private set; }
    public string EditingStatus { get; private set; } = "";
    public SecurityMutationFeedback? LastMutationFeedback { get; private set; }

    public bool BeginRolePermissions()
    {
        if (!IsCurrent || IsBusy || RequiresRecovery || !Roles.IsCurrent(OwnerVersion) || !Catalog.IsCurrent(OwnerVersion) || SelectedRole == null) return false;
        if (RoleDraft?.RoleId == SelectedRoleId) return true;
        if (HasDirtyEditor) return false;
        InvalidateImportPreview();
        ClearEditorForLifetime();
        var role = SelectedRole;
        RoleDraft = new SecurityRolePermissionDraft(role.RoleId, role.AssignmentRevision, role.PermissionKeys,
            Catalog.Items.Where(k => k.Status == "Active").Select(k => k.Key), OwnerVersion);
        _editorId = RoleDraft.EditorId;
        Notify();
        return true;
    }

    public void SetRoleGranted(string key, bool granted)
    {
        if (!IsCurrent || IsBusy || RequiresRecovery || RoleDraft == null) return;
        RoleDraft.SetGranted(key, granted);
        InvalidateImportPreview();
        Notify();
    }

    public bool BeginDirectPermission(string key)
    {
        if (!IsCurrent || IsBusy || RequiresRecovery || SelectedUser == null || SelectedUser.IsProtectedAccount || !Users.IsCurrent(OwnerVersion) || !Catalog.IsCurrent(OwnerVersion) || !Catalog.Items.Any(k => k.Key == key && k.Status == "Active")) return false;
        if (DirectPermissionKey == key && _directUserId == SelectedUserId) return true;
        if (HasDirtyEditor) return false;
        InvalidateImportPreview();
        ClearEditorForLifetime();
        DirectPermissionKey = key;
        _directUserId = SelectedUser.UserId;
        _directRevision = SelectedUser.AssignmentRevision;
        DirectDecision = _originalDirectDecision = SelectedUser.DirectDecisions.FirstOrDefault(d => d.PermissionKey == key)?.Decision;
        _editorId = Guid.NewGuid();
        _directEditRevision = 0;
        Notify();
        return true;
    }

    public void SetDirectDecision(bool? decision)
    {
        if (!IsCurrent || IsBusy || RequiresRecovery || DirectPermissionKey == null) return;
        if (DirectDecision != decision) _directEditRevision++;
        InvalidateImportPreview();
        DirectDecision = decision;
        Notify();
    }

    public void DiscardEditor()
    {
        if (IsBusy || RequiresRecovery) return;
        ClearEditorForLifetime();
        Notify();
    }

    public Task<bool> SetMembershipAsync(int roleId, bool assigned, CancellationToken token)
    {
        var user = SelectedUser;
        var role = Roles.Items.FirstOrDefault(r => r.RoleId == roleId);
        if (user == null || user.IsProtectedAccount || role == null || !Users.IsCurrent(OwnerVersion) || !Roles.IsCurrent(OwnerVersion)) return Task.FromResult(false);
        return ExecuteMutationAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User,
            SecurityAdministrationOperationDto.SetMembership, user.UserId, roleId, isActive: assigned,
            expectedRevision: user.AssignmentRevision, expectedRelatedRevision: role.AssignmentRevision), token);
    }

    public async Task<bool> ExecuteMutationAsync(SecurityAdministrationMutationRequest request, CancellationToken token)
    {
        if (!IsCurrent || !CanNavigate) return false;
        if (request.Target == SecurityAdministrationTargetDto.User && Users.Items.Any(u => u.UserId == request.TargetId && u.IsProtectedAccount)) return false;
        IsBusy = true;
        LastMutationFeedback = null;
        var editor = _editorId;
        var submittedRevision = EditorRevision;
        EditingStatus = "Đang lưu";
        Notify();
        try
        {
            var response = await _administration.MutateAsync(request, token);
            if (!IsCurrent || _editorId != editor) return false;
            if (!response.IsSuccess)
            {
                LastMutationFeedback = SecurityMutationFeedback.From(response.Error);
                if (response.Error?.Status == ApiStatusCode.Forbidden)
                    RevokeTargetData(request.Target);
                else if (response.Error?.Status == ApiStatusCode.Conflict)
                    MarkRecovery(request.Target, request.Operation == SecurityAdministrationOperationDto.SetMembership);
                EditingStatus = response.Error?.Status == ApiStatusCode.Conflict ? "Xung đột" :
                    response.Error?.Status == ApiStatusCode.Forbidden ? "Không đủ quyền" : "Không lưu được thay đổi";
                Notify();
                return false;
            }
            await LoadCoreAsync(token, requiredSaveRefresh: true);
            if (!IsCurrent || _editorId != editor) return false;
            if (request.Operation == SecurityAdministrationOperationDto.Create)
            {
                if (request.Target == SecurityAdministrationTargetDto.User)
                {
                    var matches = Users.Items.Where(u => u.Username == request.Name).ToArray();
                    SelectedUserId = matches.Length == 1 ? matches[0].UserId : null;
                }
                else
                {
                    var matches = Roles.Items.Where(r => r.Name == request.Name).ToArray();
                    SelectedRoleId = matches.Length == 1 ? matches[0].RoleId : null;
                }
            }
            var refreshed = !token.IsCancellationRequested && (request.Target == SecurityAdministrationTargetDto.User ? Users.IsCurrent(OwnerVersion) : Roles.IsCurrent(OwnerVersion));
            if (request.Operation == SecurityAdministrationOperationDto.SetMembership) refreshed &= Roles.IsCurrent(OwnerVersion);
            RequiresRecovery = !refreshed;
            if (!refreshed) MarkRecovery(request.Target, request.Operation == SecurityAdministrationOperationDto.SetMembership);
            EditingStatus = refreshed ? "Đã lưu" : "Đã lưu, chưa tải lại được";
            if (!refreshed)
                LastMutationFeedback = new SecurityMutationFeedback(ApiStatusCode.Conflict,
                    "Đã lưu, chưa tải lại được. Tải lại để đối chiếu; không gửi lại thao tác.");
            return refreshed;
        }
        catch
        {
            if (IsCurrent && _editorId == editor)
            {
                MarkRecovery(request.Target, request.Operation == SecurityAdministrationOperationDto.SetMembership);
                EditingStatus = "Cần đối chiếu kết quả";
                LastMutationFeedback = new SecurityMutationFeedback(ApiStatusCode.InternalServerError,
                    "Chưa rõ kết quả. Tải lại để đối chiếu; không gửi lại thao tác.");
            }
            return false;
        }
        finally
        {
            if (IsCurrent && _editorId == editor) { IsBusy = false; Notify(); }
        }
    }

    public async Task<bool> ReloadForComparisonAsync(CancellationToken token)
    {
        if (!IsCurrent || IsBusy) return false;
        IsBusy = true;
        try { await LoadCoreAsync(token); return IsCurrent && !token.IsCancellationRequested; }
        finally { IsBusy = false; Notify(); }
    }

    public bool ReviewRecoveryAndRebase()
    {
        if (!CanReviewRecovery) return false;
        if (RoleDraft != null)
        {
            var role = Roles.Items.FirstOrDefault(r => r.RoleId == RoleDraft.RoleId);
            if (!Roles.IsCurrent(OwnerVersion) || role == null) return false;
            var remainingIntent = RoleDraft.Changes;
            var active = Catalog.Items.Where(k => k.Status == "Active").Select(k => k.Key).ToArray();
            var rebased = new SecurityRolePermissionDraft(role.RoleId, role.AssignmentRevision, role.PermissionKeys, active, OwnerVersion, RoleDraft.EditorId);
            foreach (var change in remainingIntent.Where(change => active.Contains(change.PermissionKey)))
                rebased.SetGranted(change.PermissionKey, change.Granted);
            RoleDraft = rebased;
        }
        else if (DirectPermissionKey != null)
        {
            var user = Users.Items.FirstOrDefault(u => u.UserId == _directUserId);
            if (!Users.IsCurrent(OwnerVersion) || user == null || user.IsProtectedAccount) return false;
            _originalDirectDecision = user.DirectDecisions.FirstOrDefault(d => d.PermissionKey == DirectPermissionKey)?.Decision;
            _directRevision = user.AssignmentRevision;
        }
        RequiresRecovery = false;
        _recoveryTarget = null;
        _recoveryNeedsRelatedRoles = false;
        _recoveryMarkedGeneration = 0;
        EditingStatus = "Đã đối chiếu dữ liệu hiện tại; kết quả lệnh trước vẫn theo bằng chứng đã ghi";
        Notify();
        return true;
    }

    public async Task<bool> ResolveDraftAsync(SecurityDraftResolution resolution, CancellationToken token)
    {
        if (IsBusy || RequiresRecovery) return false;
        if (!HasDirtyEditor) return true;
        if (resolution == SecurityDraftResolution.Stay) return false;
        if (resolution == SecurityDraftResolution.Discard) { DiscardEditor(); return true; }
        await SaveEditorAsync(token);
        return IsCurrent && CanNavigate;
    }

    public async Task SaveEditorAsync(CancellationToken token)
    {
        if (!IsCurrent || IsBusy || RequiresRecovery || !HasDirtyEditor) return;
        var editor = _editorId;
        IsBusy = true;
        EditingStatus = "Đang lưu";
        Notify();
        var submittedRevision = EditorRevision;
        var saveTarget = RoleDraft != null ? SecurityAdministrationTargetDto.Role : SecurityAdministrationTargetDto.User;
        SecuritySaveOutcome outcome;
        if (RoleDraft != null)
            outcome = await new SecurityAssignmentSaveCoordinator(_administration,
                submission => IsCurrent && _editorId == submission.EditorId).SaveRolePermissionsAsync(RoleDraft, token);
        else
        {
            try
            {
                var response = await _administration.MutateAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User,
                    SecurityAdministrationOperationDto.SetPermission, _directUserId, permissionKey: DirectPermissionKey,
                    decision: DirectDecision, expectedRevision: _directRevision), token);
                outcome = SingleOutcome(response, DirectPermissionKey!);
            }
            catch { outcome = new SecuritySaveOutcome(SecuritySaveCompletion.NoneConfirmed, SecuritySaveStopReason.OutcomeUnknown,
                SecurityRefreshOutcome.NotAttempted, Array.Empty<string>(), Array.Empty<string>(), new[] { DirectPermissionKey! }, Array.Empty<string>(), null); }
        }
        var refreshed = false;
        if (IsCurrent && _editorId == editor && outcome.StopReason == SecuritySaveStopReason.None)
        {
            try
            {
                await LoadCoreAsync(token, requiredSaveRefresh: true);
                refreshed = !token.IsCancellationRequested && Catalog.IsCurrent(OwnerVersion) &&
                    (saveTarget == SecurityAdministrationTargetDto.Role ? Roles.IsCurrent(OwnerVersion) : Users.IsCurrent(OwnerVersion));
            }
            catch { refreshed = false; }
            outcome = new SecuritySaveOutcome(outcome.Completion, outcome.StopReason,
                !IsCurrent ? SecurityRefreshOutcome.SessionChanged : refreshed ? SecurityRefreshOutcome.Complete : SecurityRefreshOutcome.Failed,
                outcome.ConfirmedKeys, outcome.RejectedKeys, outcome.UncertainKeys, outcome.UnattemptedKeys, outcome.LastConfirmedRevision);
        }
        await _dispatcher.InvokeAsync(() =>
        {
            if (!IsCurrent || _editorId != editor) return;
            IsBusy = false;
            LastSaveOutcome = outcome;
            LastSaveTarget = saveTarget;
            if (outcome.StopReason == SecuritySaveStopReason.Forbidden)
            {
                RevokeTargetData(saveTarget);
                EditingStatus = RecoveryStatus(outcome);
            }
            else if (outcome.StopReason == SecuritySaveStopReason.None)
            {
                // Retain intent until every required read is confirmed current.

                if (!IsCurrent || _editorId != editor) return;
                if (refreshed)
                {
                    ClearEditorForLifetime();
                    _editorId = editor;
                    _completedEditRevision = submittedRevision;
                    EditingStatus = "Đã lưu";
                }
                else { MarkRecovery(saveTarget); EditingStatus = "Đã lưu, chưa tải lại được"; }
            }
            else { MarkRecovery(saveTarget); EditingStatus = RecoveryStatus(outcome); }
            Notify();
        }, CancellationToken.None);
    }

    static string RecoveryStatus(SecuritySaveOutcome outcome)
    {
        var stop = outcome.StopReason switch
        {
            SecuritySaveStopReason.Forbidden => "Không đủ quyền",
            SecuritySaveStopReason.Conflict => "xung đột",
            SecuritySaveStopReason.OutcomeUnknown => "cần đối chiếu kết quả",
            SecuritySaveStopReason.Rejected => "Không lưu — yêu cầu bị từ chối; tải lại để đối chiếu",
            _ => "cần đối chiếu kết quả"
        };
        if (outcome.Completion != SecuritySaveCompletion.Partial)
            return outcome.StopReason == SecuritySaveStopReason.Conflict ? "Xung đột — tải lại để đối chiếu" : stop;
        var counts = new[]
        {
            outcome.ConfirmedKeys.Count > 0 ? "Đã lưu " + outcome.ConfirmedKeys.Count : null,
            outcome.RejectedKeys.Count > 0 ? "không lưu " + outcome.RejectedKeys.Count : null,
            outcome.UncertainKeys.Count > 0 ? "chưa rõ " + outcome.UncertainKeys.Count : null,
            outcome.UnattemptedKeys.Count > 0 ? "chưa gửi " + outcome.UnattemptedKeys.Count : null
        };
        return string.Join("; ", counts.Where(count => count != null)) + " — " + stop;
    }

    static SecuritySaveOutcome SingleOutcome(ApiResponse<SecurityAdministrationResultDto> response, string key)
    {
        var success = response.IsSuccess && response.Data != null;
        var reason = success ? SecuritySaveStopReason.None : response.Error?.Status == ApiStatusCode.Conflict ? SecuritySaveStopReason.Conflict : response.Error?.Status == ApiStatusCode.Forbidden ? SecuritySaveStopReason.Forbidden : SecuritySaveStopReason.Rejected;
        return new SecuritySaveOutcome(success ? SecuritySaveCompletion.Complete : SecuritySaveCompletion.NoneConfirmed, reason,
            SecurityRefreshOutcome.NotAttempted, success ? new[] { key } : Array.Empty<string>(), success ? Array.Empty<string>() : new[] { key }, Array.Empty<string>(), Array.Empty<string>(), response.Data?.AssignmentRevision);
    }

    void ClearEditorForLifetime()
    {
        RoleDraft = null;
        DirectPermissionKey = null;
        DirectDecision = _originalDirectDecision = null;
        _editorId = Guid.NewGuid();
        RequiresRecovery = false;
        _recoveryTarget = null;
        _recoveryNeedsRelatedRoles = false;
        _recoveryMarkedGeneration = 0;
        IsBusy = false;
    }

    void MarkRecovery(SecurityAdministrationTargetDto target, bool needsRelatedRoles = false)
    {
        RequiresRecovery = true;
        _recoveryTarget = target;
        _recoveryNeedsRelatedRoles = needsRelatedRoles;
        _recoveryMarkedGeneration = _generation;
    }

    void RevokeTargetData(SecurityAdministrationTargetDto target, bool requiredSaveRefresh = false)
    {
        InvalidateImportPreview();
        if ((target == SecurityAdministrationTargetDto.Role ? RoleDraft != null : DirectPermissionKey != null) ||
            _recoveryTarget == target)
        {
            if (requiredSaveRefresh || RequiresRecovery)
            {
                // The target is forbidden, but the committed/unknown outcome still needs
                // an authoritative refresh before ordinary close can be admitted.
                RoleDraft = null;
                DirectPermissionKey = null;
                DirectDecision = _originalDirectDecision = null;
            }
            else
                ClearEditorForLifetime();
        }
        if (target == SecurityAdministrationTargetDto.User)
        {
            Users = Empty<SecurityUserDto>(SecurityDatasetState.Forbidden);
            SelectedUserId = null;
        }
        else
        {
            Roles = Empty<SecurityRoleDto>(SecurityDatasetState.Forbidden);
            SelectedRoleId = null;
        }
        Notify();
    }

    void InvalidateCleanEditorForRead(SecurityAdministrationTargetDto target)
    {
        if (IsBusy || RequiresRecovery) return;
        if (target == SecurityAdministrationTargetDto.Role && RoleDraft != null)
        {
            var current = Roles.Items.FirstOrDefault(role => role.RoleId == RoleDraft.RoleId);
            if (!Roles.IsCurrent(OwnerVersion) || current?.AssignmentRevision != RoleDraft.ServerRevision)
                ClearEditorForLifetime();
        }
        else if (target == SecurityAdministrationTargetDto.User && DirectPermissionKey != null)
        {
            var current = Users.Items.FirstOrDefault(user => user.UserId == _directUserId);
            if (!Users.IsCurrent(OwnerVersion) || current?.AssignmentRevision != _directRevision)
                ClearEditorForLifetime();
        }
    }
}
