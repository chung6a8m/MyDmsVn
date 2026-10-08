using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Infrastructure.Persistence;
using RepoDb;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class RepositoryAndQueryIntegrationTests
{
    [SqlServerFact]
    public async Task Explicit_repository_uses_RepoDb_and_Dapper_sees_its_uncommitted_write()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            await CreateProbeTableAsync(database.ConnectionString);
            var services = new ServiceCollection();
            services
                .AddSqlPersistence(database.ConnectionString)
                .AddRepository<ITestProbeRepository, TestProbeRepository>()
                .AddRepoDbMapping<TestProbeMapping>();
            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IUnitOfWorkFactory>();
            using var unitOfWork = await factory.CreateAsync(CancellationToken.None);
            unitOfWork.BeginTransaction();

            var first = unitOfWork.Repository<ITestProbeRepository>();
            var second = unitOfWork.Repository<ITestProbeRepository>();
            var insertedId = await first.InsertAsync("uncommitted", CancellationToken.None);
            var context = Assert.IsAssignableFrom<ISqlExecutionContext>(unitOfWork);
            var value = await context.Connection.QuerySingleAsync<string>(
                "SELECT ProbeValue FROM dbo.P1TestProbe WHERE ProbeId = @insertedId;",
                new { insertedId },
                context.Transaction);

            Assert.Same(first, second);
            Assert.Equal("uncommitted", value);
            unitOfWork.Rollback();
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Unregistered_repository_is_rejected_instead_of_discovered()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var services = new ServiceCollection();
            services.AddSqlPersistence(database.ConnectionString);
            using var provider = services.BuildServiceProvider();
            using var unitOfWork = provider.GetRequiredService<IUnitOfWorkFactory>().Create();

            var exception = Assert.Throws<InvalidOperationException>(
                () => unitOfWork.Repository<ITestProbeRepository>());

            Assert.Contains("not explicitly registered", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task RepoDb_mapping_initialization_is_thread_safe_and_retries_after_failure()
    {
        var mapping = new FailOnceMapping();
        var initializer = new RepoDbMappingInitializer(new IRepoDbMapping[] { mapping });

        Assert.Throws<InvalidOperationException>(() => initializer.Initialize());

        var attempts = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => initializer.Initialize()));
        await Task.WhenAll(attempts);

        Assert.Equal(2, mapping.Attempts);
    }

    private static async Task CreateProbeTableAsync(string connectionString)
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.ExecuteAsync(
            "CREATE TABLE dbo.P1TestProbe (" +
            "ProbeId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_P1TestProbe PRIMARY KEY, " +
            "ProbeValue nvarchar(128) NOT NULL);");
    }

    private static Task<SqlTestDatabase> CreateDatabaseAsync()
    {
        return SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
    }

    private interface ITestProbeRepository
    {
        Task<int> InsertAsync(string value, CancellationToken cancellationToken);
    }

    private sealed class TestProbeRepository : ITestProbeRepository
    {
        private readonly ISqlExecutionContext _context;

        public TestProbeRepository(ISqlExecutionContext context)
        {
            _context = context;
        }

        public Task<int> InsertAsync(string value, CancellationToken cancellationToken)
        {
            var transaction = _context.Transaction
                ?? throw new InvalidOperationException("This repository write requires an active transaction.");
            return _context.Connection.InsertAsync<TestProbe, int>(
                new TestProbe { Value = value },
                transaction: transaction,
                cancellationToken: cancellationToken);
        }
    }

    private sealed class TestProbe
    {
        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;
    }

    private sealed class TestProbeMapping : IRepoDbMapping
    {
        public void Configure()
        {
            ClassMapper.Add<TestProbe>("dbo.P1TestProbe");
            PropertyMapper.Add<TestProbe>(entity => entity.Id, "ProbeId");
            PropertyMapper.Add<TestProbe>(entity => entity.Value, "ProbeValue");
            PrimaryMapper.Add<TestProbe>(entity => entity.Id);
            IdentityMapper.Add<TestProbe>(entity => entity.Id);
        }
    }

    private sealed class FailOnceMapping : IRepoDbMapping
    {
        private int _attempts;

        public int Attempts => _attempts;

        public void Configure()
        {
            if (Interlocked.Increment(ref _attempts) == 1)
            {
                throw new InvalidOperationException("Expected first-attempt failure.");
            }
        }
    }
}
