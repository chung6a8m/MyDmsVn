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

[CollectionDefinition(ApplicationSqlCollectionFixture.CollectionName)]
public sealed class ApplicationSqlDataCollection : ICollectionFixture<ApplicationSqlCollectionFixture> { }

// Linked into each owner: one lazy, independently owned allocation per assembly/collection.
// Mixed-class fresh exceptions never evaluate the lazy fixture and retain their own helper.
public sealed class ApplicationSqlCollectionFixture : IAsyncLifetime
{
    internal const string CollectionName = "Application SQL data";
    internal const string FaultEnvironmentVariable = "MYDMSVN_SQL_TEST_APPLICATION_PROBE";
    private readonly IMessageSink _diagnostics;
    private readonly Lazy<ReusableSqlFixture> _fixture;
    private int _reported;
    private string? _fault;

    public ApplicationSqlCollectionFixture(IMessageSink diagnosticMessageSink)
    {
        _diagnostics = diagnosticMessageSink;
        _fixture = new Lazy<ReusableSqlFixture>(() =>
        {
            _fault = Environment.GetEnvironmentVariable(FaultEnvironmentVariable);
            if (!string.IsNullOrEmpty(_fault) && !new[]
                { "initialize-failure", "reset-failure", "seed-failure", "schema-failure", "trigger-failure", "cleanup-failure" }
                .Contains(_fault, StringComparer.Ordinal))
            {
                throw new ArgumentException("Unknown application fixture fault probe.");
            }
            return ReusableSqlFixture.CreateApplication(
                migrate: _fault == "initialize-failure"
                    ? (_, _) => throw new InvalidOperationException("Expected application initialization failure.") : null,
                prepare: async (database, token) =>
                {
                    var ready = false;
                    var profile = await RespawnResetProfile.CreateAsync(database,
                        afterTriggerDisabled: (_, _) =>
                        {
                            if (ready && _fault == "reset-failure")
                            {
                                throw new InvalidOperationException("Expected application reset failure.");
                            }
                            return Task.CompletedTask;
                        }, cancellationToken: token);
                    ready = true;
                    return profile;
                });
        });
    }

    internal ReusableSqlFixture Fixture => _fixture.Value;
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        if (!_fixture.IsValueCreated) { return; }
        try { await Fixture.DisposeAsync(); }
        finally { Report(message => _diagnostics.OnMessage(new DiagnosticMessage(message))); }
    }

    internal async Task RunAsync(string label, ITestOutputHelper output, Func<SqlFixtureTestScope, Task> scenario,
        Func<SqlFixtureTestScope, Task>? seed = null)
    {
        try
        {
            await Fixture.RunTestAsync(label, async (scope, token) =>
            {
                await AssertEmptyAsync(scope, token);
                if (seed != null) { await seed(scope); }
                if (_fault == "seed-failure")
                {
                    await SeedContaminationAsync(scope);
                    throw new InvalidOperationException("Expected application seed failure.");
                }
                if (_fault == "schema-failure" || _fault == "trigger-failure")
                {
                    using var connection = new SqlConnection(scope.ConnectionString);
                    await connection.ExecuteAsync(new CommandDefinition(_fault == "schema-failure"
                        ? "ALTER TABLE dbo.Users ADD FixtureProbeOnly int NULL;"
                        : "DISABLE TRIGGER dbo.TR_StockLedger_AppendOnly ON dbo.StockLedger;", cancellationToken: token));
                }
            }, async (scope, token) =>
            {
                if (_fault == "cleanup-failure") { Track(scope, new FailingCleanupProbe()); }
                output.WriteLine("SQL fixture body-entered case=" + label);
                await scenario(scope);
            });
        }
        finally { if (_fixture.IsValueCreated) { Report(output.WriteLine); } }
    }

    internal async Task RunAsync<T>(string label, ITestOutputHelper output, Func<SqlFixtureTestScope, Task<T>> seed,
        Func<SqlFixtureTestScope, T, Task> scenario)
    {
        T value = default!;
        await RunAsync(label, output, scope => scenario(scope, value), async scope => { value = await seed(scope); });
    }

    private sealed class FailingCleanupProbe : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("Expected application cleanup failure.");
    }

    internal const string ExpectedScenarioFailure = "Expected scenario assertion after committed full-schema contamination.";

    internal static async Task AssertExpectedScenarioFailureAsync(Func<Task> scenario)
    {
        var error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(scenario);
        Assert.Equal(ExpectedScenarioFailure, error.Message);
    }

    internal static T Track<T>(SqlFixtureTestScope scope, T resource) where T : IDisposable
    {
        scope.RegisterCleanup(() => { resource.Dispose(); return Task.CompletedTask; });
        return resource;
    }

    internal static readonly string[] MutableTables =
    {
        "Users", "Roles", "UserRoles", "RolePermissions", "UserPermissions", "SystemSecuritySettings",
        "Products", "Warehouses", "Employees", "Customers", "GoodsReceipts", "GoodsReceiptLines",
        "StockLedger", "StockBalances", "SecurityAuditEvents",
    };

    internal static async Task AssertEmptyAsync(SqlFixtureTestScope scope, CancellationToken token)
    {
        using var connection = new SqlConnection(scope.ConnectionString);
        foreach (var table in MutableTables)
        {
            Assert.Equal(0, await connection.QuerySingleAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM dbo.[" + table + "];", cancellationToken: token)));
        }
    }

    internal static async Task SeedContaminationAsync(SqlFixtureTestScope scope)
    {
        using var connection = new SqlConnection(scope.ConnectionString);
        await connection.ExecuteAsync(ContaminationSql);
        foreach (var table in MutableTables)
        {
            Assert.True(await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.[" + table + "];") > 0);
        }
    }

    private void Report(Action<string> write)
    {
        var observations = Fixture.Observations;
        while (_reported < observations.Count) { write(observations[_reported++].ToString()); }
    }

    private const string ContaminationSql = @"
DECLARE @u int, @r int, @w int, @p int, @e int, @g bigint, @l bigint;
INSERT dbo.Users (Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm)
VALUES (N'probe',N'PROBE',N'Probe',N'Local',N'hash',N'',N'BCrypt');
SET @u=SCOPE_IDENTITY();
INSERT dbo.Roles(RoleName,NormalizedRoleName) VALUES(N'probe',N'PROBE'); SET @r=SCOPE_IDENTITY();
INSERT dbo.UserRoles(UserId,RoleId) VALUES(@u,@r);
INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@r,N'Catalog.Products.Read');
INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@u,N'Catalog.Products.Read',0);
INSERT dbo.SystemSecuritySettings(Id,SuperuserUserId,BoundNormalizedUsername) VALUES(1,@u,N'PROBE');
INSERT dbo.Warehouses(Code,Name) VALUES(N'W',N'Warehouse'); SET @w=SCOPE_IDENTITY();
INSERT dbo.Products(Code,Name,Unit) VALUES(N'P',N'Product',N'EA'); SET @p=SCOPE_IDENTITY();
INSERT dbo.Employees(Code,Name,UserId) VALUES(N'E',N'Employee',@u); SET @e=SCOPE_IDENTITY();
INSERT dbo.Customers(Code,Name) VALUES(N'C',N'Customer');
INSERT dbo.GoodsReceipts(ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,CreatedByUserId)
VALUES(N'probe-receipt','20261018',@w,@e,@u); SET @g=SCOPE_IDENTITY();
INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId)
VALUES(@g,1,@p,2,3,@u); SET @l=SCOPE_IDENTITY();
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId)
VALUES(@w,@p,'GoodsReceipt',@g,@l,2,SYSUTCDATETIME(),@u);
INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity) VALUES(@w,@p,2);
INSERT dbo.SecurityAuditEvents(ActorUserId,TargetType,TargetId,Operation,CorrelationId)
VALUES(@u,N'User',@u,N'Probe',NEWID());
UPDATE dbo.Users SET AssignmentRevision=7;
UPDATE dbo.Roles SET AssignmentRevision=9;
SELECT NEXT VALUE FOR dbo.GoodsReceiptNumberSequence;";
}
