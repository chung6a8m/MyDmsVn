using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Infrastructure.Persistence;
using LinqToDB;
using LinqToDB.Mapping;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class RepositoryAndQueryIntegrationTests
{
    [SqlServerFact]
    public async Task Explicit_repository_uses_Linq_and_native_oracle_sees_its_uncommitted_write()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            TestDatabaseMigrationRunner.Migrate(database.ConnectionString);
            var services = new ServiceCollection();
            services
                .AddSqlPersistence(database.ConnectionString)
                .AddRepository<ITestProbeRepository, TestProbeRepository>();
            services.AddSingleton<ILinqToDbMapping>(new TestProbeMapping());
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
    public async Task Linq_mapping_initialization_is_thread_safe_and_retries_after_failure()
    {
        var mapping = new FailOnceMapping();
        var initializer = new LinqToDbMappingSchemaProvider(new ILinqToDbMapping[] { mapping });

        Assert.Throws<InvalidOperationException>(() => initializer.GetSchema());

        var attempts = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => initializer.GetSchema()));
        await Task.WhenAll(attempts);

        Assert.Equal(2, mapping.Attempts);
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
        private readonly ILinqToDbSessionFactory _sessions;

        public TestProbeRepository(ISqlExecutionContext context, ILinqToDbSessionFactory sessions)
        {
            _context = context;
            _sessions = sessions;
        }

        public async Task<int> InsertAsync(string value, CancellationToken cancellationToken)
        {
            using var db = _sessions.Borrow(_context, true);
            return await db.GetTable<TestProbe>().Value(row => row.Value, value).InsertWithInt32IdentityAsync(cancellationToken)
                ?? throw new InvalidOperationException("Probe insert returned no identity.");
        }
    }

    private sealed class TestProbe
    {
        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;
    }

    private sealed class TestProbeMapping : ILinqToDbMapping
    {
        public void Configure(FluentMappingBuilder builder)
        {
            var probe = builder.Entity<TestProbe>().HasSchemaName("dbo").HasTableName("P1TestProbe");
            probe.Property(row => row.Id).HasColumnName("ProbeId").IsPrimaryKey().IsIdentity();
            probe.Property(row => row.Value).HasColumnName("ProbeValue").HasDataType(DataType.NVarChar).HasLength(128);
        }
    }

    private sealed class FailOnceMapping : ILinqToDbMapping
    {
        private int _attempts;

        public int Attempts => _attempts;

        public void Configure(FluentMappingBuilder builder)
        {
            if (Interlocked.Increment(ref _attempts) == 1)
            {
                throw new InvalidOperationException("Expected first-attempt failure.");
            }
        }
    }
}
