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
        using var deadline = new CancellationTokenSource(command.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var stdout = owned.StandardOutput.ReadToEndAsync(linked.Token);
        var stderr = owned.StandardError.ReadToEndAsync(linked.Token);
        var unexpectedWindow = string.Empty;
        var timedOut = false;
        var terminated = new HashSet<int>();

        try
        {
            while (GetJobProcessIds(job).Length != 0)
            {
                unexpectedWindow = FindVisibleJobWindow(job);
                if (unexpectedWindow.Length != 0)
                {
                    terminated.UnionWith(GetJobProcessIds(job));
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
            terminated.UnionWith(GetJobProcessIds(job));
            job.Terminate(1460);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            terminated.UnionWith(GetJobProcessIds(job));
            job.Terminate(1223);
            throw;
        }

        if (!timedOut && !process.HasExited)
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }

        var exitCode = timedOut ? -1 : owned.ExitCode;

        var output = await Drain(stdout, timedOut).ConfigureAwait(false);
        var error = await Drain(stderr, timedOut).ConfigureAwait(false);
        if (unexpectedWindow.Length != 0)
        {
            error += Environment.NewLine + "[MYDMSVN-UNEXPECTED-UI] " + unexpectedWindow;
        }

        var orphans = GetJobProcessIds(job).Where(id => id != processId).ToArray();
        if (orphans.Length != 0)
        {
            terminated.UnionWith(orphans);
            job.Terminate(3);
        }

        stopwatch.Stop();
        var forced = timedOut || unexpectedWindow.Length != 0 || orphans.Length != 0 || terminated.Count != 0;
        return new CommandResult(forced ? -1 : exitCode, timedOut, processId, output, error,
            forced ? new[] { processId }.Concat(terminated).Concat(orphans).Distinct().ToArray() : Array.Empty<int>(), stopwatch.Elapsed);
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

    private static int[] GetJobProcessIds(WindowsJobObject job)
    {
        var processes = Process.GetProcesses();
        try
        {
            return processes.Where(candidate => job.ContainsProcess(candidate.Id))
                .Select(candidate => candidate.Id)
                .ToArray();
        }
        finally
        {
            foreach (var candidate in processes) candidate.Dispose();
        }
    }

    private static async Task<string> Drain(Task<string> output, bool timedOut)
    {
        try
        {
            return await output.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timedOut)
        {
            return string.Empty;
        }
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
