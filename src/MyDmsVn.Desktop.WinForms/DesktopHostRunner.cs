using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyDmsVn.Desktop.WinForms
{
    public static class DesktopHostRunner
    {
        public static int Run(FoundationShellForm shell, bool smokeTest)
        {
            if (shell == null)
            {
                throw new ArgumentNullException(nameof(shell));
            }

            if (!smokeTest)
            {
                System.Windows.Forms.Application.Run(shell);
                return 0;
            }

            _ = shell.Handle;
            shell.PerformLayout();
            using (var bitmap = new Bitmap(shell.Width, shell.Height))
            {
                shell.DrawToBitmap(bitmap, new Rectangle(Point.Empty, shell.Size));
            }

            return 0;
        }
    }
}
