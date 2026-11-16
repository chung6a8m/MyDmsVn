using System;
using System.Globalization;
using System.Linq;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.HttpHost;

// Explicit v1 projection. The Local DTOs and their numeric serialization remain unchanged.
internal static class SalesWireOutput
{
    public static SalesDetailHttpResponse Detail(SalesReceiptDto value) => new SalesDetailHttpResponse(
        Id(value.SaleId), value.SaleNumber, Date(value.SaleDate), value.CustomerId, value.WarehouseId, value.EmployeeId,
        value.Status, value.Note, value.PostedAtUtc.HasValue ? Utc(value.PostedAtUtc.Value) : null, value.Version,
        value.Lines.Select(line => new SalesDetailLineHttpResponse(
            Id(line.SaleLineId), line.LineNo, line.ProductId, line.Unit, Number(line.Qty), Number(line.UnitPrice),
            line.IsGift, Number(line.LineAmount), Number(line.PostedUnitCost), Number(line.CostAmount), line.ProductDisplayName, line.ProductIsActive)).ToArray(),
        Number(value.TotalAmount), Number(value.TotalCost), Number(value.GrossProfit), value.CustomerDisplayName, value.CustomerIsActive,
        value.WarehouseDisplayName, value.WarehouseIsActive, value.EmployeeDisplayName, value.EmployeeIsActive);

    public static SalesListPageHttpResponse Page(SalesReceiptListPage value) => new SalesListPageHttpResponse(
        value.Items.Select(row => new SalesListRowHttpResponse(
            Id(row.SaleId), row.SaleNumber, Date(row.SaleDate), row.CustomerId, row.CustomerDisplayName, row.WarehouseId,
            row.WarehouseDisplayName, row.EmployeeId, row.EmployeeDisplayName, row.Status, row.LineCount, Number(row.TotalAmount))).ToArray(),
        value.NextCursor, value.PreviousCursor);

    public static SalesPostHttpResponse Post(PostSalesReceiptResponse value) =>
        new SalesPostHttpResponse(Id(value.SaleId), value.Status, Utc(value.PostedAtUtc), value.Version);
    private static string Id(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string? Number(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);
    private static string Date(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Utc(DateTime value)
    {
        // SQL datetime2 UTC values arrive with Unspecified kind; never apply the server's local offset.
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);
    }
}

internal sealed class SalesDetailHttpResponse
{
    public SalesDetailHttpResponse(string saleId, string saleNumber, string saleDate, int customerId, int warehouseId, int employeeId,
        string status, string? note, string? postedAtUtc, string version, SalesDetailLineHttpResponse[] lines, string totalAmount,
        string? totalCost, string? grossProfit, string? customerDisplayName, bool customerIsActive, string? warehouseDisplayName,
        bool warehouseIsActive, string? employeeDisplayName, bool employeeIsActive)
    {
        SaleId = saleId; SaleNumber = saleNumber; SaleDate = saleDate; CustomerId = customerId; WarehouseId = warehouseId; EmployeeId = employeeId;
        Status = status; Note = note; PostedAtUtc = postedAtUtc; Version = version; Lines = lines; TotalAmount = totalAmount; TotalCost = totalCost;
        GrossProfit = grossProfit; CustomerDisplayName = customerDisplayName; CustomerIsActive = customerIsActive; WarehouseDisplayName = warehouseDisplayName;
        WarehouseIsActive = warehouseIsActive; EmployeeDisplayName = employeeDisplayName; EmployeeIsActive = employeeIsActive;
    }
    public string SaleId { get; }
    public string SaleNumber { get; }
    public string SaleDate { get; }
    public int CustomerId { get; }
    public int WarehouseId { get; }
    public int EmployeeId { get; }
    public string Status { get; }
    public string? Note { get; }
    public string? PostedAtUtc { get; }
    public string Version { get; }
    public SalesDetailLineHttpResponse[] Lines { get; }
    public string TotalAmount { get; }
    public string? TotalCost { get; }
    public string? GrossProfit { get; }
    public string? CustomerDisplayName { get; }
    public bool CustomerIsActive { get; }
    public string? WarehouseDisplayName { get; }
    public bool WarehouseIsActive { get; }
    public string? EmployeeDisplayName { get; }
    public bool EmployeeIsActive { get; }
}

internal sealed class SalesDetailLineHttpResponse
{
    public SalesDetailLineHttpResponse(string saleLineId, int lineNo, int productId, string unit, string qty, string unitPrice,
        bool isGift, string lineAmount, string? postedUnitCost, string? costAmount, string? productDisplayName, bool productIsActive)
    {
        SaleLineId = saleLineId; LineNo = lineNo; ProductId = productId; Unit = unit; Qty = qty; UnitPrice = unitPrice; IsGift = isGift;
        LineAmount = lineAmount; PostedUnitCost = postedUnitCost; CostAmount = costAmount; ProductDisplayName = productDisplayName; ProductIsActive = productIsActive;
    }
    public string SaleLineId { get; }
    public int LineNo { get; }
    public int ProductId { get; }
    public string Unit { get; }
    public string Qty { get; }
    public string UnitPrice { get; }
    public bool IsGift { get; }
    public string LineAmount { get; }
    public string? PostedUnitCost { get; }
    public string? CostAmount { get; }
    public string? ProductDisplayName { get; }
    public bool ProductIsActive { get; }
}

internal sealed class SalesListPageHttpResponse
{
    public SalesListPageHttpResponse(SalesListRowHttpResponse[] items, string? nextCursor, string? previousCursor)
    { Items = items; NextCursor = nextCursor; PreviousCursor = previousCursor; }
    public SalesListRowHttpResponse[] Items { get; }
    public string? NextCursor { get; }
    public string? PreviousCursor { get; }
}

internal sealed class SalesListRowHttpResponse
{
    public SalesListRowHttpResponse(string saleId, string saleNumber, string saleDate, int customerId, string? customerDisplayName,
        int warehouseId, string? warehouseDisplayName, int employeeId, string? employeeDisplayName, string status, int lineCount, string totalAmount)
    {
        SaleId = saleId; SaleNumber = saleNumber; SaleDate = saleDate; CustomerId = customerId; CustomerDisplayName = customerDisplayName;
        WarehouseId = warehouseId; WarehouseDisplayName = warehouseDisplayName; EmployeeId = employeeId; EmployeeDisplayName = employeeDisplayName;
        Status = status; LineCount = lineCount; TotalAmount = totalAmount;
    }
    public string SaleId { get; }
    public string SaleNumber { get; }
    public string SaleDate { get; }
    public int CustomerId { get; }
    public string? CustomerDisplayName { get; }
    public int WarehouseId { get; }
    public string? WarehouseDisplayName { get; }
    public int EmployeeId { get; }
    public string? EmployeeDisplayName { get; }
    public string Status { get; }
    public int LineCount { get; }
    public string TotalAmount { get; }
}

internal sealed class SalesPostHttpResponse
{
    public SalesPostHttpResponse(string saleId, string status, string postedAtUtc, string version)
    { SaleId = saleId; Status = status; PostedAtUtc = postedAtUtc; Version = version; }
    public string SaleId { get; }
    public string Status { get; }
    public string PostedAtUtc { get; }
    public string Version { get; }
}
