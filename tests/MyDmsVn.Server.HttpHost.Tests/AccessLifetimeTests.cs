using System;
using System.Collections.Generic;
using System.Text;
using MyDmsVn.Server.HttpHost;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

[CollectionDefinition("WEB2 environment", DisableParallelization = true)]
public sealed class Web2EnvironmentCollection { }

[Collection("WEB2 environment")]
public sealed class AccessLifetimeTests
{
    [Theory]
    [InlineData(null, 10)][InlineData("1", 1)][InlineData("15", 15)]
    public void Operator_access_lifetime_is_loaded_with_bounded_default(string? configured, int expected)
    {
        WithEnvironment(configured, () => Assert.Equal(expected, WebHostOptions.FromEnvironment().AccessLifetimeMinutes));
    }

    [Theory]
    [InlineData("0")][InlineData("16")][InlineData(" 1")]
    [InlineData("-1")][InlineData("2147483648")][InlineData("secret-invalid")]
    public void Invalid_access_lifetime_refuses_startup_without_echo(string configured)
    {
        WithEnvironment(configured, () =>
        {
            var error = Assert.Throws<InvalidOperationException>(() => WebHostOptions.FromEnvironment());
            Assert.Equal("WEB2 configuration is missing or invalid.", error.Message);
            Assert.Null(error.InnerException);
        });
    }

    [Theory]
    [InlineData(null)][InlineData("")][InlineData("WEB4")][InlineData("web4 ")][InlineData("unknown-mode")]
    public void Missing_or_unknown_operation_mode_refuses_startup_without_silent_legacy_fallback(string? mode)
    {
        WithEnvironment(null, () =>
        {
            var error = Assert.Throws<InvalidOperationException>(() => WebHostOptions.FromEnvironment());
            Assert.Null(error.InnerException);
            Assert.DoesNotContain("unknown-mode", error.Message);
        }, mode);
    }

    [Theory]
    [InlineData("legacy-web11", false)][InlineData("web4", true)][InlineData("web5", true)]
    public void Explicit_mode_selects_matching_host_admission(string mode, bool enabled)
    {
        WithEnvironment(null, () => Assert.Equal(enabled, WebHostOptions.FromEnvironment().Web4Enabled), mode);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("disabled", null, false)]
    [InlineData("receipt-v1", "candidate-1", true)]
    public void Resend_environment_is_explicit_and_fail_closed(string? feature, string? candidate, bool enabled)
    {
        WithEnvironment(null, () =>
        {
            var options = WebHostOptions.FromEnvironment();
            Assert.Equal(enabled, options.Web6ResendEligibilityEnabled);
            Assert.Equal(candidate, options.Web6CandidateId);
        }, "web5", feature, candidate);
    }

    [Theory]
    [InlineData("receipt-v1", null, "web5")]
    [InlineData("receipt-v1", "candidate-1", "web4")]
    [InlineData("disabled", "candidate-1", "web5")]
    [InlineData("unknown", null, "web5")]
    public void Invalid_resend_environment_refuses_startup(string feature, string? candidate, string mode)
    {
        WithEnvironment(null, () => Assert.Throws<InvalidOperationException>(() => WebHostOptions.FromEnvironment()),
            mode, feature, candidate);
    }

    private static void WithEnvironment(string? lifetime, Action check, string? mode = "legacy-web11",
        string? resend = null, string? candidate = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["MYDMSVN_WEB_ORIGIN"] = AuthFixture.Origin,
            ["MYDMSVN_WEB_JWT_ISSUER"] = AuthFixture.Origin,
            ["MYDMSVN_WEB_JWT_AUDIENCE"] = "auth-tests",
            ["MYDMSVN_WEB_JWT_ACTIVE_KID"] = "active",
            ["MYDMSVN_WEB_JWT_KEYS"] = "active:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-only-access-lifetime-key-32-bytes-minimum")),
            ["MYDMSVN_WEB_ACCESS_LIFETIME_MINUTES"] = lifetime,
            ["MYDMSVN_WEB_OPERATION_MODE"] = mode,
            ["MYDMSVN_WEB6_RESEND_ELIGIBILITY"] = resend,
            ["MYDMSVN_WEB6_CANDIDATE_ID"] = candidate,
        };
        var previous = new Dictionary<string, string?>();
        foreach (var name in settings.Keys) previous[name] = Environment.GetEnvironmentVariable(name);
        try
        {
            foreach (var setting in settings) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            check();
        }
        finally
        {
            foreach (var setting in previous) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }
    }
}
