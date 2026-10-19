using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB.Mapping;

namespace MyDmsVn.Server.Infrastructure.Persistence;

/// <summary>Publishes only a complete composition. Published schemas must be used read-only.</summary>
internal sealed class LinqToDbMappingSchemaProvider
{
    private readonly object _sync = new();
    private readonly ILinqToDbMapping[] _mappings;
    private MappingSchema? _schema;

    public LinqToDbMappingSchemaProvider(IEnumerable<ILinqToDbMapping> mappings) =>
        _mappings = (mappings ?? throw new ArgumentNullException(nameof(mappings))).ToArray();

    public MappingSchema GetSchema()
    {
        lock (_sync)
        {
            if (_schema is not null) return _schema;
            var candidate = new MappingSchema();
            var builder = new FluentMappingBuilder(candidate);
            foreach (var mapping in _mappings) mapping.Configure(builder);
            builder.Build();
            _schema = candidate;
            return candidate;
        }
    }
}
