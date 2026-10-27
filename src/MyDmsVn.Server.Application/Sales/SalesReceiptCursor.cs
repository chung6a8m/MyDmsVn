using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using FluentValidation.Results;
using MyDmsVn.Contracts;
using Newtonsoft.Json;

namespace MyDmsVn.Server.Application.Sales;

/// <summary>Provider-free normalized criteria and a consistency-bound live-navigation anchor.</summary>
public sealed class SalesReceiptCursor
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private SalesReceiptCursor(SalesReceiptListRequest request, long? anchor, bool previous)
    { Request = request; Anchor = anchor; IsPrevious = previous; }
    public SalesReceiptListRequest Request { get; }
    public long? Anchor { get; }
    public bool IsPrevious { get; }

    public static SalesReceiptCursor Parse(SalesReceiptListRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var failures = new List<ValidationFailure>();
        void Add(string field, string code, string message) => failures.Add(new ValidationFailure("Request." + field, message) { ErrorCode = code });
        if (request.PageSize < 1 || request.PageSize > 200) Add("PageSize", "Validation.Range", "Page size must be between 1 and 200.");
        if (request.FromDate > request.ToDate) Add("ToDate", "Validation.Range", "The end date must not precede the start date.");
        foreach (var pair in new[] { ("CustomerId", request.CustomerId), ("WarehouseId", request.WarehouseId), ("EmployeeId", request.EmployeeId), ("ProductId", request.ProductId) })
            if (pair.Item2.HasValue && pair.Item2 <= 0) Add(pair.Item1, "Validation.GreaterThan", "The ID must be positive.");
        if (request.Status != null && request.Status != "Draft" && request.Status != "Posted") Add("Status", "Validation.InvalidValue", "Status must be Draft or Posted.");
        var search = request.Search?.Trim();
        if (search?.Length == 0) search = null;
        if (search?.Length > 256) Add("Search", "Validation.MaximumLength", "Search cannot exceed 256 characters.");
        if (search?.IndexOf('\0') >= 0) Add("Search", "Validation.InvalidCharacter", "Search cannot contain NUL.");
        try { if (search != null) Utf8.GetByteCount(search); }
        catch (EncoderFallbackException) { Add("Search", "Validation.InvalidCharacter", "Search must contain valid Unicode."); }
        if (failures.Count != 0) throw new ValidationException(failures);
        var normalized = new SalesReceiptListRequest(request.FromDate, request.ToDate, request.CustomerId, request.WarehouseId,
            request.EmployeeId, request.Status, request.ProductId, search, request.PageSize, request.Cursor);
        if (request.Cursor == null) return new SalesReceiptCursor(normalized, null, false);
        try
        {
            var text = Utf8.GetString(Decode(request.Cursor, 4096, 3072));
            var fields = ReadFlatObject(text);
            if (fields.Count != 6 || fields["v"] != "1" || fields["order"] != "SaleIdDesc" ||
                (fields["direction"] != "next" && fields["direction"] != "previous") ||
                !long.TryParse(fields["anchor"], NumberStyles.None, CultureInfo.InvariantCulture, out var anchor) || anchor <= 0 ||
                fields["pageSize"] != request.PageSize.ToString(CultureInfo.InvariantCulture) ||
                fields["filterHash"] != Fingerprint(normalized)) throw new FormatException();
            Decode(fields["filterHash"], 43, 32);
            return new SalesReceiptCursor(normalized, anchor, fields["direction"] == "previous");
        }
        catch (Exception error) when (error is FormatException || error is DecoderFallbackException || error is EncoderFallbackException ||
                                     error is JsonException || error is KeyNotFoundException || error is OverflowException)
        {
            throw new ValidationException(new[] { new ValidationFailure("Request.Cursor", "The cursor is invalid or does not match the current filters.") { ErrorCode = "Validation.InvalidCursor" } });
        }
    }

    public string Encode(long anchor, bool previous)
    {
        if (anchor <= 0) throw new ArgumentOutOfRangeException(nameof(anchor));
        return Base64(Utf8.GetBytes(Json(writer =>
        {
            writer.WriteStartObject(); Property(writer, "v", 1); Property(writer, "order", "SaleIdDesc");
            Property(writer, "direction", previous ? "previous" : "next"); Property(writer, "anchor", anchor);
            Property(writer, "pageSize", Request.PageSize); Property(writer, "filterHash", Fingerprint(Request)); writer.WriteEndObject();
        })));
    }

    private static string Fingerprint(SalesReceiptListRequest request)
    {
        var canonical = Json(writer =>
        {
            writer.WriteStartObject(); Property(writer, "FromDate", request.FromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Property(writer, "ToDate", request.ToDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Property(writer, "CustomerId", request.CustomerId); Property(writer, "WarehouseId", request.WarehouseId);
            Property(writer, "EmployeeId", request.EmployeeId); Property(writer, "Status", request.Status); Property(writer, "ProductId", request.ProductId);
            Property(writer, "Search", request.Search); Property(writer, "PageSize", request.PageSize); writer.WriteEndObject();
        });
        using var sha = SHA256.Create(); return Base64(sha.ComputeHash(Utf8.GetBytes(canonical)));
    }
    private static string Json(Action<JsonTextWriter> write)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new JsonTextWriter(text) { Formatting = Formatting.None, Culture = CultureInfo.InvariantCulture };
        write(writer); writer.Flush(); return text.ToString();
    }
    private static void Property(JsonTextWriter writer, string name, object? value) { writer.WritePropertyName(name); writer.WriteValue(value); }
    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value, int maxChars, int maxBytes)
    {
        if (value.Length == 0 || value.Length > maxChars || value.Any(c => !(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')) throw new FormatException();
        var bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
        if (bytes.Length > maxBytes || Base64(bytes) != value) throw new FormatException(); return bytes;
    }

    // A flat, fixed schema needs no general object deserializer. This rejects JSON extensions,
    // nested content, coercion, duplicate fields and oversized strings before materializing values.
    private static Dictionary<string, string> ReadFlatObject(string text)
    {
        var index = 0;
        void White() { while (index < text.Length && (text[index] == ' ' || text[index] == '\t' || text[index] == '\r' || text[index] == '\n')) index++; }
        void Take(char expected) { White(); if (index >= text.Length || text[index++] != expected) throw new FormatException(); }
        string String(int max)
        {
            White(); var start = index; Take('"'); var escaped = false; var closed = false;
            while (index < text.Length)
            {
                var ch = text[index++];
                if (ch < 32) throw new FormatException();
                if (escaped) { if ("\"\\/bfnrtu".IndexOf(ch) < 0) throw new FormatException(); escaped = false; }
                else if (ch == '\\') escaped = true;
                else if (ch == '"') { closed = true; break; }
                if (index - start > max * 6 + 2) throw new FormatException();
            }
            if (!closed) throw new FormatException();
            var value = JsonConvert.DeserializeObject<string>(text.Substring(start, index - start))!;
            if (value.Length > max) throw new FormatException(); Utf8.GetByteCount(value); return value;
        }
        string Integer()
        {
            White(); var start = index;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++;
            if (index == start || index - start > 19 || (index - start > 1 && text[start] == '0')) throw new FormatException();
            return text.Substring(start, index - start);
        }
        var result = new Dictionary<string, string>(StringComparer.Ordinal); Take('{');
        while (true)
        {
            var name = String(16); Take(':');
            if (result.ContainsKey(name)) throw new FormatException();
            switch (name)
            {
                case "v": case "anchor": case "pageSize": result.Add(name, Integer()); break;
                case "order": case "direction": case "filterHash": result.Add(name, String(43)); break;
                default: throw new FormatException();
            }
            White(); if (index < text.Length && text[index] == '}') { index++; break; }
            Take(',');
        }
        White(); if (index != text.Length) throw new FormatException(); return result;
    }
}
