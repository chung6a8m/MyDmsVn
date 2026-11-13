using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CutoverSessionTests
{
    [Fact]
    public async Task Session_passes_the_same_explicit_action_deadline_to_every_runtime_call()
    {
        using var cancellation = new CancellationTokenSource();
        var runtime = new Runtime(cancellation.Token);
        var session = await Start(runtime, cancellation.Token);
        runtime.Deadlines.Clear();
        var deadline = new Web6CutoverDeadline(TimeSpan.FromSeconds(1));
        await session.Handle(Request("oldClientNewHostOn"), deadline, cancellation.Token);
        Assert.NotEmpty(runtime.Deadlines);
        Assert.All(runtime.Deadlines, observed => Assert.Same(deadline, observed));
    }

    [Theory]
    [InlineData("oldClientNewHostOn", "old-client-new-host", "Current:cached")]
    [InlineData("newClientOldHostOn", "new-client-old-host", "Old:normal")]
    public async Task Stable_cached_or_old_reset_switches_to_current_once(string action, string marker, string switched)
    {
        using var source = new CancellationTokenSource();
        var runtime = new Runtime(source.Token);
        var session = await Start(runtime, source.Token);

        Assert.Equal(marker, (string?)(await session.Handle(Request(action), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token))["marker"]);
        Assert.Equal(Web6CutoverJournalState.Stable, session.JournalState);
        Assert.Empty(await session.Handle(Request("resetCutover"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token));

        Assert.Equal(new[] { "lifecycle", "switch:" + switched, "lifecycle", "switch:Current:normal", "lifecycle" }, runtime.Calls);
        Assert.Equal((Web6CutoverPair.Current, false, runtime.Plan.CurrentEpoch), runtime.Host);
        Assert.Equal(Web6CutoverJournalState.Stable, session.JournalState);
    }

    [Fact]
    public async Task Restored_reset_reverifies_the_same_host_after_intentional_grant_restore()
    {
        using var source = new CancellationTokenSource();
        var runtime = new Runtime(source.Token);
        var session = await Start(runtime, source.Token);
        var operation = Guid.NewGuid();

        Assert.Equal(runtime.Plan.CurrentEpoch.ToString("D"),
            (string?)(await session.Handle(Request("sameEpochRestart", operation), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token))["deploymentEpoch"]);
        await session.Handle(Request("revokeGrantOn", operation), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token);
        Assert.False(runtime.Granted);
        await session.Handle(Request("revokeGrantOff", operation), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token);
        Assert.True(runtime.Granted);
        Assert.Equal(runtime.Plan.RestoredEpoch.ToString("D"),
            (string?)(await session.Handle(Request("restoreEpoch", operation), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token))["deploymentEpoch"]);
        runtime.Calls.Clear();

        Assert.Empty(await session.Handle(Request("resetCutover"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token));

        Assert.Equal(new[] { "admit:Restored:normal", "lifecycle" }, runtime.Calls);
        Assert.Equal((Web6CutoverPair.Restored, false, runtime.Plan.RestoredEpoch), runtime.Host);
        Assert.Equal(Web6CutoverJournalState.Stable, session.JournalState);
    }

    [Theory]
    [InlineData("restoreEpoch")]
    [InlineData("resetCutover")]
    public async Task Restore_and_reset_refuse_to_bypass_the_in_body_grant_restoration(string action)
    {
        using var source = new CancellationTokenSource();
        var runtime = new Runtime(source.Token);
        var session = await Start(runtime, source.Token);
        var operation = Guid.NewGuid();
        await session.Handle(Request("sameEpochRestart", operation), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token);
        await session.Handle(Request("revokeGrantOn", operation), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token);
        runtime.Calls.Clear();

        await Assert.ThrowsAsync<InvalidDataException>(() => session.Handle(
            Request(action, action == "restoreEpoch" ? operation : (Guid?)null), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token));

        Assert.False(runtime.Granted);
        Assert.DoesNotContain(runtime.Calls, call => call.StartsWith("switch:", StringComparison.Ordinal) || call.StartsWith("grant:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Restore_requires_recorded_grant_restoration_even_with_grant_currently_enabled()
    {
        using var source = new CancellationTokenSource();
        var runtime = new Runtime(source.Token);
        var session = await Start(runtime, source.Token);
        var operation = Guid.NewGuid();
        await session.Handle(Request("sameEpochRestart", operation), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token);

        await Assert.ThrowsAsync<InvalidDataException>(() => session.Handle(Request("restoreEpoch", operation), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token));

        Assert.Equal(Web6CutoverPair.Current, runtime.Host.Pair);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Runtime_failure_or_cancellation_leaves_reset_uncertain(bool cancel, bool afterEffect)
    {
        using var source = new CancellationTokenSource();
        using var reset = new CancellationTokenSource();
        var runtime = new Runtime(source.Token);
        var session = await Start(runtime, source.Token);
        var failure = new IOException("Injected runtime failure.");
        void Interrupt()
        {
            Assert.Equal(Web6CutoverJournalState.Transitioning, session.JournalState);
            if (cancel) source.Cancel();
            else throw failure;
        }
        if (afterEffect) runtime.AfterSwitch = Interrupt;
        else runtime.BeforeSwitch = Interrupt;

        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.Handle(Request("newClientOldHostOn"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token));
        else
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => session.Handle(Request("newClientOldHostOn"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token)));

        Assert.Equal(afterEffect ? Web6CutoverPair.Old : Web6CutoverPair.Current, runtime.Host.Pair);
        Assert.Equal(Web6CutoverJournalState.Uncertain, session.JournalState);
        Assert.Equal(new[] { "lifecycle", "switch:Old:normal" }, runtime.Calls);
        runtime.Calls.Clear();
        await Assert.ThrowsAsync<InvalidDataException>(() => session.Handle(Request("resetCutover"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), reset.Token));
        Assert.Empty(runtime.Calls);
    }

    [Fact]
    public async Task Post_lifecycle_failure_does_not_publish_a_stable_switch()
    {
        using var source = new CancellationTokenSource();
        var runtime = new Runtime(source.Token);
        var session = await Start(runtime, source.Token);
        runtime.OnLifecycle = () =>
        {
            if (runtime.Host.Cached) throw new IOException("Injected post-lifecycle failure.");
        };

        await Assert.ThrowsAsync<IOException>(() => session.Handle(Request("oldClientNewHostOn"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token));

        Assert.True(runtime.Host.Cached);
        Assert.Equal(Web6CutoverJournalState.Uncertain, session.JournalState);
        await Assert.ThrowsAsync<InvalidDataException>(() => session.Handle(Request("resetCutover"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token));
    }

    [Fact]
    public async Task Verify_only_reset_with_failed_admission_leaves_the_existing_host_uncertain()
    {
        using var source = new CancellationTokenSource();
        var runtime = new Runtime(source.Token);
        var session = await Start(runtime, source.Token);
        runtime.OnAdmission = () => throw new IOException("Injected admission failure.");

        await Assert.ThrowsAsync<IOException>(() => session.Handle(Request("resetCutover"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token));

        Assert.Equal(new[] { "admit:Current:normal" }, runtime.Calls);
        Assert.Equal(Web6CutoverJournalState.Uncertain, session.JournalState);
        Assert.Equal((Web6CutoverPair.Current, false, runtime.Plan.CurrentEpoch), runtime.Host);
    }

    [Fact]
    public async Task Reset_cannot_overlap_an_active_transition()
    {
        using var source = new CancellationTokenSource();
        var runtime = new Runtime(source.Token);
        var session = await Start(runtime, source.Token);
        var active = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.FinishSwitch = active.Task;
        var transition = session.Handle(Request("oldClientNewHostOn"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token);
        try
        {
            Assert.Equal(Web6CutoverJournalState.Transitioning, session.JournalState);
            await Assert.ThrowsAsync<InvalidDataException>(() => session.Handle(Request("resetCutover"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token));
            Assert.Equal(new[] { "lifecycle", "switch:Current:cached" }, runtime.Calls);
        }
        finally
        {
            active.SetResult(true);
            await transition;
        }

        await session.Handle(Request("resetCutover"), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), source.Token);
        Assert.Equal((Web6CutoverPair.Current, false, runtime.Plan.CurrentEpoch), runtime.Host);
    }

    [Fact]
    public async Task Unjoined_controller_stop_blocks_host_drain_and_allocation_cleanup_even_after_late_join()
    {
        var gate = new Web6CutoverCleanupGate();
        var stopFailure = new IOException("Injected unjoined controller Stop.");
        var joined = false;
        var hostDrains = 0;
        var allocationCleanups = 0;

        var failure = await Assert.ThrowsAsync<AggregateException>(() => Web6CutoverLifetime.Run(
            () => gate.Stop(() => Task.FromException(stopFailure), () => joined),
            async () =>
            {
                joined = true;
                await gate.DrainHost(() => { hostDrains++; return Task.CompletedTask; });
                return true;
            },
            new Func<Task>[] { () => { allocationCleanups++; return Task.CompletedTask; } }, stopOnCleanupFailure: true));

        Assert.Collection(failure.InnerExceptions, error => Assert.Same(stopFailure, error),
            error => Assert.Contains("drain-uncertain", error.Message));
        Assert.True(gate.RetainForContainment);
        Assert.Equal(0, hostDrains);
        Assert.Equal(0, allocationCleanups);
    }

    private static Web6CutoverRequest Request(string action, Guid? operation = null) =>
        new Web6CutoverRequest(action, operation, null, TimeSpan.FromSeconds(10));

    private static async Task<Web6CutoverSession> Start(Runtime runtime, CancellationToken token)
    {
        var session = new Web6CutoverSession(runtime.Plan, runtime);
        await session.Begin(new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), token);
        Assert.Equal(Web6CutoverJournalState.Stable, session.JournalState);
        Assert.Equal(new[] { "switch:Current:normal", "lifecycle" }, runtime.Calls);
        runtime.Calls.Clear();
        return session;
    }

    private sealed class Runtime : IWeb6CutoverRuntime
    {
        private readonly CancellationToken _expectedToken;

        internal Runtime(CancellationToken expectedToken) => _expectedToken = expectedToken;

        internal Web6CandidateCutoverPlan Plan { get; } = new Web6CandidateCutoverPlan(
            "current", "old", "restored", new Uri("https://localhost:44321/"), new Uri("https://localhost:44322/"),
            Web6CandidateCutoverPlan.FrontendArchiveSha, new string('a', 40), new string('b', 64),
            Web6CandidateCutoverPlan.BackendArchiveSha, new string('c', 40), new string('d', 64), new string('e', 64),
            13, 12, new string('f', 64), new string('1', 64), new string('2', 64), Guid.NewGuid(), Guid.NewGuid());
        internal List<Web6CutoverDeadline> Deadlines { get; } = new List<Web6CutoverDeadline>();
        internal List<string> Calls { get; } = new List<string>();
        internal (Web6CutoverPair Pair, bool Cached, Guid Epoch) Host { get; private set; }
        internal bool Granted { get; private set; } = true;
        internal Action? BeforeSwitch { get; set; }
        internal Action? AfterSwitch { get; set; }
        internal Action? OnLifecycle { get; set; }
        internal Action? OnAdmission { get; set; }
        internal Task FinishSwitch { get; set; } = Task.CompletedTask;

        public Task Switch(Web6CutoverPair pair, bool cached, Guid epoch, Web6CutoverDeadline deadline, CancellationToken token)
        {
            Record("switch:" + HostLabel(pair, cached), deadline, token);
            BeforeSwitch?.Invoke();
            token.ThrowIfCancellationRequested();
            Host = (pair, cached, epoch);
            AfterSwitch?.Invoke();
            return FinishSwitch;
        }

        public Task VerifyStableAdmission(Web6CutoverPair pair, bool cached, Guid epoch, Web6CutoverDeadline deadline, CancellationToken token)
        {
            Record("admit:" + HostLabel(pair, cached), deadline, token);
            OnAdmission?.Invoke();
            if (Host != (pair, cached, epoch)) throw new IOException("Host admission drifted.");
            return Task.CompletedTask;
        }

        public Task VerifyLifecycle(Web6CutoverDeadline deadline, CancellationToken token)
        {
            Record("lifecycle", deadline, token);
            OnLifecycle?.Invoke();
            return Task.CompletedTask;
        }

        public Task SetGrant(bool granted, Web6CutoverDeadline deadline, CancellationToken token)
        {
            Record("grant:" + granted, deadline, token);
            Granted = granted;
            return Task.CompletedTask;
        }

        public Task VerifyOperation(Guid operation, bool committed, Web6CutoverDeadline deadline, CancellationToken token) => Record("operation", deadline, token);
        public Task VerifyCommittedSetup(Web6CommittedSetup proof, Web6CutoverDeadline deadline, CancellationToken token) => Record("setup", deadline, token);
        public Task MakeCommittedUnavailable(Guid operation, Web6CutoverDeadline deadline, CancellationToken token) => Record("unavailable", deadline, token);
        public Task VerifyZeroMutation(Guid operation, Web6CutoverDeadline deadline, CancellationToken token) => Record("zero", deadline, token);

        private Task Record(string call, Web6CutoverDeadline deadline, CancellationToken token)
        {
            Assert.Equal(_expectedToken, token);
            token.ThrowIfCancellationRequested();
            Deadlines.Add(deadline);
            Calls.Add(call);
            return Task.CompletedTask;
        }

        private static string HostLabel(Web6CutoverPair pair, bool cached) => pair + (cached ? ":cached" : ":normal");
    }
}
