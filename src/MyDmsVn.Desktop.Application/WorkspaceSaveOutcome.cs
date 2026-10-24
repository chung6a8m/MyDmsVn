using System;

namespace MyDmsVn.Desktop.Application
{
    public enum WorkspaceSaveStatus
    {
        Saved,
        Rejected,
        CanceledBeforeMutation,
        Unknown,
    }

    public sealed class WorkspaceSaveOutcome
    {
        public WorkspaceSaveOutcome(WorkspaceSaveStatus status, Guid editorIdentity,
            long submittedRevision, bool refreshFailed = false)
        {
            Status = status;
            EditorIdentity = editorIdentity;
            SubmittedRevision = submittedRevision;
            RefreshFailed = refreshFailed;
        }

        public WorkspaceSaveStatus Status { get; }
        public Guid EditorIdentity { get; }
        public long SubmittedRevision { get; }
        public bool RefreshFailed { get; }
    }
}
