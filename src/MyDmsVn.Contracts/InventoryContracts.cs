using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MyDmsVn.Contracts
{
    public static class GoodsReceiptContractLimits
    {
        public const int DecimalPlaces = 4;
        public const decimal MaximumQuantity = 99999999999999.9999m;
        public const decimal MaximumUnitCost = 999999999999999.9999m;
    }

    public sealed class GoodsReceiptLineDto
    {
        public GoodsReceiptLineDto(
            long id,
            int lineNumber,
            int productId,
            decimal quantity,
            decimal unitCost,
            string? productDisplayName = null,
            bool productIsActive = true)
        {
            Id = id;
            LineNumber = lineNumber;
            ProductId = productId;
            Quantity = quantity;
            UnitCost = unitCost;
            ProductDisplayName = productDisplayName;
            ProductIsActive = productIsActive;
        }

        public long Id { get; }
        public int LineNumber { get; }
        public int ProductId { get; }
        public decimal Quantity { get; }
        public decimal UnitCost { get; }
        public string? ProductDisplayName { get; }
        public bool ProductIsActive { get; }
    }

    public sealed class GoodsReceiptDto
    {
        public GoodsReceiptDto(
            long id,
            string receiptNumber,
            DateTime receiptDate,
            int warehouseId,
            int employeeId,
            string status,
            string? note,
            DateTime? postedAtUtc,
            string version,
            IEnumerable<GoodsReceiptLineDto> lines)
            : this(
                id,
                receiptNumber,
                receiptDate,
                warehouseId,
                employeeId,
                status,
                note,
                postedAtUtc,
                version,
                lines,
                null,
                true,
                null,
                true)
        {
        }

        [JsonConstructor]
        public GoodsReceiptDto(
            long id,
            string receiptNumber,
            DateTime receiptDate,
            int warehouseId,
            int employeeId,
            string status,
            string? note,
            DateTime? postedAtUtc,
            string version,
            IEnumerable<GoodsReceiptLineDto> lines,
            string? warehouseDisplayName = null,
            bool warehouseIsActive = true,
            string? employeeDisplayName = null,
            bool employeeIsActive = true)
        {
            Id = id;
            ReceiptNumber = receiptNumber;
            ReceiptDate = receiptDate;
            WarehouseId = warehouseId;
            EmployeeId = employeeId;
            Status = status;
            Note = note;
            PostedAtUtc = postedAtUtc;
            Version = version;
            Lines = lines.ToArray();
            WarehouseDisplayName = warehouseDisplayName;
            WarehouseIsActive = warehouseIsActive;
            EmployeeDisplayName = employeeDisplayName;
            EmployeeIsActive = employeeIsActive;
        }

        public long Id { get; }
        public string ReceiptNumber { get; }
        [JsonConverter(typeof(CalendarDateJsonConverter))]
        public DateTime ReceiptDate { get; }
        public int WarehouseId { get; }
        public int EmployeeId { get; }
        public string Status { get; }
        public string? Note { get; }
        public DateTime? PostedAtUtc { get; }
        public string Version { get; }
        public IReadOnlyList<GoodsReceiptLineDto> Lines { get; }
        public string? WarehouseDisplayName { get; }
        public bool WarehouseIsActive { get; }
        public string? EmployeeDisplayName { get; }
        public bool EmployeeIsActive { get; }
    }

    public sealed class SaveGoodsReceiptLineRequest
    {
        public SaveGoodsReceiptLineRequest(int productId, decimal quantity, decimal unitCost)
        {
            ProductId = productId;
            Quantity = quantity;
            UnitCost = unitCost;
        }

        public int ProductId { get; }
        public decimal Quantity { get; }
        public decimal UnitCost { get; }
    }

    public sealed class SaveGoodsReceiptRequest
    {
        public SaveGoodsReceiptRequest(
            DateTime receiptDate,
            int warehouseId,
            int employeeId,
            string? note,
            IEnumerable<SaveGoodsReceiptLineRequest> lines)
        {
            ReceiptDate = receiptDate;
            WarehouseId = warehouseId;
            EmployeeId = employeeId;
            Note = note;
            Lines = lines.ToArray();
        }

        [JsonConverter(typeof(CalendarDateJsonConverter))]
        public DateTime ReceiptDate { get; }
        public int WarehouseId { get; }
        public int EmployeeId { get; }
        public string? Note { get; }
        public IReadOnlyList<SaveGoodsReceiptLineRequest> Lines { get; }
    }

    public sealed class GoodsReceiptListRequest
    {
        public GoodsReceiptListRequest(
            int pageNumber,
            int pageSize,
            int? warehouseId,
            string? status,
            string? search = null)
        {
            PageNumber = pageNumber;
            PageSize = pageSize;
            WarehouseId = warehouseId;
            Status = status;
            Search = search;
        }

        public int PageNumber { get; }
        public int PageSize { get; }
        public int? WarehouseId { get; }
        public string? Status { get; }
        public string? Search { get; }
    }

    public sealed class UpdateGoodsReceiptDraftRequest
    {
        public UpdateGoodsReceiptDraftRequest(
            long receiptId,
            string expectedVersion,
            DateTime receiptDate,
            int warehouseId,
            int employeeId,
            string? note,
            IEnumerable<SaveGoodsReceiptLineRequest> lines)
        {
            ReceiptId = receiptId;
            ExpectedVersion = expectedVersion;
            ReceiptDate = receiptDate;
            WarehouseId = warehouseId;
            EmployeeId = employeeId;
            Note = note;
            Lines = lines.ToArray();
        }

        public long ReceiptId { get; }
        public string ExpectedVersion { get; }
        [JsonConverter(typeof(CalendarDateJsonConverter))]
        public DateTime ReceiptDate { get; }
        public int WarehouseId { get; }
        public int EmployeeId { get; }
        public string? Note { get; }
        public IReadOnlyList<SaveGoodsReceiptLineRequest> Lines { get; }
    }

    public sealed class PostGoodsReceiptRequest
    {
        public PostGoodsReceiptRequest(long receiptId, string expectedVersion)
        {
            ReceiptId = receiptId;
            ExpectedVersion = expectedVersion;
        }

        public long ReceiptId { get; }
        public string ExpectedVersion { get; }
    }

    public sealed class PostGoodsReceiptResponse
    {
        public PostGoodsReceiptResponse(long receiptId, string status, DateTime postedAtUtc)
        {
            ReceiptId = receiptId;
            Status = status;
            PostedAtUtc = postedAtUtc;
        }

        public long ReceiptId { get; }
        public string Status { get; }
        public DateTime PostedAtUtc { get; }
    }

    public sealed class StockBalanceQuery
    {
        public StockBalanceQuery(int? warehouseId, int pageNumber, int pageSize)
        {
            WarehouseId = warehouseId;
            PageNumber = pageNumber;
            PageSize = pageSize;
        }

        public int? WarehouseId { get; }
        public int PageNumber { get; }
        public int PageSize { get; }
    }

    public sealed class StockBalanceRow
    {
        public StockBalanceRow(int warehouseId, int productId, string productCode, string productName, decimal quantity)
        {
            WarehouseId = warehouseId;
            ProductId = productId;
            ProductCode = productCode;
            ProductName = productName;
            Quantity = quantity;
        }

        public int WarehouseId { get; }
        public int ProductId { get; }
        public string ProductCode { get; }
        public string ProductName { get; }
        public decimal Quantity { get; }
    }

    public sealed class StockCardQuery
    {
        public StockCardQuery(int warehouseId, int productId, DateTime? fromUtc, DateTime? toUtc, int pageNumber, int pageSize)
        {
            WarehouseId = warehouseId;
            ProductId = productId;
            FromUtc = fromUtc;
            ToUtc = toUtc;
            PageNumber = pageNumber;
            PageSize = pageSize;
        }

        public int WarehouseId { get; }
        public int ProductId { get; }
        public DateTime? FromUtc { get; }
        public DateTime? ToUtc { get; }
        public int PageNumber { get; }
        public int PageSize { get; }
    }

    public sealed class StockCardRow
    {
        public StockCardRow(long ledgerId, DateTime postedAtUtc, string documentType, string documentNumber, decimal quantityChange)
        {
            LedgerId = ledgerId;
            PostedAtUtc = postedAtUtc;
            DocumentType = documentType;
            DocumentNumber = documentNumber;
            QuantityChange = quantityChange;
        }

        public long LedgerId { get; }
        public DateTime PostedAtUtc { get; }
        public string DocumentType { get; }
        public string DocumentNumber { get; }
        public decimal QuantityChange { get; }
    }
}
