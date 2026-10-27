using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Domain.Sales;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SalesReceiptMappingTests
{
    [SqlServerFact]
    public Task Registered_sales_mapping_round_trips_precision_defaults_and_sql_owned_version_on_one_transaction() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle);
        using var provider = data.Provider(database.ConnectionString);
        using (var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create())
        {
            uow.BeginTransaction();
            var repo = uow.Repository<ISalesReceiptWriteRepository>();
            var header = await repo.InsertDraftAsync(new SalesReceipt
            {
                SaleDate = new DateTime(2026, 10, 22),
                CustomerId = data.Customer,
                WarehouseId = data.Warehouse,
                EmployeeId = data.Employee,
                CreatedByUserId = data.Actor,
                CreatedAtUtc = new DateTime(2001, 1, 1),
                Version = new byte[8]
            },
                new[] { new SalesReceiptLine { LineNo = 1, ProductId = data.Product, Unit = "EA", Qty = .0001m, UnitPrice = .5m, LineAmount = .0001m, CreatedByUserId = data.Actor } }, CancellationToken.None);
            var context = (ISqlExecutionContext)uow;
            using var db = provider.GetRequiredService<ILinqToDbSessionFactory>().Borrow(context, true);
            var line = await db.GetTable<SalesReceiptLine>().SingleAsync();
            Assert.Equal(8, header.Version.Length); Assert.NotEqual(new byte[8], header.Version);
            Assert.NotEqual(new DateTime(2001, 1, 1), header.CreatedAtUtc);
            Assert.True(line.SaleLineId > 0); Assert.Equal(header.SaleId, line.SaleId);
            Assert.Equal(.0001m, line.Qty); Assert.Equal(.5m, line.UnitPrice); Assert.Equal(.0001m, line.LineAmount);
            Assert.Null(line.PostedUnitCost); Assert.Null(line.CostAmount);
            Assert.Equal(header.SaleId, await NativeSql.Scalar<long>((SqlConnection)context.Connection, (SqlTransaction)context.Transaction!, "SELECT SaleId FROM dbo.SalesReceipts"));
            Assert.Equal(line.SaleLineId, await NativeSql.Scalar<long>((SqlConnection)context.Connection, (SqlTransaction)context.Transaction!, "SELECT SaleLineId FROM dbo.SalesReceiptLines"));
            Assert.Equal(header.Version, await NativeSql.Scalar<byte[]>((SqlConnection)context.Connection, (SqlTransaction)context.Transaction!, "SELECT RowVersion FROM dbo.SalesReceipts"));
            await db.GetTable<SalesReceiptLine>().Set(row => row.PostedUnitCost, 99999999999999999999.99999999m).Set(row => row.CostAmount, .0001m).UpdateAsync();
            var costLine = await db.GetTable<SalesReceiptLine>().SingleAsync();
            Assert.Equal(99999999999999999999.99999999m, costLine.PostedUnitCost);
            Assert.Equal(.0001m, costLine.CostAmount);
            db.Dispose();
            uow.Rollback();
        }
        Assert.Equal(0, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SalesReceipts"));
        Assert.Equal(0, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SalesReceiptLines"));
    });

    [Fact]
    public void Sales_queries_and_typed_repository_are_registered_without_a_second_mapping_provider()
    {
        var services = new ServiceCollection(); services.AddSqlPersistence("Server=invalid;Database=unused;Integrated Security=true");
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ISalesReceiptQueryService>());
        Assert.Single(provider.GetServices<ILinqToDbMapping>(), mapping => mapping.GetType().Name == "InventoryLinqToDbMapping");
        var mappings = provider.GetRequiredService<LinqToDbMappingSchemaProvider>().GetSchema();
        Assert.Equal("SalesReceipts", mappings.GetEntityDescriptor(typeof(SalesReceipt)).TableName);
        Assert.Equal("SalesReceiptLines", mappings.GetEntityDescriptor(typeof(SalesReceiptLine)).TableName);
    }
}
