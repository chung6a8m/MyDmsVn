using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web5HttpServiceStateTests
{
    [Fact]
    public async Task Output_drain_cancels_at_deadline_but_joins_the_exact_noncooperative_read()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new PendingOutputStream();
        var output = Web6CutoverNative.ReadOutput(stream, System.Text.Encoding.UTF8, 100, cancellation.Token);
        var drain = Web6CutoverNative.JoinOutput(output, Task.CompletedTask,
            new Web6CutoverDeadline(TimeSpan.FromMilliseconds(20)), default, cancellation.Cancel);
        try
        {
            // Bounded fake wait observes cancellation; the read itself deliberately ignores it.
            Assert.True(cancellation.Token.WaitHandle.WaitOne(2000));
            Assert.False(drain.IsCompleted);
            Assert.False(output.IsCompleted);
        }
        finally { stream.Finish.TrySetResult(0); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => drain);
        Assert.True(output.IsCompleted);
    }

    [Fact]
    public async Task Output_reader_preserves_text_and_refuses_overflow_after_joined_EOF()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("dấu"));
        Assert.Equal("dấu", await Web6CutoverNative.ReadOutput(stream, System.Text.Encoding.UTF8, 3, default));
        stream.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => Web6CutoverNative.ReadOutput(stream, System.Text.Encoding.UTF8, 2, default));
        Assert.Equal(stream.Length, stream.Position);
    }

    private sealed class PendingOutputStream : MemoryStream
    {
        internal TaskCompletionSource<int> Finish { get; } = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => Finish.Task;
    }

    [Fact]
    public async Task Output_cancellation_callback_failure_cannot_abandon_owned_readers()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var output = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new IOException("fake cancellation callback");
        var drain = Web6CutoverNative.JoinOutput(output.Task, Task.CompletedTask,
            new Web6CutoverDeadline(TimeSpan.FromSeconds(1)), cancellation.Token, () => throw failure);
        try { Assert.False(drain.IsCompleted); }
        finally { output.TrySetResult(true); }
        var observed = await Assert.ThrowsAsync<IOException>(() => drain);
        Assert.Same(failure, observed);
    }

    [Theory]
    [InlineData("start-expired")]
    [InlineData("start-cancelled")]
    [InlineData("start-threw")]
    [InlineData("identity-failed")]
    [InlineData("wait-cancelled")]
    [InlineData("wait-cancelled-readers")]
    public async Task Strict_child_failures_retain_ownership_even_with_completed_readers_until_proven_exit(string fault)
    {
        using var cancellation = new CancellationTokenSource();
        var now = TimeSpan.Zero;
        var child = new FakeCutoverChild();
        child.AfterStart = () =>
        {
            if (fault == "start-expired") now = TimeSpan.FromSeconds(61);
            if (fault == "start-cancelled") cancellation.Cancel();
            if (fault == "start-threw") throw new IOException("fake post-launch Start failure");
        };
        child.IdentityFailure = fault == "identity-failed";
        if (fault == "wait-cancelled-readers")
        {
            child.Output = Task.FromCanceled<string>(new CancellationToken(true));
            child.Error = Task.FromCanceled<string>(new CancellationToken(true));
        }
        var run = Web6CutoverNative.RunChild(child,
            new Web6CutoverDeadline(TimeSpan.FromSeconds(60), () => now), cancellation.Token,
            (_, token) => { cancellation.Cancel(); return Task.CompletedTask; });
        try
        {
            Assert.Equal(1, child.Starts);
            Assert.True(child.Output.IsCompleted && child.Error.IsCompleted);
            Assert.False(run.IsCompleted);
            Assert.False(child.Disposed);
            Assert.Equal(0, child.Terminations);
            Assert.True(child.ExitAwaited);
        }
        finally { child.ProveExit(); }
        await Assert.ThrowsAnyAsync<Exception>(() => run);
        Assert.True(child.Disposed);
        Assert.True(child.HasExited);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Strict_child_verified_termination_still_owns_process_until_each_reader_joins(bool holdStdout)
    {
        var now = TimeSpan.Zero;
        var child = new FakeCutoverChild { ExitOnTerminate = true };
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (holdStdout) child.Output = pending.Task;
        else child.Error = pending.Task;
        var run = Web6CutoverNative.RunChild(child,
            new Web6CutoverDeadline(TimeSpan.FromSeconds(60), () => now), default,
            (_, token) => { now += TimeSpan.FromSeconds(31); return Task.CompletedTask; });
        try
        {
            Assert.Equal(1, child.Terminations);
            Assert.Equal(1, child.Verifications);
            Assert.True(child.HasExited);
            Assert.True(child.ExitAwaited);
            Assert.False(run.IsCompleted);
            Assert.False(child.Disposed);
        }
        finally { child.ProveExit(); pending.TrySetResult("done"); }
        await Assert.ThrowsAsync<TimeoutException>(() => run);
        Assert.True(child.Disposed);
    }

    [Fact]
    public async Task Strict_child_changed_identity_prevents_kill_and_keeps_the_child_for_containment()
    {
        var now = TimeSpan.Zero;
        var child = new FakeCutoverChild { VerifyFailure = true };
        var run = Web6CutoverNative.RunChild(child,
            new Web6CutoverDeadline(TimeSpan.FromSeconds(60), () => now), default,
            (_, token) => { now += TimeSpan.FromSeconds(31); return Task.CompletedTask; });
        try
        {
            Assert.Equal(1, child.Verifications);
            Assert.Equal(0, child.Terminations);
            Assert.False(child.Disposed);
            Assert.False(run.IsCompleted);
            Assert.True(child.ExitAwaited);
        }
        finally { child.ProveExit(); }
        await Assert.ThrowsAnyAsync<Exception>(() => run);
        Assert.True(child.Disposed);
    }

    [Fact]
    public async Task Strict_child_partial_output_start_failure_joins_the_started_reader_after_exit()
    {
        var child = new FakeCutoverChild { OutputStartFailure = true, ExitOnTerminate = true };
        var output = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        child.Output = output.Task;
        var run = Web6CutoverNative.RunChild(child, new Web6CutoverDeadline(TimeSpan.FromSeconds(60)), default);
        try
        {
            Assert.True(child.HasExited);
            Assert.Equal(1, child.Terminations);
            Assert.False(child.Disposed);
            Assert.False(run.IsCompleted);
        }
        finally { child.ProveExit(); output.TrySetResult("partial"); }
        await Assert.ThrowsAsync<IOException>(() => run);
        Assert.True(child.Disposed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task Strict_child_natural_exit_before_capture_preserves_both_outputs_and_exit_code(int exitCode)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            $"/d /c \"echo stdout&echo stderr>&2&exit /b {exitCode}\"")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        var child = new ExitedBeforeCaptureChild(new Web6CutoverProcessChild(start, 100));
        var result = await Web6CutoverNative.RunChild(child, new Web6CutoverDeadline(TimeSpan.FromSeconds(30)), default);
        Assert.Equal((exitCode, "stdout\r\n", "stderr\r\n"), result);
        if (exitCode == 0)
            Assert.Equal(result.Output, Web5HttpServiceState.ValidateCommandResult(result.ExitCode, result.Output, result.Error));
        else
            Assert.Throws<InvalidDataException>(() => Web5HttpServiceState.ValidateCommandResult(result.ExitCode, result.Output, result.Error));
    }

    [Fact]
    public void Strict_live_child_requires_the_exact_launch_handle_and_executable_before_termination()
    {
        var executable = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var start = new ProcessStartInfo(executable, "/d /c \"set /p owned=&exit /b 0\"")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
        };
        using var child = new Process { StartInfo = start };
        Assert.True(child.Start());
        try
        {
            var handle = child.SafeHandle;
            var pid = child.Id;
            var started = child.StartTime.ToUniversalTime();
            using var other = Process.GetCurrentProcess();
            var deadline = new Web6CutoverDeadline(TimeSpan.FromSeconds(15));
            void TerminateWith(SafeProcessHandle expected, string path) =>
                Web6CutoverNative.TerminateVerified(
                    () => Web6CutoverNative.VerifyRunningHandle(child, expected, pid, started, path),
                    child.Kill, deadline, default);

            Assert.Throws<InvalidDataException>(() => TerminateWith(other.SafeHandle, executable));
            Assert.Throws<InvalidDataException>(() => TerminateWith(handle, Path.Combine(Environment.SystemDirectory, "different.exe")));
            Assert.False(child.HasExited);
            TerminateWith(handle, executable);
            Assert.True(child.WaitForExit(5000));
            Assert.NotEqual(0, child.ExitCode);
        }
        finally
        {
            child.StandardInput.Close();
            Assert.True(child.WaitForExit(5000));
        }
    }

    private sealed class ExitedBeforeCaptureChild : IWeb6CutoverChild
    {
        private readonly Web6CutoverProcessChild _child;

        internal ExitedBeforeCaptureChild(Web6CutoverProcessChild child) => _child = child;

        public bool Start() => _child.Start();
        public void CaptureIdentity()
        {
            // Gate on this exact process's exit signal, not on how quickly cmd.exe usually exits.
            if (!_child.ProvenExit.Wait(TimeSpan.FromSeconds(5)))
            {
                if (!_child.HasExited)
                {
                    _child.CaptureIdentity();
                    _child.VerifyRunningIdentity();
                    _child.Terminate();
                }
                throw new TimeoutException("Disposable command did not signal its expected natural exit.");
            }
            _child.CaptureIdentity();
        }
        public void VerifyRunningIdentity() => _child.VerifyRunningIdentity();
        public bool HasExited => _child.HasExited;
        public void Terminate() => throw new InvalidOperationException("The disposable command already exited naturally.");
        public Task ProvenExit => _child.ProvenExit;
        public void StartOutput(CancellationToken token) => _child.StartOutput(token);
        public Task<string> Output => _child.Output;
        public Task<string> Error => _child.Error;
        public int ExitCode => _child.ExitCode;
        public void Dispose() => _child.Dispose();
    }

    private sealed class FakeCutoverChild : IWeb6CutoverChild
    {
        private readonly TaskCompletionSource<bool> _exit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Action? AfterStart;
        internal bool IdentityFailure, VerifyFailure, ExitOnTerminate, OutputStartFailure;
        internal int Starts, Verifications, Terminations;
        internal bool Disposed, ExitAwaited;
        public bool Start() { Starts++; AfterStart?.Invoke(); return true; }
        public void CaptureIdentity() { if (IdentityFailure) throw new IOException("fake identity capture"); }
        public void VerifyRunningIdentity() { Verifications++; if (VerifyFailure) throw new IOException("fake identity mismatch"); }
        public bool HasExited { get; private set; }
        public void Terminate() { Terminations++; if (ExitOnTerminate) ProveExit(); }
        public Task ProvenExit { get { ExitAwaited = true; return _exit.Task; } }
        public void StartOutput(CancellationToken token) { if (OutputStartFailure) throw new IOException("fake partial reader start"); }
        public Task<string> Output { get; set; } = Task.FromResult("");
        public Task<string> Error { get; set; } = Task.FromResult("");
        public int ExitCode => 0;
        public void Dispose() => Disposed = true;
        internal void ProveExit() { HasExited = true; _exit.TrySetResult(true); }
    }

    private const string Empty = @"
Snapshot of HTTP service state (Request Queue View):
-----------------------------------------------------
";
    // Mutation vectors use LF regardless of the checkout's required CRLF encoding.
    private static string Owned => OwnedRaw.Replace("\r\n", "\n");
    private const string OwnedRaw = @"
Snapshot of HTTP service state (Request Queue View):
-----------------------------------------------------

Request queue name: IISExpressAppPool{11111111-1111-1111-1111-111111111111}
    Version: 2.0
    State: Active
    Number of active processes attached: 1
    Controller process:
        ID: 18404, image: C:\Program Files\IIS Express\iisexpress.exe
    Processes:
        ID: 18404, image: C:\Program Files\IIS Express\iisexpress.exe
    URL groups:
    URL group ID: FB00000B20000003
        State: Active
        Request queue name: IISExpressAppPool{11111111-1111-1111-1111-111111111111}
        Properties:
            Number of registered URLs: 2
            Registered URLs:
                HTTPS://LOCALHOST:44321/
                HTTP://LOCALHOST:18081/
        Server session ID: FD00000B1000019F
";

    [Fact]
    public void Exact_active_queue_with_one_owned_controller_and_worker_is_admitted()
    {
        var queues = Web5HttpServiceState.Parse(Owned);
        var queue = Web5HttpServiceState.RequireOwnedQueue(queues, new Uri("https://localhost:44321/"), 18404);
        Assert.Equal("IISExpressAppPool{11111111-1111-1111-1111-111111111111}", queue.Name);
        Assert.Equal(18404, queue.ControllerProcessId);
        Assert.Equal(new[] { 18404 }, queue.ProcessIds);
        Assert.Equal(2, queue.RegisteredUrls.Count);
    }

    [Theory]
    [InlineData("ID: 18404, image:", "ID: 4, image:")]
    [InlineData("Number of active processes attached: 1", "Number of active processes attached: 0")]
    [InlineData("HTTPS://LOCALHOST:44321/", "HTTPS://LOCALHOST:44322/")]
    [InlineData("State: Active", "State: Inactive")]
    public void Ambiguous_or_wrong_queue_is_rejected(string oldValue, string newValue)
    {
        Assert.Throws<InvalidDataException>(() =>
        {
            var queues = Web5HttpServiceState.Parse(Owned.Replace(oldValue, newValue));
            Web5HttpServiceState.RequireOwnedQueue(queues, new Uri("https://localhost:44321/"), 18404);
        });
    }

    [Fact]
    public void Multiple_attached_workers_are_rejected_even_when_one_is_owned()
    {
        var text = Owned
            .Replace("Number of active processes attached: 1", "Number of active processes attached: 2")
            .Replace("        ID: 18404, image: C:\\Program Files\\IIS Express\\iisexpress.exe\n    URL groups:",
                "        ID: 18404, image: C:\\Program Files\\IIS Express\\iisexpress.exe\n        ID: 19000, image: C:\\Program Files\\IIS Express\\iisexpress.exe\n    URL groups:");
        var queues = Web5HttpServiceState.Parse(text);
        Assert.Throws<InvalidDataException>(() => Web5HttpServiceState.RequireOwnedQueue(queues, new Uri("https://localhost:44321/"), 18404));
    }

    [Fact]
    public void Controller_mismatch_is_rejected_when_attached_worker_matches()
    {
        var text = Owned.Replace("Controller process:\n        ID: 18404", "Controller process:\n        ID: 19000");
        var queues = Web5HttpServiceState.Parse(text);
        Assert.Throws<InvalidDataException>(() => Web5HttpServiceState.RequireOwnedQueue(queues, new Uri("https://localhost:44321/"), 18404));
    }

    [Fact]
    public void Duplicate_matching_queue_is_rejected()
    {
        var queues = Web5HttpServiceState.Parse(Owned + Owned);
        Assert.Throws<InvalidDataException>(() => Web5HttpServiceState.RequireOwnedQueue(queues, new Uri("https://localhost:44321/"), 18404));
    }

    [Fact]
    public void Target_URL_in_inactive_URL_group_is_rejected()
    {
        var queues = Web5HttpServiceState.Parse(Owned.Replace("        State: Active", "        State: Inactive"));
        Assert.Throws<InvalidDataException>(() =>
            Web5HttpServiceState.RequireOwnedQueue(queues, new Uri("https://localhost:44321/"), 18404));
    }

    [Fact]
    public void Duplicate_matching_URL_groups_in_one_queue_are_rejected()
    {
        const string extraGroup = @"
    URL group ID: FB00000B20000004
        State: Active
        Request queue name: IISExpressAppPool{11111111-1111-1111-1111-111111111111}
        Properties:
            Number of registered URLs: 1
            Registered URLs:
                HTTPS://LOCALHOST:44321/
        Server session ID: FD00000B100001A0
";
        var queues = Web5HttpServiceState.Parse(Owned + extraGroup);
        Assert.Throws<InvalidDataException>(() =>
            Web5HttpServiceState.RequireOwnedQueue(queues, new Uri("https://localhost:44321/"), 18404));
    }

    [Fact]
    public void Foreign_HTTP_sys_wildcard_prefix_does_not_block_exact_owned_queue()
    {
        var foreign = Owned
            .Replace("IISExpressAppPool{11111111-1111-1111-1111-111111111111}", "DefaultAppPool")
            .Replace("HTTPS://LOCALHOST:44321/", "HTTP://*:80/")
            .Replace("                HTTP://LOCALHOST:18081/\n", string.Empty)
            .Replace("Number of registered URLs: 2", "Number of registered URLs: 1");
        var queues = Web5HttpServiceState.Parse(foreign + Owned);

        var queue = Web5HttpServiceState.RequireOwnedQueue(queues, new Uri("https://localhost:44321/"), 18404);

        Assert.StartsWith("IISExpressAppPool", queue.Name);
    }

    [Theory]
    [InlineData("HTTPS://+:44321/")]
    [InlineData("HTTPS://LOCALHOST:44321/API/")]
    public void Foreign_active_registration_that_can_capture_owned_paths_is_rejected(string registeredUrl)
    {
        var foreign = Owned
            .Replace("IISExpressAppPool{11111111-1111-1111-1111-111111111111}", "DefaultAppPool")
            .Replace("ID: 18404", "ID: 19000")
            .Replace("HTTPS://LOCALHOST:44321/", registeredUrl)
            .Replace("                HTTP://LOCALHOST:18081/\n", string.Empty)
            .Replace("Number of registered URLs: 2", "Number of registered URLs: 1");
        var queues = Web5HttpServiceState.Parse(foreign + Owned);

        Assert.Throws<InvalidDataException>(() =>
            Web5HttpServiceState.RequireOwnedQueue(queues, new Uri("https://localhost:44321/"), 18404));
    }

    [Fact]
    public void Empty_valid_request_queue_snapshot_is_absent_but_never_admitted()
    {
        var queues = Web5HttpServiceState.Parse(Empty);
        Assert.Empty(queues);
        Assert.Throws<InvalidDataException>(() =>
            Web5HttpServiceState.RequireOwnedQueue(queues, new Uri("https://localhost:44321/"), 18404));
    }

    [Fact]
    public void Localized_or_changed_request_queue_view_header_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => Web5HttpServiceState.Parse(
            Owned.Replace("Snapshot of HTTP service state (Request Queue View):", "Localized request queue view:")));
    }

    [Fact]
    public void Netsh_trailing_header_spaces_are_accepted_without_loosening_header_text()
    {
        var snapshot = Owned
            .Replace("Snapshot of HTTP service state (Request Queue View):", "Snapshot of HTTP service state (Request Queue View): ")
            .Replace("-----------------------------------------------------", "----------------------------------------------------- ");
        Assert.NotEmpty(Web5HttpServiceState.Parse(snapshot));
    }

    [Theory]
    [InlineData("Number of active processes attached: 1", "Number of active processes attached: many")]
    [InlineData("ID: 18404, image:", "ID: none, image:")]
    [InlineData("Number of registered URLs: 2", "Number of registered URLs: 3")]
    public void Malformed_or_internally_inconsistent_snapshot_is_rejected(string oldValue, string newValue)
    {
        Assert.Throws<InvalidDataException>(() => Web5HttpServiceState.Parse(Owned.Replace(oldValue, newValue)));
    }

    [Fact]
    public void Registered_URL_count_smaller_than_present_entries_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => Web5HttpServiceState.Parse(
            Owned.Replace("Number of registered URLs: 2", "Number of registered URLs: 1")));
    }

    [Fact]
    public void Oversized_snapshot_is_rejected_before_parsing()
    {
        Assert.Throws<InvalidDataException>(() => Web5HttpServiceState.Parse(new string('x', 1024 * 1024 + 1)));
    }

    [Fact]
    public void Nonzero_netsh_exit_is_rejected_without_returning_diagnostics()
    {
        var error = Assert.Throws<InvalidDataException>(() => Web5HttpServiceState.ValidateCommandResult(1, "partial", "private diagnostics"));
        Assert.DoesNotContain("private", error.Message);
        Assert.DoesNotContain("partial", error.Message);
    }

    [Fact]
    public async Task Precancelled_read_never_starts_netsh()
    {
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Web5HttpServiceState.ReadAsync(TimeSpan.FromSeconds(1), stop.Token));
    }

    [Fact]
    public async Task Bounded_reader_rejects_overflow_after_draining_without_retaining_excess_text()
    {
        Assert.Equal("exact", await Web5HttpServiceState.ReadBoundedAsync(new StringReader("exact"), 5));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Web5HttpServiceState.ReadBoundedAsync(new StringReader(new string('x', 1025)), 1024));
    }
}
