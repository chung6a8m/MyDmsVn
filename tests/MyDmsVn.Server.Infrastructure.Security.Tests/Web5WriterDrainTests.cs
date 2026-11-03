using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class Web5WriterDrainTests
{
    [Fact]
    public void Reused_spid_and_observer_query_are_not_the_original_active_writer()
    {
        var login = new DateTime(2026, 11, 3); var connection = Guid.NewGuid();
        var writer = new Web5WriterIdentity(42, login, connection);
        Assert.False(writer.IsActive(42, login.AddSeconds(1), Guid.NewGuid(), 7, 1, 0));
        Assert.False(writer.IsActive(42, login, connection, 42, 1, 0));
        Assert.True(writer.IsActive(42, login, connection, 7, 1, 0));
        Assert.True(writer.IsActive(42, login, connection, 7, 0, 1));
    }
    [Fact]
    public async Task Wait_is_bounded_and_never_claims_a_busy_writer_drained()
    {
        var probes = 0;
        await Assert.ThrowsAsync<TimeoutException>(() => Web5WriterDrain.Wait(_ => { probes++; return Task.FromResult(true); }, TimeSpan.FromMilliseconds(80), CancellationToken.None));
        Assert.True(probes > 0);
    }
    [Fact]
    public async Task Wait_finishes_when_writer_quiesces()
    {
        var probes = 0;
        await Web5WriterDrain.Wait(_ => Task.FromResult(++probes < 2), TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.Equal(2, probes);
    }
    [Fact]
    public async Task Completed_fault_is_reported_without_being_mistaken_for_an_undrained_worker()
    {
        var fault = new InvalidOperationException("worker failed");
        Assert.Same(fault, await Web5WriterDrain.AwaitWorker(Task.FromException(fault), TimeSpan.FromSeconds(1), CancellationToken.None));
    }
    [Fact]
    public async Task Unfinished_worker_is_uncertain_after_bounded_wait()
    {
        var pending = new TaskCompletionSource<bool>();
        await Assert.ThrowsAsync<TimeoutException>(() => Web5WriterDrain.AwaitWorker(pending.Task, TimeSpan.FromMilliseconds(50), CancellationToken.None));
    }
    [Fact]
    public void Scenario_and_cleanup_failures_are_both_retained()
    {
        var scenario = new InvalidOperationException("scenario");
        var cleanup = new TimeoutException("cleanup");
        var aggregate = Assert.Throws<AggregateException>(() => Web5WriterDrain.ThrowFailures(scenario, cleanup, null));
        Assert.Contains(scenario, aggregate.InnerExceptions); Assert.Contains(cleanup, aggregate.InnerExceptions);
    }
}
