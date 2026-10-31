using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
namespace MyDmsVn.Server.HttpHost.Tests;
public sealed class ConfigurationTests
{
    [Theory]
    [InlineData("origin")][InlineData("origin-path")][InlineData("issuer")][InlineData("audience")]
    [InlineData("kid")][InlineData("weak")][InlineData("skew")][InlineData("body")][InlineData("lifetime")]
    public void Unsafe_deployment_options_are_refused(string defect)
    {
        Assert.ThrowsAny<ArgumentException>(() => new WebHostOptions(
            defect == "origin" ? "http://web2.test" : defect == "origin-path" ? "https://web2.test/" : "https://web2.test",
            defect == "issuer" ? "http://issuer.test" : "https://issuer.test",
            defect == "audience" ? "" : "audience", defect == "kid" ? "unknown" : "current",
            new Dictionary<string, byte[]> { ["current"] = new byte[defect == "weak" ? 16 : 32] },
            clockSkewSeconds: defect == "skew" ? 31 : 30, maximumRequestBytes: defect == "body" ? int.MaxValue : 1024,
            accessLifetimeMinutes: defect == "lifetime" ? 60 : 10));
    }

    [Fact]
    public void Validation_keys_are_copied_so_caller_cannot_change_live_validation()
    {
        var key = Encoding.UTF8.GetBytes("test-only-random-looking-key-32-bytes-minimum");
        var keys = new Dictionary<string, byte[]> { ["test"] = key };
        var options = new WebHostOptions(HostFixture.Origin, HostFixture.Origin, "web2-tests", "test", keys);
        using var fixture = new HostFixture();
        var token = fixture.Token();
        Array.Clear(key, 0, key.Length); keys.Clear();
        Assert.NotNull(new BearerTokenValidator(options, new FixedClock()).Validate(token));
    }
    private sealed class FixedClock : MyDmsVn.Server.Application.Identity.IUtcClock { public DateTime UtcNow => HostFixture.Now; }
}
