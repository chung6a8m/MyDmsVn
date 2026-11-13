using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
