using System;
using System.IO;
using MyDmsVn.SqlTestHarness.Approval;
using Xunit;

namespace MyDmsVn.SqlTestHarness.Tests.Approval;

public sealed class SqlInstanceApprovalLoaderTests
{
    private const string ExpectedFingerprint =
        "3e68cd38f51b859ba18373c96bf81945b44cbc94ebe8e83435dcc1720892f7e7";

    [Fact]
    public void Missing_approval_file_is_rejected()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "approval.json");

        var exception = Assert.Throws<InvalidOperationException>(
            () => SqlInstanceApprovalLoader.Load(path, new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero)));

        Assert.Contains("does not exist", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Malformed_json_is_rejected()
    {
        WithApprovalFile("{ not-json", path =>
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => SqlInstanceApprovalLoader.Load(path, UtcNow()));

            Assert.Contains("valid JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("\"schemaVersion\": 2", "schemaVersion")]
    [InlineData("\"nonProductionAttestation\": false", "attestation")]
    [InlineData("\"nonProductionAttestation\": null", "attestation")]
    public void Invalid_security_fields_are_rejected(string replacement, string expectedMessage)
    {
        var json = ValidJson().Replace(
            replacement.StartsWith("\"schemaVersion", StringComparison.Ordinal)
                ? "\"schemaVersion\": 1"
                : "\"nonProductionAttestation\": true",
            replacement);

        WithApprovalFile(json, path =>
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => SqlInstanceApprovalLoader.Load(path, UtcNow()));

            Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Expired_approval_is_rejected()
    {
        WithApprovalFile(ValidJson().Replace(
            "2026-10-15T00:00:00Z",
            "2026-10-13T23:59:59Z"), path =>
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => SqlInstanceApprovalLoader.Load(path, UtcNow()));

            Assert.Contains("expired", exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Fingerprint_not_matching_the_declared_identity_is_rejected()
    {
        WithApprovalFile(ValidJson().Replace(ExpectedFingerprint, new string('0', 64)), path =>
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => SqlInstanceApprovalLoader.Load(path, UtcNow()));

            Assert.Contains("fingerprint", exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Valid_approval_preserves_exact_identity_and_verified_fingerprint()
    {
        WithApprovalFile(ValidJson(), path =>
        {
            var approval = SqlInstanceApprovalLoader.Load(path, UtcNow());

            Assert.Equal("local-sql-test", approval.ApprovalId);
            Assert.Equal("vsql01", approval.Identity.ServerName);
            Assert.Equal("TESTBOX", approval.Identity.MachineName);
            Assert.Equal("SQLEXPRESS", approval.Identity.InstanceName);
            Assert.Equal("tcp:10.0.0.5,1433", approval.Identity.Endpoint);
            Assert.Equal(ExpectedFingerprint, approval.FingerprintSha256);
        });
    }

    private static DateTimeOffset UtcNow() =>
        new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);

    private static string ValidJson() =>
        "{" +
        "\"schemaVersion\": 1," +
        "\"approvalId\": \"local-sql-test\"," +
        "\"nonProductionAttestation\": true," +
        "\"validUntilUtc\": \"2026-10-15T00:00:00Z\"," +
        "\"identity\": {" +
        "\"serverName\": \"vsql01\"," +
        "\"machineName\": \"TESTBOX\"," +
        "\"instanceName\": \"SQLEXPRESS\"," +
        "\"endpoint\": \"tcp:10.0.0.5,1433\"" +
        "}," +
        "\"fingerprintSha256\": \"" + ExpectedFingerprint + "\"" +
        "}";

    private static void WithApprovalFile(string contents, Action<string> assertion)
    {
        var directory = Path.Combine(Path.GetTempPath(), "MyDmsVn.SqlTestHarness.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "approval.json");
        File.WriteAllText(path, contents);

        try
        {
            assertion(path);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
