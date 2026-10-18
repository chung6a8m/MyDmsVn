using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[CollectionDefinition(CatalogSqlCollectionFixture.CollectionName)]
public sealed class CatalogSqlCollection : ICollectionFixture<CatalogSqlCollectionFixture> { }

// Every Catalog business case and adapter probe belongs to this one collection.
public sealed class CatalogSqlCollectionFixture : IAsyncLifetime
{
    internal const string CollectionName = "Catalog SQL";
    internal const string FaultEnvironmentVariable = "MYDMSVN_SQL_TEST_ADAPTER_PROBE";
    private readonly IMessageSink _diagnostics;
    private readonly string? _fault;
    private int _reported;
    private bool _resetFaultEnabled;

    public CatalogSqlCollectionFixture(IMessageSink diagnosticMessageSink)
    {
        _diagnostics = diagnosticMessageSink;
        _fault = Environment.GetEnvironmentVariable(FaultEnvironmentVariable);
        if (!string.IsNullOrEmpty(_fault) && !new[] { "initialize-failure", "seed-failure", "reset-failure", "cleanup-failure", "schema-failure", "trigger-failure" }.Contains(_fault, StringComparer.Ordinal))
        {
            throw new ArgumentException("Unknown SQL adapter fault probe.");
        }
        Fixture = ReusableSqlFixture.CreateApplication(
            migrate: _fault == "initialize-failure" ? (_, _) => throw new InvalidOperationException("Expected adapter initialization failure.") : null,
            prepare: (database, token) => RespawnResetProfile.CreateAsync(database, afterTriggerDisabled: (connection, cancellation) =>
            {
                if (_resetFaultEnabled) { throw new InvalidOperationException("Expected adapter reset failure."); }
                return Task.CompletedTask;
            }, cancellationToken: token));
    }

    internal ReusableSqlFixture Fixture { get; }

    public async Task InitializeAsync()
    {
        // SqlServerFact/Theory mark absent configuration NOT RUN; never allocate for skipped cases.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable))) { return; }
        try
        {
            await Fixture.InitializeAsync();
            _resetFaultEnabled = _fault == "reset-failure";
        }
        finally { Report(message => _diagnostics.OnMessage(new DiagnosticMessage(message))); }
    }

    public async Task DisposeAsync()
    {
        try { await Fixture.DisposeAsync(); }
        finally { Report(message => _diagnostics.OnMessage(new DiagnosticMessage(message))); }
    }

    internal async Task RunAsync(string label, ITestOutputHelper output,
        Func<SqlFixtureTestScope, CancellationToken, Task> scenario,
        Func<SqlFixtureTestScope, CancellationToken, Task>? seed = null)
    {
        try
        {
            await Fixture.RunTestAsync(label, async (scope, token) =>
            {
                using var connection = new SqlConnection(scope.ConnectionString);
                Assert.Equal(0, await connection.QuerySingleAsync<int>(new CommandDefinition(@"
SELECT (SELECT COUNT(*) FROM dbo.Users) + (SELECT COUNT(*) FROM dbo.Products)
     + (SELECT COUNT(*) FROM dbo.Warehouses) + (SELECT COUNT(*) FROM dbo.Employees)
     + (SELECT COUNT(*) FROM dbo.Customers);", cancellationToken: token)));
                if (seed != null) { await seed(scope, token); }
                if (_fault == "seed-failure") { throw new InvalidOperationException("Expected adapter seed failure."); }
                if (_fault == "schema-failure")
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "ALTER TABLE dbo.Products ADD FixtureProbeOnly int NULL;", cancellationToken: token));
                }
                if (_fault == "trigger-failure")
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "DISABLE TRIGGER dbo.TR_StockLedger_AppendOnly ON dbo.StockLedger;", cancellationToken: token));
                }
            }, async (scope, token) =>
            {
                if (_fault == "cleanup-failure") { Track(scope, new FailingCleanupProbe()); }
                output.WriteLine("SQL fixture body-entered case=" + label);
                await scenario(scope, token);
            });
        }
        finally { Report(output.WriteLine); }
    }

    // Resource cleanup is part of admission, so a failed Dispose poisons reuse.
    internal static T Track<T>(SqlFixtureTestScope scope, T resource) where T : IDisposable
    {
        scope.RegisterCleanup(() => { resource.Dispose(); return Task.CompletedTask; });
        return resource;
    }

    private sealed class FailingCleanupProbe : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("Expected adapter cleanup failure.");
    }

    private void Report(Action<string> write)
    {
        var observations = Fixture.Observations;
        while (_reported < observations.Count) { write(observations[_reported++].ToString()); }
    }
}
