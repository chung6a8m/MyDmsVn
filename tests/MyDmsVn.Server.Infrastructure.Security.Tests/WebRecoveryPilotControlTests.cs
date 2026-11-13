using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebRecoveryPilotControlTests
{
    [Fact]
    public async Task Successful_handler_writes_one_correlated_final_frame_after_completion()
    {
        var started = Signal();
        var release = Signal();
        var calls = 0;
        var control = WebRecoveryPilotControl.StartCutover(Guid.NewGuid(), async (request, token) =>
        {
            Assert.Equal("oldClientNewHostOn", request.Action);
            Assert.True(token.CanBeCanceled);
            Interlocked.Increment(ref calls);
            started.SetResult(true);
            await release.Task;
            return new JObject { ["marker"] = "old-client-new-host" };
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            using var pipe = await Send(control, "oldClientNewHostOn", 10000, timeout.Token);
            await WaitFor(started.Task, timeout.Token);
            var response = WebFrontendPortBridge.ReadFrame(pipe, timeout.Token);
            Assert.False(response.IsCompleted);
            release.SetResult(true);

            Assert.True(JToken.DeepEquals(new JObject { ["marker"] = "old-client-new-host", ["id"] = 17, ["ok"] = true }, await response));
            await Assert.ThrowsAsync<EndOfStreamException>(() => WebFrontendPortBridge.ReadFrame(pipe, timeout.Token));
            Assert.Equal(1, Volatile.Read(ref calls));
        }
        finally
        {
            release.TrySetResult(true);
            await control.Stop();
        }
    }

    [Theory]
    [InlineData(false, "action-deadline")]
    [InlineData(true, "controller-cancelled")]
    public async Task Linked_request_cancellation_prevents_even_a_returned_success(bool stop, string category)
    {
        var started = Signal();
        var cancelled = Signal();
        var actions = new List<string>();
        var control = WebRecoveryPilotControl.StartCutover(Guid.NewGuid(), async (request, token) =>
        {
            actions.Add(request.Action);
            started.SetResult(true);
            using (token.Register(() => cancelled.TrySetResult(true))) await cancelled.Task;
            return new JObject { ["marker"] = "should-never-be-written" };
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        Task? stopping = null;
        try
        {
            using var pipe = await Send(control, "oldClientNewHostOn", stop ? 12000 : 5250, timeout.Token);
            await WaitFor(started.Task, timeout.Token);
            if (stop) stopping = control.Stop();
            await WaitFor(cancelled.Task, timeout.Token);
            await Assert.ThrowsAsync<EndOfStreamException>(() => WebFrontendPortBridge.ReadFrame(pipe, timeout.Token));

            var failure = await Assert.ThrowsAsync<AggregateException>(() => stopping ??= control.Stop());
            Assert.Contains(category, Assert.Single(failure.InnerExceptions).Message);
            Assert.True(control.CutoverJoined);
            Assert.Equal(new[] { "oldClientNewHostOn" }, actions);
        }
        finally
        {
            cancelled.TrySetResult(true);
            if (stopping == null) await control.Stop();
        }
    }

    [Fact]
    public async Task Faulted_serve_rejects_later_reset_and_retains_handler_and_drain_failures_in_order()
    {
        var primary = new IOException("Injected handler failure.");
        var calls = 0;
        var control = WebRecoveryPilotControl.StartCutover(Guid.NewGuid(), (request, token) =>
        {
            Interlocked.Increment(ref calls);
            throw primary;
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            using var pipe = await Send(control, "oldClientNewHostOn", 10000, timeout.Token);
            await Assert.ThrowsAsync<EndOfStreamException>(() => WebFrontendPortBridge.ReadFrame(pipe, timeout.Token));
            await AssertUnavailable(control, timeout.Token);

            var failure = await Assert.ThrowsAsync<AggregateException>(() => control.Stop(browserDrained: false));
            Assert.Collection(failure.InnerExceptions, error => Assert.Same(primary, error),
                error => Assert.Contains("drain-uncertain", error.Message));
            Assert.True(control.CutoverJoined);
            Assert.Equal(1, Volatile.Read(ref calls));
        }
        finally
        {
            await Assert.ThrowsAsync<AggregateException>(() => control.Stop(browserDrained: false));
        }
    }

    [Fact]
    public async Task Unread_response_has_its_own_finite_write_budget_and_faults_serve()
    {
        var returned = Signal();
        var calls = 0;
        var control = WebRecoveryPilotControl.StartCutover(Guid.NewGuid(), (request, token) =>
        {
            Interlocked.Increment(ref calls);
            returned.SetResult(true);
            return Task.FromResult(new JObject { ["marker"] = new string('a', 900000) });
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            using var pipe = await Send(control, "oldClientNewHostOn", 60000, timeout.Token);
            await WaitFor(returned.Task, timeout.Token);
            while (!control.CutoverJoined) await Task.Delay(50, timeout.Token);
            await AssertUnavailable(control, timeout.Token);

            var failure = await Assert.ThrowsAsync<AggregateException>(() => control.Stop());
            Assert.Contains("response-timeout", Assert.Single(failure.InnerExceptions).Message);
            Assert.Equal(1, Volatile.Read(ref calls));
        }
        finally
        {
            try { await control.Stop(); }
            catch (AggregateException) { }
        }
    }

    [Fact]
    public async Task Finite_stop_retains_unjoined_handler_until_exact_delegate_exits()
    {
        var started = Signal();
        var expired = Signal();
        var release = Signal();
        var primary = new IOException("Injected late handler failure.");
        var calls = 0;
        var control = WebRecoveryPilotControl.StartCutover(Guid.NewGuid(), async (request, token) =>
        {
            Interlocked.Increment(ref calls);
            started.SetResult(true);
            using (token.Register(() => expired.TrySetResult(true))) await release.Task;
            throw primary;
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            using var pipe = await Send(control, "oldClientNewHostOn", 5250, timeout.Token);
            await WaitFor(started.Task, timeout.Token);
            await WaitFor(expired.Task, timeout.Token);
            await Assert.ThrowsAsync<EndOfStreamException>(() => WebFrontendPortBridge.ReadFrame(pipe, timeout.Token));
            await AssertUnavailable(control, timeout.Token);
            Assert.Equal(TimeSpan.FromSeconds(5), Web6CutoverBudgets.ControllerJoin);
            var stopping = control.Stop();
            await WaitFor(stopping, timeout.Token);

            var unjoined = await Assert.ThrowsAsync<AggregateException>(() => stopping);
            Assert.Contains("drain-uncertain", Assert.Single(unjoined.InnerExceptions).Message);
            Assert.False(control.CutoverJoined);
            await AssertUnavailable(control, timeout.Token);
            Assert.Equal(1, Volatile.Read(ref calls));
        }
        finally
        {
            release.TrySetResult(true);
            var joined = await Assert.ThrowsAsync<AggregateException>(() => control.Stop());
            Assert.Collection(joined.InnerExceptions, error => Assert.Same(primary, error),
                error => Assert.Contains("drain-uncertain", error.Message));
            Assert.True(control.CutoverJoined);
        }
    }

    private static TaskCompletionSource<bool> Signal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitFor(Task task, CancellationToken token)
    {
        if (await Task.WhenAny(task, Task.Delay(Timeout.Infinite, token)) != task) token.ThrowIfCancellationRequested();
    }

    private static async Task AssertUnavailable(WebRecoveryPilotControl control, CancellationToken token) =>
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            using var pipe = await Send(control, "resetCutover", 10000, token, connectTimeout: 200);
        });

    private static async Task<NamedPipeClientStream> Send(WebRecoveryPilotControl control, string action, int allowance,
        CancellationToken token, int connectTimeout = 3000)
    {
        var manifest = control.Manifest;
        var pipe = new NamedPipeClientStream(".", Path.GetFileName((string)manifest["controlPipe"]!), PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(connectTimeout, token);
            await WebFrontendPortBridge.WriteFrame(pipe, new JObject
            {
                ["action"] = action, ["id"] = 17, ["token"] = manifest["controlToken"]!.DeepClone(), ["requestAllowanceMs"] = allowance,
            }, token);
            return pipe;
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }
}
