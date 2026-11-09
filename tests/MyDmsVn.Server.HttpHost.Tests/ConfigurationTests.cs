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

    [Fact]
    public void Resend_eligibility_is_disabled_by_default()
    {
        var options = Options();
        Assert.False(options.Web6ResendEligibilityEnabled);
        Assert.Null(options.Web6CandidateId);
    }

    [Fact]
    public void Receipt_v1_requires_exact_web5_mode_and_bounded_candidate()
    {
        var enabled = Options(web5: true, resend: true, candidate: "candidate-20261109.1");
        Assert.True(enabled.Web6ResendEligibilityEnabled);
        Assert.Equal("candidate-20261109.1", enabled.Web6CandidateId);

        Assert.Throws<ArgumentException>(() => Options(web5: false, resend: true, candidate: "candidate-1"));
        Assert.Throws<ArgumentException>(() => Options(web5: true, resend: true, candidate: null));
        Assert.Throws<ArgumentException>(() => Options(web5: true, resend: false, candidate: "candidate-1"));
    }

    [Theory]
    [InlineData("")][InlineData(" candidate")][InlineData("candidate/")][InlineData("-candidate")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Resend_candidate_rejects_noncanonical_values(string candidate)
    {
        Assert.Throws<ArgumentException>(() => Options(web5: true, resend: true, candidate: candidate));
    }

    private static WebHostOptions Options(bool web5 = false, bool resend = false, string? candidate = null) =>
        new WebHostOptions(HostFixture.Origin, HostFixture.Origin, "web2-tests", "test",
            new Dictionary<string, byte[]> { ["test"] = new byte[32] }, web5Enabled: web5,
            web6ResendEligibilityEnabled: resend, web6CandidateId: candidate);

    private sealed class FixedClock : MyDmsVn.Server.Application.Identity.IUtcClock { public DateTime UtcNow => HostFixture.Now; }
}
