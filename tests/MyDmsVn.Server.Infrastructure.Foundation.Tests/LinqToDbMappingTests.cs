using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB.Mapping;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class LinqToDbMappingTests
{
    [Fact]
    public async Task Failed_mapping_is_not_published_and_concurrent_retry_publishes_one_complete_schema()
    {
        var mapping = new FailOnceMapping();
        var provider = new LinqToDbMappingSchemaProvider(new[] { mapping });
        Assert.Throws<InvalidOperationException>(() => provider.GetSchema());
        var schemas = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => provider.GetSchema())));
        Assert.All(schemas, schema => Assert.Same(schemas[0], schema));
        Assert.Equal(2, mapping.Attempts);
        var descriptor = schemas[0].GetEntityDescriptor(typeof(Probe));
        Assert.Equal("LinqProbe", descriptor.TableName);
        Assert.Equal("ProbeId", Assert.Single(descriptor.Columns).ColumnName);
        Assert.True(Assert.Single(descriptor.Columns).IsPrimaryKey);
    }

    [Fact]
    public void Independent_mapping_compositions_do_not_share_mutable_configuration()
    {
        var first = new LinqToDbMappingSchemaProvider(Array.Empty<ILinqToDbMapping>());
        var second = new LinqToDbMappingSchemaProvider(new ILinqToDbMapping[] { new ProbeMapping() });
        Assert.NotSame(first.GetSchema(), second.GetSchema());
        Assert.Equal("LinqProbe", second.GetSchema().GetEntityDescriptor(typeof(Probe)).TableName);
        Assert.Equal("Probe", first.GetSchema().GetEntityDescriptor(typeof(Probe)).TableName);
    }

    internal sealed class Probe { public int Id { get; set; } }

    internal class ProbeMapping : ILinqToDbMapping
    {
        public virtual void Configure(FluentMappingBuilder builder) => builder.Entity<Probe>()
            .HasTableName("LinqProbe").Property(x => x.Id).HasColumnName("ProbeId").IsPrimaryKey();
    }

    private sealed class FailOnceMapping : ProbeMapping
    {
        public int Attempts;
        public override void Configure(FluentMappingBuilder builder)
        {
            base.Configure(builder);
            if (Interlocked.Increment(ref Attempts) == 1)
                throw new InvalidOperationException("Expected mapping initialization failure.");
        }
    }
}
