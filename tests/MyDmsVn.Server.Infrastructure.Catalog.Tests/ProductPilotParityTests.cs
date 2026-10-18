using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Testing.Sql;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Historical logical IDs retained; these assertions now exercise the sole production implementation.
[Collection(CatalogSqlCollectionFixture.CollectionName)]
public sealed class ProductPilotParityTests(CatalogSqlCollectionFixture fixture, ITestOutputHelper output)
{
    [SqlServerFact]
    public async Task Product_pilot_preserves_identity_audit_allowlists_updates_inactive_history_and_errors()
    {
            await fixture.RunAsync(nameof(Product_pilot_preserves_identity_audit_allowlists_updates_inactive_history_and_errors), output, async (scope, token) =>
            {
                using var connections = new SqlCommandObserverConnectionFactory(scope.ConnectionString);
                using var provider = Services(scope.ConnectionString, connections);
                using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); uow.BeginTransaction();
                var writes = uow.Repository<ICatalogWriteRepository>();
                var queries = provider.GetRequiredService<IProductQueryService>();
                var product = new Product { Id = 9876, Code = "P-01", Name = "Hàng Việt", Unit = "cái", CreatedAtUtc = new DateTime(2000, 1, 1), CreatedByUserId = 42, UpdatedAtUtc = new DateTime(2001, 1, 1), UpdatedByUserId = 99 };
                var id = await writes.InsertProductAsync(product, token); Assert.True(id > 0); Assert.NotEqual(9876, id);
                var context = (Persistence.ISqlExecutionContext)uow; var connection = (SqlConnection)context.Connection; var tx = (SqlTransaction)context.Transaction!;
                Assert.NotEqual(product.CreatedAtUtc, await NativeSql.Scalar<DateTime>(connection, tx, "SELECT CreatedAtUtc FROM dbo.Products WHERE ProductId=@id", NativeSql.P("@id", SqlDbType.Int, id)));
                Assert.Equal(1, await NativeSql.Scalar<int>(connection, tx, "SELECT COUNT(*) FROM dbo.Products WHERE ProductId=@id AND CreatedByUserId=42 AND UpdatedAtUtc IS NULL AND UpdatedByUserId IS NULL", NativeSql.P("@id", SqlDbType.Int, id)));
                product.Id = id; product.Name = "Đã sửa"; product.IsActive = false; product.CreatedByUserId = 999; product.UpdatedAtUtc = new DateTime(2026, 10, 18); product.UpdatedByUserId = 43;
                Assert.NotNull(await writes.UpdateProductAsync(product, token));
                Assert.Equal(1, await NativeSql.Scalar<int>(connection, tx, "SELECT COUNT(*) FROM dbo.Products WHERE ProductId=@id AND IsActive=1 AND CreatedByUserId=42 AND UpdatedByUserId=43", NativeSql.P("@id", SqlDbType.Int, id)));
                Assert.True(await writes.SetProductActiveAsync(id, false, new DateTime(2026, 10, 18), 43, token)); Assert.False(await writes.SetProductActiveAsync(int.MaxValue, false, new DateTime(2026, 10, 18), 43, token));
                Assert.Null(await writes.UpdateProductAsync(new Product { Id = int.MaxValue, Code = "MISSING", Name = "Missing", Unit = "EA" }, token));
                var error = await Assert.ThrowsAsync<CatalogWriteConflictException>(() => writes.InsertProductAsync(new Product { Code = "p-01", Name = "Duplicate", Unit = "EA" }, token));
                Assert.Equal(CatalogWriteConflict.DuplicateCode, error.Conflict);
                uow.Commit();
                var found = await queries.GetByIdAsync(id, token); Assert.NotNull(found); Assert.Equal("Đã sửa", found!.Name); Assert.False(found.IsActive);
                Assert.Null(await queries.GetByIdAsync(int.MaxValue, token)); Assert.Empty(await queries.LookupAsync(new CatalogLookupRequest(null, 3), token));
            });
    }

    [SqlServerFact]
    public async Task Product_pilot_preserves_literal_unicode_search_stable_pages_and_one_long_offset_batch()
    {
            await fixture.RunAsync(nameof(Product_pilot_preserves_literal_unicode_search_stable_pages_and_one_long_offset_batch), output, async (scope, token) =>
            {
                using var oracle = new SqlConnection(scope.ConnectionString); await oracle.OpenAsync(token);
                await NativeSql.Execute(oracle, null, "INSERT dbo.Products(Code,Name,Unit,IsActive) VALUES (N'A_%[',N'Hàng Việt',N'EA',1),(N'B',N'ordinary',N'EA',1),(N'C',N'percent%',N'EA',1),(N'D',N'under_score',N'EA',1),(N'E',N'bracket[',N'EA',1),(N'F',N'inactive',N'EA',0);");
                using var connections = new SqlCommandObserverConnectionFactory(scope.ConnectionString);
                using var provider = Services(scope.ConnectionString, connections);
                var queries = provider.GetRequiredService<IProductQueryService>();
                foreach (var search in new string?[] { null, "", "   ", "%", "_", "[", "Việt", " ordinary " })
                {
                    var normalized = string.IsNullOrWhiteSpace(search) ? null : search!.Trim();
                    var expected = await NativeSql.Ids(oracle, "SELECT ProductId FROM dbo.Products WHERE IsActive=1 AND (@s IS NULL OR CHARINDEX(@s,Code)>0 OR CHARINDEX(@s,Name)>0) ORDER BY Code,ProductId", NativeSql.P("@s", SqlDbType.NVarChar, normalized));
                    connections.Commands.Clear();
                    var list = await queries.ListAsync(new CatalogListRequest(search, 1, 2, false), token);
                    Assert.Equal(expected.Length, list.TotalCount); Assert.Equal(expected.Take(2), list.Items.Select(x => x.Id));
                    Assert.Equal(1L, connections.RoundTrips);
                    var batch = Assert.Single(connections.Commands); Assert.Contains("COUNT_BIG", batch.Sql, StringComparison.Ordinal); Assert.Contains("BigInt", batch.Parameters, StringComparison.Ordinal);
                    output.WriteLine("Product list provider=" + "production" + " observed one COUNT_BIG/OFFSET batch; parameter types=" + batch.Parameters);
                    Assert.Equal(expected.Take(3), (await queries.LookupAsync(new CatalogLookupRequest(search, 3), token)).Select(x => x.Id));
                }
                var all = await queries.ListAsync(new CatalogListRequest(null, 2, 2, true), token); Assert.Equal(6, all.TotalCount); Assert.Equal(new[] { "C", "D" }, all.Items.Select(x => x.Code));
                var far = await queries.ListAsync(new CatalogListRequest(null, int.MaxValue, 100, true), token); Assert.Equal(6, far.TotalCount); Assert.Empty(far.Items); Assert.Equal(1L, connections.RoundTrips);
                output.WriteLine("Product pilot provider=" + "production" + " list roundtrips=1; offset=Int64; count=Int64; rows materialized; literal/Unicode search verified.");
            });
    }
    private static ServiceProvider Services(string cs, IDbConnectionFactory connections) { var s = new ServiceCollection(); s.AddSqlPersistence(cs); s.Replace(ServiceDescriptor.Singleton(connections)); return s.BuildServiceProvider(); }
}
