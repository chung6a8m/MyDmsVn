using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Domain.Inventory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

public sealed class WebOperationResultCodec : IWebOperationResultCodec
{
    public const int MaximumResultBytes = 1048576;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    // Capture the original projection before commit. Never reconstruct it with a later Read/Get.
    public byte[] Encode<T>(WebOperationKind kind, T originalResult)
    {
        try
        {
            object projected = kind switch
            {
                WebOperationKind.SalesCreate or WebOperationKind.SalesUpdate when originalResult is SalesReceiptDto sale && sale.Lines.Count <= 200 => SalesWireOutput.Detail(sale),
                WebOperationKind.SalesPost when originalResult is PostSalesReceiptResponse salePost => SalesWireOutput.Post(salePost),
                WebOperationKind.ReceiptCreate or WebOperationKind.ReceiptUpdate when originalResult is GoodsReceiptDto receipt => InventoryWireOutput.Detail(receipt),
                WebOperationKind.ReceiptPost when originalResult is PostGoodsReceiptResponse receiptPost => InventoryWireOutput.Post(receiptPost),
                _ => throw Unavailable()
            };
            // Exactly the same envelope, serializer and wire projections as the original HTTP success.
            var bytes = Utf8.GetBytes(JsonConvert.SerializeObject(ApiResponse<object>.Success(projected), ApiJson.CreateSerializerSettings()));
            DecodeEnvelope(kind, bytes);
            return bytes;
        }
        catch (Exception error) when (CodecError(error)) { throw Unavailable(); }
    }

    public T Decode<T>(WebOperationKind kind, byte[] bytes)
    {
        try
        {
            var data = (JObject)DecodeEnvelope(kind, bytes)["data"]!;
            object result = kind switch
            {
                WebOperationKind.SalesCreate or WebOperationKind.SalesUpdate => Sales(data),
                WebOperationKind.SalesPost => new PostSalesReceiptResponse(Id(data["saleId"]), "Posted", Utc(data["postedAtUtc"]), Text(data["version"], 12)),
                WebOperationKind.ReceiptCreate or WebOperationKind.ReceiptUpdate => Receipt(data),
                WebOperationKind.ReceiptPost => new PostGoodsReceiptResponse(Id(data["receiptId"]), "Posted", Utc(data["postedAtUtc"])),
                _ => throw Unavailable()
            };
            return result is T typed ? typed : throw Unavailable();
        }
        catch (Exception error) when (CodecError(error)) { throw Unavailable(); }
    }

    // Status uses this same validation without acquiring Read permission or performing I/O.
    public JObject DecodeEnvelope(WebOperationKind kind, byte[] bytes)
    {
        try
        {
            Require(bytes != null && bytes.Length > 0 && bytes.Length <= MaximumResultBytes);
            var envelope = StrictWireJson.Read(bytes!);
            Shape(envelope, "data");
            Require(envelope["data"] is JObject);
            var data = (JObject)envelope["data"]!;
            switch (kind)
            {
                case WebOperationKind.SalesCreate:
                case WebOperationKind.SalesUpdate: Sales(data); break;
                case WebOperationKind.ReceiptCreate:
                case WebOperationKind.ReceiptUpdate: Receipt(data); break;
                case WebOperationKind.SalesPost:
                    Shape(data, "saleId,status,postedAtUtc,version"); Id(data["saleId"]); Equal(data["status"], "Posted"); Utc(data["postedAtUtc"]); Version(data["version"]); break;
                case WebOperationKind.ReceiptPost:
                    Shape(data, "receiptId,status,postedAtUtc"); Id(data["receiptId"]); Equal(data["status"], "Posted"); Utc(data["postedAtUtc"]); break;
                default: throw Unavailable();
            }
            return envelope;
        }
        catch (Exception error) when (CodecError(error)) { throw Unavailable(); }
    }

    private static SalesReceiptDto Sales(JObject data)
    {
        Shape(data, "saleId,saleNumber,saleDate,customerId,warehouseId,employeeId,status,version,lines,totalAmount,customerIsActive,warehouseIsActive,employeeIsActive,customerDisplayName,warehouseDisplayName,employeeDisplayName", "note");
        Equal(data["status"], "Draft"); Active(data["customerIsActive"]); Active(data["warehouseIsActive"]); Active(data["employeeIsActive"]);
        var saleId = Id(data["saleId"]); var number = Number(data["saleNumber"], "SA"); var date = Date(data["saleDate"]);
        var customer = Int(data["customerId"]); var warehouse = Int(data["warehouseId"]); var employee = Int(data["employeeId"]);
        var customerName = Text(data["customerDisplayName"], 291); var warehouseName = Text(data["warehouseDisplayName"], 291); var employeeName = Text(data["employeeDisplayName"], 291);
        var version = Version(data["version"]); var note = data["note"] == null ? null : Text(data["note"], 1000);
        var lines = new List<SalesReceiptLineDto>(); var ids = new HashSet<long>();
        foreach (var token in Lines(data["lines"]))
        {
            Require(token is JObject); var line = (JObject)token;
            Shape(line, "saleLineId,lineNo,productId,qty,unitPrice,isGift,lineAmount,productIsActive,unit,productDisplayName");
            var id = Id(line["saleLineId"]); Require(ids.Add(id)); var lineNo = Int(line["lineNo"]); Require(lineNo == lines.Count + 1);
            var product = Int(line["productId"]); var unit = Text(line["unit"], 32); Require(!string.IsNullOrWhiteSpace(unit));
            var productName = Text(line["productDisplayName"], 291); Active(line["productIsActive"]);
            var quantity = Decimal(line["qty"], SalesContractLimits.MaximumQuantity, true);
            var price = Decimal(line["unitPrice"], SalesContractLimits.MaximumUnitPrice);
            Require(line["isGift"]?.Type == JTokenType.Boolean); var gift = (bool)line["isGift"]!;
            var amount = Decimal(line["lineAmount"], SalesContractLimits.MaximumAmount);
            Require(amount == InventoryValuationMath.CalculateRevenue(quantity, price, gift));
            lines.Add(new SalesReceiptLineDto(id, lineNo, product, unit, quantity, price, gift, amount, null, null, productName, true));
        }
        var total = Decimal(data["totalAmount"], SalesContractLimits.MaximumAmount);
        Require(total == InventoryValuationMath.SumAmounts(lines.Select(line => line.LineAmount)));
        return new SalesReceiptDto(saleId, number, date, customer, warehouse, employee, "Draft", note, null, version, lines,
            total, null, null, customerName, true, warehouseName, true, employeeName, true);
    }

    private static GoodsReceiptDto Receipt(JObject data)
    {
        Shape(data, "receiptId,receiptNumber,receiptDate,warehouseId,warehouseDisplayName,warehouseIsActive,employeeId,employeeDisplayName,employeeIsActive,status,note,postedAtUtc,version,lines");
        Equal(data["status"], "Draft"); Null(data["postedAtUtc"]); Null(data["warehouseDisplayName"]); Null(data["employeeDisplayName"]);
        Active(data["warehouseIsActive"]); Active(data["employeeIsActive"]);
        var receiptId = Id(data["receiptId"]); var number = Number(data["receiptNumber"], "GR"); var date = Date(data["receiptDate"]);
        var warehouse = Int(data["warehouseId"]); var employee = Int(data["employeeId"]); var version = Version(data["version"]);
        var note = data["note"]!.Type == JTokenType.Null ? null : Text(data["note"], 1000);
        var lines = new List<GoodsReceiptLineDto>(); var ids = new HashSet<long>(); var products = new HashSet<int>();
        foreach (var token in Lines(data["lines"]))
        {
            Require(token is JObject); var line = (JObject)token;
            Shape(line, "receiptLineId,lineNumber,productId,quantity,unitCost,productDisplayName,productIsActive");
            var id = Id(line["receiptLineId"]); Require(ids.Add(id)); var lineNo = Int(line["lineNumber"]); Require(lineNo == lines.Count + 1);
            var product = Int(line["productId"]); Require(products.Add(product)); Null(line["productDisplayName"]); Active(line["productIsActive"]);
            lines.Add(new GoodsReceiptLineDto(id, lineNo, product, Decimal(line["quantity"], GoodsReceiptContractLimits.MaximumQuantity, true),
                Decimal(line["unitCost"], GoodsReceiptContractLimits.MaximumUnitCost)));
        }
        return new GoodsReceiptDto(receiptId, number, date, warehouse, employee, "Draft", note, null, version, lines);
    }

    private static void Shape(JObject value, string required, string optional = "")
    {
        var names = required.Split(','); var permitted = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var name in optional.Split(',')) if (name.Length != 0) permitted.Add(name);
        Require(names.All(name => value.Property(name, StringComparison.Ordinal) != null) && value.Properties().All(property => permitted.Contains(property.Name)));
    }
    private static JArray Lines(JToken? value)
    {
        Require(value is JArray lines && lines.Count >= 1 && lines.Count <= 200);
        return (JArray)value!;
    }
    private static string Text(JToken? token, int maximum)
    {
        Require(token?.Type == JTokenType.String); var value = (string)token!;
        Require(value.Length <= maximum && value.IndexOf('\0') < 0); Utf8.GetByteCount(value); return value;
    }
    private static long Id(JToken? token)
    {
        var value = Text(token, 19);
        Require(Regex.IsMatch(value, "\\A[1-9][0-9]*\\z", RegexOptions.CultureInvariant) && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _));
        return long.Parse(value, CultureInfo.InvariantCulture);
    }
    private static int Int(JToken? token)
    {
        Require(token?.Type == JTokenType.Integer);
        Require(int.TryParse(token!.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0); return value;
    }
    private static decimal Decimal(JToken? token, decimal maximum, bool positive = false)
    {
        var value = Text(token, 64);
        Require(Regex.IsMatch(value, "\\A[0-9]+(?:\\.[0-9]{1,4})?\\z", RegexOptions.CultureInvariant));
        Require(decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) && number <= maximum && (positive ? number > 0 : number >= 0));
        return number;
    }
    private static string Number(JToken? token, string prefix)
    {
        var value = Text(token, 12);
        Require(Regex.IsMatch(value, "\\A" + prefix + "[0-9]{10}\\z", RegexOptions.CultureInvariant) && value != prefix + "0000000000"); return value;
    }
    private static string Version(JToken? token)
    {
        var value = Text(token, 12); var bytes = Convert.FromBase64String(value);
        Require(bytes.Length == 8 && Convert.ToBase64String(bytes) == value); return value;
    }
    private static DateTime Date(JToken? token)
    {
        var value = Text(token, 10);
        Require(Regex.IsMatch(value, "\\A[0-9]{4}-[0-9]{2}-[0-9]{2}\\z", RegexOptions.CultureInvariant));
        Require(DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date != default); return date;
    }
    private static DateTime Utc(JToken? token)
    {
        var value = Text(token, 28);
        Require(Regex.IsMatch(value, "\\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\\.[0-9]{1,7})?Z\\z", RegexOptions.CultureInvariant));
        Require(DateTime.TryParseExact(value, new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" }, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)); return date;
    }
    private static void Equal(JToken? token, string expected) => Require(token?.Type == JTokenType.String && (string?)token == expected);
    private static void Active(JToken? token) => Require(token?.Type == JTokenType.Boolean && (bool)token);
    private static void Null(JToken? token) => Require(token?.Type == JTokenType.Null);
    private static void Require(bool condition) { if (!condition) throw Unavailable(); }
    private static WebOperationException Unavailable() => new WebOperationException("Operation.ResultUnavailable");
    private static bool CodecError(Exception error) => error is WireInputException || error is JsonException || error is ArgumentException ||
        error is InvalidOperationException || error is FormatException || error is OverflowException;
}
