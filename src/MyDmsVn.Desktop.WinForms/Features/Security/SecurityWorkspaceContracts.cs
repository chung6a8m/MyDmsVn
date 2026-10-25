using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

public interface ISecurityAdministrationControlFactory
{
    Control Create();
}

public interface IActivatableDesktopControl
{
    Task ActivateAsync(CancellationToken cancellationToken);
}