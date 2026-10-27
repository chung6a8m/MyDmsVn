using System;
using System.Windows.Forms;

namespace MyDmsVn.Desktop.UiLab;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme =
            MyDmsVn.Desktop.WinForms.BusinessCompactAppearance.CreateTheme(MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme.Mode);
        System.Windows.Forms.Application.Run(new UiLabForm());
    }
}
