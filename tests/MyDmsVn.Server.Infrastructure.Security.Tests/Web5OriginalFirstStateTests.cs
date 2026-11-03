using System;
using System.IO;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class Web5OriginalFirstStateTests
{
    [Fact]
    public void Release_requires_observed_writer_and_distinct_waiting_operator()
    {
        var state = new Web5OriginalFirstState(); var id = Guid.NewGuid();
        state.Arm(id);
        Assert.Throws<InvalidDataException>(() => state.Release(id));
        state.WriterObserved(id, 44);
        Assert.Throws<InvalidDataException>(() => state.OperatorObserved(id, 44));
        Assert.Throws<InvalidDataException>(() => state.OperatorObserved(Guid.NewGuid(), 45));
        state.OperatorObserved(id, 45); state.Release(id);
        Assert.Throws<InvalidDataException>(() => state.Release(id));
        state.Complete(id); Assert.True(state.IsComplete);
    }
}
