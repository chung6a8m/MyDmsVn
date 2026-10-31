using System;
using System.Globalization;
using System.Linq;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.HttpHost;

// Explicit v1 projection. The Local DTOs and their numeric serialization remain unchanged.
internal static class SalesWireOutput
{
    public static object Detail(SalesReceiptDto value) => new
    {
        saleId = Id(value.SaleId), value.SaleNumber, saleDate = Date(value.SaleDate), value.CustomerId, value.WarehouseId, value.EmployeeId,
        value.Status, value.Note, postedAtUtc = value.PostedAtUtc.HasValue ? Utc(value.PostedAtUtc.Value) : null, value.Version,
        lines = value.Lines.Select(line => new
        {
            saleLineId = Id(line.SaleLineId), line.LineNo, line.ProductId, line.Unit, qty = Number(line.Qty), unitPrice = Number(line.UnitPrice),
            line.IsGift, lineAmount = Number(line.LineAmount), postedUnitCost = Number(line.PostedUnitCost), costAmount = Number(line.CostAmount),
            line.ProductDisplayName, line.ProductIsActive,
        }).ToArray(),
        totalAmount = Number(value.TotalAmount), totalCost = Number(value.TotalCost), grossProfit = Number(value.GrossProfit),
        value.CustomerDisplayName, value.CustomerIsActive, value.WarehouseDisplayName, value.WarehouseIsActive, value.EmployeeDisplayName, value.EmployeeIsActive,
    };
    public static object Page(SalesReceiptListPage value) => new
    {
        items = value.Items.Select(row => new
        {
            saleId = Id(row.SaleId), row.SaleNumber, saleDate = Date(row.SaleDate), row.CustomerId, row.CustomerDisplayName,
            row.WarehouseId, row.WarehouseDisplayName, row.EmployeeId, row.EmployeeDisplayName, row.Status, row.LineCount, totalAmount = Number(row.TotalAmount),
        }).ToArray(),
        value.NextCursor, value.PreviousCursor,
    };
    public static object Post(PostSalesReceiptResponse value) => new { saleId = Id(value.SaleId), value.Status, postedAtUtc = Utc(value.PostedAtUtc), value.Version };
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
