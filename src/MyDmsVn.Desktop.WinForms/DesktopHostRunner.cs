using System;
using System.Drawing;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    public static class DesktopHostRunner
    {
        public static int Run(
            Func<LoginForm> loginFactory,
            Func<FoundationShellForm> shellFactory,
            IDesktopSession session,
            bool smokeTest)
        {
            if (loginFactory == null)
            {
                throw new ArgumentNullException(nameof(loginFactory));
            }

            if (shellFactory == null)
            {
                throw new ArgumentNullException(nameof(shellFactory));
            }

            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (smokeTest)
            {
                var smokeLogin = loginFactory();
                Render(smokeLogin);
                return 0;
            }

            while (true)
            {
                using (var login = loginFactory())
                {
                    if (login.ShowDialog() != DialogResult.OK)
                    {
                        return 0;
                    }
                }

                if (!session.IsAuthenticated)
                {
                    return 2;
                }

                using (var shell = shellFactory())
                {
                    EventHandler sessionChanged = (_, _) =>
                    {
                        if (session.IsAuthenticated || shell.IsDisposed)
                        {
                            return;
                        }

                        if (shell.InvokeRequired && shell.IsHandleCreated)
                        {
                            shell.BeginInvoke(new Action(shell.Close));
                        }
                        else
                        {
                            shell.Close();
                        }
                    };
                    session.SessionChanged += sessionChanged;
                    try
                    {
                        System.Windows.Forms.Application.Run(shell);
                    }
                    finally
                    {
                        session.SessionChanged -= sessionChanged;
                    }
                }

                if (session.IsAuthenticated)
                {
                    return 0;
                }
            }
        }

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

            Render(shell);

            return 0;
        }

        private static void Render(Form form)
        {
            _ = form.Handle;
            form.PerformLayout();
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            }
        }
    }
}
