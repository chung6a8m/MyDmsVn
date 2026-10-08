using System.Windows.Forms;

using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    public sealed class FoundationShellForm : Form
    {
        private readonly FoundationViewModel _viewModel;

        public FoundationShellForm(FoundationViewModel viewModel)
        {
            _viewModel = viewModel;
            Text = "MyDmsVn";
        }

        public FoundationViewModel ViewModel => _viewModel;
    }
}
