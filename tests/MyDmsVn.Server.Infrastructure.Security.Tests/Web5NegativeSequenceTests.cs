using System;
using System.IO;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class Web5NegativeSequenceTests
{
    [Fact]
    public void Only_one_pending_exact_key_and_each_kind_once_are_admitted()
    {
        var sequence = new Web5NegativeSequence(); var first = Guid.NewGuid();
        sequence.Begin(first, "Sales.Create");
        Assert.Throws<InvalidDataException>(() => sequence.Begin(Guid.NewGuid(), "Sales.Update"));
        Assert.Throws<InvalidDataException>(() => sequence.Verify(Guid.NewGuid()));
        sequence.Verify(first);
        Assert.Throws<InvalidDataException>(() => sequence.Begin(first, "Sales.Update"));
        Assert.Throws<InvalidDataException>(() => sequence.Begin(Guid.NewGuid(), "Sales.Create"));
        Assert.False(sequence.Complete);
        foreach (var kind in new[] { "Sales.Update", "Sales.Post", "Receipt.Create", "Receipt.Update", "Receipt.Post" })
        { var key = Guid.NewGuid(); sequence.Begin(key, kind); sequence.Verify(key); }
        Assert.True(sequence.Complete);
        Assert.Throws<InvalidDataException>(() => sequence.Begin(Guid.NewGuid(), "Sales.Create"));
    }
}
