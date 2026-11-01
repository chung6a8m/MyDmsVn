using System;
using MyDmsVn.Server.Application.Operations;
using Xunit;
namespace MyDmsVn.Server.Application.Tests;

public sealed class WebOperationContextTests
{
    [Theory]
    [InlineData("11111111-1111-4111-0111-111111111111")]
    [InlineData("11111111-1111-3111-8111-111111111111")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Metadata_requires_version4_and_RFC4122_variant(string id)
    {
        Assert.Throws<ArgumentException>(() => Create(Guid.Parse(id), WebOperationKind.SalesCreate, null));
    }

    [Fact]
    public void Fingerprint_copies_are_immutable_and_targets_match_the_kind()
    {
        var hash = new byte[32];
        var context = new WebOperationContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WebOperationKind.SalesCreate, null, hash, new Codec());
        hash[0] = 1;
        var returned = context.Fingerprint;
        returned[1] = 2;
        Assert.Equal(new byte[32], context.Fingerprint);
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), WebOperationKind.SalesCreate, 1));
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), WebOperationKind.SalesUpdate, null));
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), WebOperationKind.ReceiptPost, 0));
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), (WebOperationKind)7, null));
    }

    private static WebOperationContext Create(Guid id, WebOperationKind kind, long? target)
        => new WebOperationContext(id, Guid.NewGuid(), Guid.NewGuid(), kind, target, new byte[32], new Codec());
    private sealed class Codec : IWebOperationResultCodec
    {
        public byte[] Encode<T>(WebOperationKind kind, T result) => throw new NotSupportedException();
        public T Decode<T>(WebOperationKind kind, byte[] bytes) => throw new NotSupportedException();
    }
}
