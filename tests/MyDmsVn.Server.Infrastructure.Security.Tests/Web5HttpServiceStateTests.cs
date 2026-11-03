using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web5HttpServiceStateTests
{
    private const string Owned = @"
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

    [Theory]
    [InlineData("Number of active processes attached: 1", "Number of active processes attached: many")]
    [InlineData("ID: 18404, image:", "ID: none, image:")]
    [InlineData("Number of registered URLs: 2", "Number of registered URLs: 3")]
    public void Malformed_or_internally_inconsistent_snapshot_is_rejected(string oldValue, string newValue)
    {
        Assert.Throws<InvalidDataException>(() => Web5HttpServiceState.Parse(Owned.Replace(oldValue, newValue)));
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
}
