// Compiled only as a temporary net48/x86 test probe, never into product or testhost.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class GuiProcessProbe
{
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    [DllImport("kernel32.dll")] private static extern int WerSetFlags(uint flags);

    [STAThread]
    private static int Main(string[] args)
    {
        SetErrorMode(0x0001 | 0x0002);
        WerSetFlags(0x0002);
        string mode = args[0];
        if (mode == "draining-child") { Thread.Sleep(150); return 0; }
        if (mode == "draining-parent")
        {
            using (var child = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "draining-child") { UseShellExecute = false, CreateNoWindow = true }))
                Console.WriteLine("PID=" + child.Id);
            return 0;
        }
        Console.WriteLine("PID=" + Process.GetCurrentProcess().Id);
        Console.WriteLine("BITNESS=" + (IntPtr.Size * 8));
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
}
