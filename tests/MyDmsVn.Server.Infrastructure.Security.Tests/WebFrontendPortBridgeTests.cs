using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebFrontendPortBridgeTests
{
    [Fact]
    public async Task Transport_preserves_exact_frontend_body_and_headers_without_inventing_authorization_or_key()
    {
        var body = "{\"note\":\"Bán hàng\",\"quantity\":\"0003.0000\"}";
        var frame = new JObject { ["id"] = 1, ["type"] = "http", ["method"] = "POST", ["path"] = "/api/v1/sales-receipts",
            ["headers"] = new JArray(new JArray("Content-Type", "application/json"), new JArray("Idempotency-Key", "original-key")), ["body"] = body };
        using var request = WebFrontendPortBridge.CreateRequest(frame);
        Assert.Equal(body, await request.Content.ReadAsStringAsync());
        Assert.Equal("application/json", request.Content.Headers.ContentType.ToString());
        Assert.Equal("original-key", request.Headers.GetValues("Idempotency-Key").Single());
        Assert.False(request.Headers.Contains("Authorization"));
        Assert.False(request.Headers.Contains("X-MyDmsVn-Deployment-Epoch"));
        Assert.False(request.Headers.Contains("X-Requested-With"));
        Assert.Equal(WebFrontendPortBridge.Origin, request.Headers.GetValues("Origin").Single());
        Assert.Equal("same-origin", request.Headers.GetValues("Sec-Fetch-Site").Single());
    }

    [Theory]
    [InlineData("https://other.test/api/v1/operations/context")]
    [InlineData("//other.test/api/v1/operations/context")]
    [InlineData("/api/v1/../private")]
    [InlineData("/api/v1/operations/context#fragment")]
    public void Transport_refuses_non_fixture_request_targets(string path)
    {
        var frame = new JObject { ["id"] = 1, ["type"] = "http", ["method"] = "GET", ["path"] = path, ["headers"] = new JArray(), ["body"] = null };
        Assert.Throws<InvalidDataException>(() => WebFrontendPortBridge.CreateRequest(frame));
    }

    [Fact]
    public async Task Framing_preserves_unicode_and_refuses_oversized_or_truncated_input()
    {
        using var stream = new MemoryStream();
        var original = new JObject { ["note"] = "Nhập kho — hàng tặng" };
        await WebFrontendPortBridge.WriteFrame(stream, original, CancellationToken.None);
        stream.Position = 0;
        Assert.True(JToken.DeepEquals(original, await WebFrontendPortBridge.ReadFrame(stream, CancellationToken.None)));
        using var oversized = new MemoryStream(BitConverter.GetBytes(WebFrontendPortBridge.MaximumFrameBytes + 1));
        await Assert.ThrowsAsync<InvalidDataException>(() => WebFrontendPortBridge.ReadFrame(oversized, CancellationToken.None));
        using var truncated = new MemoryStream(new byte[] { 3, 0, 0, 0, (byte)'{' });
        await Assert.ThrowsAsync<EndOfStreamException>(() => WebFrontendPortBridge.ReadFrame(truncated, CancellationToken.None));
    }

    [Theory]
    [InlineData("{\"id\":1,\"id\":2}")]
    [InlineData("{\"id\":1} {}")]
    public async Task Framing_rejects_duplicate_properties_and_trailing_json(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(BitConverter.GetBytes(bytes.Length).Concat(bytes).ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => WebFrontendPortBridge.ReadFrame(stream, CancellationToken.None));
    }
}
