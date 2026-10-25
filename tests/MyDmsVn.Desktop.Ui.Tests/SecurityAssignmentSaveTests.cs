using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SecurityAssignmentSaveTests
{
    [Fact]
    public async Task Serial_save_chains_returned_revisions_including_noop()
    {
        var client = new RecordingClient();
        client.Replies.Enqueue(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(17, false)));
        client.Replies.Enqueue(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(29, true)));
        var draft = Draft("A", "B");

        var outcome = await new SecurityAssignmentSaveCoordinator(client, submission => submission.SessionVersion == 4)
            .SaveRolePermissionsAsync(draft, CancellationToken.None);

        Assert.Equal(new[] { "A", "B" }, client.Requests.Select(request => request.PermissionKey));
        Assert.Equal(new long?[] { 10, 17 }, client.Requests.Select(request => request.ExpectedRevision));
        Assert.Equal(SecuritySaveCompletion.Complete, outcome.Completion);
        Assert.Equal(SecuritySaveStopReason.None, outcome.StopReason);
        Assert.Equal(29, outcome.LastConfirmedRevision);
    }

    [Fact]
    public async Task Conflict_after_success_preserves_committed_and_unattempted_keys()
    {
        var client = new RecordingClient();
        client.Replies.Enqueue(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(21, true)));
        client.Replies.Enqueue(ApiResponse<SecurityAdministrationResultDto>.Failure(
            new ApiError(ApiStatusCode.Conflict, "Security.AssignmentConflict", "Changed elsewhere.")));

        var outcome = await new SecurityAssignmentSaveCoordinator(client, _ => true)
            .SaveRolePermissionsAsync(Draft("A", "B", "C"), CancellationToken.None);

        Assert.Equal(new[] { "A", "B" }, client.Requests.Select(request => request.PermissionKey));
        Assert.Equal(new[] { "A" }, outcome.ConfirmedKeys);
        Assert.Equal(new[] { "B" }, outcome.RejectedKeys);
        Assert.Equal(new[] { "C" }, outcome.UnattemptedKeys);
        Assert.Equal(SecuritySaveCompletion.Partial, outcome.Completion);
        Assert.Equal(SecuritySaveStopReason.Conflict, outcome.StopReason);
        Assert.Equal(21, outcome.LastConfirmedRevision);
    }

    [Fact]
    public async Task Unknown_outcome_never_replays_automatically()
    {
        var client = new RecordingClient { ThrowOnCall = 2 };
        client.Replies.Enqueue(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(18, true)));
        var coordinator = new SecurityAssignmentSaveCoordinator(client, _ => true);

        var outcome = await coordinator.SaveRolePermissionsAsync(Draft("A", "B", "C"), CancellationToken.None);

        Assert.Equal(2, client.Requests.Count);
        Assert.Equal(new[] { "A" }, outcome.ConfirmedKeys);
        Assert.Equal(new[] { "B" }, outcome.UncertainKeys);
        Assert.Equal(new[] { "C" }, outcome.UnattemptedKeys);
        Assert.Equal(SecuritySaveStopReason.OutcomeUnknown, outcome.StopReason);
    }

    [Fact]
    public async Task Ownership_loss_stops_before_the_next_item()
    {
        var client = new RecordingClient();
        var current = true;
        client.AfterCall = () => current = false;
        client.Replies.Enqueue(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(31, true)));

        var outcome = await new SecurityAssignmentSaveCoordinator(client, _ => current)
            .SaveRolePermissionsAsync(Draft("A", "B"), CancellationToken.None);

        Assert.Single(client.Requests);
        Assert.Equal(new[] { "A" }, outcome.ConfirmedKeys);
        Assert.Equal(new[] { "B" }, outcome.UnattemptedKeys);
        Assert.Equal(SecuritySaveStopReason.CanceledBeforeDispatch, outcome.StopReason);
    }

    [Fact]
    public async Task Empty_diff_dispatches_nothing()
    {
        var client = new RecordingClient();
        var draft = new SecurityRolePermissionDraft(7, 10, new[] { "A" }, new[] { "A" }, 4);
        draft.SetGranted("A", false);
        draft.SetGranted("A", true);

        var outcome = await new SecurityAssignmentSaveCoordinator(client, _ => true)
            .SaveRolePermissionsAsync(draft, CancellationToken.None);

        Assert.Empty(client.Requests);
        Assert.Equal(SecuritySaveCompletion.NoChanges, outcome.Completion);
    }

    [Fact]
    public async Task Forbidden_after_success_retains_confirmed_item()
    {
        var client = new RecordingClient();
        client.Replies.Enqueue(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(22, true)));
        client.Replies.Enqueue(ApiResponse<SecurityAdministrationResultDto>.Failure(
            new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Access changed.")));

        var outcome = await new SecurityAssignmentSaveCoordinator(client, _ => true)
            .SaveRolePermissionsAsync(Draft("A", "B", "C"), CancellationToken.None);

        Assert.Equal(SecuritySaveStopReason.Forbidden, outcome.StopReason);
        Assert.Equal(SecuritySaveCompletion.Partial, outcome.Completion);
        Assert.Equal(new[] { "A" }, outcome.ConfirmedKeys);
        Assert.Equal(new[] { "C" }, outcome.UnattemptedKeys);
    }

    private static SecurityRolePermissionDraft Draft(params string[] keys)
    {
        var draft = new SecurityRolePermissionDraft(7, 10, Array.Empty<string>(), keys, 4);
        foreach (var key in keys) draft.SetGranted(key, true);
        return draft;
    }

    private sealed class RecordingClient : ISecurityAdministrationApiClient
    {
        public readonly List<SecurityAdministrationMutationRequest> Requests = new();
        public readonly Queue<ApiResponse<SecurityAdministrationResultDto>> Replies = new();
        public int ThrowOnCall { get; set; }
        public Action? AfterCall { get; set; }

        public Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(SecurityAdministrationMutationRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            AfterCall?.Invoke();
            if (ThrowOnCall == Requests.Count) throw new InvalidOperationException("Transport response lost.");
            return Task.FromResult(Replies.Dequeue());
        }

        public Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ListUsersAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewPermissionImportAsync(PermissionImportTargetTypeDto targetType, int targetId, string json, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<PermissionAssignmentImportResultDto>> ApplyPermissionImportAsync(PermissionImportTargetTypeDto targetType, int targetId, PermissionAssignmentImportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
