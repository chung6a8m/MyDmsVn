using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Inventory;
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
        var parameters = new
        {
            request.WarehouseId,
            request.ProductId,
            Search = Normalize(request.Search),
            Offset = (long)(request.PageNumber - 1) * request.PageSize,
            request.PageSize,
        };
        var command = new CommandDefinition(
            "SELECT COUNT_BIG(*)" + BalanceFromAndFilter + "; " +
            "SELECT balance.WarehouseId, warehouse.Code AS WarehouseCode, " +
            "warehouse.Name AS WarehouseName, warehouse.IsActive AS WarehouseIsActive, " +
            "balance.ProductId, product.Code AS ProductCode, product.Name AS ProductName, " +
            "product.IsActive AS ProductIsActive, balance.Quantity" +
            BalanceFromAndFilter +
            "ORDER BY warehouse.Code, balance.WarehouseId, product.Code, balance.ProductId " +
            "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            parameters,
            cancellationToken: cancellationToken);
        using var results = await connection.QueryMultipleAsync(command).ConfigureAwait(false);
        var totalCount = await results.ReadSingleAsync<long>().ConfigureAwait(false);
        var rows = (await results.ReadAsync<BalanceRow>().ConfigureAwait(false))
            .Select(ToContract)
            .ToArray();
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
        var parameters = new DynamicParameters();
        parameters.Add("WarehouseId", request.WarehouseId);
        parameters.Add("ProductId", request.ProductId);
        parameters.Add("FromUtc", request.FromUtc, DbType.DateTime2);
        parameters.Add("ToUtc", request.ToUtc, DbType.DateTime2);
        parameters.Add("Offset", (long)(request.PageNumber - 1) * request.PageSize);
        parameters.Add("PageSize", request.PageSize);
        var command = new CommandDefinition(
            "SELECT COUNT_BIG(*)" + CardFromAndFilter + "; " +
            "SELECT ledger.LedgerId, ledger.WarehouseId, warehouse.Code AS WarehouseCode, " +
            "warehouse.Name AS WarehouseName, warehouse.IsActive AS WarehouseIsActive, " +
            "ledger.ProductId, product.Code AS ProductCode, product.Name AS ProductName, " +
            "product.IsActive AS ProductIsActive, ledger.PostedAtUtc, ledger.DocumentType, " +
            "ledger.DocumentId, COALESCE(receipt.ReceiptNo, " +
            "CONCAT(ledger.DocumentType, '#', CONVERT(varchar(20), ledger.DocumentId)) " +
            "COLLATE Latin1_General_100_CI_AI) AS DocumentNumber, " +
            "ledger.QuantityChange" + CardFromAndFilter +
            "ORDER BY ledger.PostedAtUtc, ledger.LedgerId " +
            "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            parameters,
            cancellationToken: cancellationToken);
        using var results = await connection.QueryMultipleAsync(command).ConfigureAwait(false);
        var totalCount = await results.ReadSingleAsync<long>().ConfigureAwait(false);
        var rows = (await results.ReadAsync<CardRow>().ConfigureAwait(false))
            .Select(ToContract)
            .ToArray();
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
            row.Quantity);

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
            row.QuantityChange);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

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
    }
}
