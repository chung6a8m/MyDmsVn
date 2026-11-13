using System;
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
