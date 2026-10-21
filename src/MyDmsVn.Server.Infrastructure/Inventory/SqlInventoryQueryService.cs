using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Inventory;

internal sealed class SqlInventoryQueryService : IInventoryQueryService
{
    private const string BalanceFromAndFilter =
        " FROM dbo.StockBalances balance " +
        "INNER JOIN dbo.Warehouses warehouse ON warehouse.WarehouseId = balance.WarehouseId " +
        "INNER JOIN dbo.Products product ON product.ProductId = balance.ProductId " +
        "WHERE (@WarehouseId IS NULL OR balance.WarehouseId = @WarehouseId) " +
        "AND (@ProductId IS NULL OR balance.ProductId = @ProductId) " +
        "AND (@Search IS NULL " +
        "OR CHARINDEX(@Search, warehouse.Code) > 0 " +
        "OR CHARINDEX(@Search, warehouse.Name) > 0 " +
        "OR CHARINDEX(@Search, product.Code) > 0 " +
        "OR CHARINDEX(@Search, product.Name) > 0) ";

    private const string CardFromAndFilter =
        " FROM dbo.StockLedger ledger " +
        "INNER JOIN dbo.Warehouses warehouse ON warehouse.WarehouseId = ledger.WarehouseId " +
        "INNER JOIN dbo.Products product ON product.ProductId = ledger.ProductId " +
        "LEFT JOIN dbo.GoodsReceipts receipt ON ledger.DocumentType = 'GoodsReceipt' " +
        "AND receipt.ReceiptId = ledger.DocumentId " +
        "LEFT JOIN dbo.SalesReceipts sale ON ledger.DocumentType = 'SalesReceipt' AND sale.SaleId = ledger.DocumentId " +
        "LEFT JOIN dbo.StockValuationEntries valuation ON valuation.LedgerId = ledger.LedgerId " +
        "WHERE ledger.WarehouseId = @WarehouseId " +
        "AND ledger.ProductId = @ProductId " +
        "AND (@FromUtc IS NULL OR ledger.PostedAtUtc >= @FromUtc) " +
        "AND (@ToUtc IS NULL OR ledger.PostedAtUtc <= @ToUtc) ";

    private readonly IDbConnectionFactory _connectionFactory;

    public SqlInventoryQueryService(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<PagedResult<StockBalanceRow>> GetStockBalancesAsync(
        StockBalanceQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        using var command = InventorySql.Command(connection, null,
            "SELECT COUNT_BIG(*)" + BalanceFromAndFilter + "; " +
            "SELECT balance.WarehouseId, warehouse.Code AS WarehouseCode, " +
            "warehouse.Name AS WarehouseName, warehouse.IsActive AS WarehouseIsActive, " +
            "balance.ProductId, product.Code AS ProductCode, product.Name AS ProductName, " +
            "product.IsActive AS ProductIsActive, balance.Quantity, balance.InventoryValue" +
            BalanceFromAndFilter +
            "ORDER BY warehouse.Code, balance.WarehouseId, product.Code, balance.ProductId " +
            "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;");
        InventorySql.Add(command, "@WarehouseId", SqlDbType.Int, request.WarehouseId);
        InventorySql.Add(command, "@ProductId", SqlDbType.Int, request.ProductId);
        InventorySql.AddText(command, "@Search", Normalize(request.Search));
        InventorySql.Add(command, "@Offset", SqlDbType.BigInt, (long)(request.PageNumber - 1) * request.PageSize);
        InventorySql.Add(command, "@PageSize", SqlDbType.Int, request.PageSize);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var totalCount = await InventorySql.ReadCountAsync(reader, cancellationToken).ConfigureAwait(false);
        var rows = await InventorySql.ReadRowsAsync(reader, row => ToContract(ReadBalance(row)), cancellationToken).ConfigureAwait(false);
        return new PagedResult<StockBalanceRow>(
            rows,
            request.PageNumber,
            request.PageSize,
            totalCount);
    }

    public async Task<PagedResult<StockCardRow>> GetStockCardAsync(
        StockCardQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        using var command = InventorySql.Command(connection, null,
            "SELECT COUNT_BIG(*)" + CardFromAndFilter + "; " +
            "SELECT ledger.LedgerId, ledger.WarehouseId, warehouse.Code AS WarehouseCode, " +
            "warehouse.Name AS WarehouseName, warehouse.IsActive AS WarehouseIsActive, " +
            "ledger.ProductId, product.Code AS ProductCode, product.Name AS ProductName, " +
            "product.IsActive AS ProductIsActive, ledger.PostedAtUtc, ledger.DocumentType, " +
            "ledger.DocumentId, COALESCE(receipt.ReceiptNo, sale.SaleNo, " +
            "CONCAT(ledger.DocumentType, '#', CONVERT(varchar(20), ledger.DocumentId)) " +
            "COLLATE Latin1_General_100_CI_AI) AS DocumentNumber, " +
            "ledger.QuantityChange, valuation.ValueChange" + CardFromAndFilter +
            "ORDER BY ledger.PostedAtUtc, ledger.LedgerId " +
            "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;");
        InventorySql.Add(command, "@WarehouseId", SqlDbType.Int, request.WarehouseId);
        InventorySql.Add(command, "@ProductId", SqlDbType.Int, request.ProductId);
        InventorySql.Add(command, "@FromUtc", SqlDbType.DateTime2, request.FromUtc);
        InventorySql.Add(command, "@ToUtc", SqlDbType.DateTime2, request.ToUtc);
        InventorySql.Add(command, "@Offset", SqlDbType.BigInt, (long)(request.PageNumber - 1) * request.PageSize);
        InventorySql.Add(command, "@PageSize", SqlDbType.Int, request.PageSize);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var totalCount = await InventorySql.ReadCountAsync(reader, cancellationToken).ConfigureAwait(false);
        var rows = await InventorySql.ReadRowsAsync(reader, row => ToContract(ReadCard(row)), cancellationToken).ConfigureAwait(false);
        return new PagedResult<StockCardRow>(
            rows,
            request.PageNumber,
            request.PageSize,
            totalCount);
    }

    private static StockBalanceRow ToContract(BalanceRow row) =>
        new StockBalanceRow(
            row.WarehouseId,
            row.WarehouseCode,
            row.WarehouseName,
            row.WarehouseIsActive,
            row.ProductId,
            row.ProductCode,
            row.ProductName,
            row.ProductIsActive,
            row.Quantity,
            row.InventoryValue,
            InventoryValuationMath.CalculateAverageUnitCost(row.Quantity, row.InventoryValue));

    private static StockCardRow ToContract(CardRow row) =>
        new StockCardRow(
            row.LedgerId,
            row.WarehouseId,
            row.WarehouseCode,
            row.WarehouseName,
            row.WarehouseIsActive,
            row.ProductId,
            row.ProductCode,
            row.ProductName,
            row.ProductIsActive,
            DateTime.SpecifyKind(row.PostedAtUtc, DateTimeKind.Utc),
            row.DocumentType,
            row.DocumentId,
            row.DocumentNumber,
            row.QuantityChange, row.ValueChange);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

    private static BalanceRow ReadBalance(SqlDataReader reader) => new BalanceRow
    {
        WarehouseId = reader.GetInt32(0),
        WarehouseCode = reader.GetString(1),
        WarehouseName = reader.GetString(2),
        WarehouseIsActive = reader.GetBoolean(3),
        ProductId = reader.GetInt32(4),
        ProductCode = reader.GetString(5),
        ProductName = reader.GetString(6),
        ProductIsActive = reader.GetBoolean(7),
        Quantity = reader.GetDecimal(8),
        InventoryValue = reader.GetDecimal(9),
    };

    private static CardRow ReadCard(SqlDataReader reader) => new CardRow
    {
        LedgerId = reader.GetInt64(0),
        WarehouseId = reader.GetInt32(1),
        WarehouseCode = reader.GetString(2),
        WarehouseName = reader.GetString(3),
        WarehouseIsActive = reader.GetBoolean(4),
        ProductId = reader.GetInt32(5),
        ProductCode = reader.GetString(6),
        ProductName = reader.GetString(7),
        ProductIsActive = reader.GetBoolean(8),
        PostedAtUtc = reader.GetDateTime(9),
        DocumentType = reader.GetString(10),
        DocumentId = reader.GetInt64(11),
        DocumentNumber = reader.GetString(12),
        QuantityChange = reader.GetDecimal(13),
        ValueChange = reader.IsDBNull(14) ? throw new InvalidOperationException("Stock movement has no valuation companion.") : reader.GetDecimal(14),
    };

    private sealed class BalanceRow
    {
        public int WarehouseId { get; set; }
        public string WarehouseCode { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public bool WarehouseIsActive { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public bool ProductIsActive { get; set; }
        public decimal Quantity { get; set; }
        public decimal InventoryValue { get; set; }
    }

    private sealed class CardRow
    {
        public long LedgerId { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseCode { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public bool WarehouseIsActive { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public bool ProductIsActive { get; set; }
        public DateTime PostedAtUtc { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public long DocumentId { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public decimal QuantityChange { get; set; }
        public decimal ValueChange { get; set; }
    }
}
