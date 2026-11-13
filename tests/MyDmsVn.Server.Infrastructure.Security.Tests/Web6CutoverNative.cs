using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Reader tasks are always joined by their owner, including on cancellation. A noncooperative
// native read keeps that delegate unjoined for controller/selector containment.
internal static class Web6CutoverNative
{
    internal static async Task<(int ExitCode, string Output, string Error)> RunChild(IWeb6CutoverChild child,
        Web6CutoverDeadline deadline, CancellationToken token, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));
        var launchAttempted = false;
        var identityCaptured = false;
        var releaseOwnedChild = true;
        var failures = new List<Exception>();
        try
        {
            try
            {
                deadline.ThrowIfExpired(token);
                // Set ownership before Start can produce an OS child. Start throwing is uncertain,
                // whereas an explicit false result proves that this launch produced no process.
                launchAttempted = true;
                releaseOwnedChild = false;
                if (!child.Start())
                {
                    launchAttempted = false;
                    releaseOwnedChild = true;
                    throw new IOException("Owned service-state child did not start.");
                }
                deadline.ThrowIfExpired(token);
                deadline.Observe(child.CaptureIdentity, token);
                identityCaptured = true;
                child.StartOutput(token);
                deadline.ThrowIfExpired(token);
                var work = deadline.Clamp(TimeSpan.FromTicks(deadline.Remaining.Ticks / 2));
                await WaitForExit(() => child.HasExited, work, token, delay).ConfigureAwait(false);
            }
            catch (Exception failure) { failures.Add(failure); }

            if (launchAttempted)
            {
                try
                {
                    if (!child.HasExited && identityCaptured)
                        TerminateVerified(child.VerifyIdentity, child.Terminate, deadline, token);
                }
                catch (Exception failure) { failures.Add(failure); }

                // This is containment ownership, not extra work allowance. On an expired/cancelled
                // action or uncertain identity, no unsafe kill or new phase is admitted. Keep the
                // exact child and delegate unjoined until natural exit (or selector termination).
                // The concrete child's task can only complete successfully on exact exit proof.
                await child.ProvenExit.ConfigureAwait(false);
                try { await JoinOutput(child.Output, child.Error, deadline, token).ConfigureAwait(false); }
                catch (Exception failure) { failures.Add(failure); }
                releaseOwnedChild = true;
            }
            if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException("Owned service-state child lifecycle failed.", failures);
            deadline.ThrowIfExpired(token);
            return (child.ExitCode, await child.Output, await child.Error);
        }
        finally
        {
            // No Process.Dispose shortcut can release a live or unjoined child.
            if (releaseOwnedChild) child.Dispose();
        }
    }

    internal static async Task<string> ReadOutput(Stream stream, Encoding encoding, int maximumLength,
        CancellationToken token)
    {
        var bytes = new byte[4096];
        var chars = new char[encoding.GetMaxCharCount(bytes.Length)];
        var decoder = encoding.GetDecoder();
        var text = new StringBuilder();
        var overflow = false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var length = decoder.GetChars(bytes, 0, read, chars, 0, read == 0);
            var remaining = maximumLength - text.Length;
            if (length > remaining) overflow = true;
            text.Append(chars, 0, Math.Min(length, remaining));
            if (read == 0) break;
        }
        if (overflow) throw new InvalidDataException("Owned process output exceeded its bound.");
        return text.ToString();
    }

    internal static async Task WaitForExit(Func<bool> hasExited, Web6CutoverDeadline deadline,
        CancellationToken token, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        deadline.ThrowIfExpired(token);
        while (!hasExited()) await deadline.Delay(TimeSpan.FromMilliseconds(20), token, delay).ConfigureAwait(false);
        deadline.ThrowIfExpired(token);
    }

    internal static void TerminateVerified(Action verify, Action kill, Web6CutoverDeadline deadline, CancellationToken token)
    {
        deadline.Observe(verify, token);
        deadline.Observe(kill, token);
    }

    internal static void VerifyHandle(Process process, int id, DateTime start, string executable)
    {
        process.Refresh();
        if (process.HasExited || process.Id != id || process.StartTime.ToUniversalTime() != start ||
            !string.Equals(Path.GetFullPath(process.MainModule!.FileName), Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Owned process handle identity changed.");
    }

    internal static async Task JoinOutput(Task output, Task error, Web6CutoverDeadline deadline, CancellationToken token,
        Action? cancel = null, bool cancelImmediately = false)
    {
        using var phase = CancellationTokenSource.CreateLinkedTokenSource(token);
        if (deadline.Remaining <= TimeSpan.Zero) phase.Cancel(); else phase.CancelAfter(deadline.Remaining);
        Exception? cancellationFailure = null;
        void Cancel()
        {
            try { cancel?.Invoke(); }
            catch (Exception failure) { Interlocked.CompareExchange(ref cancellationFailure, failure, null); }
        }
        using var cancellation = phase.Token.Register(Cancel);
        if (cancelImmediately) Cancel();
        Exception? outputFailure = null;
        try { await Task.WhenAll(output, error).ConfigureAwait(false); }
        catch (Exception failure) { outputFailure = failure; }
        // Dispose joins an in-flight callback. It is never detached if native cancellation blocks.
        cancellation.Dispose();
        if (cancellationFailure != null && outputFailure != null)
            throw new AggregateException("Owned output cancellation and drain failed.", cancellationFailure, outputFailure);
        if (cancellationFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cancellationFailure).Throw();
        if (outputFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outputFailure).Throw();
        deadline.ThrowIfExpired(phase.Token);
    }
}

internal interface IWeb6CutoverChild : IDisposable
{
    bool Start();
    void CaptureIdentity();
    void VerifyIdentity();
    bool HasExited { get; }
    void Terminate();
    // Completes successfully only when this exact owned process has exited.
    Task ProvenExit { get; }
    void StartOutput(CancellationToken token);
    Task<string> Output { get; }
    Task<string> Error { get; }
    int ExitCode { get; }
}

// Created before Start; event registration, process wrapper and reader task references belong
// to this one owner until RunChild has observed exit and joined output/cancellation callbacks.
internal sealed class Web6CutoverProcessChild : IWeb6CutoverChild
{
    private readonly Process _process;
    private readonly string _executable;
    private readonly int _maximumOutput;
    private readonly TaskCompletionSource<bool> _exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _id;
    private DateTime _started;

    internal Web6CutoverProcessChild(ProcessStartInfo start, int maximumOutput)
    {
        _executable = start.FileName;
        _maximumOutput = maximumOutput;
        _process = new Process { StartInfo = start, EnableRaisingEvents = true };
        _process.Exited += (_, _) => _exited.TrySetResult(true);
    }

    public bool Start() => _process.Start();
    public void CaptureIdentity()
    {
        _id = _process.Id;
        _started = _process.StartTime.ToUniversalTime();
        VerifyIdentity();
    }
    public void VerifyIdentity() => Web6CutoverNative.VerifyHandle(_process, _id, _started, _executable);
    public bool HasExited
    {
        get
        {
            var exited = _process.HasExited;
            if (exited) _exited.TrySetResult(true);
            return exited;
        }
    }
    public void Terminate() => _process.Kill();
    public Task ProvenExit => _exited.Task;
    public Task<string> Output { get; private set; } = Task.FromResult("");
    public Task<string> Error { get; private set; } = Task.FromResult("");
    public void StartOutput(CancellationToken token)
    {
        Output = Web6CutoverNative.ReadOutput(_process.StandardOutput.BaseStream,
            _process.StandardOutput.CurrentEncoding, _maximumOutput, token);
        Error = Web6CutoverNative.ReadOutput(_process.StandardError.BaseStream,
            _process.StandardError.CurrentEncoding, 4096, token);
    }
    public int ExitCode => _process.ExitCode;
    public void Dispose() => _process.Dispose();
}
