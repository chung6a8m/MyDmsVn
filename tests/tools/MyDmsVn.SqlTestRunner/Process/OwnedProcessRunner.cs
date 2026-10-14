using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.SqlTestRunner.Processes;

public interface IProcessRunner
{
    Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken);
}

public sealed class OwnedProcessRunner : IProcessRunner, IDisposable
{
    private bool _disposed;

    public async Task<CommandResult> RunAsync(
        CommandSpec command,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var startInfo = new ProcessStartInfo(command.FileName, command.Arguments)
        {
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var pair in command.Environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        using var job = new WindowsJobObject();
        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("The owned process did not start.");
        }

        var rootPid = process.Id;
        job.Assign(process.Handle);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(command.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);

        var timedOut = false;
        try
        {
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

        var output = await stdout.ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        return new CommandResult(
            timedOut ? -1 : process.ExitCode,
            timedOut,
            rootPid,
            output,
            error,
            timedOut ? new[] { rootPid } : Array.Empty<int>());
    }

    public void Dispose() => _disposed = true;
}
