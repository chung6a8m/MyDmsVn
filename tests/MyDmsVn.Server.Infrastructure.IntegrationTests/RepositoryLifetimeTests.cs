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
    public async System.Threading.Tasks.Task Repository_uses_caller_scoped_context_without_taking_ownership_of_it()
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
            var callerScope = provider.CreateScope();
            var tracker = provider.GetRequiredService<DisposalTracker>();
            try
            {
                var callerDependency = callerScope.ServiceProvider.GetRequiredService<ScopedRepositoryDependency>();
                callerDependency.Value = "caller-context";
                var factory = callerScope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
                using (var unitOfWork = factory.Create())
                {
                    var repository = unitOfWork.Repository<IRepositoryWithScopedDependency>();
                    Assert.Equal(callerDependency.Id, repository.DependencyId);
                    Assert.Equal("caller-context", repository.DependencyValue);
                }

                Assert.Equal(1, tracker.RepositoryDisposeCount);
                Assert.Equal(0, tracker.DependencyDisposeCount);
            }
            finally
            {
                callerScope.Dispose();
            }

            Assert.Equal(1, tracker.DependencyDisposeCount);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private interface IRepositoryWithScopedDependency
    {
        Guid DependencyId { get; }

        string DependencyValue { get; }
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
            DependencyValue = dependency.Value;
            _tracker = tracker;
        }

        public Guid DependencyId { get; }

        public string DependencyValue { get; }

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

        public string Value { get; set; } = string.Empty;

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
