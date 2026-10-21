using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
namespace MyDmsVn.Contracts;

public static class SalesContractLimits
{
    public const int MaximumLines = 200;
    public const int MaximumNoteLength = 1000;
    public const decimal MaximumQuantity = 99999999999999.9999m;
    public const decimal MaximumUnitPrice = 999999999999999.9999m;
    public const decimal MaximumAmount = 999999999999999999999999.9999m;
}

public sealed class SaveSalesReceiptLineRequest
{
    [JsonConstructor]
    public SaveSalesReceiptLineRequest(long saleLineId, int productId, decimal qty, decimal unitPrice, bool isGift)
    {
        SaleLineId = saleLineId;
        ProductId = productId;
        Qty = qty;
        UnitPrice = unitPrice;
        IsGift = isGift;
    }
    public long SaleLineId { get; }
    public int ProductId { get; }
    public decimal Qty { get; }
    public decimal UnitPrice { get; }
    public bool IsGift { get; }
}

public sealed class SaveSalesReceiptRequest
{
    [JsonConstructor]
    public SaveSalesReceiptRequest(DateTime saleDate, int customerId, int warehouseId, int employeeId, string? note, IReadOnlyList<SaveSalesReceiptLineRequest> lines)
    {
        SaleDate = SalesCalendarDateJsonConverter.Normalize(saleDate);
        CustomerId = customerId;
        WarehouseId = warehouseId;
        EmployeeId = employeeId;
        Note = note;
        Lines = Array.AsReadOnly((lines ?? throw new ArgumentNullException(nameof(lines))).ToArray());
    }
    [JsonConverter(typeof(SalesCalendarDateJsonConverter))]
    public DateTime SaleDate { get; }
    public int CustomerId { get; }
    public int WarehouseId { get; }
    public int EmployeeId { get; }
    public string? Note { get; }
    public IReadOnlyList<SaveSalesReceiptLineRequest> Lines { get; }
}

public sealed class UpdateSalesReceiptDraftRequest
{
    [JsonConstructor]
    public UpdateSalesReceiptDraftRequest(long saleId, string expectedVersion, DateTime saleDate, int customerId, int warehouseId, int employeeId, string? note, IReadOnlyList<SaveSalesReceiptLineRequest> lines)
    {
        SaleId = saleId;
        ExpectedVersion = expectedVersion;
        SaleDate = SalesCalendarDateJsonConverter.Normalize(saleDate);
        CustomerId = customerId;
        WarehouseId = warehouseId;
        EmployeeId = employeeId;
        Note = note;
        Lines = Array.AsReadOnly((lines ?? throw new ArgumentNullException(nameof(lines))).ToArray());
    }
    public long SaleId { get; }
    public string ExpectedVersion { get; }
    [JsonConverter(typeof(SalesCalendarDateJsonConverter))]
    public DateTime SaleDate { get; }
    public int CustomerId { get; }
    public int WarehouseId { get; }
    public int EmployeeId { get; }
    public string? Note { get; }
    public IReadOnlyList<SaveSalesReceiptLineRequest> Lines { get; }
}

public sealed class PostSalesReceiptRequest
{
    [JsonConstructor]
    public PostSalesReceiptRequest(long saleId, string expectedVersion)
    {
        SaleId = saleId;
        ExpectedVersion = expectedVersion;
    }
    public long SaleId { get; }
    public string ExpectedVersion { get; }
}

public sealed class PostSalesReceiptResponse
{
    [JsonConstructor]
    public PostSalesReceiptResponse(long saleId, string status, DateTime postedAtUtc, string version)
    {
        SaleId = saleId;
        Status = status;
        PostedAtUtc = postedAtUtc;
        Version = version;
    }
    public long SaleId { get; }
    public string Status { get; }
    public DateTime PostedAtUtc { get; }
    public string Version { get; }
}

public sealed class SalesReceiptLineDto
{
    [JsonConstructor]
    public SalesReceiptLineDto(long saleLineId, int lineNo, int productId, string unit, decimal qty, decimal unitPrice, bool isGift, decimal lineAmount, decimal? postedUnitCost, decimal? costAmount, string? productDisplayName, bool productIsActive)
    {
        SaleLineId = saleLineId;
        LineNo = lineNo;
        ProductId = productId;
        Unit = unit;
        Qty = qty;
        UnitPrice = unitPrice;
        IsGift = isGift;
        LineAmount = lineAmount;
        PostedUnitCost = postedUnitCost;
        CostAmount = costAmount;
        ProductDisplayName = productDisplayName;
        ProductIsActive = productIsActive;
    }
    public long SaleLineId { get; }
    public int LineNo { get; }
    public int ProductId { get; }
    public string Unit { get; }
    public decimal Qty { get; }
    public decimal UnitPrice { get; }
    public bool IsGift { get; }
    public decimal LineAmount { get; }
    public decimal? PostedUnitCost { get; }
    public decimal? CostAmount { get; }
    public string? ProductDisplayName { get; }
    public bool ProductIsActive { get; }
}

public sealed class SalesReceiptDto
{
    [JsonConstructor]
    public SalesReceiptDto(long saleId, string saleNumber, DateTime saleDate, int customerId, int warehouseId, int employeeId, string status, string? note, DateTime? postedAtUtc, string version, IReadOnlyList<SalesReceiptLineDto> lines, decimal totalAmount, decimal? totalCost, decimal? grossProfit, string? customerDisplayName, bool customerIsActive, string? warehouseDisplayName, bool warehouseIsActive, string? employeeDisplayName, bool employeeIsActive)
    {
        SaleId = saleId;
        SaleNumber = saleNumber;
        SaleDate = SalesCalendarDateJsonConverter.Normalize(saleDate);
        CustomerId = customerId;
        WarehouseId = warehouseId;
        EmployeeId = employeeId;
        Status = status;
        Note = note;
        PostedAtUtc = postedAtUtc;
        Version = version;
        Lines = Array.AsReadOnly((lines ?? throw new ArgumentNullException(nameof(lines))).ToArray());
        if (status != "Draft" && status != "Posted") throw new ArgumentException("Unknown sales status.", nameof(status));
        var posted = status == "Posted";
        if (posted != postedAtUtc.HasValue || posted != totalCost.HasValue || posted != grossProfit.HasValue ||
            Lines.Any(line => line == null || line.PostedUnitCost.HasValue != posted || line.CostAmount.HasValue != posted))
            throw new ArgumentException("Sales cost fields must be absent for Draft and present for Posted.", nameof(lines));
        TotalAmount = totalAmount;
        TotalCost = totalCost;
        GrossProfit = grossProfit;
        CustomerDisplayName = customerDisplayName;
        CustomerIsActive = customerIsActive;
        WarehouseDisplayName = warehouseDisplayName;
        WarehouseIsActive = warehouseIsActive;
        EmployeeDisplayName = employeeDisplayName;
        EmployeeIsActive = employeeIsActive;
    }
    public long SaleId { get; }
    public string SaleNumber { get; }
    [JsonConverter(typeof(SalesCalendarDateJsonConverter))]
    public DateTime SaleDate { get; }
    public int CustomerId { get; }
    public int WarehouseId { get; }
    public int EmployeeId { get; }
    public string Status { get; }
    public string? Note { get; }
    public DateTime? PostedAtUtc { get; }
    public string Version { get; }
    public IReadOnlyList<SalesReceiptLineDto> Lines { get; }
    public decimal TotalAmount { get; }
    public decimal? TotalCost { get; }
    public decimal? GrossProfit { get; }
    public string? CustomerDisplayName { get; }
    public bool CustomerIsActive { get; }
    public string? WarehouseDisplayName { get; }
    public bool WarehouseIsActive { get; }
    public string? EmployeeDisplayName { get; }
    public bool EmployeeIsActive { get; }
}

public sealed class SalesReceiptListRequest
{
    [JsonConstructor]
    public SalesReceiptListRequest(DateTime? fromDate, DateTime? toDate, int? customerId, int? warehouseId, int? employeeId, string? status, int? productId, string? search, int? pageSize = null, string? cursor = null)
    {
        FromDate = fromDate.HasValue ? SalesCalendarDateJsonConverter.Normalize(fromDate.Value) : (DateTime?)null;
        ToDate = toDate.HasValue ? SalesCalendarDateJsonConverter.Normalize(toDate.Value) : (DateTime?)null;
        CustomerId = customerId;
        WarehouseId = warehouseId;
        EmployeeId = employeeId;
        Status = status;
        ProductId = productId;
        Search = search;
        PageSize = pageSize ?? 50;
        Cursor = cursor;
    }
    [JsonConverter(typeof(SalesCalendarDateJsonConverter))]
    public DateTime? FromDate { get; }
    [JsonConverter(typeof(SalesCalendarDateJsonConverter))]
    public DateTime? ToDate { get; }
    public int? CustomerId { get; }
    public int? WarehouseId { get; }
    public int? EmployeeId { get; }
    public string? Status { get; }
    public int? ProductId { get; }
    public string? Search { get; }
    public int PageSize { get; }
    public string? Cursor { get; }
}

public sealed class SalesReceiptListRow
{
    [JsonConstructor]
    public SalesReceiptListRow(long saleId, string saleNumber, DateTime saleDate, int customerId, string customerDisplayName, int warehouseId, string warehouseDisplayName, int employeeId, string employeeDisplayName, string status, int lineCount, decimal totalAmount)
    {
        SaleId = saleId;
        SaleNumber = saleNumber;
        SaleDate = SalesCalendarDateJsonConverter.Normalize(saleDate);
        CustomerId = customerId;
        CustomerDisplayName = customerDisplayName;
        WarehouseId = warehouseId;
        WarehouseDisplayName = warehouseDisplayName;
        EmployeeId = employeeId;
        EmployeeDisplayName = employeeDisplayName;
        Status = status;
        LineCount = lineCount;
        TotalAmount = totalAmount;
    }
    public long SaleId { get; }
    public string SaleNumber { get; }
    [JsonConverter(typeof(SalesCalendarDateJsonConverter))]
    public DateTime SaleDate { get; }
    public int CustomerId { get; }
    public string CustomerDisplayName { get; }
    public int WarehouseId { get; }
    public string WarehouseDisplayName { get; }
    public int EmployeeId { get; }
    public string EmployeeDisplayName { get; }
    public string Status { get; }
    public int LineCount { get; }
    public decimal TotalAmount { get; }
}

public sealed class SalesReceiptListPage
{
    [JsonConstructor]
    public SalesReceiptListPage(IReadOnlyList<SalesReceiptListRow> items, string? nextCursor, string? previousCursor)
    {
        Items = Array.AsReadOnly((items ?? throw new ArgumentNullException(nameof(items))).ToArray());
        NextCursor = nextCursor;
        PreviousCursor = previousCursor;
    }
    public IReadOnlyList<SalesReceiptListRow> Items { get; }
    public string? NextCursor { get; }
    public string? PreviousCursor { get; }
}
