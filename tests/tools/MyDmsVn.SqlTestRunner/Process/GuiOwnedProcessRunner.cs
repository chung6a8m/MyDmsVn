using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.SqlTestRunner.Processes;

public sealed class GuiOwnedProcessRunner : IProcessRunner, IDisposable
{
    private bool _disposed;

    public async Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var job = new WindowsJobObject();
        var stopwatch = Stopwatch.StartNew();
        using var owned = SuspendedOwnedProcess.Start(command, job);
        var process = owned.Process;
        var processId = process.Id;
        var stdout = owned.StandardOutput.ReadToEndAsync();
        var stderr = owned.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(command.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var unexpectedWindow = string.Empty;
        var timedOut = false;

        try
        {
            while (!process.HasExited)
            {
                unexpectedWindow = FindVisibleJobWindow(job);
                if (unexpectedWindow.Length != 0)
                {
                    job.Terminate(3);
                    break;
                }

                await Task.Delay(100, linked.Token).ConfigureAwait(false);
            }

            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            job.Terminate(1460);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            job.Terminate(1223);
            throw;
        }

        var exitCode = timedOut ? -1 : owned.ExitCode;

        var output = await stdout.ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        if (unexpectedWindow.Length != 0)
        {
            error += Environment.NewLine + "[MYDMSVN-UNEXPECTED-UI] " + unexpectedWindow;
        }

        var systemProcesses = Process.GetProcesses();
        int[] orphans;
        try
        {
            orphans = systemProcesses.Select(candidate => candidate.Id)
                .Where(id => id != processId && job.ContainsProcess(id))
                .ToArray();
        }
        finally
        {
            foreach (var candidate in systemProcesses) candidate.Dispose();
        }
        if (orphans.Length != 0)
        {
            job.Terminate(3);
        }

        stopwatch.Stop();
        var forced = timedOut || unexpectedWindow.Length != 0 || orphans.Length != 0;
        return new CommandResult(forced ? -1 : exitCode, timedOut, processId, output, error,
            forced ? new[] { processId }.Concat(orphans).ToArray() : Array.Empty<int>(), stopwatch.Elapsed);
    }

    public void Dispose() => _disposed = true;

    private static string FindVisibleJobWindow(WindowsJobObject job)
    {
        var found = string.Empty;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || !IsUnexpectedDialog(window)) return true;
            GetWindowThreadProcessId(window, out var processId);
            if (!job.ContainsProcess(unchecked((int)processId))) return true;
            var length = GetWindowTextLength(window);
            var title = new StringBuilder(Math.Max(1, length + 1));
            GetWindowText(window, title, title.Capacity);
            found = title.ToString();
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static bool IsUnexpectedDialog(IntPtr window)
    {
        const uint getOwner = 4;
        var className = new StringBuilder(256);
        GetClassName(window, className, className.Capacity);
        return string.Equals(className.ToString(), "#32770", StringComparison.Ordinal) ||
            GetWindow(window, getOwner) != IntPtr.Zero;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder className, int maximum);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
}
