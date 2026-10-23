using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class WorkspaceCloseAdmissionTests
{
    [Fact]
    public void Rejected_or_canceled_save_never_admits_a_dirty_close()
    {
        StaTest.Run(_ =>
        {
            using var content = new FakeCloseContent { Dirty = true };
            var coordinator = new WorkspaceCloseCoordinator((_, _) => Task.FromResult(WorkspaceCloseChoice.Save));
            content.Save = () => new WorkspaceSaveOutcome(WorkspaceSaveStatus.Rejected, content.Identity, content.Revision);

            Assert.Equal(WorkspaceCloseDecision.KeepOpen, coordinator.RequestCloseAsync(content, CancellationToken.None).GetAwaiter().GetResult());
            Assert.True(content.Dirty);
            content.Save = () => new WorkspaceSaveOutcome(WorkspaceSaveStatus.CanceledBeforeMutation, content.Identity, content.Revision);
            Assert.Equal(WorkspaceCloseDecision.KeepOpen, coordinator.RequestCloseAsync(content, CancellationToken.None).GetAwaiter().GetResult());
            Assert.True(content.Dirty);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Confirmed_write_with_refresh_failure_requires_a_second_explicit_clean_close()
    {
        StaTest.Run(_ =>
        {
            using var content = new FakeCloseContent { Dirty = true };
            var prompts = 0;
            var coordinator = new WorkspaceCloseCoordinator((_, _) => { prompts++; return Task.FromResult(WorkspaceCloseChoice.Save); });
            content.Save = () =>
            {
                content.Dirty = false;
                return new WorkspaceSaveOutcome(WorkspaceSaveStatus.Saved, content.Identity, content.Revision, refreshFailed: true);
            };

            Assert.Equal(WorkspaceCloseDecision.KeepOpen, coordinator.RequestCloseAsync(content, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Equal(WorkspaceCloseDecision.Admitted, coordinator.RequestCloseAsync(content, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Equal(1, prompts);
            Assert.Equal(1, content.SaveCount);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void New_revision_during_prompt_and_repeated_close_request_cannot_discard_the_newer_edit()
    {
        StaTest.Run(_ =>
        {
            using var content = new FakeCloseContent { Dirty = true };
            var answer = new TaskCompletionSource<WorkspaceCloseChoice>();
            var prompts = 0;
            var coordinator = new WorkspaceCloseCoordinator((_, _) => { prompts++; return answer.Task; });

            var pending = coordinator.RequestCloseAsync(content, CancellationToken.None);
            Assert.Equal(WorkspaceCloseDecision.KeepOpen, coordinator.RequestCloseAsync(content, CancellationToken.None).GetAwaiter().GetResult());
            content.Revision++;
            answer.SetResult(WorkspaceCloseChoice.Discard);
            Assert.Equal(WorkspaceCloseDecision.KeepOpen, pending.GetAwaiter().GetResult());
            Assert.Equal(1, prompts);
            Assert.True(content.Dirty);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Mutation_or_uncertain_outcome_refuses_close_without_a_prompt()
    {
        StaTest.Run(_ =>
        {
            using var content = new FakeCloseContent { Dirty = true, MutationBusy = true };
            var prompts = 0;
            var coordinator = new WorkspaceCloseCoordinator((_, _) => { prompts++; return Task.FromResult(WorkspaceCloseChoice.Discard); });
            Assert.Equal(WorkspaceCloseDecision.KeepOpen, coordinator.RequestCloseAsync(content, CancellationToken.None).GetAwaiter().GetResult());
            content.MutationBusy = false;
            content.Recovery = true;
            Assert.Equal(WorkspaceCloseDecision.KeepOpen, coordinator.RequestCloseAsync(content, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Equal(0, prompts);
        }, TimeSpan.FromSeconds(10));
    }

    private sealed class FakeCloseContent : Panel, IWorkspaceCloseParticipant
    {
        public Guid Identity { get; } = Guid.NewGuid();
        public long Revision { get; set; } = 1;
        public bool Dirty { get; set; }
        public bool MutationBusy { get; set; }
        public bool Recovery { get; set; }
        public int SaveCount { get; private set; }
        public Func<WorkspaceSaveOutcome>? Save { get; set; }

        public WorkspaceCloseState CaptureCloseState() => new WorkspaceCloseState(Identity, Revision, Dirty, MutationBusy, Recovery, true);
        public Task<WorkspaceSaveOutcome> SaveForCloseAsync(CancellationToken token)
        {
            SaveCount++;
            return Task.FromResult(Save!());
        }
    }
}
