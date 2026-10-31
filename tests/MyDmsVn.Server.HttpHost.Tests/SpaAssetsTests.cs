using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Owin.Testing;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class SpaAssetsTests
{
    [Fact]
    public async Task Each_HTML_response_binds_a_fresh_style_nonce_to_metadata_without_allowing_inline_script()
    {
        using var fixture = new Assets();
        var nonces = new List<string>();
        foreach (var path in new[] { "/", "/sales/42", "/index.html" })
        {
            using var response = await fixture.Server.HttpClient.GetAsync(HostFixture.Origin + path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            var match = Regex.Match(body, "<meta name=\"mydmsvn-style-nonce\" content=\"([^\"]+)\" />");
            Assert.True(match.Success, "HTML must carry the nonce used by approved component styles.");
            var nonce = match.Groups[1].Value;
            Assert.Equal(32, Convert.FromBase64String(nonce).Length);
            var policy = response.Headers.GetValues("Content-Security-Policy").Single();
            Assert.Contains("style-src 'self' 'nonce-" + nonce + "'", policy);
            Assert.Contains("script-src 'self';", policy);
            Assert.DoesNotContain("unsafe-inline", policy); Assert.DoesNotContain("unsafe-eval", policy);
            Assert.True(body.IndexOf(match.Value, StringComparison.Ordinal) < body.IndexOf("<script", StringComparison.Ordinal));
            Assert.True(response.Headers.CacheControl.NoStore);
            nonces.Add(nonce);
        }
        Assert.Equal(3, nonces.Distinct().Count());
    }

    [Theory]
    [InlineData("app.js", "text/javascript")]
    [InlineData("app.css", "text/css")]
    [InlineData("font.woff2", "font/woff2")]
    public async Task Approved_assets_keep_exact_bytes_and_types_and_HEAD_has_no_body(string name, string contentType)
    {
        using var fixture = new Assets();
        using var response = await fixture.Server.HttpClient.GetAsync(HostFixture.Origin + "/" + name);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType.MediaType);
        Assert.Equal(Assets.Bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.DoesNotContain("nonce-", response.Headers.GetValues("Content-Security-Policy").Single());
        using var head = await fixture.Server.HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, HostFixture.Origin + "/" + name));
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("/missing.js")][InlineData("/Web.config")][InlineData("/bin/private.dll")]
    public async Task Missing_or_nonallowlisted_asset_never_becomes_HTML(string path)
    {
        using var fixture = new Assets();
        using var response = await fixture.Server.HttpClient.GetAsync(HostFixture.Origin + path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    private sealed class Assets : IDisposable
    {
        public static readonly byte[] Bytes = { 0, 1, 2, 254, 255, 13, 10 };
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "web2-assets-" + Guid.NewGuid().ToString("N"));
        public TestServer Server { get; }
        public Assets()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "index.html"), "<!doctype html><html><head><script src=\"/app.js\"></script></head><body>pilot</body></html>", new UTF8Encoding(false));
            foreach (var name in new[] { "app.js", "app.css", "font.woff2", "Web.config" }) File.WriteAllBytes(Path.Combine(_directory, name), Bytes);
            var options = new WebHostOptions(HostFixture.Origin, HostFixture.Origin, "assets", "test", new Dictionary<string, byte[]> { ["test"] = new byte[32] });
            // Invoke the internal composition seam without making it a public runtime API.
            Server = TestServer.Create(app => typeof(Startup).Assembly.GetType("MyDmsVn.Server.HttpHost.SpaAssets")!
                .GetMethod("Configure", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, new object[] { app, _directory, options }));
        }
        public void Dispose() { Server.Dispose(); Directory.Delete(_directory, recursive: true); }
    }
}
