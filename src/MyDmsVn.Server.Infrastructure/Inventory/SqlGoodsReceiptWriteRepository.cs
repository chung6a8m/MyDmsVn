using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using RepoDb;

namespace MyDmsVn.Server.Infrastructure.Inventory;

internal sealed class SqlGoodsReceiptWriteRepository : IGoodsReceiptWriteRepository
{
    private const string LockHeaderSql =
        "SELECT ReceiptId, ReceiptNo, ReceiptDate, WarehouseId, EmployeeId, Status, Note, " +
        "CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId, PostedAtUtc, " +
        "PostedByUserId, RowVersion " +
        "FROM dbo.GoodsReceipts WITH (UPDLOCK, HOLDLOCK) WHERE ReceiptId = @receiptId;";

    private const string ApplyBalanceDeltaSql =
        "UPDATE dbo.StockBalances WITH (UPDLOCK, HOLDLOCK) " +
        "SET Quantity = Quantity + @delta " +
        "WHERE WarehouseId = @warehouseId AND ProductId = @productId; " +
        "IF @@ROWCOUNT = 0 " +
        "BEGIN " +
        "    BEGIN TRY " +
        "        INSERT dbo.StockBalances (WarehouseId, ProductId, Quantity) " +
        "        VALUES (@warehouseId, @productId, @delta); " +
        "    END TRY " +
        "    BEGIN CATCH " +
        "        IF ERROR_NUMBER() NOT IN (2601, 2627) THROW; " +
        "        UPDATE dbo.StockBalances WITH (UPDLOCK, HOLDLOCK) " +
        "        SET Quantity = Quantity + @delta " +
        "        WHERE WarehouseId = @warehouseId AND ProductId = @productId; " +
        "        IF @@ROWCOUNT <> 1 THROW 51001, 'Stock balance unique-key retry failed.', 1; " +
        "    END CATCH " +
        "END;";

    private readonly ISqlExecutionContext _context;

    public SqlGoodsReceiptWriteRepository(ISqlExecutionContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<GoodsReceiptReferenceSnapshot> GetReferenceSnapshotAsync(
        int warehouseId,
        int employeeId,
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken)
    {
        var transaction = GetTransaction();
        var warehouse = (await _context.Connection.QueryAsync<Warehouse>(
            warehouseId,
            transaction: transaction,
            cancellationToken: cancellationToken).ConfigureAwait(false)).SingleOrDefault();
        var employee = (await _context.Connection.QueryAsync<Employee>(
            employeeId,
            transaction: transaction,
            cancellationToken: cancellationToken).ConfigureAwait(false)).SingleOrDefault();
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

        var transaction = GetTransaction();
        var sequenceValue = await NextReceiptNumberValueAsync(cancellationToken).ConfigureAwait(false);
        header.ReceiptNumber = "GR" + sequenceValue.ToString("D10", CultureInfo.InvariantCulture);
        header.Id = await _context.Connection.InsertAsync<GoodsReceipt, long>(
            header,
            fields: InventoryRepoDbWriteFields.GoodsReceiptCreate,
            transaction: transaction,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var line in lines)
        {
            line.ReceiptId = header.Id;
            line.Id = await _context.Connection.InsertAsync<GoodsReceiptLine, long>(
                line,
                fields: InventoryRepoDbWriteFields.GoodsReceiptLineCreate,
                transaction: transaction,
                cancellationToken: cancellationToken).ConfigureAwait(false);
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
        var warehouse = await LockCatalogStateAsync(
            "SELECT IsActive FROM dbo.Warehouses WITH (UPDLOCK, HOLDLOCK) WHERE WarehouseId = @id;",
            header.WarehouseId,
            cancellationToken).ConfigureAwait(false);
        var employee = await LockCatalogStateAsync(
            "SELECT IsActive FROM dbo.Employees WITH (UPDLOCK, HOLDLOCK) WHERE EmployeeId = @id;",
            header.EmployeeId,
            cancellationToken).ConfigureAwait(false);
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

    public async Task AppendStockLedgerAsync(
        long receiptId,
        int warehouseId,
        IReadOnlyList<GoodsReceiptLine> lines,
        DateTime postedAtUtc,
        int postedByUserId,
        CancellationToken cancellationToken)
    {
        var transaction = GetTransaction();
        foreach (var line in lines.OrderBy(line => line.LineNumber).ThenBy(line => line.Id))
        {
            var entry = new StockLedgerEntry
            {
                WarehouseId = warehouseId,
                ProductId = line.ProductId,
                DocumentType = "GoodsReceipt",
                DocumentId = receiptId,
                DocumentLineId = line.Id,
                QuantityChange = line.Quantity,
                PostedAtUtc = postedAtUtc,
                PostedByUserId = postedByUserId,
            };
            entry.Id = await _context.Connection.InsertAsync<StockLedgerEntry, long>(
                entry,
                fields: InventoryRepoDbWriteFields.StockLedgerCreate,
                transaction: transaction,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ApplyStockBalanceDeltasAsync(
        int warehouseId,
        IReadOnlyDictionary<int, decimal> deltasByProductId,
        CancellationToken cancellationToken)
    {
        foreach (var delta in deltasByProductId.OrderBy(item => item.Key))
        {
            using var command = new SqlCommand(
                ApplyBalanceDeltaSql,
                GetSqlConnection(),
                GetSqlTransaction());
            command.Parameters.Add("@warehouseId", SqlDbType.Int).Value = warehouseId;
            command.Parameters.Add("@productId", SqlDbType.Int).Value = delta.Key;
            var quantity = command.Parameters.Add("@delta", SqlDbType.Decimal);
            quantity.Precision = 18;
            quantity.Scale = 4;
            quantity.Value = delta.Value;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
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

        var transaction = GetTransaction();
        var affected = await _context.Connection.UpdateAsync(
            header,
            fields: InventoryRepoDbWriteFields.GoodsReceiptDraftUpdate,
            transaction: transaction,
            cancellationToken: cancellationToken).ConfigureAwait(false);
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
            line.Id = await _context.Connection.InsertAsync<GoodsReceiptLine, long>(
                line,
                fields: InventoryRepoDbWriteFields.GoodsReceiptLineCreate,
                transaction: transaction,
                cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private async Task<CatalogReferenceState> LockCatalogStateAsync(
        string sql,
        int id,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(sql, GetSqlConnection(), GetSqlTransaction());
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value == null || value == DBNull.Value
            ? CatalogReferenceState.Missing
            : ToReferenceState(Convert.ToBoolean(value, CultureInfo.InvariantCulture));
    }

    private async Task<IReadOnlyDictionary<int, CatalogReferenceState>> LockProductStatesAsync(
        IReadOnlyList<int> productIds,
        CancellationToken cancellationToken)
    {
        var states = productIds.ToDictionary(id => id, _ => CatalogReferenceState.Missing);
        if (productIds.Count == 0)
        {
            return states;
        }

        var sql = new StringBuilder(
            "SELECT ProductId, IsActive FROM dbo.Products WITH (UPDLOCK, HOLDLOCK) WHERE ProductId IN (");
        using var command = new SqlCommand
        {
            Connection = GetSqlConnection(),
            Transaction = GetSqlTransaction(),
        };
        for (var index = 0; index < productIds.Count; index++)
        {
            if (index > 0)
            {
                sql.Append(',');
            }

            var parameterName = "@lockedProductId" + index.ToString(CultureInfo.InvariantCulture);
            sql.Append(parameterName);
            command.Parameters.Add(parameterName, SqlDbType.Int).Value = productIds[index];
        }

        sql.Append(") ORDER BY ProductId;");
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

    private async Task<GoodsReceipt> ReloadHeaderAsync(long receiptId, CancellationToken cancellationToken)
    {
        var rows = await _context.Connection.QueryAsync<GoodsReceipt>(
            receiptId,
            transaction: GetTransaction(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return rows.Single();
    }

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
