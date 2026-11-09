using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Operations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class Web6ResendReceiptTests
{
    private static readonly DateTime Issued = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Epoch = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid Operation = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static readonly byte[] Fingerprint = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

    public static IEnumerable<object[]> Kinds()
    {
        yield return new object[] { WebOperationKind.SalesCreate, 0L, "Sales.Create", "POST", "/api/v1/sales-receipts" };
        yield return new object[] { WebOperationKind.SalesUpdate, 9007199254740993L, "Sales.Update", "PUT", "/api/v1/sales-receipts/{saleId}/draft" };
        yield return new object[] { WebOperationKind.SalesPost, 9007199254740993L, "Sales.Post", "POST", "/api/v1/sales-receipts/{saleId}/post" };
        yield return new object[] { WebOperationKind.ReceiptCreate, 0L, "Receipt.Create", "POST", "/api/v1/goods-receipts" };
        yield return new object[] { WebOperationKind.ReceiptUpdate, 9007199254740993L, "Receipt.Update", "PUT", "/api/v1/goods-receipts/{receiptId}/draft" };
        yield return new object[] { WebOperationKind.ReceiptPost, 9007199254740993L, "Receipt.Post", "POST", "/api/v1/goods-receipts/{receiptId}/post" };
    }

    [Theory, MemberData(nameof(Kinds))]
    public void All_six_identities_derive_only_allowlisted_method_and_route(WebOperationKind kind, long targetValue,
        string wireKind, string method, string route)
    {
        long? target = targetValue == 0 ? null : targetValue;
        var identity = Identity(kind, target);
        Assert.Equal(wireKind, identity.OperationKind);
        Assert.Equal(method, identity.Method);
        Assert.Equal(route, identity.RouteScope);
        Assert.Equal(target, identity.RequestTargetId);
    }

    [Fact]
    public void Create_and_targeted_kinds_reject_inconsistent_targets()
    {
        Assert.Throws<ArgumentException>(() => Identity(WebOperationKind.SalesCreate, 1));
        Assert.Throws<ArgumentException>(() => Identity(WebOperationKind.ReceiptCreate, 1));
        Assert.Throws<ArgumentException>(() => Identity(WebOperationKind.SalesUpdate, null));
        Assert.Throws<ArgumentException>(() => Identity(WebOperationKind.ReceiptPost, 0));
    }

    [Fact]
    public void Issued_receipt_has_exact_domain_separated_canonical_contract()
    {
        var issued = Codec(Issued).Issue(Identity(WebOperationKind.SalesUpdate, 9007199254740993L));
        var parts = issued.Token.Split('.');
        Assert.Equal(3, parts.Length);
        var header = Read(parts[0]);
        Assert.Equal(new[] { "alg", "kid", "typ" }, header.Properties().Select(p => p.Name).OrderBy(x => x).ToArray());
        Assert.Equal("HS256", (string?)header["alg"]);
        Assert.Equal("active", (string?)header["kid"]);
        Assert.Equal("MYDMSVN-WEB6-RESEND-RECEIPT", (string?)header["typ"]);

        var payload = Read(parts[1]);
        Assert.Equal(new[] { "aud", "candidate", "epoch", "exp", "fingerprint", "iat", "iss", "kind", "method", "nbf", "operation", "route", "sub", "target" },
            payload.Properties().Select(p => p.Name).OrderBy(x => x).ToArray());
        Assert.Equal(AuthFixture.Origin, (string?)payload["iss"]);
        Assert.Equal("auth-tests:web6-resend-eligibility-v1", (string?)payload["aud"]);
        Assert.Equal("42", (string?)payload["sub"]);
        Assert.Equal("candidate-1", (string?)payload["candidate"]);
        Assert.Equal(Epoch.ToString("D"), (string?)payload["epoch"]);
        Assert.Equal(Operation.ToString("D"), (string?)payload["operation"]);
        Assert.Equal("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", (string?)payload["fingerprint"]);
        Assert.Equal("Sales.Update", (string?)payload["kind"]);
        Assert.Equal("PUT", (string?)payload["method"]);
        Assert.Equal("/api/v1/sales-receipts/{saleId}/draft", (string?)payload["route"]);
        Assert.Equal("9007199254740993", (string?)payload["target"]);
        var iat = (long)payload["iat"]!;
        Assert.Equal(iat, (long)payload["nbf"]!);
        Assert.Equal(604800, (long)payload["exp"]! - iat);
        Assert.Equal(Issued, issued.IssuedAtUtc);
        Assert.Equal(Issued.AddDays(7), issued.EligibleUntilUtc);
    }

    [Fact]
    public void Previous_root_key_remains_valid_until_removed()
    {
        var keys = Keys();
        var token = Codec(Issued, "previous", keys).Issue(Identity()).Token;
        var validated = Codec(Issued.AddDays(1), "active", keys).Validate(token, Identity());
        Assert.False(validated.IsExpired);
        Assert.Equal(Issued.AddDays(7), validated.EligibleUntilUtc);

        Assert.Throws<WireInputException>(() => Codec(Issued.AddDays(1), "active",
            new Dictionary<string, byte[]> { ["active"] = keys["active"] }).Validate(token, Identity()));
    }

    [Fact]
    public void Receipt_and_access_tokens_are_not_cross_protocol_credentials()
    {
        var receipt = Codec(Issued).Issue(Identity()).Token;
        Assert.Null(new BearerTokenValidator(Options(), new Clock(Issued)).Validate(receipt));

        using var fixture = new HostFixture();
        Assert.Throws<WireInputException>(() => Codec(Issued).Validate(fixture.Token(), Identity()));
    }

    [Theory]
    [InlineData("actor")][InlineData("candidate")][InlineData("epoch")][InlineData("operation")]
    [InlineData("fingerprint")][InlineData("kind")][InlineData("target")]
    public void Receipt_is_bound_to_every_immutable_identity_field(string mismatch)
    {
        var token = Codec(Issued).Issue(Identity()).Token;
        var expected = mismatch switch
        {
            "actor" => Identity(actor: 43),
            "candidate" => Identity(candidate: "candidate-2"),
            "epoch" => Identity(epoch: Guid.Parse("22222222-3333-4444-8555-666666666666")),
            "operation" => Identity(operation: Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff")),
            "fingerprint" => Identity(fingerprint: Enumerable.Repeat((byte)7, 32).ToArray()),
            "kind" => Identity(WebOperationKind.ReceiptCreate),
            "target" => Identity(WebOperationKind.SalesUpdate, 5),
            _ => throw new InvalidOperationException()
        };
        Assert.Throws<WebOperationException>(() => Codec(Issued).Validate(token, expected));
    }

    [Fact]
    public void Only_a_canonical_bounded_receipt_is_accepted()
    {
        var token = Codec(Issued).Issue(Identity()).Token;
        Assert.Throws<WireInputException>(() => Codec(Issued).Validate(token + "é", Identity()));
        Assert.Throws<WireInputException>(() => Codec(Issued).Validate(new string('a', 4097), Identity()));
        Assert.Throws<WireInputException>(() => Codec(Issued).Validate(token.Substring(0, token.Length - 1) + "a", Identity()));
    }

    [Fact]
    public void Exact_expiry_boundary_is_denied_without_using_browser_time()
    {
        var token = Codec(Issued).Issue(Identity()).Token;
        Assert.False(Codec(Issued.AddDays(7).AddSeconds(-1)).Validate(token, Identity()).IsExpired);
        Assert.True(Codec(Issued.AddDays(7)).Validate(token, Identity()).IsExpired);
    }

    private static Web6ResendIdentity Identity(WebOperationKind kind = WebOperationKind.SalesCreate, long? target = null,
        int actor = 42, string candidate = "candidate-1", Guid? epoch = null, Guid? operation = null, byte[]? fingerprint = null) =>
        new Web6ResendIdentity(actor, candidate, epoch ?? Epoch, operation ?? Operation, kind, target, fingerprint ?? Fingerprint);

    private static Web6ResendReceiptCodec Codec(DateTime now, string active = "active", IReadOnlyDictionary<string, byte[]>? keys = null) =>
        new Web6ResendReceiptCodec(Options(active, keys), new Clock(now));

    private static WebHostOptions Options(string active = "active", IReadOnlyDictionary<string, byte[]>? keys = null) =>
        new WebHostOptions(AuthFixture.Origin, AuthFixture.Origin, "auth-tests", active, keys ?? Keys(),
            web5Enabled: true, web6ResendEligibilityEnabled: true, web6CandidateId: "candidate-1");

    private static Dictionary<string, byte[]> Keys() => new Dictionary<string, byte[]>
    {
        ["active"] = Encoding.UTF8.GetBytes("active-test-key-at-least-32-bytes-long"),
        ["previous"] = Encoding.UTF8.GetBytes("previous-test-key-at-least-32-bytes-long")
    };

    private static JObject Read(string encoded)
    {
        var json = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(encoded));
        using var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
    }

    private sealed class Clock : IUtcClock
    {
        internal Clock(DateTime utcNow) { UtcNow = utcNow; }
        public DateTime UtcNow { get; }
    }
}
