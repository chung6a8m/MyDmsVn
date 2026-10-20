// Compiled only as a temporary net48/x86 test probe, never into product or testhost.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class GuiProcessProbe
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    [DllImport("kernel32.dll")] private static extern int WerSetFlags(uint flags);

    [STAThread]
    private static int Main(string[] args)
    {
        SetErrorMode(0x0001 | 0x0002);
        WerSetFlags(0x0002);
        string mode = args[0];
        if (mode == "focus-target")
        {
            using (var target = new Form { Text = "GUI focus target", Width = 480, Height = 200 })
            using (var other = new Form { Text = "GUI owned distraction", Width = 300, Height = 160 })
            using (var focusReady = EventWaitHandle.OpenExisting(args[1]))
            using (var timer = new System.Windows.Forms.Timer { Interval = 25 })
            {
                var refusing = args.Length > 2 && args[2] == "refuse";
                var sink = new Button { Text = "Focus sink", Dock = DockStyle.Fill };
                var editor = new FocusTargetEditor { AccessibleName = "Focus target editor", Dock = DockStyle.Top,
                    FocusSink = refusing ? sink : null };
                var receipt = new Label { Text = "Awaiting F4", Dock = DockStyle.Bottom };
                editor.KeyDown += (sender, key) => { if (key.KeyCode == Keys.F4) receipt.Text = "F4 delivered"; };
                target.Controls.Add(editor);
                target.Controls.Add(receipt);
                target.Controls.Add(sink);
                target.Shown += (sender, ignored) =>
                {
                    if (refusing) { sink.Focus(); focusReady.Set(); }
                    else { other.Show(target); other.Activate(); timer.Start(); }
                };
                timer.Tick += (sender, ignored) =>
                {
                    if (GetForegroundWindow() == other.Handle) { timer.Stop(); focusReady.Set(); }
                };
                Application.Run(target);
            }
            return 0;
        }
        if (mode == "draining-child") { Thread.Sleep(150); return 0; }
        if (mode == "draining-parent")
        {
            using (var child = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "draining-child") { UseShellExecute = false, CreateNoWindow = true }))
                Console.WriteLine("PID=" + child.Id);
            return 0;
        }
        Console.WriteLine("PID=" + Process.GetCurrentProcess().Id);
        Console.WriteLine("BITNESS=" + (IntPtr.Size * 8));
        if (mode == "environment")
        {
            var excluded = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING")) &&
                string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING")) &&
                string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING"));
            Console.WriteLine("SQL_ENVIRONMENT_EXCLUDED=" + excluded);
            return excluded ? 0 : 1;
        }
        if (mode == "poison-admission")
        {
            var tests = Assembly.LoadFrom(args[1]);
            var cause = new InvalidOperationException("Synthetic uncertain cleanup.");
            tests.GetType("MyDmsVn.Desktop.Tests.GuiTestSafety", true).GetMethod("Poison").Invoke(null, new object[] { cause });
            try
            {
                var task = (System.Threading.Tasks.Task)tests.GetType("MyDmsVn.Desktop.Tests.GuiSqlEnvironment", true)
                    .GetMethod("CreateAsync").Invoke(null, new object[] { "unused-package" });
                task.GetAwaiter().GetResult();
            }
            catch (Exception error)
            {
                while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
                if (ReferenceEquals(cause, error.InnerException))
                {
                    Console.WriteLine("POISON_BLOCKS_SQL_ADMISSION=PASS");
                    return 0;
                }
                Console.WriteLine("ADMISSION_EXCEPTION=" + error.GetType().Name);
            }
            return 1;
        }
        if (mode == "tree" || mode == "orphan")
        {
            using (var child = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "branch") { UseShellExecute = false, CreateNoWindow = true }))
            {
                Console.WriteLine("PID=" + child.Id);
            }
        }
        if (mode == "branch")
        {
            using (var child = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "hang") { UseShellExecute = false, CreateNoWindow = true }))
            {
                Console.WriteLine("PID=" + child.Id);
            }
        }
        Console.WriteLine("READY");
        Console.Out.Flush();
        var ready = Environment.GetEnvironmentVariable("MYDMSVN_GUI_PROBE_READY");
        if (!string.IsNullOrEmpty(ready))
        {
            using (var signal = EventWaitHandle.OpenExisting(ready)) signal.Set();
        }
        if (mode == "output")
        {
            Console.WriteLine("STDOUT");
            Console.Error.WriteLine("STDERR");
            for (int i = 0; i < 5000; i++)
            {
                Console.WriteLine(new string('O', 100));
                Console.Error.WriteLine(new string('E', 100));
            }
            return 0;
        }
        if (mode == "exit" || mode == "orphan") return 0;
        if (mode == "crash") Environment.FailFast("Synthetic GUI fatal probe.");
        if (mode == "modal") MessageBox.Show("Synthetic owned modal probe", "MyDmsVn test probe");
        // Intentionally noncooperative STA: only the owning Windows job can terminate this.
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }

    private sealed class FocusTargetEditor : TextBox
    {
        public Control FocusSink;
        protected override void OnGotFocus(EventArgs args)
        {
            base.OnGotFocus(args);
            if (FocusSink != null) FocusSink.Focus();
        }
    }
}
