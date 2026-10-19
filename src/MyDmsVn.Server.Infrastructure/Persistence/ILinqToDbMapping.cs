using LinqToDB.Mapping;

namespace MyDmsVn.Server.Infrastructure.Persistence;

internal interface ILinqToDbMapping
{
    void Configure(FluentMappingBuilder builder);
}
