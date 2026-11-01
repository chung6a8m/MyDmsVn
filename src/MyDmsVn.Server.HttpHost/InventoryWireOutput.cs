using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MyDmsVn.Contracts;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

internal static class InventoryWireOutput
{
    private const decimal MaximumAmount = 999999999999999999999999.9999m;
    private const decimal MaximumAverage = 99999999999999999999.99999999m;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    // JObject keeps required explicit null members local, regardless of ApiJson's global null-ignore setting.
    public static JObject Detail(GoodsReceiptDto value)
    {
        Require(value.Status == "Draft" || value.Status == "Posted");
        Require(value.Status == "Draft" ? !value.PostedAtUtc.HasValue : value.PostedAtUtc.HasValue);
        Require(value.ReceiptDate != default && value.Lines.Count >= 1 && value.Lines.Count <= 200);
        Require(value.ReceiptNumber != null && Regex.IsMatch(value.ReceiptNumber, "\\AGR[0-9]{10}\\z", RegexOptions.CultureInvariant) && value.ReceiptNumber != "GR0000000000");
        Require(CanonicalVersion(value.Version));
        var products = new HashSet<int>(); var ids = new HashSet<long>(); var lines = new JArray();
        for (var index = 0; index < value.Lines.Count; index++)
        {
            var line = value.Lines[index];
            Require(line.LineNumber == index + 1 && products.Add(line.ProductId) && ids.Add(line.Id));
            lines.Add(new JObject
            {
                ["receiptLineId"] = Id(line.Id), ["lineNumber"] = line.LineNumber, ["productId"] = Positive(line.ProductId),
                ["quantity"] = Number(line.Quantity, 4, GoodsReceiptContractLimits.MaximumQuantity, positive: true),
                ["unitCost"] = Number(line.UnitCost, 4, GoodsReceiptContractLimits.MaximumUnitCost),
                ["productDisplayName"] = NullableText(line.ProductDisplayName), ["productIsActive"] = line.ProductIsActive,
            });
        }
        return new JObject
        {
            ["receiptId"] = Id(value.Id), ["receiptNumber"] = value.ReceiptNumber, ["receiptDate"] = value.ReceiptDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["warehouseId"] = Positive(value.WarehouseId), ["warehouseDisplayName"] = NullableText(value.WarehouseDisplayName), ["warehouseIsActive"] = value.WarehouseIsActive,
            ["employeeId"] = Positive(value.EmployeeId), ["employeeDisplayName"] = NullableText(value.EmployeeDisplayName), ["employeeIsActive"] = value.EmployeeIsActive,
            ["status"] = value.Status, ["note"] = NullableText(value.Note, 1000),
            ["postedAtUtc"] = value.PostedAtUtc.HasValue ? new JValue(Utc(value.PostedAtUtc.Value)) : JValue.CreateNull(),
            ["version"] = value.Version, ["lines"] = lines,
        };
    }
    public static JObject ReceiptPage(PagedResult<GoodsReceiptDto> value) => Page(value, Detail);
    public static JObject Post(PostGoodsReceiptResponse value)
    {
        Require(value.Status == "Posted");
        return new JObject { ["receiptId"] = Id(value.ReceiptId), ["status"] = value.Status, ["postedAtUtc"] = Utc(value.PostedAtUtc) };
    }
    public static JObject BalancesPage(PagedResult<StockBalanceRow> value) => Page(value, Balance);
    public static JObject StockCardPage(PagedResult<StockCardRow> value) => Page(value, Card);
    private static JObject Page<T>(PagedResult<T> value, Func<T, JObject> map)
    {
        Require(value.PageSize <= 200 && value.Items.Count <= value.PageSize);
        // Inventory counts and rows are independent reads; a concurrent write may make count < items.Length.
        return new JObject
        {
            ["items"] = new JArray(value.Items.Select(map)), ["pageNumber"] = Positive(value.PageNumber),
            ["pageSize"] = Positive(value.PageSize), ["totalCount"] = Id(value.TotalCount, true),
        };
    }
    private static JObject Balance(StockBalanceRow value)
    {
        Require(value.Quantity == 0 ? value.InventoryValue == 0 && !value.AverageUnitCost.HasValue : value.AverageUnitCost.HasValue);
        var result = References(value.WarehouseId, value.WarehouseCode, value.WarehouseName, value.WarehouseIsActive,
            value.ProductId, value.ProductCode, value.ProductName, value.ProductIsActive);
        result["quantity"] = Number(value.Quantity, 4, GoodsReceiptContractLimits.MaximumQuantity);
        result["inventoryValue"] = Number(value.InventoryValue, 4, MaximumAmount);
        result["averageUnitCost"] = value.AverageUnitCost.HasValue ? new JValue(Number(value.AverageUnitCost.Value, 8, MaximumAverage)) : JValue.CreateNull();
        return result;
    }
    private static JObject Card(StockCardRow value)
    {
        Require(value.QuantityChange != 0);
        if (value.DocumentType == "GoodsReceipt") Require(value.QuantityChange > 0 && value.ValueChange >= 0);
        if (value.DocumentType == "SalesReceipt") Require(value.QuantityChange < 0 && value.ValueChange <= 0);
        Require(!string.IsNullOrEmpty(value.DocumentType) && !string.IsNullOrEmpty(value.DocumentNumber));
        var result = References(value.WarehouseId, value.WarehouseCode, value.WarehouseName, value.WarehouseIsActive,
            value.ProductId, value.ProductCode, value.ProductName, value.ProductIsActive);
        result["ledgerId"] = Id(value.LedgerId); result["postedAtUtc"] = Utc(value.PostedAtUtc);
        result["documentType"] = Text(value.DocumentType); result["documentId"] = Id(value.DocumentId); result["documentNumber"] = Text(value.DocumentNumber);
        result["quantityChange"] = Number(value.QuantityChange, 4, GoodsReceiptContractLimits.MaximumQuantity, signed: true);
        result["valueChange"] = Number(value.ValueChange, 4, MaximumAmount, signed: true);
        return result;
    }
    private static JObject References(int warehouseId, string warehouseCode, string warehouseName, bool warehouseIsActive,
        int productId, string productCode, string productName, bool productIsActive) => new JObject
    {
        ["warehouseId"] = Positive(warehouseId), ["warehouseCode"] = Text(warehouseCode), ["warehouseName"] = Text(warehouseName), ["warehouseIsActive"] = warehouseIsActive,
        ["productId"] = Positive(productId), ["productCode"] = Text(productCode), ["productName"] = Text(productName), ["productIsActive"] = productIsActive,
    };
    private static string Id(long value, bool zero = false)
    { Require(value >= (zero ? 0 : 1)); return value.ToString(CultureInfo.InvariantCulture); }
    private static int Positive(int value) { Require(value > 0); return value; }
    private static string Number(decimal value, int scale, decimal maximum, bool positive = false, bool signed = false)
    {
        Require(value <= maximum && value >= (signed ? -maximum : 0) && (!positive || value > 0) && decimal.Round(value, scale) == value);
        return value.ToString(scale == 4 ? "0.0000" : "0.00000000", CultureInfo.InvariantCulture);
    }
    private static string Utc(DateTime value)
    {
        // SQL datetime2 values use Unspecified kind. Label them UTC without shifting by the server offset.
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);
    }
    private static JValue NullableText(string? value, int maximum = int.MaxValue) => value == null ? JValue.CreateNull() : new JValue(Text(value, maximum));
    private static string Text(string value, int maximum = int.MaxValue)
    {
        Require(value != null && value.Length <= maximum && value.IndexOf('\0') < 0);
        try { Utf8.GetByteCount(value!); } catch (EncoderFallbackException) { throw new InvalidOperationException("Invalid inventory wire response."); }
        return value!;
    }
    private static bool CanonicalVersion(string value)
    {
        if (value == null || value.Length != 12) return false;
        try { var bytes = Convert.FromBase64String(value); return bytes.Length == 8 && Convert.ToBase64String(bytes) == value; }
        catch (FormatException) { return false; }
    }
    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("Invalid inventory wire response."); }
}
