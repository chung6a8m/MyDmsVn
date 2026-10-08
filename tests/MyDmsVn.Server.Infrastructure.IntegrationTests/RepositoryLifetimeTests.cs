using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class RepositoryLifetimeTests
{
    [SqlServerFact]
    public async System.Threading.Tasks.Task Each_unit_of_work_owns_a_scope_and_disposes_created_repositories()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<DisposalTracker>();
            services.AddScoped<ScopedRepositoryDependency>();
            services
                .AddSqlPersistence(database.ConnectionString)
                .AddRepository<IRepositoryWithScopedDependency, RepositoryWithScopedDependency>();
            using var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            var factory = provider.GetRequiredService<IUnitOfWorkFactory>();

            Guid firstDependencyId;
            using (var first = factory.Create())
            {
                firstDependencyId = first.Repository<IRepositoryWithScopedDependency>().DependencyId;
            }

            Guid secondDependencyId;
            using (var second = factory.Create())
            {
                secondDependencyId = second.Repository<IRepositoryWithScopedDependency>().DependencyId;
            }

            var tracker = provider.GetRequiredService<DisposalTracker>();
            Assert.NotEqual(firstDependencyId, secondDependencyId);
            Assert.Equal(2, tracker.RepositoryDisposeCount);
            Assert.Equal(2, tracker.DependencyDisposeCount);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private interface IRepositoryWithScopedDependency
    {
        Guid DependencyId { get; }
    }

    private sealed class RepositoryWithScopedDependency : IRepositoryWithScopedDependency, IDisposable
    {
        private readonly DisposalTracker _tracker;
        private int _disposed;

        public RepositoryWithScopedDependency(
            ISqlExecutionContext context,
            ScopedRepositoryDependency dependency,
            DisposalTracker tracker)
        {
            _ = context;
            DependencyId = dependency.Id;
            _tracker = tracker;
        }

        public Guid DependencyId { get; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Increment(ref _tracker.RepositoryDisposeCount);
            }
        }
    }

    private sealed class ScopedRepositoryDependency : IDisposable
    {
        private readonly DisposalTracker _tracker;

        public ScopedRepositoryDependency(DisposalTracker tracker)
        {
            _tracker = tracker;
        }

        public Guid Id { get; } = Guid.NewGuid();

        public void Dispose()
        {
            Interlocked.Increment(ref _tracker.DependencyDisposeCount);
        }
    }

    private sealed class DisposalTracker
    {
        public int RepositoryDisposeCount;

        public int DependencyDisposeCount;
    }
}
