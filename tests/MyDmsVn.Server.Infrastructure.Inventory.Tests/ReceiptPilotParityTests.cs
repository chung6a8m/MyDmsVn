using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Testing.Sql;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class ReceiptPilotParityTests(ITestOutputHelper output)
{
    [SqlServerFact]
    public Task Receipt_pilot_commits_typed_and_native_writes_on_one_session_with_generated_tokens() => Run(false);
    [SqlServerFact]
    public Task Receipt_pilot_mid_ledger_failure_rolls_back_header_lines_ledger_and_balances() => Run(true);
    private async Task Run(bool fail)
    {
        // Historical IDs retained; commit/fault scenarios run the sole production implementation on owned fresh databases.
            await NativeSql.Owned(async database =>
            {
                using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
                var actor = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'pilot',N'PILOT',N'Pilot',N'Local',N'hash',N'',N'BCrypt')");
                var warehouse = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'WH',N'Warehouse')");
                var employee = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Employees(Code,Name) OUTPUT INSERTED.EmployeeId VALUES(N'EMP',N'Employee')");
                var p1 = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P1',N'One',N'EA')");
                var p2 = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
                var services = new ServiceCollection(); services.AddSqlPersistence(database.ConnectionString); using var provider = services.BuildServiceProvider();
                using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); uow.BeginTransaction(); var ctx = (ISqlExecutionContext)uow;
                var production = uow.Repository<IGoodsReceiptWriteRepository>(); await production.AcquirePostingGateAsync(CancellationToken.None); var sessions = provider.GetRequiredService<ILinqToDbSessionFactory>();
                var lines = new[] { new GoodsReceiptLine { LineNumber = 1, ProductId = p1, Quantity = 1.2500m, UnitCost = 19.9901m, CreatedByUserId = actor }, new GoodsReceiptLine { LineNumber = 2, ProductId = p2, Quantity = 2.5000m, UnitCost = 29.9902m, CreatedByUserId = actor } };
                var header = new GoodsReceipt { ReceiptDate = new DateTime(2026, 10, 18), WarehouseId = warehouse, EmployeeId = employee, CreatedByUserId = actor, CreatedAtUtc = new DateTime(2000, 1, 1), Version = new byte[] { 9 } };
                var conn = (SqlConnection)ctx.Connection; conn.StatisticsEnabled = true; conn.ResetStatistics();
                var saved = await production.InsertDraftAsync(header, lines, CancellationToken.None);
                Assert.True(saved.Id > 0); Assert.All(lines, l => Assert.True(l.Id > 0)); Assert.Equal(8, saved.Version.Length); Assert.NotEqual(header.CreatedAtUtc, saved.CreatedAtUtc); Assert.StartsWith("GR", saved.ReceiptNumber);
                output.WriteLine("Receipt insert provider=" + "production" + " observed server roundtrips=" + conn.RetrieveStatistics()["ServerRoundtrips"]);
                var tx = (SqlTransaction)ctx.Transaction!;
                Assert.Equal(2, await NativeSql.Scalar<int>(conn, tx, "SELECT COUNT(*) FROM dbo.GoodsReceiptLines WHERE ReceiptId=@id", NativeSql.P("@id", SqlDbType.BigInt, saved.Id)));
                Assert.Equal(1.2500m, await NativeSql.Scalar<decimal>(conn, tx, "SELECT Quantity FROM dbo.GoodsReceiptLines WHERE ReceiptId=@id AND [LineNo]=1", NativeSql.P("@id", SqlDbType.BigInt, saved.Id)));
                Assert.Equal(19.9901m, await NativeSql.Scalar<decimal>(conn, tx, "SELECT UnitCost FROM dbo.GoodsReceiptLines WHERE ReceiptId=@id AND [LineNo]=1", NativeSql.P("@id", SqlDbType.BigInt, saved.Id)));
                using (var borrowed = sessions.Borrow(ctx, true))
                    Assert.Equal(await NativeSql.Scalar<int>(conn, tx, "SELECT CONVERT(int,@@SPID)"), await LinqToDB.Data.DataContextExtensions.ExecuteAsync<int>(borrowed, "SELECT CONVERT(int,@@SPID)", CancellationToken.None));
                var locked = await production.LockHeaderAsync(saved.Id, CancellationToken.None); Assert.NotNull(locked);
                var snapshot = await production.LockPostSnapshotAsync(locked!, CancellationToken.None); Assert.Equal(2, snapshot.Lines.Count);
                await production.LockValuationBalancesAsync(warehouse, lines.Select(l => l.ProductId).ToArray(), CancellationToken.None);
                Assert.True(await production.TryMarkPostedAsync(saved.Id, saved.Version, new DateTime(2026, 10, 18), actor, CancellationToken.None));
                foreach (var line in lines)
                {
                    conn.ResetStatistics();
                    await production.ApplyPostedInventoryAsync(saved, new[] { line }, new DateTime(2026, 10, 18), actor, CancellationToken.None);
                    output.WriteLine("Receipt ledger/balance provider=" + "production" + " observed server roundtrips=" + conn.RetrieveStatistics()["ServerRoundtrips"]);
                    if (fail)
                    {
                        Assert.Equal(1, await NativeSql.Scalar<int>(conn, tx, "SELECT COUNT(*) FROM dbo.StockLedger"));
                        Assert.Equal(1, await NativeSql.Scalar<int>(conn, tx, "SELECT COUNT(*) FROM dbo.StockBalances"));
                        var error = await Assert.ThrowsAsync<SqlException>(() => NativeSql.Execute(conn, tx, "THROW 51192, 'Expected mid-ledger pilot failure.', 1;")); Assert.Equal(51192, error.Number); break;
                    }
                }
                if (fail) uow.Rollback(); else uow.Commit();
                Assert.Equal(fail ? 0 : 1, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.GoodsReceipts"));
                Assert.Equal(fail ? 0 : 2, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.GoodsReceiptLines"));
                Assert.Equal(fail ? 0 : 2, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.StockLedger"));
                Assert.Equal(fail ? 0 : 2, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.StockBalances"));
                if (!fail)
                {
                    Assert.Equal("Posted", await NativeSql.Scalar<string>(oracle, null, "SELECT Status FROM dbo.GoodsReceipts"));
                    var version = await NativeSql.Scalar<byte[]>(oracle, null, "SELECT RowVersion FROM dbo.GoodsReceipts"); Assert.Equal(8, version.Length); Assert.False(version.SequenceEqual(saved.Version));
                    Assert.Equal(3.7500m, await NativeSql.Scalar<decimal>(oracle, null, "SELECT SUM(QuantityChange) FROM dbo.StockLedger"));
                    Assert.Equal(3.7500m, await NativeSql.Scalar<decimal>(oracle, null, "SELECT SUM(Quantity) FROM dbo.StockBalances"));
                }
                output.WriteLine("Receipt pilot provider=" + "production" + " outcome=" + (fail ? "rollback" : "commit") + "; identity/rowversion/decimal/same-session checked by native SQL.");
            });
    }
}
