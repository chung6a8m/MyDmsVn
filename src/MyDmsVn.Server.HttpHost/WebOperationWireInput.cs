using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MyDmsVn.Server.Application.Operations;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

public static class WebOperationWireInput
{
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    // Body must come from StrictWireJson, before conversion to transport-neutral command DTOs.
    public static WebOperationContext CreateContext(HttpRequestMessage request, WebOperationKind kind, long? target, JObject strictlyDecodedBody, Guid sessionId)
    {
        var key = OperationId(Header(request, "Idempotency-Key"));
        var epoch = DeploymentEpoch(request);
        var canonical = Canonicalize(kind, target, strictlyDecodedBody);
        Path(request, Method(kind), RouteScope(kind).Replace("{saleId}", target?.ToString(CultureInfo.InvariantCulture))
            .Replace("{receiptId}", target?.ToString(CultureInfo.InvariantCulture)));
        using var sha256 = SHA256.Create();
        return new WebOperationContext(key, epoch, sessionId, kind, target, sha256.ComputeHash(Utf8.GetBytes(canonical)), new WebOperationResultCodec());
    }

    public static Guid DeploymentEpoch(HttpRequestMessage request) => GuidValue(Header(request, "X-MyDmsVn-Deployment-Epoch"), false);
    public static Guid OperationId(string value) => GuidValue(value, true);
    public static byte[] StatusFingerprint(HttpRequestMessage request)
    {
        var value = Header(request, "X-MyDmsVn-Operation-Fingerprint");
        if (!Regex.IsMatch(value, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)) throw new WireInputException();
        var bytes = new byte[32];
        for (var index = 0; index < bytes.Length; index++) bytes[index] = byte.Parse(value.Substring(index * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return bytes;
    }
    public static void StatusPath(HttpRequestMessage request, Guid operationId)
    {
        OperationId(operationId.ToString("D"));
        Path(request, "GET", "/api/v1/operations/" + operationId.ToString("D"));
    }
    public static void ContextPath(HttpRequestMessage request) => Path(request, "GET", "/api/v1/operations/context");
    public static string KindName(WebOperationKind kind) => kind switch
    {
        WebOperationKind.SalesCreate => "Sales.Create", WebOperationKind.SalesUpdate => "Sales.Update", WebOperationKind.SalesPost => "Sales.Post",
        WebOperationKind.ReceiptCreate => "Receipt.Create", WebOperationKind.ReceiptUpdate => "Receipt.Update", WebOperationKind.ReceiptPost => "Receipt.Post",
        _ => throw new WireInputException()
    };
    public static string Method(WebOperationKind kind) => kind switch
    {
        WebOperationKind.SalesUpdate or WebOperationKind.ReceiptUpdate => "PUT",
        WebOperationKind.SalesCreate or WebOperationKind.SalesPost or WebOperationKind.ReceiptCreate or WebOperationKind.ReceiptPost => "POST",
        _ => throw new WireInputException()
    };
    public static string RouteScope(WebOperationKind kind) => kind switch
    {
        WebOperationKind.SalesCreate => "/api/v1/sales-receipts",
        WebOperationKind.SalesUpdate => "/api/v1/sales-receipts/{saleId}/draft",
        WebOperationKind.SalesPost => "/api/v1/sales-receipts/{saleId}/post",
        WebOperationKind.ReceiptCreate => "/api/v1/goods-receipts",
        WebOperationKind.ReceiptUpdate => "/api/v1/goods-receipts/{receiptId}/draft",
        WebOperationKind.ReceiptPost => "/api/v1/goods-receipts/{receiptId}/post",
        _ => throw new WireInputException()
    };

    public static string Canonicalize(WebOperationKind kind, long? target, JObject body)
    {
        var create = kind == WebOperationKind.SalesCreate || kind == WebOperationKind.ReceiptCreate;
        if (body == null || (create ? target.HasValue : !target.HasValue || target.Value <= 0)) throw new WireInputException();
        // Reuse each existing wire decoder, including Sales' legacy Base64 input semantics.
        switch (kind)
        {
            case WebOperationKind.SalesCreate: SalesWireInput.Create(body); break;
            case WebOperationKind.SalesUpdate: SalesWireInput.Update(target!.Value, body); break;
            case WebOperationKind.SalesPost: SalesWireInput.Post(target!.Value, body); break;
            case WebOperationKind.ReceiptCreate: InventoryWireInput.Create(body); break;
            case WebOperationKind.ReceiptUpdate: InventoryWireInput.Update(target!.Value, body); break;
            case WebOperationKind.ReceiptPost: InventoryWireInput.Post(target!.Value, body); break;
            default: throw new WireInputException();
        }
        var normalized = (JObject)body.DeepClone();
        var sales = kind == WebOperationKind.SalesCreate || kind == WebOperationKind.SalesUpdate || kind == WebOperationKind.SalesPost;
        if (kind != WebOperationKind.SalesPost && kind != WebOperationKind.ReceiptPost)
        {
            if (normalized["note"] == null) normalized["note"] = JValue.CreateNull();
            foreach (var line in ((JArray)normalized["lines"]!).Cast<JObject>())
            {
                foreach (var name in sales ? new[] { "qty", "unitPrice" } : new[] { "quantity", "unitCost" })
                    line[name] = decimal.Parse((string)line[name]!, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture).ToString("F4", CultureInfo.InvariantCulture);
            }
        }
        if (sales && normalized["expectedVersion"] != null) normalized["expectedVersion"] = Convert.ToBase64String(Convert.FromBase64String((string)normalized["expectedVersion"]!));
        var intent = new JObject
        {
            ["contract"] = "web4-v1", ["operationKind"] = KindName(kind), ["method"] = Method(kind), ["routeScope"] = RouteScope(kind),
            ["requestTargetId"] = target.HasValue ? new JValue(target.Value.ToString(CultureInfo.InvariantCulture)) : JValue.CreateNull(), ["body"] = normalized
        };
        var result = new StringBuilder(); CanonicalJson(intent, result); return result.ToString();
    }

    public static string FingerprintHex(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 32) throw new WireInputException();
        return string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }
    private static string Header(HttpRequestMessage request, string name)
    {
        if (!request.Headers.TryGetValues(name, out var values)) throw new WireInputException();
        var headers = values.ToArray();
        if (headers.Length != 1 || headers[0].IndexOf(',') >= 0) throw new WireInputException();
        return headers[0];
    }
    private static Guid GuidValue(string value, bool v4)
    {
        if (value == null || !Regex.IsMatch(value, "\\A[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\\z", RegexOptions.CultureInvariant) ||
            !Guid.TryParseExact(value, "D", out var guid) || guid == Guid.Empty || (v4 && (value[14] != '4' || "89ab".IndexOf(value[19]) < 0)))
            throw new WireInputException();
        return guid;
    }
    private static void Path(HttpRequestMessage request, string method, string path)
    {
        var uri = request.RequestUri;
        if (request.Method.Method != method || uri == null || !uri.IsAbsoluteUri) throw new WireInputException();
        // OriginalString rejects aliases (% escapes, dot segments, query and trailing slash) that Uri can normalize.
        var original = uri.OriginalString;
        var index = original.IndexOf('/', original.IndexOf("://", StringComparison.Ordinal) + 3);
        if (index < 0 || original.Substring(index) != path) throw new WireInputException();
    }
    private static void CanonicalJson(JToken value, StringBuilder output)
    {
        if (value is JObject obj)
        {
            output.Append('{'); var first = true;
            foreach (var property in obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                if (!first) output.Append(','); first = false;
                JsonString(property.Name, output); output.Append(':'); CanonicalJson(property.Value, output);
            }
            output.Append('}');
        }
        else if (value is JArray array)
        {
            output.Append('[');
            for (var index = 0; index < array.Count; index++) { if (index != 0) output.Append(','); CanonicalJson(array[index], output); }
            output.Append(']');
        }
        else if (value.Type == JTokenType.String) JsonString((string)value!, output);
        else if (value.Type == JTokenType.Null) output.Append("null");
        else if (value.Type == JTokenType.Boolean) output.Append((bool)value ? "true" : "false");
        else if (value.Type == JTokenType.Integer) output.Append(((int)value).ToString(CultureInfo.InvariantCulture));
        else throw new WireInputException();
    }
    private static void JsonString(string value, StringBuilder output)
    {
        // Json.NET escapes U+2028/U+2029; web4-v1 requires these to remain literal UTF-8.
        output.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\t': output.Append("\\t"); break;
                case '\n': output.Append("\\n"); break;
                case '\f': output.Append("\\f"); break;
                case '\r': output.Append("\\r"); break;
                default:
                    if (character < 32) output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    else output.Append(character);
                    break;
            }
        }
        output.Append('"');
    }
}
