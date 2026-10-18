using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.IntegrationTests.Pilot;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[Collection(CatalogSqlCollectionFixture.CollectionName)]
public sealed class CatalogLinqMigrationTests(CatalogSqlCollectionFixture fixture, ITestOutputHelper output)
{
    private static ServiceProvider Services(string cs, IDbConnectionFactory? connections = null)
    {
        var services = new ServiceCollection(); services.AddSqlPersistence(cs);
        if (connections != null) services.Replace(ServiceDescriptor.Singleton(connections));
        return services.BuildServiceProvider();
    }

    [SqlServerFact]
    public Task Registered_catalog_mapping_materializes_all_four_entities_with_native_audit_oracle() =>
        fixture.RunAsync(nameof(Registered_catalog_mapping_materializes_all_four_entities_with_native_audit_oracle), output, async (scope, token) =>
        {
            using var provider = Services(scope.ConnectionString);
            Assert.Contains(provider.GetServices<ILinqToDbMapping>(), m => m.GetType().Name == "CatalogLinqToDbMapping");
            using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); uow.BeginTransaction();
            var writes = uow.Repository<ICatalogWriteRepository>();
            var p = await writes.InsertProductAsync(new Product { Code = "P", Name = "Hàng Việt", Unit = "cái", CreatedByUserId = 41 }, token);
            var w = await writes.InsertWarehouseAsync(new Warehouse { Code = "W", Name = "Kho", CreatedByUserId = 41 }, token);
            var e = await writes.InsertEmployeeAsync(new Employee { Code = "E", Name = "Người", CreatedByUserId = 41 }, token);
            var c = await writes.InsertCustomerAsync(new Customer { Code = "C", Name = "Khách", CreatedByUserId = 41 }, token);
            using var db = provider.GetRequiredService<ILinqToDbSessionFactory>().Borrow((ISqlExecutionContext)uow, true);
            var product = await db.GetTable<Product>().SingleAsync(x => x.Id == p, token);
            var warehouse = await db.GetTable<Warehouse>().SingleAsync(x => x.Id == w, token);
            var employee = await db.GetTable<Employee>().SingleAsync(x => x.Id == e, token);
            var customer = await db.GetTable<Customer>().SingleAsync(x => x.Id == c, token);
            Assert.Equal("cái", product.Unit); Assert.Null(warehouse.Address); Assert.Null(employee.Phone); Assert.Null(employee.UserId);
            Assert.Null(customer.Address); Assert.Null(customer.Phone); Assert.Null(customer.TaxCode);
            var context = (ISqlExecutionContext)uow;
            foreach (var row in new[] { ("Products", "ProductId", p, product.CreatedAtUtc, product.CreatedByUserId), ("Warehouses", "WarehouseId", w, warehouse.CreatedAtUtc, warehouse.CreatedByUserId), ("Employees", "EmployeeId", e, employee.CreatedAtUtc, employee.CreatedByUserId), ("Customers", "CustomerId", c, customer.CreatedAtUtc, customer.CreatedByUserId) })
            {
                Assert.Equal(await PilotSql.Scalar<DateTime>((SqlConnection)context.Connection, (SqlTransaction)context.Transaction!, "SELECT CreatedAtUtc FROM dbo." + row.Item1 + " WHERE " + row.Item2 + "=@id", PilotSql.P("@id", SqlDbType.Int, row.Item3)), row.Item4);
                Assert.Equal(41, row.Item5);
            }
            foreach (var type in new[] { typeof(Product), typeof(Warehouse), typeof(Employee), typeof(Customer) })
            {
                Assert.DoesNotContain(type.GetProperties(), x => x.Name == "Version" || x.Name == "RowVersion");
                foreach (var column in db.MappingSchema.GetEntityDescriptor(type, null).Columns)
                {
                    var nullable = await PilotSql.Scalar<int>((SqlConnection)context.Connection, (SqlTransaction)context.Transaction!,
                        "SELECT CONVERT(int,is_nullable) FROM sys.columns WHERE object_id=OBJECT_ID(@table) AND name=@column",
                        PilotSql.P("@table", SqlDbType.NVarChar, "dbo." + type.Name + "s"), PilotSql.P("@column", SqlDbType.NVarChar, column.ColumnName));
                    Assert.Equal(nullable == 1, column.CanBeNull);
                }
            }
            db.Dispose(); uow.Rollback();
        });

    [SqlServerFact]
    public Task Other_catalog_writes_preserve_identity_nullable_audit_active_allowlists_and_conflicts() =>
        fixture.RunAsync(nameof(Other_catalog_writes_preserve_identity_nullable_audit_active_allowlists_and_conflicts), output, async (scope, token) =>
        {
            using var provider = Services(scope.ConnectionString); using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); uow.BeginTransaction();
            var r = uow.Repository<ICatalogWriteRepository>(); var ctx = (ISqlExecutionContext)uow; var sql = (SqlConnection)ctx.Connection; var tx = (SqlTransaction)ctx.Transaction!;
            var old = new DateTime(2000, 1, 1); var updated = new DateTime(2026, 10, 18);
            var w = new Warehouse { Id = 9999, Code = "W", Name = "Kho", CreatedAtUtc = old, CreatedByUserId = 41, UpdatedAtUtc = old, UpdatedByUserId = 999 };
            var e = new Employee { Id = 9999, Code = "E", Name = "Người", CreatedAtUtc = old, CreatedByUserId = 41, UpdatedAtUtc = old, UpdatedByUserId = 999 };
            var c = new Customer { Id = 9999, Code = "C", Name = "Khách", CreatedAtUtc = old, CreatedByUserId = 41, UpdatedAtUtc = old, UpdatedByUserId = 999 };
            w.Id = await r.InsertWarehouseAsync(w, token); e.Id = await r.InsertEmployeeAsync(e, token); c.Id = await r.InsertCustomerAsync(c, token);
            foreach (var id in new[] { w.Id, e.Id, c.Id }) { Assert.True(id > 0); Assert.NotEqual(9999, id); }
            foreach (var table in new[] { "Warehouses", "Employees", "Customers" })
                Assert.Equal(1, await PilotSql.Scalar<int>(sql, tx, "SELECT COUNT(*) FROM dbo." + table + " WHERE CreatedAtUtc>@old AND CreatedByUserId=41 AND UpdatedAtUtc IS NULL AND UpdatedByUserId IS NULL", PilotSql.P("@old", SqlDbType.DateTime2, old)));
            w.Name = "Kho sửa"; w.Address = "Địa chỉ"; w.IsActive = false; w.CreatedByUserId = 999; w.UpdatedAtUtc = updated; w.UpdatedByUserId = 42;
            e.Name = "Người sửa"; e.Phone = "0123"; e.IsActive = false; e.CreatedByUserId = 999; e.UpdatedAtUtc = updated; e.UpdatedByUserId = 42;
            c.Name = "Khách sửa"; c.Address = "Địa chỉ"; c.Phone = "0456"; c.TaxCode = "TAX"; c.IsActive = false; c.CreatedByUserId = 999; c.UpdatedAtUtc = updated; c.UpdatedByUserId = 42;
            var ww = await r.UpdateWarehouseAsync(w, token); var ee = await r.UpdateEmployeeAsync(e, token); var cc = await r.UpdateCustomerAsync(c, token);
            Assert.NotNull(ww); Assert.NotNull(ee); Assert.NotNull(cc); Assert.Equal("Địa chỉ", ww!.Address); Assert.Equal("0123", ee!.Phone); Assert.Equal("TAX", cc!.TaxCode);
            foreach (var table in new[] { "Warehouses", "Employees", "Customers" })
                Assert.Equal(1, await PilotSql.Scalar<int>(sql, tx, "SELECT COUNT(*) FROM dbo." + table + " WHERE IsActive=1 AND CreatedByUserId=41 AND CreatedAtUtc>@old AND UpdatedAtUtc=@updated AND UpdatedByUserId=42", PilotSql.P("@old", SqlDbType.DateTime2, old), PilotSql.P("@updated", SqlDbType.DateTime2, updated)));
            Assert.True(await r.SetWarehouseActiveAsync(w.Id, false, updated, 43, token)); Assert.True(await r.SetEmployeeActiveAsync(e.Id, false, updated, 43, token)); Assert.True(await r.SetCustomerActiveAsync(c.Id, false, updated, 43, token));
            Assert.False(await r.SetWarehouseActiveAsync(int.MaxValue, false, updated, 43, token)); Assert.False(await r.SetEmployeeActiveAsync(int.MaxValue, false, updated, 43, token)); Assert.False(await r.SetCustomerActiveAsync(int.MaxValue, false, updated, 43, token));
            Assert.Null(await r.UpdateWarehouseAsync(new Warehouse { Id = int.MaxValue, Code = "missing", Name = "Missing" }, token)); Assert.Null(await r.UpdateEmployeeAsync(new Employee { Id = int.MaxValue, Code = "missing", Name = "Missing" }, token)); Assert.Null(await r.UpdateCustomerAsync(new Customer { Id = int.MaxValue, Code = "missing", Name = "Missing" }, token));
            foreach (var action in new Func<Task<int>>[] { () => r.InsertWarehouseAsync(new Warehouse { Code = "w", Name = "Duplicate" }, token), () => r.InsertEmployeeAsync(new Employee { Code = "e", Name = "Duplicate" }, token), () => r.InsertCustomerAsync(new Customer { Code = "c", Name = "Duplicate" }, token) })
                Assert.Equal(CatalogWriteConflict.DuplicateCode, (await Assert.ThrowsAsync<CatalogWriteConflictException>(action)).Conflict);
            // Clear nullable fields explicitly; ordinary edits must preserve inactive state.
            w.Address = null; e.Phone = null; c.Address = null; c.Phone = null; c.TaxCode = null;
            Assert.Null((await r.UpdateWarehouseAsync(w, token))!.Address); Assert.Null((await r.UpdateEmployeeAsync(e, token))!.Phone); Assert.Null((await r.UpdateCustomerAsync(c, token))!.TaxCode);
            uow.Commit();
            Assert.False((await provider.GetRequiredService<IWarehouseQueryService>().GetByIdAsync(w.Id, token))!.IsActive);
            Assert.False((await provider.GetRequiredService<IEmployeeQueryService>().GetByIdAsync(e.Id, token))!.IsActive);
            Assert.False((await provider.GetRequiredService<ICustomerQueryService>().GetByIdAsync(c.Id, token))!.IsActive);
        });

    [SqlServerFact]
    public Task Employee_linkage_errors_translate_for_insert_update_and_nullable_unlink() =>
        fixture.RunAsync(nameof(Employee_linkage_errors_translate_for_insert_update_and_nullable_unlink), output, async (scope, token) =>
        {
            using var provider = Services(scope.ConnectionString); using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); uow.BeginTransaction();
            var ctx = (ISqlExecutionContext)uow; var sql = (SqlConnection)ctx.Connection; var tx = (SqlTransaction)ctx.Transaction!;
            var user = await PilotSql.Scalar<int>(sql, tx, "INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'link.user',N'LINK.USER',N'Link',N'Local',N'hash',N'',N'BCrypt')");
            var r = uow.Repository<ICatalogWriteRepository>(); var id = await r.InsertEmployeeAsync(new Employee { Code = "E1", Name = "First", UserId = user }, token);
            var second = await r.InsertEmployeeAsync(new Employee { Code = "E2", Name = "Second" }, token);
            foreach (var update in new[] { false, true })
                foreach (var pair in new[] { (user, CatalogWriteConflict.EmployeeUserAlreadyLinked), (int.MaxValue, CatalogWriteConflict.EmployeeUserNotFound) })
                {
                    var employee = new Employee { Id = second, Code = "E2", Name = "Conflict", UserId = pair.Item1 };
                    var error = await Assert.ThrowsAsync<CatalogWriteConflictException>(async () => { if (update) await r.UpdateEmployeeAsync(employee, token); else await r.InsertEmployeeAsync(new Employee { Code = "NEW", Name = "Conflict", UserId = pair.Item1 }, token); });
                    Assert.Equal(pair.Item2, error.Conflict);
                }
            var duplicate = await Assert.ThrowsAsync<CatalogWriteConflictException>(() => r.UpdateEmployeeAsync(new Employee { Id = second, Code = "e1", Name = "Duplicate" }, token)); Assert.Equal(CatalogWriteConflict.DuplicateCode, duplicate.Conflict);
            Assert.Null((await r.UpdateEmployeeAsync(new Employee { Id = id, Code = "E1", Name = "Unlinked", UserId = null }, token))!.UserId);
            Assert.NotNull(await r.UpdateEmployeeAsync(new Employee { Id = second, Code = "E2", Name = "Linked", UserId = user }, token));
            Assert.Equal(1, await PilotSql.Scalar<int>(sql, tx, "SELECT COUNT(*) FROM dbo.Employees WHERE UserId=@user", PilotSql.P("@user", SqlDbType.Int, user)));
            uow.Rollback();
        });

    [SqlServerFact]
    public Task All_catalog_queries_preserve_literal_unicode_search_order_long_pages_and_limits() =>
        fixture.RunAsync(nameof(All_catalog_queries_preserve_literal_unicode_search_order_long_pages_and_limits), output, async (scope, token) =>
        {
            using var oracle = new SqlConnection(scope.ConnectionString); await oracle.OpenAsync(token);
            foreach (var table in new[] { "Products", "Warehouses", "Employees", "Customers" })
                await PilotSql.Execute(oracle, null, "INSERT dbo." + table + "(Code,Name,IsActive" + (table == "Products" ? ",Unit" : "") + ") VALUES " + string.Join(",", new[] { ("A_%[", "Hàng Việt", 1), ("B", "ordinary", 1), ("C", "percent%", 1), ("D", "under_score", 1), ("E", "bracket[", 1), ("F", "inactive", 0) }.Select(x => "(N'" + x.Item1 + "',N'" + x.Item2 + "'," + x.Item3 + (table == "Products" ? ",N'EA'" : "") + ")")));
            using var connections = new PilotConnections(scope.ConnectionString); using var provider = Services(scope.ConnectionString, connections);
            var p = provider.GetRequiredService<IProductQueryService>(); var w = provider.GetRequiredService<IWarehouseQueryService>(); var e = provider.GetRequiredService<IEmployeeQueryService>(); var c = provider.GetRequiredService<ICustomerQueryService>();
            var cases = new QueryCase[]{
                new("Products","ProductId",async req=>{var r=await p.ListAsync(req,token);return(r.TotalCount,r.Items.Select(x=>x.Id).ToArray());},req=>p.LookupAsync(req,token),async id=>await p.GetByIdAsync(id,token)!=null),
                new("Warehouses","WarehouseId",async req=>{var r=await w.ListAsync(req,token);return(r.TotalCount,r.Items.Select(x=>x.Id).ToArray());},req=>w.LookupAsync(req,token),async id=>await w.GetByIdAsync(id,token)!=null),
                new("Employees","EmployeeId",async req=>{var r=await e.ListAsync(req,token);return(r.TotalCount,r.Items.Select(x=>x.Id).ToArray());},req=>e.LookupAsync(req,token),async id=>await e.GetByIdAsync(id,token)!=null),
                new("Customers","CustomerId",async req=>{var r=await c.ListAsync(req,token);return(r.TotalCount,r.Items.Select(x=>x.Id).ToArray());},req=>c.LookupAsync(req,token),async id=>await c.GetByIdAsync(id,token)!=null)};
            foreach (var q in cases)
            {
                foreach (var search in new string?[] { null, "", "   ", "%", "_", "[", "Việt", " ordinary " })
                {
                    var s = string.IsNullOrWhiteSpace(search) ? null : search!.Trim(); var expected = await PilotSql.Ids(oracle, "SELECT " + q.Key + " FROM dbo." + q.Table + " WHERE IsActive=1 AND (@s IS NULL OR CHARINDEX(@s,Code)>0 OR CHARINDEX(@s,Name)>0) ORDER BY Code," + q.Key, PilotSql.P("@s", SqlDbType.NVarChar, s));
                    connections.Commands.Clear(); var result = await q.List(new CatalogListRequest(search, 1, 2, false));
                    Assert.Equal(expected.Length, result.Total); Assert.Equal(expected.Take(2), result.Ids); Assert.Equal(1L, connections.RoundTrips);
                    var command = Assert.Single(connections.Commands); Assert.Contains("COUNT_BIG", command.Sql, StringComparison.Ordinal); Assert.Contains("BigInt", command.Parameters, StringComparison.Ordinal);
                    Assert.Equal(expected.Take(3), (await q.Lookup(new CatalogLookupRequest(search, 3))).Select(x => x.Id));
                }
                var all = await q.List(new CatalogListRequest(null, 2, 2, true)); Assert.Equal(6, all.Total);
                var ordered = await PilotSql.Ids(oracle, "SELECT " + q.Key + " FROM dbo." + q.Table + " ORDER BY Code," + q.Key); Assert.Equal(ordered.Skip(2).Take(2), all.Ids);
                var far = await q.List(new CatalogListRequest(null, int.MaxValue, 100, true)); Assert.Equal(6, far.Total); Assert.Empty(far.Ids); Assert.Equal(1L, connections.RoundTrips);
                Assert.False(await q.Exists(int.MaxValue)); Assert.True(await q.Exists(ordered.Last()));
                output.WriteLine(q.Table + ": literal Unicode/null/blank search, one COUNT_BIG/Int64-offset batch, stable page and active-only limit preserved.");
            }
        });

    [SqlServerFact]
    public Task Catalog_writes_share_current_transaction_with_legacy_receipt_reference_reads_and_rollback() =>
        PilotSql.Owned(async database =>
        {
            using var provider = Services(database.ConnectionString); using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create();
            var catalog = uow.Repository<ICatalogWriteRepository>(); var receipts = uow.Repository<IGoodsReceiptWriteRepository>(); uow.BeginTransaction();
            var p = await catalog.InsertProductAsync(new Product { Code = "P", Name = "Product", Unit = "EA" }, CancellationToken.None);
            var w = await catalog.InsertWarehouseAsync(new Warehouse { Code = "W", Name = "Warehouse" }, CancellationToken.None);
            var e = await catalog.InsertEmployeeAsync(new Employee { Code = "E", Name = "Employee" }, CancellationToken.None);
            var references = await receipts.GetReferenceSnapshotAsync(w, e, new[] { p }, CancellationToken.None);
            Assert.Equal(CatalogReferenceState.Active, references.Warehouse); Assert.Equal(CatalogReferenceState.Active, references.Employee); Assert.Equal(CatalogReferenceState.Active, references.Products[p]);
            var ctx = (ISqlExecutionContext)uow; var sql = (SqlConnection)ctx.Connection; var tx = (SqlTransaction)ctx.Transaction!;
            Assert.Equal(3, await PilotSql.Scalar<int>(sql, tx, "SELECT (SELECT COUNT(*) FROM dbo.Products)+(SELECT COUNT(*) FROM dbo.Warehouses)+(SELECT COUNT(*) FROM dbo.Employees)"));
            using (var db = provider.GetRequiredService<ILinqToDbSessionFactory>().Borrow(ctx, true))
                Assert.Equal(await PilotSql.Scalar<int>(sql, tx, "SELECT CONVERT(int,@@SPID)"), await db.ExecuteAsync<int>("SELECT CONVERT(int,@@SPID)", CancellationToken.None));
            uow.Rollback(); using var owner = new SqlConnection(database.ConnectionString); await owner.OpenAsync(); Assert.Equal(0, await PilotSql.Scalar<int>(owner, null, "SELECT (SELECT COUNT(*) FROM dbo.Products)+(SELECT COUNT(*) FROM dbo.Warehouses)+(SELECT COUNT(*) FROM dbo.Employees)"));
        });

    [SqlServerFact]
    public Task Overlong_catalog_fields_fail_without_silent_truncation() =>
        fixture.RunAsync(nameof(Overlong_catalog_fields_fail_without_silent_truncation), output, async (scope, token) =>
        {
            using var provider = Services(scope.ConnectionString); using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); uow.BeginTransaction();
            var r = uow.Repository<ICatalogWriteRepository>();
            foreach (var insert in new Func<Task<int>>[]{
                ()=>r.InsertProductAsync(new Product{Code="P",Name="Too long",Unit=new string('x',33)},token),
                ()=>r.InsertWarehouseAsync(new Warehouse{Code="W",Name="Too long",Address=new string('x',501)},token),
                ()=>r.InsertEmployeeAsync(new Employee{Code="E",Name="Too long",Phone=new string('x',65)},token),
                ()=>r.InsertCustomerAsync(new Customer{Code="C",Name="Too long",TaxCode=new string('x',33)},token)})
            {
                var error = await Assert.ThrowsAsync<SqlException>(insert);
                Assert.True(error.Number == 2628 || error.Number == 8152);
            }
            var ctx = (ISqlExecutionContext)uow;
            Assert.Equal(0, await PilotSql.Scalar<int>((SqlConnection)ctx.Connection, (SqlTransaction)ctx.Transaction!, "SELECT (SELECT COUNT(*) FROM dbo.Products)+(SELECT COUNT(*) FROM dbo.Warehouses)+(SELECT COUNT(*) FROM dbo.Employees)+(SELECT COUNT(*) FROM dbo.Customers)"));
            uow.Rollback();
        });

    [SqlServerFact]
    public Task Cancellation_does_not_write_or_terminate_the_current_catalog_transaction() =>
        fixture.RunAsync(nameof(Cancellation_does_not_write_or_terminate_the_current_catalog_transaction), output, async (scope, token) =>
        {
            using var provider = Services(scope.ConnectionString); using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IProductQueryService>().ListAsync(new CatalogListRequest(null, 1, 10, false), cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IWarehouseQueryService>().GetByIdAsync(1, cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IEmployeeQueryService>().LookupAsync(new CatalogLookupRequest(null, 10), cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<ICustomerQueryService>().ListAsync(new CatalogListRequest(null, 1, 10, false), cancel.Token));
            using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); uow.BeginTransaction(); var r = uow.Repository<ICatalogWriteRepository>();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => r.InsertProductAsync(new Product { Code = "P", Name = "Cancelled", Unit = "EA" }, cancel.Token));
            Assert.Equal(UnitOfWorkState.ActiveTransaction, uow.State); var ctx = (ISqlExecutionContext)uow;
            Assert.Equal(0, await PilotSql.Scalar<int>((SqlConnection)ctx.Connection, (SqlTransaction)ctx.Transaction!, "SELECT COUNT(*) FROM dbo.Products")); uow.Rollback();
        });

    private sealed class QueryCase(string table, string key, Func<CatalogListRequest, Task<(long Total, int[] Ids)>> list, Func<CatalogLookupRequest, Task<IReadOnlyList<CatalogLookupDto>>> lookup, Func<int, Task<bool>> exists)
    { public string Table => table; public string Key => key; public Func<CatalogListRequest, Task<(long Total, int[] Ids)>> List => list; public Func<CatalogLookupRequest, Task<IReadOnlyList<CatalogLookupDto>>> Lookup => lookup; public Func<int, Task<bool>> Exists => exists; }
}
