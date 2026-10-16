using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Processes;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Processes;

public sealed class OwnedProcessRunnerTests
{
    [Fact]
    public void Assignment_failure_terminates_the_suspended_process()
    {
        var processId = 0;

        Assert.Throws<InvalidOperationException>(() =>
            SuspendedOwnedProcess.Start(
                new CommandSpec(
                    "powershell.exe",
                    "-NoProfile -Command Start-Sleep -Seconds 30",
                    Environment.CurrentDirectory,
                    TimeSpan.FromSeconds(10)),
                (createdProcessId, _) =>
                {
                    processId = createdProcessId;
                    throw new InvalidOperationException("Injected assignment failure.");
                }));

        Assert.NotEqual(0, processId);
        Assert.Throws<ArgumentException>(() => System.Diagnostics.Process.GetProcessById(processId));
    }

    [Fact]
    public async Task Normal_command_drains_stdout_and_stderr()
    {
        using var runner = new OwnedProcessRunner();
        var result = await runner.RunAsync(
            new CommandSpec(
                "cmd.exe",
                "/d /s /c \"echo stdout-line & echo stderr-line 1>&2\"",
                Environment.CurrentDirectory,
                TimeSpan.FromSeconds(10)),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.True(result.Duration > TimeSpan.Zero);
        Assert.Contains("stdout-line", result.StandardOutput);
        Assert.Contains("stderr-line", result.StandardError);
    }

    [Fact]
    public async Task Gui_owned_runner_reports_a_fast_process_exit()
    {
        using var runner = new GuiOwnedProcessRunner();

        var result = await runner.RunAsync(
            new CommandSpec(
                "cmd.exe",
                "/d /s /c \"exit 0\"",
                Environment.CurrentDirectory,
                TimeSpan.FromSeconds(10)),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Empty(result.TerminatedProcessIds);
    }

    [Fact]
    public async Task Gui_owned_runner_times_out_and_terminates_child_holding_inherited_pipes_after_root_exit()
    {
        var childIdFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pid");
        var escapedPath = childIdFile.Replace("'", "''", StringComparison.Ordinal);
        var command = "$startInfo=[System.Diagnostics.ProcessStartInfo]::new('powershell.exe','-NoProfile -Command Start-Sleep -Seconds 30'); $startInfo.UseShellExecute=$false; $startInfo.CreateNoWindow=$true; $child=[System.Diagnostics.Process]::Start($startInfo); [IO.File]::WriteAllText('" + escapedPath + "',[string]$child.Id); exit 0";
        using var runner = new GuiOwnedProcessRunner();
        Task<CommandResult>? run = null;
        try
        {
            run = runner.RunAsync(
                new CommandSpec("powershell.exe", "-NoProfile -Command \"" + command + "\"",
                    Environment.CurrentDirectory, TimeSpan.FromSeconds(2)),
                CancellationToken.None);
            var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(8)));

            Assert.Same(run, completed);
            var result = await run;
            Assert.True(result.TimedOut);
            Assert.True(File.Exists(childIdFile));
            var childId = int.Parse(File.ReadAllText(childIdFile));
            Assert.Contains(childId, result.TerminatedProcessIds);
            Assert.Throws<ArgumentException>(() => Process.GetProcessById(childId));
        }
        finally
        {
            if (File.Exists(childIdFile))
            {
                var childId = int.Parse(File.ReadAllText(childIdFile));
                try
                {
                    using var child = Process.GetProcessById(childId);
                    if (!child.HasExited) child.Kill(entireProcessTree: true);
                }
                catch (ArgumentException)
                {
                }

                File.Delete(childIdFile);
            }

            if (run is not null)
            {
                try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (TimeoutException) { }
            }
        }
    }

    [Fact]
    public async Task Deadline_terminates_only_the_owned_process_tree()
    {
        using var unrelated = System.Diagnostics.Process.Start(new ProcessStartInfo(
            "powershell.exe",
            "-NoProfile -Command Start-Sleep -Seconds 20") { CreateNoWindow = true });
        Assert.NotNull(unrelated);
        try
        {
            using var runner = new OwnedProcessRunner();
            var result = await runner.RunAsync(
                new CommandSpec(
                    "powershell.exe",
                    "-NoProfile -Command \"$startInfo=[System.Diagnostics.ProcessStartInfo]::new('powershell.exe','-NoProfile -Command Start-Sleep -Seconds 20'); $startInfo.UseShellExecute=$false; $startInfo.CreateNoWindow=$true; $child=[System.Diagnostics.Process]::Start($startInfo); Write-Output $child.Id; Start-Sleep -Seconds 20\"",
                    Environment.CurrentDirectory,
                    TimeSpan.FromMilliseconds(750)),
                CancellationToken.None);

            Assert.True(result.TimedOut);
            Assert.Contains(result.RootProcessId, result.TerminatedProcessIds);
            var childProcessId = int.Parse(result.StandardOutput.Trim());
            Assert.Throws<ArgumentException>(() =>
                System.Diagnostics.Process.GetProcessById(childProcessId));
            Assert.False(unrelated!.HasExited);
        }
        finally
        {
            if (unrelated is { HasExited: false })
            {
                unrelated.Kill(entireProcessTree: true);
            }

            unrelated?.Dispose();
        }
    }
}
