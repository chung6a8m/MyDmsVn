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

    [Fact]
    public async Task Stop_joins_timer_callback_even_after_the_handler_has_returned()
    {
        var callbackEntered = Signal();
        var handlerReturning = Signal();
        var releaseCallback = Signal();
        var primary = new IOException("Injected handler failure.");
        var control = WebRecoveryPilotControl.StartCutover(Guid.NewGuid(), async (request, token) =>
        {
            token.Register(() =>
            {
                callbackEntered.SetResult(true);
                releaseCallback.Task.GetAwaiter().GetResult();
            });
            await callbackEntered.Task;
            handlerReturning.SetResult(true);
            throw primary;
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            using var pipe = await Send(control, "oldClientNewHostOn", 5250, timeout.Token);
            await WaitFor(handlerReturning.Task, timeout.Token);
            var stopping = control.Stop();
            await WaitFor(stopping, timeout.Token);
            var failure = await Assert.ThrowsAsync<AggregateException>(() => stopping);
            Assert.Collection(failure.InnerExceptions, error => Assert.Same(primary, error),
                error => Assert.Contains("drain-uncertain", error.Message));
            Assert.False(control.CutoverJoined);
            await AssertUnavailable(control, timeout.Token);
        }
        finally
        {
            releaseCallback.TrySetResult(true);
            try { await control.Stop(); }
            catch (AggregateException) { }
        }
        var joined = await Assert.ThrowsAsync<AggregateException>(() => control.Stop());
        Assert.Collection(joined.InnerExceptions, error => Assert.Same(primary, error),
            error => Assert.Contains("drain-uncertain", error.Message));
        Assert.True(control.CutoverJoined);
    }

    [Fact]
    public async Task Explicit_stop_retains_callback_failure_after_primary_once_across_repeated_stop()
    {
        var started = Signal();
        var cancelled = Signal();
        var primary = new IOException("Injected handler failure.");
        var callback = new InvalidOperationException("Injected cancellation callback failure.");
        var callbackCount = 0;
        var control = WebRecoveryPilotControl.StartCutover(Guid.NewGuid(), async (request, token) =>
        {
            token.Register(() =>
            {
                Interlocked.Increment(ref callbackCount);
                cancelled.SetResult(true);
                throw callback;
            });
            started.SetResult(true);
            await cancelled.Task;
            throw primary;
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            using var pipe = await Send(control, "oldClientNewHostOn", 15000, timeout.Token);
            await WaitFor(started.Task, timeout.Token);
            var failure = await Assert.ThrowsAsync<AggregateException>(() => control.Stop(browserDrained: false));
            var repeated = await Assert.ThrowsAsync<AggregateException>(() => control.Stop());

            Assert.Collection(failure.InnerExceptions, error => Assert.Same(primary, error), error => Assert.Same(callback, error),
                error => Assert.Contains("drain-uncertain", error.Message));
            Assert.Equal(failure.InnerExceptions, repeated.InnerExceptions);
            Assert.Equal(1, Volatile.Read(ref callbackCount));
            Assert.True(control.CutoverJoined);
        }
        finally
        {
            cancelled.TrySetResult(true);
            try { await control.Stop(); }
            catch (AggregateException) { }
        }
    }

    [Fact]
    public async Task Work_deadline_owns_a_throwing_callback_and_preserves_its_handler_failure_first()
    {
        var cancelled = Signal();
        var primary = new IOException("Injected handler failure.");
        var callback = new InvalidOperationException("Injected work timer callback failure.");
        var control = WebRecoveryPilotControl.StartCutover(Guid.NewGuid(), async (request, token) =>
        {
            token.Register(() =>
            {
                cancelled.SetResult(true);
                throw callback;
            });
            await cancelled.Task;
            throw primary;
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            using var pipe = await Send(control, "oldClientNewHostOn", 5250, timeout.Token);
            await WaitFor(cancelled.Task, timeout.Token);
            await Assert.ThrowsAsync<EndOfStreamException>(() => WebFrontendPortBridge.ReadFrame(pipe, timeout.Token));
            var failure = await Assert.ThrowsAsync<AggregateException>(() => control.Stop());
            Assert.Collection(failure.InnerExceptions, error => Assert.Same(primary, error), error => Assert.Same(callback, error));
            Assert.True(control.CutoverJoined);
        }
        finally
        {
            cancelled.TrySetResult(true);
            try { await control.Stop(); }
            catch (AggregateException) { }
        }
    }

    [Fact]
    public async Task Response_close_failure_follows_response_timeout_and_work_callbacks_are_disarmed()
    {
        var closeFailure = new IOException("Injected response close failure.");
        var workCallbacks = 0;
        var responseCloses = 0;
        var control = WebRecoveryPilotControl.StartCutover(Guid.NewGuid(), (request, token) =>
        {
            token.Register(() => Interlocked.Increment(ref workCallbacks));
            return Task.FromResult(new JObject { ["marker"] = new string('a', 900000) });
        }, closeResponse: pipe =>
        {
            Interlocked.Increment(ref responseCloses);
            pipe.Dispose();
            throw closeFailure;
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            // Work allowance is 300 ms, below the five-second unread-response timeout.
            using var pipe = await Send(control, "oldClientNewHostOn", 5300, timeout.Token);
            while (!control.CutoverJoined) await Task.Delay(50, timeout.Token);
            var failure = await Assert.ThrowsAsync<AggregateException>(() => control.Stop());
            Assert.Collection(failure.InnerExceptions, error => Assert.Contains("response-timeout", error.Message),
                error => Assert.Same(closeFailure, error));
            Assert.Equal(0, Volatile.Read(ref workCallbacks));
            Assert.Equal(1, Volatile.Read(ref responseCloses));
        }
        finally
        {
            try { await control.Stop(); }
            catch (AggregateException) { }
        }
    }

    [Fact]
    public async Task Idle_controller_deadline_closes_the_pipe_without_starting_a_handler()
    {
        var calls = 0;
        var control = WebRecoveryPilotControl.StartCutover(Guid.NewGuid(), (request, token) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new JObject());
        }, closeResponse: null, controllerLifetime: TimeSpan.FromSeconds(2));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            using var pipe = CreateClient(control.Manifest);
            await pipe.ConnectAsync(1000, timeout.Token);
            await Assert.ThrowsAsync<EndOfStreamException>(() => WebFrontendPortBridge.ReadFrame(pipe, timeout.Token));
            await control.Stop();
            Assert.True(control.CutoverJoined);
            Assert.Equal(0, Volatile.Read(ref calls));
        }
        finally { await control.Stop(); }
    }

    private static TaskCompletionSource<bool> Signal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitFor(Task task, CancellationToken token)
    {
        if (await Task.WhenAny(task, Task.Delay(Timeout.Infinite, token)) != task) token.ThrowIfCancellationRequested();
    }

    private static async Task AssertUnavailable(WebRecoveryPilotControl control, CancellationToken token)
    {
        using var pipe = CreateClient(control.Manifest);
        // Windows can report either WAIT_TIMEOUT or ERROR_SEM_TIMEOUT while the previous instance closes.
        var failure = await Record.ExceptionAsync(() => pipe.ConnectAsync(200, token));
        Assert.True(failure is TimeoutException || failure is IOException { HResult: unchecked((int)0x80070079) });
    }

    private static NamedPipeClientStream CreateClient(JObject manifest) =>
        new NamedPipeClientStream(".", Path.GetFileName((string)manifest["controlPipe"]!), PipeDirection.InOut, PipeOptions.Asynchronous);

    private static async Task<NamedPipeClientStream> Send(WebRecoveryPilotControl control, string action, int allowance, CancellationToken token)
    {
        var manifest = control.Manifest;
        var pipe = CreateClient(manifest);
        try
        {
            await pipe.ConnectAsync(3000, token);
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
