using System;
using System.Windows.Forms;
using MyDmsVn.Desktop.WinForms;

namespace MyDmsVn.Desktop.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.Run(new FoundationShellForm());
        }
    }
}
