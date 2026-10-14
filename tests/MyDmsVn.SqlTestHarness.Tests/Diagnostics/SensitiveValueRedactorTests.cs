using System;
using MyDmsVn.SqlTestHarness.Diagnostics;
using Xunit;

namespace MyDmsVn.SqlTestHarness.Tests.Diagnostics;

public sealed class SensitiveValueRedactorTests
{
    [Fact]
    public void Redaction_removes_every_supplied_sensitive_value_case_insensitively()
    {
        const string connectionString = "Server=SecretHost;Database=master;Password=TopSecret";
        const string rawHost = "SecretHost";

        var actual = SensitiveValueRedactor.Redact(
            "Failure at SECRETHOST using Server=SecretHost;Database=master;Password=TopSecret",
            connectionString,
            rawHost,
            "TopSecret");

        Assert.DoesNotContain(connectionString, actual, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(rawHost, actual, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TopSecret", actual, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", actual, StringComparison.Ordinal);
    }

    [Fact]
    public void Short_fingerprint_exposes_only_the_first_twelve_hex_characters()
    {
        var actual = SensitiveValueRedactor.ShortenFingerprint(
            "3e68cd38f51b859ba18373c96bf81945b44cbc94ebe8e83435dcc1720892f7e7");

        Assert.Equal("3e68cd38f51b", actual);
    }
}
