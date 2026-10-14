using System;
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
        using var job = new WindowsJobObject();
        var stopwatch = Stopwatch.StartNew();
        using var owned = SuspendedOwnedProcess.Start(command, job);
        var process = owned.Process;
        var rootPid = process.Id;
        var stdout = owned.StandardOutput.ReadToEndAsync();
        var stderr = owned.StandardError.ReadToEndAsync();
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
        stopwatch.Stop();
        return new CommandResult(
            timedOut ? -1 : process.ExitCode,
            timedOut,
            rootPid,
            output,
            error,
            timedOut ? new[] { rootPid } : Array.Empty<int>(),
            stopwatch.Elapsed);
    }

    public void Dispose() => _disposed = true;
}
