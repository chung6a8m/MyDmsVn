using System;
using MyDmsVn.Desktop.Application.Security;
using Xunit;

namespace MyDmsVn.Desktop.Ui.Tests;

public sealed class SecurityAssignmentDraftTests
{

    [Fact]
    public void Filtering_preserves_hidden_changes_and_target_identity()
    {
        var original = new[] { "Z.Active", "Old.Key" };
        var draft = new SecurityRolePermissionDraft(42, 123, original, new[] { "Z.Active", "A.Active" }, 7);
        draft.SetGranted("A.Active", true);
        draft.SetGranted("Z.Active", false);
        var submitted = draft.FreezeSubmission();
        draft.SetGranted("A.Active", false);
        original[0] = "Tampered";
        Assert.Equal(42, submitted.RoleId);
        Assert.Equal(123, submitted.ServerRevision);
        Assert.Equal(7, submitted.SessionVersion);
        Assert.Equal(draft.EditorId, submitted.EditorId);
        Assert.Equal(new[] { "A.Active", "Z.Active" }, System.Linq.Enumerable.Select(submitted.Changes, c => c.PermissionKey));
        Assert.Contains("Old.Key", submitted.DesiredKeys);
        Assert.Equal(2, submitted.LocalEditRevision);
        Assert.Throws<InvalidOperationException>(() => draft.SetGranted("Old.Key", false));
    }

    [Fact]
    public void Reverting_to_baseline_produces_no_mutations()
    {
        var draft = new SecurityRolePermissionDraft(42, 123, new[] { "A" }, new[] { "A" }, 7);
        draft.SetGranted("A", false);
        draft.SetGranted("A", true);
        Assert.Empty(draft.FreezeSubmission().Changes);
    }
    [Theory]
    [InlineData(SecuritySaveStopReason.Conflict)]
    [InlineData(SecuritySaveStopReason.OutcomeUnknown)]
    public void Partial_completion_preserves_specific_stop_reason(SecuritySaveStopReason reason)
    {
        var result = new SecuritySaveOutcome(SecuritySaveCompletion.Partial, reason, SecurityRefreshOutcome.Failed, new[] { "A" }, reason == SecuritySaveStopReason.Conflict ? new[] { "B" } : Array.Empty<string>(), reason == SecuritySaveStopReason.OutcomeUnknown ? new[] { "B" } : Array.Empty<string>(), new[] { "C" }, 900);
        Assert.Equal(SecuritySaveCompletion.Partial, result.Completion);
        Assert.Equal(reason, result.StopReason);
        Assert.Equal(900, result.LastConfirmedRevision);
        Assert.Single(result.ConfirmedKeys);
        Assert.Single(result.UnattemptedKeys);
    }
}
