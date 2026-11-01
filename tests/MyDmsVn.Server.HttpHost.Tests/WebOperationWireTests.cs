using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Operations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class WebOperationWireTests
{
    private const string Key = "11111111-1111-4111-8111-111111111111";
    private const string Epoch = "22222222-2222-4222-8222-222222222222";
    private static readonly Guid Session = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private readonly WebOperationResultCodec _codec = new WebOperationResultCodec();

    public static IEnumerable<object[]> Vectors() => Fixture("fingerprint-vectors.json")["vectors"]!.Select(v => new object[] { (string)v["name"]!, v.ToString(Formatting.None) });

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Production_fingerprint_matches_independent_canonical_vector(string name, string json)
    {
        var vector = Parse(json); var intent = (JObject)vector["intent"]!;
        var kind = Kind((string)intent["operationKind"]!); var target = Target(intent);
        var body = (JObject)intent["body"]!; var original = body.ToString(Formatting.None);
        using var request = Request((string)intent["method"]!, Route(intent));
        var context = WebOperationWireInput.CreateContext(request, kind, target, body, Session);
        Assert.True((string?)vector["canonical"] == WebOperationWireInput.Canonicalize(kind, target, body), name);
        Assert.Equal((string?)vector["sha256"], WebOperationWireInput.FingerprintHex(context.Fingerprint));
        Assert.Equal(original, body.ToString(Formatting.None));
        Assert.Equal(Guid.Parse(Key), context.OperationId); Assert.Equal(Guid.Parse(Epoch), context.DeploymentEpoch);
        Assert.Equal(Session, context.SessionId); Assert.Equal(target, context.RequestTargetId); Assert.Equal(kind, context.Kind);
        Assert.IsType<WebOperationResultCodec>(context.Codec);
    }

    [Theory]
    [InlineData("sales-create", WebOperationKind.SalesCreate)]
    [InlineData("sales-update", WebOperationKind.SalesUpdate)]
    [InlineData("sales-post", WebOperationKind.SalesPost)]
    [InlineData("receipt-create", WebOperationKind.ReceiptCreate)]
    [InlineData("receipt-update", WebOperationKind.ReceiptUpdate)]
    [InlineData("receipt-post", WebOperationKind.ReceiptPost)]
    public void Original_result_roundtrip_preserves_full_corpus_outcome_and_current_HTTP_bytes(string name, WebOperationKind kind)
    {
        var envelope = Fixture(name + "-response.json");
        var original = Original(kind, envelope["data"]!);
        var bytes = _codec.Encode(kind, original);
        Assert.True(JToken.DeepEquals(envelope, _codec.DecodeEnvelope(kind, bytes)));
        Assert.Equal(HttpBytes(kind, original), bytes);
        var decoded = Decode(kind, bytes);
        Assert.Equal(bytes, _codec.Encode(kind, decoded));
        Assert.Equal(HttpBytes(kind, original), HttpBytes(kind, decoded));
    }

    [Theory]
    [InlineData("Idempotency-Key", "")]
    [InlineData("Idempotency-Key", "11111111-1111-4111-8111-11111111111A")]
    [InlineData("Idempotency-Key", "11111111-1111-3111-8111-111111111111")]
    [InlineData("Idempotency-Key", "11111111-1111-4111-7111-111111111111")]
    [InlineData("Idempotency-Key", "11111111-1111-4111-8111-111111111111 ")]
    [InlineData("X-MyDmsVn-Deployment-Epoch", "00000000-0000-0000-0000-000000000000")]
    [InlineData("X-MyDmsVn-Deployment-Epoch", "{22222222-2222-4222-8222-222222222222}")]
    public void Write_rejects_missing_or_noncanonical_identity_header(string header, string value)
    {
        using var request = Request("POST", "/api/v1/sales-receipts"); request.Headers.Remove(header);
        if (value.Length > 0) request.Headers.TryAddWithoutValidation(header, value);
        Assert.Throws<WireInputException>(() => WebOperationWireInput.CreateContext(request, WebOperationKind.SalesCreate, null, Body(), Session));
    }

    [Theory]
    [InlineData("Idempotency-Key", Key)]
    [InlineData("X-MyDmsVn-Deployment-Epoch", Epoch)]
    public void Write_rejects_duplicate_and_comma_joined_headers(string header, string value)
    {
        using var request = Request("POST", "/api/v1/sales-receipts"); request.Headers.TryAddWithoutValidation(header, value);
        Assert.Throws<WireInputException>(() => WebOperationWireInput.CreateContext(request, WebOperationKind.SalesCreate, null, Body(), Session));
        request.Headers.Remove(header); request.Headers.TryAddWithoutValidation(header, value + "," + value);
        Assert.Throws<WireInputException>(() => WebOperationWireInput.CreateContext(request, WebOperationKind.SalesCreate, null, Body(), Session));
    }

    [Theory]
    [InlineData("PUT", "/api/v1/sales-receipts")]
    [InlineData("POST", "/api/v1/sales-receipts/")]
    [InlineData("POST", "/api/v1/sales-receipts?unexpected=1")]
    [InlineData("POST", "/api/v1/Sales-receipts")]
    [InlineData("POST", "/api/v1/goods-receipts")]
    public void Write_rejects_method_route_or_query_drift(string method, string route)
    {
        using var request = Request(method, route);
        Assert.Throws<WireInputException>(() => WebOperationWireInput.CreateContext(request, WebOperationKind.SalesCreate, null, Body(), Session));
    }

    [Fact]
    public void Fingerprint_reuses_strict_business_wire_validation()
    {
        foreach (var replacement in new JToken[] { new JValue(1.25), new JValue("1.00001"), new JValue("1e2"), new JValue("0") })
        {
            var body = Body(); body["lines"]![0]!["qty"] = replacement;
            Assert.Throws<WireInputException>(() => WebOperationWireInput.Canonicalize(WebOperationKind.SalesCreate, null, body));
        }
        var extra = Body(); extra["actorUserId"] = 1;
        Assert.Throws<WireInputException>(() => WebOperationWireInput.Canonicalize(WebOperationKind.SalesCreate, null, extra));
        Assert.Throws<WireInputException>(() => WebOperationWireInput.Canonicalize(WebOperationKind.SalesCreate, 1, Body()));
        Assert.Throws<WireInputException>(() => WebOperationWireInput.Canonicalize(WebOperationKind.SalesUpdate, null, Body()));
    }

    [Fact]
    public void Status_headers_and_path_require_strict_key_epoch_and_fingerprint()
    {
        using var request = Request("GET", "/api/v1/operations/" + Key);
        request.Headers.TryAddWithoutValidation("X-MyDmsVn-Operation-Fingerprint", new string('a', 64));
        Assert.Equal(Guid.Parse(Key), WebOperationWireInput.OperationId(Key));
        Assert.Equal(Guid.Parse(Epoch), WebOperationWireInput.DeploymentEpoch(request));
        Assert.Equal(Enumerable.Repeat((byte)0xaa, 32), WebOperationWireInput.StatusFingerprint(request));
        WebOperationWireInput.StatusPath(request, Guid.Parse(Key));
        // Build the malformed incoming request before its first header read. net48's parsed-header
        // collection can discard a subsequently added identical raw value after materialization.
        using var duplicate = Request("GET", "/api/v1/operations/" + Key);
        duplicate.Headers.TryAddWithoutValidation("X-MyDmsVn-Operation-Fingerprint", new[] { new string('a', 64), new string('a', 64) });
        Assert.Throws<WireInputException>(() => WebOperationWireInput.StatusFingerprint(duplicate));
        request.Headers.Remove("X-MyDmsVn-Operation-Fingerprint"); request.Headers.TryAddWithoutValidation("X-MyDmsVn-Operation-Fingerprint", new string('A', 64));
        Assert.Throws<WireInputException>(() => WebOperationWireInput.StatusFingerprint(request));
        Assert.Throws<WireInputException>(() => WebOperationWireInput.OperationId(Key.ToUpperInvariant().Replace('1', 'A')));
        Assert.Throws<WireInputException>(() => WebOperationWireInput.StatusPath(request, Guid.NewGuid()));
        using var context = Request("GET", "/api/v1/operations/context"); WebOperationWireInput.ContextPath(context);
        context.RequestUri = new Uri("https://localhost/api/v1/operations/context?x=1");
        Assert.Throws<WireInputException>(() => WebOperationWireInput.ContextPath(context));
    }

    [Theory]
    [InlineData("sales-create", WebOperationKind.SalesCreate, "future", "true")]
    [InlineData("sales-create", WebOperationKind.SalesCreate, "customerIsActive", "false")]
    [InlineData("sales-create", WebOperationKind.SalesCreate, "customerDisplayName", "null")]
    [InlineData("sales-create", WebOperationKind.SalesCreate, "totalAmount", "\"2.5002\"")]
    [InlineData("sales-create", WebOperationKind.SalesCreate, "version", "\"AAAAAAAAAAF=\"")]
    [InlineData("receipt-create", WebOperationKind.ReceiptCreate, "warehouseDisplayName", "\"new label\"")]
    [InlineData("receipt-create", WebOperationKind.ReceiptCreate, "note", "false")]
    [InlineData("receipt-post", WebOperationKind.ReceiptPost, "version", "\"AAAAAAAAAAE=\"")]
    [InlineData("sales-post", WebOperationKind.SalesPost, "postedAtUtc", "\"2026-01-01T00:00:00+07:00\"")]
    public void Stored_result_rejects_non_original_or_unallowlisted_fields(string fixture, WebOperationKind kind, string field, string replacement)
    {
        var envelope = Fixture(fixture + "-response.json"); envelope["data"]![field] = JToken.Parse(replacement);
        AssertUnavailable(() => _codec.DecodeEnvelope(kind, Bytes(envelope)));
    }

    [Fact]
    public void Stored_result_rejects_duplicate_identity_line_order_and_inexact_amount()
    {
        foreach (var field in new[] { "saleLineId", "lineNo" })
        {
            var envelope = Fixture("sales-create-response.json"); envelope["data"]!["lines"]![1]![field] = envelope["data"]!["lines"]![0]![field]!.DeepClone();
            AssertUnavailable(() => _codec.DecodeEnvelope(WebOperationKind.SalesCreate, Bytes(envelope)));
        }
        var gift = Fixture("sales-create-response.json"); gift["data"]!["lines"]![2]!["lineAmount"] = "6.0000";
        AssertUnavailable(() => _codec.DecodeEnvelope(WebOperationKind.SalesCreate, Bytes(gift)));
        var receipt = Fixture("receipt-create-response.json"); receipt["data"]!["lines"]![1]!["productId"] = receipt["data"]!["lines"]![0]!["productId"]!.DeepClone();
        AssertUnavailable(() => _codec.DecodeEnvelope(WebOperationKind.ReceiptCreate, Bytes(receipt)));
    }

    [Theory]
    [InlineData("{\"data\":{},\"data\":{}}")]
    [InlineData("{\"data\":{},}")]
    [InlineData("{\"data\":{\"note\":\"\\ud800\"}}")]
    public void Stored_result_uses_bounded_strict_JSON(string invalid) => AssertUnavailable(() => _codec.DecodeEnvelope(WebOperationKind.SalesCreate, Encoding.UTF8.GetBytes(invalid)));

    [Fact]
    public void Stored_result_rejects_invalid_UTF8_oversize_and_wrong_DTO_type()
    {
        AssertUnavailable(() => _codec.DecodeEnvelope(WebOperationKind.SalesCreate, new byte[] { 0xc0, 0xaf }));
        AssertUnavailable(() => _codec.DecodeEnvelope(WebOperationKind.SalesCreate, new byte[1048577]));
        AssertUnavailable(() => _codec.Encode(WebOperationKind.SalesCreate, new object()));
        AssertUnavailable(() => _codec.Decode<GoodsReceiptDto>(WebOperationKind.SalesCreate, Bytes(Fixture("sales-create-response.json"))));
    }

    private object Decode(WebOperationKind kind, byte[] bytes) => kind switch
    {
        WebOperationKind.SalesCreate or WebOperationKind.SalesUpdate => _codec.Decode<SalesReceiptDto>(kind, bytes),
        WebOperationKind.SalesPost => _codec.Decode<PostSalesReceiptResponse>(kind, bytes),
        WebOperationKind.ReceiptCreate or WebOperationKind.ReceiptUpdate => _codec.Decode<GoodsReceiptDto>(kind, bytes),
        _ => _codec.Decode<PostGoodsReceiptResponse>(kind, bytes)
    };
    private static object Original(WebOperationKind kind, JToken data) => kind switch
    {
        WebOperationKind.SalesCreate or WebOperationKind.SalesUpdate => data.ToObject<SalesReceiptDto>()!,
        WebOperationKind.SalesPost => data.ToObject<PostSalesReceiptResponse>()!,
        WebOperationKind.ReceiptCreate or WebOperationKind.ReceiptUpdate => InventoryWireTests.Receipt(data),
        _ => data.ToObject<PostGoodsReceiptResponse>()!
    };
    private static byte[] HttpBytes(WebOperationKind kind, object value)
    {
        object projected = kind switch
        {
            WebOperationKind.SalesCreate or WebOperationKind.SalesUpdate => SalesWireOutput.Detail((SalesReceiptDto)value),
            WebOperationKind.SalesPost => SalesWireOutput.Post((PostSalesReceiptResponse)value),
            WebOperationKind.ReceiptCreate or WebOperationKind.ReceiptUpdate => InventoryWireOutput.Detail((GoodsReceiptDto)value),
            _ => InventoryWireOutput.Post((PostGoodsReceiptResponse)value)
        };
        using var response = HttpJson.Success(projected); return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
    }
    private static void AssertUnavailable(Action action) => Assert.Equal("Operation.ResultUnavailable", Assert.Throws<WebOperationException>(action).Code);
    private static byte[] Bytes(JObject value) => Encoding.UTF8.GetBytes(value.ToString(Formatting.None));
    private static JObject Body() => (JObject)Fixture("sales-create-intent.json")["body"]!;
    private static long? Target(JObject intent) => intent["requestTargetId"]!.Type == JTokenType.Null ? null : long.Parse((string)intent["requestTargetId"]!, CultureInfo.InvariantCulture);
    private static WebOperationKind Kind(string value) => (WebOperationKind)Enum.Parse(typeof(WebOperationKind), value.Replace(".", ""));
    private static string Route(JObject intent) => ((string)intent["routeScope"]!).Replace("{saleId}", (string?)intent["requestTargetId"]).Replace("{receiptId}", (string?)intent["requestTargetId"]);
    private static HttpRequestMessage Request(string method, string route)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), "https://localhost" + route);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Key); request.Headers.TryAddWithoutValidation("X-MyDmsVn-Deployment-Epoch", Epoch); return request;
    }
    internal static JObject Fixture(string name)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docs", "contracts", "web4-v1"))) directory = directory.Parent;
        if (directory == null) throw new InvalidOperationException("WEB4 corpus not found.");
        return Parse(File.ReadAllText(Path.Combine(directory.FullName, "docs", "contracts", "web4-v1", name)));
    }
    private static JObject Parse(string json) => JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })!;
}
