using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.Application.Security;

/// <summary>Submits a frozen role draft as independent, ordered single-key commands.</summary>
public sealed class SecurityAssignmentSaveCoordinator
{
    private readonly ISecurityAdministrationApiClient _administration;
    private readonly Func<SecurityRolePermissionSubmission, bool> _isCurrentOwner;

    public SecurityAssignmentSaveCoordinator(
        ISecurityAdministrationApiClient administration,
        Func<SecurityRolePermissionSubmission, bool> isCurrentOwner)
    {
        _administration = administration ?? throw new ArgumentNullException(nameof(administration));
        _isCurrentOwner = isCurrentOwner ?? throw new ArgumentNullException(nameof(isCurrentOwner));
    }

    public async Task<SecuritySaveOutcome> SaveRolePermissionsAsync(SecurityRolePermissionDraft draft, CancellationToken token)
    {
        if (draft == null) throw new ArgumentNullException(nameof(draft));
        var submitted = draft.FreezeSubmission();
        var changes = submitted.Changes;
        if (changes.Count == 0)
            return Result(SecuritySaveCompletion.NoChanges, SecuritySaveStopReason.None,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null);

        var confirmed = new List<string>();
        var rejected = new List<string>();
        var uncertain = new List<string>();
        long revision = submitted.ServerRevision;

        for (var index = 0; index < changes.Count; index++)
        {
            if (token.IsCancellationRequested || !_isCurrentOwner(submitted))
                return Stopped(SecuritySaveStopReason.CanceledBeforeDispatch, index);

            var change = changes[index];
            var request = new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.Role,
                SecurityAdministrationOperationDto.SetPermission,
                targetId: submitted.RoleId,
                permissionKey: change.PermissionKey,
                decision: change.Granted,
                expectedRevision: revision);

            ApiResponse<SecurityAdministrationResultDto> response;
            try
            {
                response = await _administration.MutateAsync(request, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                uncertain.Add(change.PermissionKey);
                return Stopped(SecuritySaveStopReason.OutcomeUnknown, index + 1);
            }
            catch (Exception)
            {
                uncertain.Add(change.PermissionKey);
                return Stopped(SecuritySaveStopReason.OutcomeUnknown, index + 1);
            }

            if (response == null)
            {
                uncertain.Add(change.PermissionKey);
                return Stopped(SecuritySaveStopReason.OutcomeUnknown, index + 1);
            }

            if (!response.IsSuccess || response.Data == null)
            {
                rejected.Add(change.PermissionKey);
                var stop = response.Error?.Status switch
                {
                    ApiStatusCode.Conflict => SecuritySaveStopReason.Conflict,
                    ApiStatusCode.Forbidden => SecuritySaveStopReason.Forbidden,
                    _ => SecuritySaveStopReason.Rejected,
                };
                return Stopped(stop, index + 1);
            }

            // A successful no-op still returns the only authoritative next revision.
            revision = response.Data.AssignmentRevision;
            confirmed.Add(change.PermissionKey);
        }

        return Result(SecuritySaveCompletion.Complete, SecuritySaveStopReason.None,
            confirmed, rejected, uncertain, Array.Empty<string>(), revision);

        SecuritySaveOutcome Stopped(SecuritySaveStopReason stop, int nextIndex)
        {
            return Result(confirmed.Count == 0 ? SecuritySaveCompletion.NoneConfirmed : SecuritySaveCompletion.Partial,
                stop, confirmed, rejected, uncertain,
                changes.Skip(nextIndex).Select(item => item.PermissionKey),
                confirmed.Count == 0 ? null : revision);
        }
    }

    private static SecuritySaveOutcome Result(
        SecuritySaveCompletion completion, SecuritySaveStopReason stop,
        IEnumerable<string> confirmed, IEnumerable<string> rejected,
        IEnumerable<string> uncertain, IEnumerable<string> unattempted,
        long? lastRevision)
    {
        return new SecuritySaveOutcome(completion, stop, SecurityRefreshOutcome.NotAttempted,
            confirmed, rejected, uncertain, unattempted, lastRevision);
    }
}
