using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Inventory;

internal sealed class SqlGoodsReceiptQueryService : IGoodsReceiptQueryService
{
    private const string ReceiptFromAndFilter =
        " FROM dbo.GoodsReceipts gr " +
        "INNER JOIN dbo.Warehouses w ON w.WarehouseId = gr.WarehouseId " +
        "INNER JOIN dbo.Employees e ON e.EmployeeId = gr.EmployeeId " +
        "WHERE (@WarehouseId IS NULL OR gr.WarehouseId = @WarehouseId) " +
        "AND (@Status IS NULL OR gr.Status = @Status) " +
        "AND (@Search IS NULL " +
        "OR CHARINDEX(@Search, gr.ReceiptNo) > 0 " +
        "OR CHARINDEX(@Search, w.Code) > 0 " +
        "OR CHARINDEX(@Search, w.Name) > 0 " +
        "OR CHARINDEX(@Search, e.Code) > 0 " +
        "OR CHARINDEX(@Search, e.Name) > 0 " +
        "OR EXISTS (" +
        "SELECT 1 FROM dbo.GoodsReceiptLines searchLine " +
        "INNER JOIN dbo.Products searchProduct ON searchProduct.ProductId = searchLine.ProductId " +
        "WHERE searchLine.ReceiptId = gr.ReceiptId " +
        "AND (CHARINDEX(@Search, searchProduct.Code) > 0 " +
        "OR CHARINDEX(@Search, searchProduct.Name) > 0))) ";

    private const string HeaderColumns =
        "gr.ReceiptId, gr.ReceiptNo, gr.ReceiptDate, gr.WarehouseId, " +
        "w.Code AS WarehouseCode, w.Name AS WarehouseName, w.IsActive AS WarehouseIsActive, " +
        "gr.EmployeeId, e.Code AS EmployeeCode, e.Name AS EmployeeName, " +
        "e.IsActive AS EmployeeIsActive, gr.Status, gr.Note, gr.PostedAtUtc, " +
        "gr.RowVersion AS Version";

    private const string LineColumns =
        "line.ReceiptLineId, line.ReceiptId, line.[LineNo] AS LineNumber, line.ProductId, " +
        "product.Code AS ProductCode, product.Name AS ProductName, " +
        "product.IsActive AS ProductIsActive, line.Quantity, line.UnitCost";

    private readonly IDbConnectionFactory _connectionFactory;

    public SqlGoodsReceiptQueryService(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<PagedResult<GoodsReceiptDto>> ListAsync(
        GoodsReceiptListRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        using var command = InventorySql.Command(connection, transaction,
            "SELECT COUNT_BIG(*)" + ReceiptFromAndFilter + "; " +
            "SELECT " + HeaderColumns + ReceiptFromAndFilter +
            "ORDER BY gr.ReceiptDate DESC, gr.ReceiptId DESC " +
            "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;");
        InventorySql.Add(command, "@WarehouseId", SqlDbType.Int, request.WarehouseId);
        InventorySql.AddText(command, "@Status", Normalize(request.Status));
        InventorySql.AddText(command, "@Search", Normalize(request.Search));
        InventorySql.Add(command, "@Offset", SqlDbType.BigInt, (long)(request.PageNumber - 1) * request.PageSize);
        InventorySql.Add(command, "@PageSize", SqlDbType.Int, request.PageSize);
        long totalCount;
        HeaderRow[] headers;
        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            totalCount = await InventorySql.ReadCountAsync(reader, cancellationToken).ConfigureAwait(false);
            headers = await InventorySql.ReadRowsAsync(reader, ReadHeader, cancellationToken).ConfigureAwait(false);
        }

        if (headers.Length == 0)
        {
            transaction.Commit();
            return new PagedResult<GoodsReceiptDto>(
                Array.Empty<GoodsReceiptDto>(),
                request.PageNumber,
                request.PageSize,
                totalCount);
        }

        var receiptIds = headers.Select(header => header.ReceiptId).ToArray();
        // The page is bounded by the existing request validator; values remain BigInt parameters.
        var parameterNames = receiptIds.Select((_, index) => "@receiptId" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        using var lineCommand = InventorySql.Command(connection, transaction,
            "SELECT " + LineColumns + " FROM dbo.GoodsReceiptLines line " +
            "INNER JOIN dbo.Products product ON product.ProductId = line.ProductId " +
            "WHERE line.ReceiptId IN (" + string.Join(",", parameterNames) + ") " +
            "ORDER BY line.ReceiptId, line.[LineNo], line.ReceiptLineId;");
        for (var index = 0; index < receiptIds.Length; index++)
            InventorySql.Add(lineCommand, parameterNames[index], SqlDbType.BigInt, receiptIds[index]);
        LineRow[] lines;
        using (var reader = await lineCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            lines = await InventorySql.ReadRowsAsync(reader, ReadLine, cancellationToken).ConfigureAwait(false);
        var linesByReceipt = lines
            .GroupBy(line => line.ReceiptId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LineRow>)group.ToArray());
        var items = headers.Select(header =>
            ToDto(
                header,
                linesByReceipt.TryGetValue(header.ReceiptId, out var receiptLines)
                    ? receiptLines
                    : Array.Empty<LineRow>())).ToArray();
        transaction.Commit();
        return new PagedResult<GoodsReceiptDto>(
            items,
            request.PageNumber,
            request.PageSize,
            totalCount);
    }

    public async Task<GoodsReceiptDto?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        using var command = InventorySql.Command(connection, transaction,
            "SELECT " + HeaderColumns +
            " FROM dbo.GoodsReceipts gr " +
            "INNER JOIN dbo.Warehouses w ON w.WarehouseId = gr.WarehouseId " +
            "INNER JOIN dbo.Employees e ON e.EmployeeId = gr.EmployeeId " +
            "WHERE gr.ReceiptId = @Id; " +
            "SELECT " + LineColumns + " FROM dbo.GoodsReceiptLines line " +
            "INNER JOIN dbo.Products product ON product.ProductId = line.ProductId " +
            "WHERE line.ReceiptId = @Id " +
            "ORDER BY line.[LineNo], line.ReceiptLineId;");
        InventorySql.Add(command, "@Id", SqlDbType.BigInt, id);
        HeaderRow? header;
        LineRow[] lines;
        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            header = await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadHeader(reader) : null;
            await InventorySql.NextResultAsync(reader, cancellationToken).ConfigureAwait(false);
            lines = await InventorySql.ReadRowsAsync(reader, ReadLine, cancellationToken).ConfigureAwait(false);
        }
        transaction.Commit();
        return header == null ? null : ToDto(header, lines);
    }

    private static GoodsReceiptDto ToDto(
        HeaderRow header,
        IReadOnlyList<LineRow> lines) =>
        new GoodsReceiptDto(
            header.ReceiptId,
            header.ReceiptNo,
            header.ReceiptDate,
            header.WarehouseId,
            header.EmployeeId,
            header.Status,
            header.Note,
            header.PostedAtUtc,
            Convert.ToBase64String(header.Version),
            lines.Select(line => new GoodsReceiptLineDto(
                line.ReceiptLineId,
                line.LineNumber,
                line.ProductId,
                line.Quantity,
                line.UnitCost,
                FormatDisplayName(line.ProductCode, line.ProductName),
                line.ProductIsActive)),
            FormatDisplayName(header.WarehouseCode, header.WarehouseName),
            header.WarehouseIsActive,
            FormatDisplayName(header.EmployeeCode, header.EmployeeName),
            header.EmployeeIsActive);

    private static string FormatDisplayName(string code, string name) =>
        code + " - " + name;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

    private static HeaderRow ReadHeader(SqlDataReader reader) => new HeaderRow
    {
        ReceiptId = reader.GetInt64(0),
        ReceiptNo = reader.GetString(1),
        ReceiptDate = reader.GetDateTime(2),
        WarehouseId = reader.GetInt32(3),
        WarehouseCode = reader.GetString(4),
        WarehouseName = reader.GetString(5),
        WarehouseIsActive = reader.GetBoolean(6),
        EmployeeId = reader.GetInt32(7),
        EmployeeCode = reader.GetString(8),
        EmployeeName = reader.GetString(9),
        EmployeeIsActive = reader.GetBoolean(10),
        Status = reader.GetString(11),
        Note = reader.IsDBNull(12) ? null : reader.GetString(12),
        PostedAtUtc = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
        Version = (byte[])reader.GetValue(14),
    };

    private static LineRow ReadLine(SqlDataReader reader) => new LineRow
    {
        ReceiptLineId = reader.GetInt64(0),
        ReceiptId = reader.GetInt64(1),
        LineNumber = reader.GetInt32(2),
        ProductId = reader.GetInt32(3),
        ProductCode = reader.GetString(4),
        ProductName = reader.GetString(5),
        ProductIsActive = reader.GetBoolean(6),
        Quantity = reader.GetDecimal(7),
        UnitCost = reader.GetDecimal(8),
    };

    private sealed class HeaderRow
    {
        public long ReceiptId { get; set; }
        public string ReceiptNo { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseCode { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public bool WarehouseIsActive { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public bool EmployeeIsActive { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime? PostedAtUtc { get; set; }
        public byte[] Version { get; set; } = Array.Empty<byte>();
    }

    private sealed class LineRow
    {
        public long ReceiptLineId { get; set; }
        public long ReceiptId { get; set; }
        public int LineNumber { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public bool ProductIsActive { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }
}
