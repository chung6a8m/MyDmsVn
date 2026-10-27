using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    internal enum WorkspaceCloseChoice { Save, Discard, Cancel }
    internal enum WorkspaceCloseDecision { Admitted, KeepOpen }

    internal sealed class WorkspaceCloseState
    {
        public WorkspaceCloseState(Guid editorIdentity, long revision, bool isDirty,
            bool isMutationBusy, bool requiresOutcomeReview, bool canSave)
        {
            EditorIdentity = editorIdentity;
            Revision = revision;
            IsDirty = isDirty;
            IsMutationBusy = isMutationBusy;
            RequiresOutcomeReview = requiresOutcomeReview;
            CanSave = canSave;
        }

        public Guid EditorIdentity { get; }
        public long Revision { get; }
        public bool IsDirty { get; }
        public bool IsMutationBusy { get; }
        public bool RequiresOutcomeReview { get; }
        public bool CanSave { get; }
    }

    internal interface IWorkspaceCloseParticipant
    {
        WorkspaceCloseState CaptureCloseState();
        Task<WorkspaceSaveOutcome> SaveForCloseAsync(CancellationToken token);
    }

    // Only a confirmed write for the same editor/revision may admit a close. The owner
    // removes/disposes the page after this returns; no prompt or callback owns the page.
    internal sealed class WorkspaceCloseCoordinator
    {
        private readonly Func<WorkspaceCloseState, CancellationToken, Task<WorkspaceCloseChoice>> _prompt;
        private readonly HashSet<IWorkspaceCloseParticipant> _pending = new HashSet<IWorkspaceCloseParticipant>();
        private readonly object _sync = new object();

        public WorkspaceCloseCoordinator(Func<WorkspaceCloseState, CancellationToken, Task<WorkspaceCloseChoice>> prompt) =>
            _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));

        public async Task<WorkspaceCloseDecision> RequestCloseAsync(IWorkspaceCloseParticipant target, CancellationToken token)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            lock (_sync)
            {
                if (!_pending.Add(target)) return WorkspaceCloseDecision.KeepOpen;
            }

            try
            {
                if (token.IsCancellationRequested) return WorkspaceCloseDecision.KeepOpen;
                var before = target.CaptureCloseState();
                if (Blocked(before)) return WorkspaceCloseDecision.KeepOpen;
                if (!before.IsDirty) return WorkspaceCloseDecision.Admitted;

                WorkspaceCloseChoice choice;
                try { choice = await _prompt(before, token); }
                catch (OperationCanceledException) { return WorkspaceCloseDecision.KeepOpen; }
                catch (Exception) { return WorkspaceCloseDecision.KeepOpen; }
                if (token.IsCancellationRequested || !SameEdit(before, target.CaptureCloseState()))
                    return WorkspaceCloseDecision.KeepOpen;
                if (choice == WorkspaceCloseChoice.Cancel) return WorkspaceCloseDecision.KeepOpen;
                if (choice == WorkspaceCloseChoice.Discard) return WorkspaceCloseDecision.Admitted;
                if (!before.CanSave) return WorkspaceCloseDecision.KeepOpen;

                WorkspaceSaveOutcome outcome;
                try { outcome = await target.SaveForCloseAsync(token); }
                catch (OperationCanceledException) { return WorkspaceCloseDecision.KeepOpen; }
                catch (Exception) { return WorkspaceCloseDecision.KeepOpen; }
                if (token.IsCancellationRequested || outcome.Status != WorkspaceSaveStatus.Saved || outcome.RefreshFailed ||
                    outcome.EditorIdentity != before.EditorIdentity)
                    return WorkspaceCloseDecision.KeepOpen;
                var after = target.CaptureCloseState();
                return after.EditorIdentity == before.EditorIdentity && after.Revision == outcome.SubmittedRevision &&
                    !after.IsDirty && !Blocked(after)
                    ? WorkspaceCloseDecision.Admitted : WorkspaceCloseDecision.KeepOpen;
            }
            finally
            {
                lock (_sync) _pending.Remove(target);
            }
        }

        private static bool Blocked(WorkspaceCloseState state) => state.IsMutationBusy || state.RequiresOutcomeReview;
        private static bool SameEdit(WorkspaceCloseState before, WorkspaceCloseState after) =>
            before.EditorIdentity == after.EditorIdentity && before.Revision == after.Revision && !Blocked(after);
    }
}
