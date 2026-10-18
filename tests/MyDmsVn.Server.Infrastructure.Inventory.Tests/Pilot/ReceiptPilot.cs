using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using LinqToDB.Mapping;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests.Pilot;

// Test-only pilot; T06 removes it. Existing native locking/post/balance algorithms remain unchanged.
internal sealed class ReceiptPilot
{
    private readonly ISqlExecutionContext _context;
    public LinqToDbSessionFactory Sessions { get; }
    public ReceiptPilot(ISqlExecutionContext context, IDbConnectionFactory connections)
    {
        _context = context;
        Sessions = new(connections, new LinqToDbMappingSchemaProvider(new ILinqToDbMapping[] { new Mapping() }));
    }
    public async Task<GoodsReceipt> Insert(GoodsReceipt header, IReadOnlyList<GoodsReceiptLine> lines)
    {
        using var db = Sessions.Borrow(_context, true);
        var sequence = await db.ExecuteAsync<long>("SELECT NEXT VALUE FOR dbo.GoodsReceiptNumberSequence;", CancellationToken.None);
        header.ReceiptNumber = "GR" + sequence.ToString("D10", CultureInfo.InvariantCulture);
        header.Id = await db.GetTable<GoodsReceipt>().Value(x => x.ReceiptNumber, header.ReceiptNumber).Value(x => x.ReceiptDate, header.ReceiptDate)
            .Value(x => x.WarehouseId, header.WarehouseId).Value(x => x.EmployeeId, header.EmployeeId).Value(x => x.Status, header.Status).Value(x => x.Note, header.Note)
            .Value(x => x.CreatedByUserId, header.CreatedByUserId).InsertWithInt64IdentityAsync(CancellationToken.None) ?? throw new InvalidOperationException("Missing receipt identity.");
        foreach (var line in lines)
        {
            line.ReceiptId = header.Id;
            line.Id = await db.GetTable<GoodsReceiptLine>().Value(x => x.ReceiptId, header.Id).Value(x => x.LineNumber, line.LineNumber).Value(x => x.ProductId, line.ProductId)
                .Value(x => x.Quantity, line.Quantity).Value(x => x.UnitCost, line.UnitCost).Value(x => x.CreatedByUserId, line.CreatedByUserId)
                .InsertWithInt64IdentityAsync(CancellationToken.None) ?? throw new InvalidOperationException("Missing line identity.");
        }
        return await db.GetTable<GoodsReceipt>().SingleAsync(x => x.Id == header.Id, CancellationToken.None);
    }
    public async Task Append(long receiptId, int warehouse, IReadOnlyList<GoodsReceiptLine> lines, int actor)
    {
        using var db = Sessions.Borrow(_context, true);
        foreach (var line in lines.OrderBy(x => x.LineNumber).ThenBy(x => x.Id))
            await db.GetTable<StockLedgerEntry>().Value(x => x.WarehouseId, warehouse).Value(x => x.ProductId, line.ProductId).Value(x => x.DocumentType, "GoodsReceipt")
                .Value(x => x.DocumentId, receiptId).Value(x => x.DocumentLineId, line.Id).Value(x => x.QuantityChange, line.Quantity)
                .Value(x => x.PostedAtUtc, new DateTime(2026, 10, 18)).Value(x => x.PostedByUserId, actor).InsertWithInt64IdentityAsync(CancellationToken.None);
    }
    private sealed class Mapping : ILinqToDbMapping
    {
        public void Configure(FluentMappingBuilder b)
        {
            var h = b.Entity<GoodsReceipt>().HasSchemaName("dbo").HasTableName("GoodsReceipts");
            h.Property(x => x.Id).HasColumnName("ReceiptId").IsPrimaryKey().IsIdentity();
            h.Property(x => x.ReceiptNumber).HasColumnName("ReceiptNo").HasDataType(DataType.NVarChar).HasLength(32);
            h.Property(x => x.ReceiptDate).HasDataType(DataType.Date); h.Property(x => x.Version).HasColumnName("RowVersion");
            h.Property(x => x.CreatedAtUtc).HasDataType(DataType.DateTime2); h.Property(x => x.UpdatedAtUtc).HasDataType(DataType.DateTime2); h.Property(x => x.PostedAtUtc).HasDataType(DataType.DateTime2);
            var l = b.Entity<GoodsReceiptLine>().HasSchemaName("dbo").HasTableName("GoodsReceiptLines");
            l.Property(x => x.Id).HasColumnName("ReceiptLineId").IsPrimaryKey().IsIdentity(); l.Property(x => x.LineNumber).HasColumnName("LineNo");
            l.Property(x => x.Quantity).HasDataType(DataType.Decimal).HasPrecision(18).HasScale(4); l.Property(x => x.UnitCost).HasDataType(DataType.Decimal).HasPrecision(19).HasScale(4);
            var e = b.Entity<StockLedgerEntry>().HasSchemaName("dbo").HasTableName("StockLedger");
            e.Property(x => x.Id).HasColumnName("LedgerId").IsPrimaryKey().IsIdentity(); e.Property(x => x.QuantityChange).HasDataType(DataType.Decimal).HasPrecision(18).HasScale(4);
            e.Property(x => x.PostedAtUtc).HasDataType(DataType.DateTime2); e.Property(x => x.DocumentType).HasDataType(DataType.NVarChar).HasLength(32);
        }
    }
}
