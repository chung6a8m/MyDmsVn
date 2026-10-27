using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Inventory;

internal sealed class SqlGoodsReceiptWriteRepository : IGoodsReceiptWriteRepository
{
    private const string LockHeaderSql =
        "SELECT ReceiptId, ReceiptNo, ReceiptDate, WarehouseId, EmployeeId, Status, Note, " +
        "CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId, PostedAtUtc, " +
        "PostedByUserId, RowVersion " +
        "FROM dbo.GoodsReceipts WITH (UPDLOCK, HOLDLOCK) WHERE ReceiptId = @receiptId;";

    private readonly ISqlExecutionContext _context;
    private readonly ILinqToDbSessionFactory _sessions;
    private readonly InventoryValuationCoordinator _valuation;

    public SqlGoodsReceiptWriteRepository(ISqlExecutionContext context, ILinqToDbSessionFactory sessions)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _valuation = new InventoryValuationCoordinator(_context, _sessions);
    }

    public async Task<GoodsReceiptReferenceSnapshot> GetReferenceSnapshotAsync(
        int warehouseId,
        int employeeId,
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken)
    {
        using var db = _sessions.Borrow(_context, requireTransaction: true);
        var warehouse = await db.GetTable<Warehouse>().SingleOrDefaultAsync(
            row => row.Id == warehouseId, cancellationToken).ConfigureAwait(false);
        var employee = await db.GetTable<Employee>().SingleOrDefaultAsync(
            row => row.Id == employeeId, cancellationToken).ConfigureAwait(false);
        var products = await GetProductReferenceStatesAsync(productIds, cancellationToken)
            .ConfigureAwait(false);

        return new GoodsReceiptReferenceSnapshot(
            ToReferenceState(warehouse?.IsActive),
            ToReferenceState(employee?.IsActive),
            products);
    }

    private async Task<IReadOnlyDictionary<int, CatalogReferenceState>> GetProductReferenceStatesAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().OrderBy(id => id).ToArray();
        var states = ids.ToDictionary(id => id, _ => CatalogReferenceState.Missing);
        if (ids.Length == 0)
        {
            return states;
        }

        var sql = new StringBuilder("SELECT ProductId, IsActive FROM dbo.Products WHERE ProductId IN (");
        using var command = new SqlCommand
        {
            Connection = GetSqlConnection(),
            Transaction = GetSqlTransaction(),
        };
        for (var index = 0; index < ids.Length; index++)
        {
            if (index > 0)
            {
                sql.Append(',');
            }

            var parameterName = "@productId" + index.ToString(CultureInfo.InvariantCulture);
            sql.Append(parameterName);
            command.Parameters.Add(parameterName, SqlDbType.Int).Value = ids[index];
        }

        sql.Append(");");
        command.CommandText = sql.ToString();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            states[reader.GetInt32(0)] = reader.GetBoolean(1)
                ? CatalogReferenceState.Active
                : CatalogReferenceState.Inactive;
        }

        return states;
    }

    public async Task<GoodsReceipt> InsertDraftAsync(
        GoodsReceipt header,
        IReadOnlyList<GoodsReceiptLine> lines,
        CancellationToken cancellationToken)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (lines == null)
        {
            throw new ArgumentNullException(nameof(lines));
        }

        using var db = _sessions.Borrow(_context, requireTransaction: true);
        var sequenceValue = await NextReceiptNumberValueAsync(cancellationToken).ConfigureAwait(false);
        header.ReceiptNumber = "GR" + sequenceValue.ToString("D10", CultureInfo.InvariantCulture);
        header.Id = await db.GetTable<GoodsReceipt>()
            .Value(row => row.ReceiptNumber, header.ReceiptNumber)
            .Value(row => row.ReceiptDate, header.ReceiptDate)
            .Value(row => row.WarehouseId, header.WarehouseId)
            .Value(row => row.EmployeeId, header.EmployeeId)
            .Value(row => row.Status, header.Status)
            .Value(row => row.Note, header.Note)
            .Value(row => row.CreatedByUserId, header.CreatedByUserId)
            .InsertWithInt64IdentityAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The goods receipt insert returned no identity.");
        foreach (var line in lines)
        {
            line.ReceiptId = header.Id;
            line.Id = await InsertLineAsync(db, line, cancellationToken).ConfigureAwait(false);
        }

        return await ReloadHeaderAsync(header.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GoodsReceipt?> LockHeaderAsync(
        long receiptId,
        CancellationToken cancellationToken)
    {
        var connection = GetSqlConnection();
        using var command = new SqlCommand(LockHeaderSql, connection, GetSqlTransaction());
        command.Parameters.AddWithValue("@receiptId", receiptId);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadHeader(reader);
    }

    public async Task<GoodsReceiptPostSnapshot> LockPostSnapshotAsync(
        GoodsReceipt header,
        CancellationToken cancellationToken)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        var lines = await LoadLinesAsync(header.Id, cancellationToken).ConfigureAwait(false);

        // Posting always takes locks in this order: receipt header, warehouse, employee,
        // products by ProductId, then balances by (WarehouseId, ProductId).
        using var db = _sessions.Borrow(_context, requireTransaction: true);
        var warehouseRow = await db.GetTable<Warehouse>().TableHint("UPDLOCK").TableHint("HOLDLOCK")
            .SingleOrDefaultAsync(row => row.Id == header.WarehouseId, cancellationToken).ConfigureAwait(false);
        var employeeRow = await db.GetTable<Employee>().TableHint("UPDLOCK").TableHint("HOLDLOCK")
            .SingleOrDefaultAsync(row => row.Id == header.EmployeeId, cancellationToken).ConfigureAwait(false);
        var warehouse = ToReferenceState(warehouseRow?.IsActive);
        var employee = ToReferenceState(employeeRow?.IsActive);
        var productIds = lines.Select(line => line.ProductId).Distinct().OrderBy(id => id).ToArray();
        var products = await LockProductStatesAsync(productIds, cancellationToken).ConfigureAwait(false);

        return new GoodsReceiptPostSnapshot(
            lines,
            new GoodsReceiptReferenceSnapshot(warehouse, employee, products));
    }

    public async Task<bool> TryMarkPostedAsync(
        long receiptId,
        byte[] expectedVersion,
        DateTime postedAtUtc,
        int postedByUserId,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(
            "UPDATE dbo.GoodsReceipts SET Status = 'Posted', PostedAtUtc = @postedAtUtc, " +
            "PostedByUserId = @postedByUserId, UpdatedAtUtc = @postedAtUtc, " +
            "UpdatedByUserId = @postedByUserId " +
            "WHERE ReceiptId = @receiptId AND Status = 'Draft' AND RowVersion = @expectedVersion;",
            GetSqlConnection(),
            GetSqlTransaction());
        command.Parameters.Add("@receiptId", SqlDbType.BigInt).Value = receiptId;
        command.Parameters.Add("@expectedVersion", SqlDbType.Timestamp, 8).Value = expectedVersion;
        command.Parameters.Add("@postedAtUtc", SqlDbType.DateTime2).Value = postedAtUtc;
        command.Parameters.Add("@postedByUserId", SqlDbType.Int).Value = postedByUserId;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public Task AcquirePostingGateAsync(CancellationToken token) => _valuation.AcquirePostingGateAsync(token);

    public Task<IReadOnlyDictionary<int, StockValuationState>> LockValuationBalancesAsync(
        int warehouseId, IReadOnlyCollection<int> productIds, CancellationToken token) =>
        _valuation.LockBalancesAsync(warehouseId, productIds, token);

    public async Task ApplyPostedInventoryAsync(GoodsReceipt header, IReadOnlyList<GoodsReceiptLine> lines,
        DateTime postedAtUtc, int postedByUserId, CancellationToken token)
    {
        // Recheck every key before the first append; callers cannot smuggle an unlocked later line.
        var balances = _valuation.GetLockedBalances(header.WarehouseId, lines.Select(line => line.ProductId));
        var values = lines.ToDictionary(line => line.Id,
            line => InventoryValuationMath.CalculateReceiptValue(line.Quantity, line.UnitCost));
        InventoryValuationMath.SumAmounts(values.Values);
        foreach (var group in lines.GroupBy(line => line.ProductId))
        {
            var balance = balances[group.Key];
            _ = new StockValuationState(
                InventoryValuationMath.SumQuantities(group.Select(line => line.Quantity).Concat(new[] { balance.Quantity })),
                InventoryValuationMath.SumAmounts(group.Select(line => values[line.Id]).Concat(new[] { balance.InventoryValue })));
        }
        foreach (var line in lines.OrderBy(line => line.LineNumber).ThenBy(line => line.Id))
        {
            await _valuation.AppendMovementAsync(new StockLedgerEntry
            {
                WarehouseId = header.WarehouseId, ProductId = line.ProductId,
                DocumentType = "GoodsReceipt", DocumentId = header.Id, DocumentLineId = line.Id,
                QuantityChange = line.Quantity, PostedAtUtc = postedAtUtc, PostedByUserId = postedByUserId,
            }, values[line.Id], token).ConfigureAwait(false);
        }
        foreach (var group in lines.GroupBy(line => line.ProductId).OrderBy(group => group.Key))
            await _valuation.ApplyBalanceAsync(header.WarehouseId, group.Key,
                InventoryValuationMath.SumQuantities(group.Select(line => line.Quantity)),
                InventoryValuationMath.SumAmounts(group.Select(line => values[line.Id])), token).ConfigureAwait(false);
    }

    public async Task<GoodsReceipt> ReplaceDraftAsync(
        GoodsReceipt header,
        IReadOnlyList<GoodsReceiptLine> lines,
        CancellationToken cancellationToken)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (lines == null)
        {
            throw new ArgumentNullException(nameof(lines));
        }

        using var db = _sessions.Borrow(_context, requireTransaction: true);
        var affected = await db.GetTable<GoodsReceipt>().Where(row => row.Id == header.Id)
            .Set(row => row.ReceiptDate, header.ReceiptDate)
            .Set(row => row.WarehouseId, header.WarehouseId)
            .Set(row => row.EmployeeId, header.EmployeeId)
            .Set(row => row.Note, header.Note)
            .Set(row => row.UpdatedAtUtc, header.UpdatedAtUtc)
            .Set(row => row.UpdatedByUserId, header.UpdatedByUserId)
            .UpdateAsync(cancellationToken).ConfigureAwait(false);
        if (affected != 1)
        {
            throw new InvalidOperationException("The locked goods receipt header could not be updated.");
        }

        using (var delete = new SqlCommand(
            "DELETE dbo.GoodsReceiptLines WHERE ReceiptId = @receiptId;",
            GetSqlConnection(),
            GetSqlTransaction()))
        {
            delete.Parameters.AddWithValue("@receiptId", header.Id);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var line in lines)
        {
            line.ReceiptId = header.Id;
            line.Id = await InsertLineAsync(db, line, cancellationToken).ConfigureAwait(false);
        }

        return await ReloadHeaderAsync(header.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> NextReceiptNumberValueAsync(CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(
            "SELECT NEXT VALUE FOR dbo.GoodsReceiptNumberSequence;",
            GetSqlConnection(),
            GetSqlTransaction());
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private async Task<IReadOnlyList<GoodsReceiptLine>> LoadLinesAsync(
        long receiptId,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(
            "SELECT ReceiptLineId, ReceiptId, [LineNo], ProductId, Quantity, UnitCost, " +
            "CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId " +
            "FROM dbo.GoodsReceiptLines WHERE ReceiptId = @receiptId " +
            "ORDER BY [LineNo], ReceiptLineId;",
            GetSqlConnection(),
            GetSqlTransaction());
        command.Parameters.Add("@receiptId", SqlDbType.BigInt).Value = receiptId;
        var lines = new List<GoodsReceiptLine>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            lines.Add(new GoodsReceiptLine
            {
                Id = reader.GetInt64(0),
                ReceiptId = reader.GetInt64(1),
                LineNumber = reader.GetInt32(2),
                ProductId = reader.GetInt32(3),
                Quantity = reader.GetDecimal(4),
                UnitCost = reader.GetDecimal(5),
                CreatedAtUtc = reader.GetDateTime(6),
                CreatedByUserId = reader.GetInt32(7),
                UpdatedAtUtc = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                UpdatedByUserId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            });
        }

        return lines;
    }

    private async Task<IReadOnlyDictionary<int, CatalogReferenceState>> LockProductStatesAsync(
        IReadOnlyList<int> productIds, CancellationToken cancellationToken)
    {
        var states = new Dictionary<int, CatalogReferenceState>();
        using var db = _sessions.Borrow(_context, requireTransaction: true);
        foreach (var id in productIds.Distinct().OrderBy(id => id))
        {
            var product = await db.GetTable<Product>().TableHint("UPDLOCK").TableHint("HOLDLOCK")
                .SingleOrDefaultAsync(row => row.Id == id, cancellationToken).ConfigureAwait(false);
            states.Add(id, ToReferenceState(product?.IsActive));
        }
        return states;
    }

    private async Task<GoodsReceipt> ReloadHeaderAsync(long receiptId, CancellationToken cancellationToken)
    {
        using var db = _sessions.Borrow(_context, requireTransaction: true);
        return await db.GetTable<GoodsReceipt>().SingleAsync(
            row => row.Id == receiptId, cancellationToken).ConfigureAwait(false);
    }

    // Explicit allowlist: SQL owns identity/default audit fields; callers cannot write rowversion.
    private static async Task<long> InsertLineAsync(DataConnection db, GoodsReceiptLine line, CancellationToken token) =>
        await db.GetTable<GoodsReceiptLine>()
            .Value(row => row.ReceiptId, line.ReceiptId)
            .Value(row => row.LineNumber, line.LineNumber)
            .Value(row => row.ProductId, line.ProductId)
            .Value(row => row.Quantity, line.Quantity)
            .Value(row => row.UnitCost, line.UnitCost)
            .Value(row => row.CreatedByUserId, line.CreatedByUserId)
            .InsertWithInt64IdentityAsync(token).ConfigureAwait(false)
        ?? throw new InvalidOperationException("The goods receipt line insert returned no identity.");

    private System.Data.IDbTransaction GetTransaction() =>
        _context.Transaction
        ?? throw new InvalidOperationException("Goods receipt writes require an active transaction.");

    private SqlConnection GetSqlConnection() =>
        _context.Connection as SqlConnection
        ?? throw new InvalidOperationException("Goods receipt writes require a SQL Server connection.");

    private SqlTransaction GetSqlTransaction() =>
        GetTransaction() as SqlTransaction
        ?? throw new InvalidOperationException("Goods receipt writes require a SQL Server transaction.");

    private static CatalogReferenceState ToReferenceState(bool? isActive)
    {
        if (!isActive.HasValue)
        {
            return CatalogReferenceState.Missing;
        }

        return isActive.Value ? CatalogReferenceState.Active : CatalogReferenceState.Inactive;
    }

    private static GoodsReceipt ReadHeader(SqlDataReader reader)
    {
        return new GoodsReceipt
        {
            Id = reader.GetInt64(0),
            ReceiptNumber = reader.GetString(1),
            ReceiptDate = reader.GetDateTime(2),
            WarehouseId = reader.GetInt32(3),
            EmployeeId = reader.GetInt32(4),
            Status = reader.GetString(5),
            Note = reader.IsDBNull(6) ? null : reader.GetString(6),
            CreatedAtUtc = reader.GetDateTime(7),
            CreatedByUserId = reader.GetInt32(8),
            UpdatedAtUtc = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            UpdatedByUserId = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            PostedAtUtc = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
            PostedByUserId = reader.IsDBNull(12) ? null : reader.GetInt32(12),
            Version = (byte[])reader.GetValue(13),
        };
    }
}
