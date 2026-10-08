using System;
using Microsoft.Extensions.DependencyInjection;

namespace MyDmsVn.Server.Infrastructure.Persistence;

public sealed class SqlPersistenceBuilder
{
    private readonly IServiceCollection _services;

    internal SqlPersistenceBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public SqlPersistenceBuilder AddRepository<TRepository, TImplementation>()
        where TRepository : class
        where TImplementation : class, TRepository
    {
        _services.AddSingleton(
            new SqlRepositoryRegistration(
                typeof(TRepository),
                (serviceProvider, context) => ActivatorUtilities.CreateInstance<TImplementation>(
                    serviceProvider,
                    context)));
        return this;
    }

    public SqlPersistenceBuilder AddRepoDbMapping<TMapping>()
        where TMapping : class, IRepoDbMapping
    {
        _services.AddSingleton<IRepoDbMapping, TMapping>();
        return this;
    }
}

internal sealed class SqlRepositoryRegistration
{
    public SqlRepositoryRegistration(
        Type repositoryType,
        Func<IServiceProvider, ISqlExecutionContext, object> create)
    {
        RepositoryType = repositoryType;
        Create = create;
    }

    public Type RepositoryType { get; }

    public Func<IServiceProvider, ISqlExecutionContext, object> Create { get; }
}
