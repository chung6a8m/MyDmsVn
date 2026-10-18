using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class ReusableSqlFixtureTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("fresh", false)]
    [InlineData("FRESH", false)]
    [InlineData("reuse", true)]
    [InlineData(" reuse ", true)]
    public void Mode_parser_uses_reuse_default_and_accepts_explicit_modes(string? value, bool reuse)
    {
        Assert.Equal(reuse ? SqlFixtureRunMode.Reuse : SqlFixtureRunMode.Fresh, SqlFixtureRunModeSetting.Parse(value));
    }

    [Theory]
    [InlineData("automatic")]
    [InlineData("re-use")]
    public void Unknown_mode_fails_without_echoing_the_input(string value)
    {
        var error = Assert.Throws<ArgumentException>(() => SqlFixtureRunModeSetting.Parse(value));
        Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Contamination_failure_probe_rejects_a_seed_assertion()
    {
        var seedError = new Xunit.Sdk.XunitException("Seed did not fill every mutable table.");
        var error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() =>
            ApplicationSqlCollectionFixture.AssertExpectedScenarioFailureAsync(() => Task.FromException(seedError)));
        Assert.Contains(seedError.Message, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("worker")]
    [InlineData("stop-throws")]
    [InlineData("stop-blocks")]
    [InlineData("async-resource")]
    [InlineData("sync-resource")]
    public async Task Cleanup_budget_bounds_uncooperative_workers_and_resources(string kind)
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var unblock = new ManualResetEventSlim();
        var closed = false;
        var laterCleanup = 0;
        var scope = new SqlFixtureTestScope("bounded-cleanup", "owned-test-connection", clean =>
        {
            Assert.False(clean);
            closed = true;
            return Task.CompletedTask;
        }, cleanupTimeout: TimeSpan.FromMilliseconds(150));
        scope.RegisterCleanup(() => { laterCleanup++; return Task.CompletedTask; });
        if (kind == "async-resource") { scope.RegisterCleanup(() => release.Task); }
        else if (kind == "sync-resource")
        {
            scope.RegisterCleanup(() => { unblock.Wait(); return Task.CompletedTask; });
        }
        else
        {
            scope.RegisterWorker(kind == "stop-blocks" ? Task.CompletedTask : release.Task, () =>
            {
                if (kind == "stop-throws") { throw new InvalidOperationException("stop failure"); }
                if (kind == "stop-blocks") { unblock.Wait(); }
            });
        }
        // Keep the test itself bounded even against the original synchronous callback path.
        var disposal = Task.Run(() => scope.DisposeAsync());
        try
        {
            Assert.Same(disposal, await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromSeconds(3))));
            var error = await Record.ExceptionAsync(() => disposal);
            Assert.NotNull(error);
            var errors = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToArray() : new[] { error };
            Assert.Contains(errors, e => e is TimeoutException);
            if (kind == "stop-throws") { Assert.Contains(errors, e => e.Message == "stop failure"); }
            Assert.True(closed);
            Assert.Equal(0, laterCleanup);
            Assert.Same(scope.DisposeAsync(), scope.DisposeAsync());
        }
        finally
        {
            release.TrySetResult(true);
            unblock.Set();
            await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromSeconds(3)));
            // Observe both the original and bounded implementation's final failure.
            await Record.ExceptionAsync(() => disposal);
        }
    }

    [Fact]
    public async Task Scope_drains_workers_then_attempts_every_cleanup_before_releasing()
    {
        var calls = new List<string>();
        var worker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scope = new SqlFixtureTestScope("scope", "owned-test-connection", ok =>
        {
            calls.Add("release:" + ok);
            return Task.CompletedTask;
        });
        scope.RegisterCleanup(() => { calls.Add("resource"); return Task.CompletedTask; });
        scope.RegisterCleanup(() => { calls.Add("failing-resource"); throw new InvalidOperationException("cleanup"); });
        var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.RegisterWorker(worker.Task, () => { calls.Add("stop"); stopped.SetResult(true); });
        var disposal = scope.DisposeAsync();
        Assert.Same(stopped.Task, await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(3))));
        Assert.False(disposal.IsCompleted);
        Assert.Equal(new[] { "stop" }, calls);
        worker.SetResult(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => disposal);
        Assert.Equal(new[] { "stop", "failing-resource", "resource", "release:False" }, calls);
    }

    [Fact]
    public async Task Worker_fault_does_not_hide_stop_or_resource_cleanup_failures()
    {
        var closed = false;
        var scope = new SqlFixtureTestScope("scope", "owned-test-connection", ok =>
        {
            Assert.False(ok);
            closed = true;
            return Task.CompletedTask;
        });
        scope.RegisterWorker(Task.FromException(new InvalidOperationException("worker")),
            () => throw new InvalidOperationException("stop"));
        scope.RegisterCleanup(() => throw new InvalidOperationException("resource"));
        var error = await Assert.ThrowsAsync<AggregateException>(() => scope.DisposeAsync());
        Assert.Equal(3, error.Flatten().InnerExceptions.Count);
        Assert.True(closed);
    }

    [Fact]
    public async Task Scope_preserves_every_failure_from_a_tracked_aggregate_worker()
    {
        var scope = new SqlFixtureTestScope("aggregate-worker", "owned-test-connection", _ => Task.CompletedTask);
        scope.RegisterWorker(Task.WhenAll(Task.FromException(new InvalidOperationException("worker-one")),
            Task.FromException(new InvalidOperationException("worker-two"))), () => { });
        var error = await Assert.ThrowsAsync<AggregateException>(() => scope.DisposeAsync());
        Assert.Equal(2, error.Flatten().InnerExceptions.Count);
    }

    [Fact]
    public async Task Scope_disposal_is_idempotent_and_forbids_late_resources_or_connection_access()
    {
        var closed = 0;
        var scope = new SqlFixtureTestScope("scope", "owned-test-connection", ok =>
        {
            Assert.True(ok);
            closed++;
            return Task.CompletedTask;
        });
        Assert.Equal("owned-test-connection", scope.ConnectionString);
        var first = scope.DisposeAsync();
        Assert.Same(first, scope.DisposeAsync());
        await first;
        Assert.Equal(1, closed);
        Assert.Throws<ObjectDisposedException>(() => scope.ConnectionString);
        Assert.Throws<ObjectDisposedException>(() => scope.RegisterCleanup(() => Task.CompletedTask));
    }
}
