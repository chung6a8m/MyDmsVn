using System;
using System.Linq;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Data.SqlClient;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[Collection(CatalogSqlCollectionFixture.CollectionName)]
public sealed class CatalogFixtureReuseIntegrationTests
{
    private readonly CatalogSqlCollectionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CatalogFixtureReuseIntegrationTests(CatalogSqlCollectionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_scenario_and_reordered_business_bodies_receive_clean_collection_scopes(bool reverse)
    {
        var label = nameof(Failed_scenario_and_reordered_business_bodies_receive_clean_collection_scopes)
            + (reverse ? "/reverse" : "/forward");
        await Assert.ThrowsAnyAsync<XunitException>(() => _fixture.RunAsync(label + "/failed-sentinel", _output, async (scope, token) =>
            {
                await AssertEmptyAsync(scope, token);
                var connection = CatalogSqlCollectionFixture.Track(scope, new SqlConnection(scope.ConnectionString));
                await connection.ExecuteAsync(@"
INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm)
VALUES(N'sentinel',N'SENTINEL',N'Sentinel',N'Local',N'hash',N'',N'BCrypt');
INSERT dbo.Products(Code,Name,Unit) VALUES(N'SP001',N'Sentinel',N'Each');
INSERT dbo.Warehouses(Code,Name) VALUES(N'KHO01',N'Sentinel');
INSERT dbo.Employees(Code,Name,UserId) SELECT N'NV001',N'Sentinel',UserId FROM dbo.Users;
INSERT dbo.Customers(Code,Name) VALUES(N'KH001',N'Sentinel');");
                Assert.Fail("Expected scenario assertion after committed Catalog/User contamination.");
            }));
        Assert.False(_fixture.Fixture.IsPoisoned);

        var backend = new CatalogBackendIntegrationTests(_fixture, _output);
        var mapping = new CatalogMappingIntegrationTests(_fixture, _output);
        Func<Task>[] scenarios =
        {
            backend.Catalog_backend_crud_queries_audit_and_conflicts_use_real_sql_boundaries,
            backend.Explicitly_denied_catalog_write_leaves_business_tables_unchanged,
            backend.Inactive_catalog_rows_remain_historically_queryable_but_are_excluded_from_lookups,
            mapping.Catalog_entities_round_trip_explicit_audit_values_through_registered_Linq_mappings,
            mapping.Catalog_write_repository_uses_database_creation_time_instead_of_entity_creation_time,
        };
        if (reverse) { Array.Reverse(scenarios); }
        foreach (var scenario in scenarios)
        {
            var before = _fixture.Fixture.Observations.Count(x => x.Operation == "reset");
            await scenario();
            // A private fresh allocation could pass the body while bypassing the collection contract.
            Assert.Equal(before + 1, _fixture.Fixture.Observations.Count(x => x.Operation == "reset"));
        }
        await _fixture.RunAsync(label + "/next-baseline", _output, AssertEmptyAsync);
    }

    private static async Task AssertEmptyAsync(SqlFixtureTestScope scope, System.Threading.CancellationToken token)
    {
        var connection = CatalogSqlCollectionFixture.Track(scope, new SqlConnection(scope.ConnectionString));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(new SqlTestCommand(@"
SELECT (SELECT COUNT(*) FROM dbo.Users) + (SELECT COUNT(*) FROM dbo.Products)
     + (SELECT COUNT(*) FROM dbo.Warehouses) + (SELECT COUNT(*) FROM dbo.Employees)
     + (SELECT COUNT(*) FROM dbo.Customers);", cancellationToken: token)));
    }
}
