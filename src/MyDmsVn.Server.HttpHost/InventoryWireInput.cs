using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

internal static class InventoryWireInput
{
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    private static readonly string[] SaveFields = { "receiptDate", "warehouseId", "employeeId", "note", "lines" };

    // Reuse only the established transport grammar; WEB3 lexical rules stay local.
    public static Task<JObject> Body(HttpRequestMessage request) => SalesWireInput.Body(request);
    public static Task EmptyBody(HttpRequestMessage request) => SalesWireInput.EmptyBody(request);
    public static void NoQuery(HttpRequestMessage request) => SalesWireInput.NoQuery(request);
    public static long Long(string value) => SalesWireInput.Long(value, field: "receiptId");
    public static SaveGoodsReceiptRequest Create(JObject body) => Save(body, false);
    public static UpdateGoodsReceiptDraftRequest Update(long id, JObject body)
    {
        var save = Save(body, true);
        return new UpdateGoodsReceiptDraftRequest(id, Version(body), save.ReceiptDate, save.WarehouseId, save.EmployeeId, save.Note, save.Lines);
    }
    public static PostGoodsReceiptRequest Post(long id, JObject body)
    {
        Fields(body, new[] { "expectedVersion" }, new[] { "expectedVersion" });
        return new PostGoodsReceiptRequest(id, Version(body));
    }
    private static SaveGoodsReceiptRequest Save(JObject body, bool update)
    {
        var allowed = update ? SaveFields.Concat(new[] { "expectedVersion" }).ToArray() : SaveFields;
        Fields(body, allowed, allowed.Where(field => field != "note").ToArray());
        if (body["lines"] is not JArray lines || lines.Count < 1 || lines.Count > 200) throw new WireInputException("lines");
        var parsed = new List<SaveGoodsReceiptLineRequest>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var prefix = "lines[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            if (lines[index] is not JObject line) throw new WireInputException(prefix);
            var fields = new[] { "productId", "quantity", "unitCost" };
            Fields(line, fields, fields, prefix);
            parsed.Add(new SaveGoodsReceiptLineRequest(Int(line["productId"], prefix + ".productId"),
                Decimal(line["quantity"], true, GoodsReceiptContractLimits.MaximumQuantity, prefix + ".quantity"),
                Decimal(line["unitCost"], false, GoodsReceiptContractLimits.MaximumUnitCost, prefix + ".unitCost")));
        }
        // Duplicate Product is a business validation with its own canonical error code in Application.
        var note = body["note"] == null || body["note"]!.Type == JTokenType.Null ? null : String(body["note"], "note");
        if (note != null) Text(note, 1000, "note");
        return new SaveGoodsReceiptRequest(Date(String(body["receiptDate"], "receiptDate")),
            Int(body["warehouseId"], "warehouseId"), Int(body["employeeId"], "employeeId"), note, parsed);
    }
    public static GoodsReceiptListRequest List(HttpRequestMessage request)
    {
        var query = Query(request, new[] { "pageNumber", "pageSize", "warehouseId", "status", "search" });
        var status = Value(query, "status");
        if (status != null && status != "Draft" && status != "Posted") throw new WireInputException("status");
        var search = Value(query, "search"); if (search != null) Text(search, 128, "search");
        return new GoodsReceiptListRequest(RequiredInt(query, "pageNumber"), RequiredInt(query, "pageSize", 200), OptionalInt(query, "warehouseId"), status, search);
    }
    public static StockBalanceQuery Balances(HttpRequestMessage request)
    {
        var query = Query(request, new[] { "pageNumber", "pageSize", "warehouseId", "productId", "search" });
        var search = Value(query, "search"); if (search != null) Text(search, 256, "search");
        return new StockBalanceQuery(OptionalInt(query, "warehouseId"), OptionalInt(query, "productId"), search,
            RequiredInt(query, "pageNumber"), RequiredInt(query, "pageSize", 200));
    }
    public static StockCardQuery StockCard(HttpRequestMessage request)
    {
        var query = Query(request, new[] { "pageNumber", "pageSize", "warehouseId", "productId", "fromUtc", "toUtc" });
        DateTime? from = query.TryGetValue("fromUtc", out var fromText) ? Utc(fromText, "fromUtc") : (DateTime?)null;
        DateTime? to = query.TryGetValue("toUtc", out var toText) ? Utc(toText, "toUtc") : (DateTime?)null;
        if (from.HasValue && to.HasValue && from.Value > to.Value) throw new WireInputException("toUtc");
        return new StockCardQuery(RequiredInt(query, "warehouseId"), RequiredInt(query, "productId"), from, to,
            RequiredInt(query, "pageNumber"), RequiredInt(query, "pageSize", 200));
    }
    private static string? Value(Dictionary<string, string> query, string key) => query.TryGetValue(key, out var value) ? value : null;
    private static int? OptionalInt(Dictionary<string, string> query, string key) => query.TryGetValue(key, out var value) ? Int(value, key) : (int?)null;
    private static int RequiredInt(Dictionary<string, string> query, string key, int maximum = int.MaxValue) =>
        query.TryGetValue(key, out var value) ? Int(value, key, maximum) : throw new WireInputException(key);
    private static int Int(JToken? token, string field) => token?.Type == JTokenType.Integer ? Int(token.ToString(), field) : throw new WireInputException(field);
    private static int Int(string text, string field, int maximum = int.MaxValue)
    {
        if (text.Length < 1 || text.Length > 10 || text[0] == '0' || text.Any(c => c < '0' || c > '9') ||
            !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1 || value > maximum) throw new WireInputException(field);
        return value;
    }
    private static string String(JToken? token, string field) => token?.Type == JTokenType.String ? token.Value<string>()! : throw new WireInputException(field);
    private static decimal Decimal(JToken? token, bool positive, decimal maximum, string field)
    {
        var text = String(token, field);
        if (text.Length > 64 || !Regex.IsMatch(text, "\\A[0-9]+(?:\\.[0-9]{1,4})?\\z", RegexOptions.CultureInvariant) ||
            !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value > maximum || (positive ? value <= 0 : value < 0))
            throw new WireInputException(field);
        return value;
    }
    private static string Version(JObject body)
    {
        var text = String(body["expectedVersion"], "expectedVersion");
        try
        {
            if (text.Length != 12) throw new FormatException();
            var bytes = Convert.FromBase64String(text);
            if (bytes.Length != 8 || Convert.ToBase64String(bytes) != text) throw new FormatException();
        }
        catch (FormatException) { throw new WireInputException("expectedVersion"); }
        return text;
    }
    private static DateTime Date(string text)
    {
        if (!Regex.IsMatch(text, "\\A[0-9]{4}-[0-9]{2}-[0-9]{2}\\z", RegexOptions.CultureInvariant) ||
            !DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) || value == default)
            throw new WireInputException("receiptDate");
        return value;
    }
    private static DateTime Utc(string text, string field)
    {
        if (!Regex.IsMatch(text, "\\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\\.[0-9]{1,7})?Z\\z", RegexOptions.CultureInvariant) ||
            !DateTime.TryParseExact(text, new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" }, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)) throw new WireInputException(field);
        return value;
    }
    private static void Fields(JObject body, string[] allowed, string[] required, string? prefix = null)
    {
        if (body.Properties().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal))) throw new WireInputException(prefix);
        foreach (var field in required) if (body[field] == null) throw new WireInputException(prefix == null ? field : prefix + "." + field);
    }
    private static Dictionary<string, string> Query(HttpRequestMessage request, string[] allowed)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var query = request.GetOwinContext().Request.QueryString.Value ?? "";
        if (query.Length == 0) return result;
        foreach (var pair in query.Split('&'))
        {
            var separator = pair.IndexOf('='); if (separator < 1) throw new WireInputException();
            var name = Decode(pair.Substring(0, separator));
            if (!allowed.Contains(name, StringComparer.Ordinal) || result.ContainsKey(name)) throw new WireInputException();
            result.Add(name, Decode(pair.Substring(separator + 1)));
        }
        return result;
    }
    private static string Decode(string value)
    {
        try
        {
            var source = Utf8.GetBytes(value.Replace('+', ' ')); var bytes = new List<byte>(source.Length);
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] != '%') { bytes.Add(source[index]); continue; }
                if (index + 2 >= source.Length || !byte.TryParse(Encoding.ASCII.GetString(source, index + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var decoded)) throw new FormatException();
                bytes.Add(decoded); index += 2;
            }
            return Utf8.GetString(bytes.ToArray());
        }
        catch (Exception error) when (error is FormatException || error is DecoderFallbackException || error is EncoderFallbackException) { throw new WireInputException(); }
    }
    private static void Text(string value, int maximum, string field)
    {
        if (value.Length > maximum || value.IndexOf('\0') >= 0) throw new WireInputException(field);
        try { Utf8.GetByteCount(value); } catch (EncoderFallbackException) { throw new WireInputException(field); }
    }
}
