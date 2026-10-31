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

internal static class SalesWireInput
{
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    private static readonly string[] HeaderFields = { "saleDate", "customerId", "warehouseId", "employeeId", "note", "lines" };
    public static async Task<JObject> Body(HttpRequestMessage request)
    {
        NoQuery(request);
        var content = request.Content;
        if (content?.Headers.ContentType == null || !string.Equals(content.Headers.ContentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            (content.Headers.ContentType.CharSet != null && !string.Equals(content.Headers.ContentType.CharSet.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)) || content.Headers.ContentEncoding.Any())
            throw new WireInputException();
        return StrictWireJson.Read(await content.ReadAsByteArrayAsync().ConfigureAwait(false));
    }
    public static async Task EmptyBody(HttpRequestMessage request)
    { if (request.Content != null && (await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false)).Length != 0) throw new WireInputException(); }
    public static void NoQuery(HttpRequestMessage request)
    { if (!string.IsNullOrEmpty(request.GetOwinContext().Request.QueryString.Value)) throw new WireInputException(); }
    public static SaveSalesReceiptRequest Create(JObject body) => Save(body, false);
    public static UpdateSalesReceiptDraftRequest Update(long id, JObject body)
    {
        var save = Save(body, true);
        return new UpdateSalesReceiptDraftRequest(id, Version(body), save.SaleDate, save.CustomerId, save.WarehouseId, save.EmployeeId, save.Note, save.Lines);
    }
    public static PostSalesReceiptRequest Post(long id, JObject body)
    { Fields(body, new[] { "expectedVersion" }, new[] { "expectedVersion" }); return new PostSalesReceiptRequest(id, Version(body)); }
    private static SaveSalesReceiptRequest Save(JObject body, bool update)
    {
        Fields(body, update ? HeaderFields.Concat(new[] { "expectedVersion" }).ToArray() : HeaderFields,
            update ? HeaderFields.Where(f => f != "note").Concat(new[] { "expectedVersion" }).ToArray() : HeaderFields.Where(f => f != "note").ToArray());
        if (body["lines"] is not JArray lines || lines.Count < 1 || lines.Count > SalesContractLimits.MaximumLines) throw new WireInputException("lines");
        var parsed = new List<SaveSalesReceiptLineRequest>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var prefix = "lines[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            if (lines[index] is not JObject line) throw new WireInputException(prefix);
            var fields = new[] { "saleLineId", "productId", "qty", "unitPrice", "isGift" };
            Fields(line, fields, fields, prefix);
            var id = Long(String(line["saleLineId"], prefix + ".saleLineId"), true, prefix + ".saleLineId");
            if (!update && id != 0) throw new WireInputException(prefix + ".saleLineId");
            if (line["isGift"]?.Type != JTokenType.Boolean) throw new WireInputException(prefix + ".isGift");
            parsed.Add(new SaveSalesReceiptLineRequest(id, Int(line["productId"], prefix + ".productId"),
                Decimal(line["qty"], true, SalesContractLimits.MaximumQuantity, prefix + ".qty"),
                Decimal(line["unitPrice"], false, SalesContractLimits.MaximumUnitPrice, prefix + ".unitPrice"), (bool)line["isGift"]!));
        }
        string? note = body["note"] == null || body["note"]!.Type == JTokenType.Null ? null : String(body["note"], "note");
        if (note != null) Text(note, 1000, "note");
        return new SaveSalesReceiptRequest(Date(String(body["saleDate"], "saleDate"), "saleDate", true), Int(body["customerId"], "customerId"),
            Int(body["warehouseId"], "warehouseId"), Int(body["employeeId"], "employeeId"), note, parsed);
    }
    public static long Long(string text, bool zero = false, string field = "saleId")
    {
        if (!CanonicalInteger(text) || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < (zero ? 0 : 1)) throw new WireInputException(field);
        return value;
    }
    private static int Int(JToken? value, string field)
    {
        if (value?.Type != JTokenType.Integer) throw new WireInputException(field);
        return Int(value.ToString(), field);
    }
    private static int Int(string text, string field, int maximum = int.MaxValue)
    {
        if (!CanonicalInteger(text) || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1 || value > maximum) throw new WireInputException(field);
        return value;
    }
    private static bool CanonicalInteger(string value) => value.Length >= 1 && value.Length <= 19 && (value.Length == 1 || value[0] != '0') && value.All(c => c >= '0' && c <= '9');
    private static string String(JToken? value, string field) => value?.Type == JTokenType.String ? value.Value<string>()! : throw new WireInputException(field);
    private static decimal Decimal(JToken? value, bool positive, decimal maximum, string field)
    {
        var text = String(value, field);
        // Lexical scale is checked before decimal.TryParse, which otherwise rounds long fractional inputs.
        if (text.Length > 64 || !Regex.IsMatch(text, "\\A[0-9]+(?:\\.[0-9]{1,4})?\\z", RegexOptions.CultureInvariant) ||
            !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || number > maximum || (positive ? number <= 0 : number < 0))
            throw new WireInputException(field);
        return number;
    }
    private static string Version(JObject body)
    {
        var version = String(body["expectedVersion"], "expectedVersion");
        try { if (version.Length > 128 || Convert.FromBase64String(version).Length != 8) throw new FormatException(); }
        catch (FormatException) { throw new WireInputException("expectedVersion"); }
        return version;
    }
    private static DateTime Date(string text, string field, bool saleDate = false)
    {
        if (!Regex.IsMatch(text, "\\A[0-9]{4}-[0-9]{2}-[0-9]{2}\\z", RegexOptions.CultureInvariant) ||
            !DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) || saleDate && value == default)
            throw new WireInputException(field);
        return value;
    }
    private static void Fields(JObject body, string[] allowed, string[] required, string? prefix = null)
    {
        if (body.Properties().Any(p => !allowed.Contains(p.Name, StringComparer.Ordinal))) throw new WireInputException(prefix);
        foreach (var name in required) if (body[name] == null) throw new WireInputException(prefix == null ? name : prefix + "." + name);
    }
    public static SalesReceiptListRequest List(HttpRequestMessage request)
    {
        var query = Query(request, new[] { "fromDate", "toDate", "customerId", "warehouseId", "employeeId", "status", "productId", "search", "pageSize", "cursor" });
        string? Value(string key) => query.TryGetValue(key, out var value) ? value : null;
        DateTime? OptionalDate(string key) => query.TryGetValue(key, out var value) ? Date(value, key) : (DateTime?)null;
        int? OptionalInt(string key) => query.TryGetValue(key, out var value) ? Int(value, key) : (int?)null;
        var status = Value("status"); if (status != null && status != "Draft" && status != "Posted") throw new WireInputException("status");
        var cursor = Value("cursor"); if (cursor != null && (cursor.Length == 0 || cursor.Length > 4096)) throw new WireInputException("cursor");
        var search = Value("search"); if (search != null) Text(search.Trim(), 256, "search");
        return new SalesReceiptListRequest(OptionalDate("fromDate"), OptionalDate("toDate"), OptionalInt("customerId"), OptionalInt("warehouseId"), OptionalInt("employeeId"), status,
            OptionalInt("productId"), search, query.TryGetValue("pageSize", out var size) ? Int(size, "pageSize", 200) : 25, cursor);
    }
    public static CatalogLookupRequest Lookup(HttpRequestMessage request)
    {
        var query = Query(request, new[] { "search", "limit" });
        if (!query.TryGetValue("limit", out var limit)) throw new WireInputException("limit");
        var search = query.TryGetValue("search", out var text) ? text : null; if (search != null) Text(search, 256, "search");
        return new CatalogLookupRequest(search, Int(limit, "limit", 200));
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
