using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Domain.Sales;
using MyDmsVn.Server.Infrastructure.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Sales;

internal sealed class SqlSalesReceiptWriteRepository : ISalesReceiptWriteRepository
{
    private readonly ISqlExecutionContext _context;
    private readonly ILinqToDbSessionFactory _sessions;
    private readonly InventoryValuationCoordinator _valuation;
    public SqlSalesReceiptWriteRepository(ISqlExecutionContext context, ILinqToDbSessionFactory sessions)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _valuation = new InventoryValuationCoordinator(context, sessions);
    }

    public Task AcquirePostingGateAsync(CancellationToken cancellationToken) => _valuation.AcquirePostingGateAsync(cancellationToken);

    public async Task<SalesReceiptReferenceSnapshot> GetReferenceSnapshotAsync(int customerId, int warehouseId, int employeeId,
        IReadOnlyCollection<int> productIds, CancellationToken cancellationToken)
    {
        using var db = _sessions.Borrow(_context, requireTransaction: true);
        return await SalesReferenceLoader.LoadAsync(db, customerId, warehouseId, employeeId, productIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SalesReceipt?> LockHeaderAsync(long saleId, CancellationToken cancellationToken)
    {
        using var db = _sessions.Borrow(_context, requireTransaction: true);
        return await db.GetTable<SalesReceipt>().TableHint("UPDLOCK").TableHint("HOLDLOCK")
            .SingleOrDefaultAsync(row => row.SaleId == saleId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SalesReceiptLine>> LoadLinesAsync(long saleId, CancellationToken cancellationToken)
    {
        using var db = _sessions.Borrow(_context, requireTransaction: true);
        return await db.GetTable<SalesReceiptLine>().Where(row => row.SaleId == saleId)
            .OrderBy(row => row.LineNo).ThenBy(row => row.SaleLineId).ToArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SalesReceipt> InsertDraftAsync(SalesReceipt header, IReadOnlyList<SalesReceiptLine> lines, CancellationToken cancellationToken)
    {
        if (header == null) throw new ArgumentNullException(nameof(header));
        ValidateLines(header.SaleId, lines, create: true);
        if (header.SaleId != 0 || header.Status != SalesReceiptStatuses.Draft || header.PostedAtUtc.HasValue || header.PostedByUserId.HasValue)
            throw new ArgumentException("A new Draft header is required.", nameof(header));
        using var db = _sessions.Borrow(_context, requireTransaction: true);
        var number = await NextNumberAsync(cancellationToken).ConfigureAwait(false);
        header.SaleNumber = "SA" + number.ToString("D10", CultureInfo.InvariantCulture);
        header.SaleId = await db.GetTable<SalesReceipt>()
            .Value(row => row.SaleNumber, header.SaleNumber).Value(row => row.SaleDate, header.SaleDate)
            .Value(row => row.CustomerId, header.CustomerId).Value(row => row.WarehouseId, header.WarehouseId)
            .Value(row => row.EmployeeId, header.EmployeeId).Value(row => row.Status, SalesReceiptStatuses.Draft)
            .Value(row => row.Note, header.Note).Value(row => row.CreatedByUserId, header.CreatedByUserId)
            .InsertWithInt64IdentityAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sales header insert returned no identity.");
        foreach (var line in lines)
        {
            line.SaleId = header.SaleId;
            line.SaleLineId = await InsertLine(db, line, cancellationToken).ConfigureAwait(false);
        }
        return await db.GetTable<SalesReceipt>().SingleAsync(row => row.SaleId == header.SaleId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SalesReceipt> ReplaceDraftAsync(SalesReceipt header, IReadOnlyList<SalesReceiptLine> lines, CancellationToken cancellationToken)
    {
        if (header == null) throw new ArgumentNullException(nameof(header));
        ValidateLines(header.SaleId, lines, create: false);
        var storedHeader = await LockHeaderAsync(header.SaleId, cancellationToken).ConfigureAwait(false);
        if (storedHeader == null || storedHeader.Status != SalesReceiptStatuses.Draft || header.Status != SalesReceiptStatuses.Draft ||
            header.Version.Length != 8 || !storedHeader.Version.SequenceEqual(header.Version))
            throw new InvalidOperationException("The locked Draft version is required for replacement.");
        var stored = await LoadLinesAsync(header.SaleId, cancellationToken).ConfigureAwait(false);
        if (stored.Count == 0 || stored.Count > 200 || stored.Select((line, index) => line.LineNo != index + 1 || line.SaleLineId <= 0 ||
            line.PostedUnitCost.HasValue || line.CostAmount.HasValue).Any(invalid => invalid))
            throw new InvalidOperationException("Stored Draft lines must have canonical identities and order.");
        var owned = stored.ToDictionary(line => line.SaleLineId);
        var retainedIds = lines.Where(line => line.SaleLineId > 0).Select(line => line.SaleLineId).ToArray();
        if (retainedIds.Any(id => !owned.ContainsKey(id))) throw new ArgumentException("Every retained line must belong to this Draft.", nameof(lines));

        using var db = _sessions.Borrow(_context, requireTransaction: true);
        var expectedVersion = header.Version;
        var affected = await db.GetTable<SalesReceipt>().Where(row => row.SaleId == header.SaleId && row.Status == SalesReceiptStatuses.Draft && row.Version == expectedVersion)
            .Set(row => row.SaleDate, header.SaleDate).Set(row => row.CustomerId, header.CustomerId)
            .Set(row => row.WarehouseId, header.WarehouseId).Set(row => row.EmployeeId, header.EmployeeId)
            .Set(row => row.Note, header.Note).Set(row => row.UpdatedAtUtc, header.UpdatedAtUtc)
            .Set(row => row.UpdatedByUserId, header.UpdatedByUserId).UpdateAsync(cancellationToken).ConfigureAwait(false);
        RequireOne(affected);
        await db.GetTable<SalesReceiptLine>().Where(row => row.SaleId == header.SaleId && !retainedIds.Contains(row.SaleLineId))
            .DeleteAsync(cancellationToken).ConfigureAwait(false);
        // Positive temporary slots avoid the unique (SaleId,LineNo) constraint during swaps.
        // All stored and incoming rows have been validated before the first write.
        for (var index = 0; index < retainedIds.Length; index++)
        {
            var id = retainedIds[index];
            RequireOne(await db.GetTable<SalesReceiptLine>().Where(row => row.SaleId == header.SaleId && row.SaleLineId == id)
                .Set(row => row.LineNo, 201 + index).UpdateAsync(cancellationToken).ConfigureAwait(false));
        }
        foreach (var line in lines)
        {
            if (line.SaleLineId == 0)
                line.SaleLineId = await InsertLine(db, line, cancellationToken).ConfigureAwait(false);
            else
            {
                RequireOne(await db.GetTable<SalesReceiptLine>().Where(row => row.SaleId == header.SaleId && row.SaleLineId == line.SaleLineId)
                    .Set(row => row.LineNo, line.LineNo).Set(row => row.ProductId, line.ProductId).Set(row => row.Unit, line.Unit)
                    .Set(row => row.Qty, line.Qty).Set(row => row.UnitPrice, line.UnitPrice).Set(row => row.IsGift, line.IsGift)
                    .Set(row => row.LineAmount, line.LineAmount).Set(row => row.UpdatedAtUtc, line.UpdatedAtUtc)
                    .Set(row => row.UpdatedByUserId, line.UpdatedByUserId).UpdateAsync(cancellationToken).ConfigureAwait(false));
                line.CreatedAtUtc = owned[line.SaleLineId].CreatedAtUtc;
                line.CreatedByUserId = owned[line.SaleLineId].CreatedByUserId;
            }
        }
        return await db.GetTable<SalesReceipt>().SingleAsync(row => row.SaleId == header.SaleId, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateLines(long saleId, IReadOnlyList<SalesReceiptLine> lines, bool create)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        if (lines.Count < 1 || lines.Count > 200) throw new ArgumentException("Require 1–200 lines.", nameof(lines));
        var ids = new HashSet<long>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line == null || line.LineNo != index + 1 || line.ProductId <= 0 || line.SaleId != saleId || line.SaleLineId < 0 ||
                (create && line.SaleLineId != 0) || (line.SaleLineId > 0 && !ids.Add(line.SaleLineId)) ||
                string.IsNullOrWhiteSpace(line.Unit) || line.Unit.Length > 32 || line.Unit.IndexOf('\0') >= 0 ||
                line.PostedUnitCost.HasValue || line.CostAmount.HasValue)
                throw new ArgumentException("Invalid Draft line identity, order, Unit or cost.", nameof(lines));
            if (line.LineAmount != InventoryValuationMath.CalculateRevenue(line.Qty, line.UnitPrice, line.IsGift))
                throw new ArgumentException("Draft amount must be server calculated.", nameof(lines));
        }
        InventoryValuationMath.SumAmounts(lines.Select(line => line.LineAmount));
        foreach (var group in lines.GroupBy(line => line.ProductId)) InventoryValuationMath.SumQuantities(group.Select(line => line.Qty));
    }

    private static async Task<long> InsertLine(DataConnection db, SalesReceiptLine line, CancellationToken token) =>
        await db.GetTable<SalesReceiptLine>()
            .Value(row => row.SaleId, line.SaleId).Value(row => row.LineNo, line.LineNo).Value(row => row.ProductId, line.ProductId)
            .Value(row => row.Unit, line.Unit).Value(row => row.Qty, line.Qty).Value(row => row.UnitPrice, line.UnitPrice)
            .Value(row => row.IsGift, line.IsGift).Value(row => row.LineAmount, line.LineAmount)
            .Value(row => row.CreatedByUserId, line.CreatedByUserId).InsertWithInt64IdentityAsync(token).ConfigureAwait(false)
        ?? throw new InvalidOperationException("Sales line insert returned no identity.");

    private async Task<long> NextNumberAsync(CancellationToken token)
    {
        // NEXT VALUE FOR is SQL Server sequence semantics; it participates on the owned transaction.
        var connection = _context.Connection as SqlConnection ?? throw new InvalidOperationException("SQL Server connection required.");
        var transaction = _context.Transaction as SqlTransaction ?? throw new InvalidOperationException("Active SQL transaction required.");
        using var command = new SqlCommand("SELECT NEXT VALUE FOR dbo.SalesReceiptNumberSequence;", connection, transaction) { CommandTimeout = 30 };
        var value = Convert.ToInt64(await command.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (value < 1 || value > 9999999999L) throw new InvalidOperationException("Sales sequence is outside its supported range.");
        return value;
    }

    private static void RequireOne(int count)
    {
        if (count != 1) throw new InvalidOperationException("The locked Sales row could not be updated.");
    }
}
