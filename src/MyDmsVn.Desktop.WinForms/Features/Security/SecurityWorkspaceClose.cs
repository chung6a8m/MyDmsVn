using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

public sealed partial class SecurityWorkspaceControl : IWorkspaceCloseParticipant
{
    internal System.Func<bool>? RecoveryConfirmationOverride { get; set; }
    internal System.Func<MyDmsVn.Desktop.Application.Security.SecurityDraftResolution>? NavigationResolutionOverride { get; set; }
    internal async Task<bool> AdmitNavigationAsync()
    {
        if (ViewModel.CanNavigate) return true;
        if (ViewModel.IsBusy || ViewModel.RequiresRecovery) return false;
        var resolution = NavigationResolutionOverride?.Invoke();
        if (resolution == null)
        {
            var answer = System.Windows.Forms.MessageBox.Show(this, "Lưu thay đổi trước khi tiếp tục?", "Thay đổi chưa lưu", System.Windows.Forms.MessageBoxButtons.YesNoCancel);
            resolution = answer == System.Windows.Forms.DialogResult.Yes ? MyDmsVn.Desktop.Application.Security.SecurityDraftResolution.Save :
                answer == System.Windows.Forms.DialogResult.No ? MyDmsVn.Desktop.Application.Security.SecurityDraftResolution.Discard : MyDmsVn.Desktop.Application.Security.SecurityDraftResolution.Stay;
        }
        return await ViewModel.ResolveDraftAsync(resolution.Value, CancellationToken.None);
    }
    internal WorkspaceCloseState CaptureCloseState() => new(ViewModel.EditorIdentity, ViewModel.EditorRevision,
        ViewModel.HasDirtyEditor, ViewModel.IsBusy, ViewModel.RequiresRecovery, ViewModel.IsCurrent);
    WorkspaceCloseState IWorkspaceCloseParticipant.CaptureCloseState() => CaptureCloseState();

    async Task<WorkspaceSaveOutcome> IWorkspaceCloseParticipant.SaveForCloseAsync(CancellationToken token)
    {
        var identity = ViewModel.EditorIdentity;
        var revision = ViewModel.EditorRevision;
        await ViewModel.SaveEditorAsync(token);
        return new WorkspaceSaveOutcome(ViewModel.CanNavigate ? WorkspaceSaveStatus.Saved : WorkspaceSaveStatus.Rejected,
            identity, revision, ViewModel.RequiresRecovery);
    }
}
