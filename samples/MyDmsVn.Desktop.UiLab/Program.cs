using System;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.WinForms;

namespace MyDmsVn.Desktop.UiLab;

public static class UiLabEntry
{
    public static void RunSales() => Run(() => new SalesLabForm());
    public static void RunSecurity() => Run(() => new SecurityLabForm());
    private static void Run(Func<Form> create)
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(BootstrapThemeManager.CurrentTheme.Mode);
        System.Windows.Forms.Application.Run(create());
    }
}
