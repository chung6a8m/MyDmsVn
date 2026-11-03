using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class Web5OriginalFirstCleanupTests
{
    [Fact]
    public async Task Drained_partial_run_cleans_owned_actions_then_completes_manifest()
    {
        var events = new List<string>();
        var failures = await Web5OriginalFirstCleanup.Run(true, false, new[] { new Func<Task>(() => { events.Add("owned"); return Task.CompletedTask; }) }, () => events.Add("manifest"));
        Assert.Empty(failures); Assert.Equal(new[] { "owned", "manifest" }, events);
    }
    [Fact]
    public async Task Cleanup_failure_retains_manifest_and_reports_uncertainty()
    {
        var completed = false;
        var failures = await Web5OriginalFirstCleanup.Run(true, false, new[] { new Func<Task>(() => throw new InvalidOperationException()), new Func<Task>(() => { completed = true; return Task.CompletedTask; }) }, () => completed = true);
        Assert.Single(failures); Assert.False(completed);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Uncertain_drain_or_existing_poison_never_attempts_cleanup(bool drained, bool poisoned)
    {
        var touched = false;
        var failures = await Web5OriginalFirstCleanup.Run(drained, poisoned, new[] { new Func<Task>(() => { touched = true; return Task.CompletedTask; }) }, () => touched = true);
        Assert.Single(failures); Assert.False(touched);
    }
}
