using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Reporting;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Reporting;

public sealed class TestGateReportExporterTests
{
    [Fact]
    public async Task Export_contains_only_versioned_sanitized_aggregate_evidence()
    {
        using var output = new TemporaryOutput();
        var secrets = new[]
        {
            "Server=private;Password=secret",
            Path.Combine(output.Root, "private-artifacts"),
            "approval-instance-fingerprint",
            "PID=4321",
            "MyPrivateDatabase",
            "Example.Tests.Theory(secret-argument)",
            new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee").ToString(),
        };
        var path = Path.Combine(output.Root, "report.json");

        var result = await new TestGateReportExporter().ExportAsync(
            Evidence(path, secrets),
            CancellationToken.None);
        var json = File.ReadAllText(path);

        Assert.True(result.IsComplete);
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"identityVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"manifestFingerprint\"", json, StringComparison.Ordinal);
        Assert.Contains("\"sourceCount\": 4", json, StringComparison.Ordinal);
        Assert.Contains("\"inventoryComplete\": true", json, StringComparison.Ordinal);
        Assert.All(secrets, secret => Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(output.Root, json, StringComparison.OrdinalIgnoreCase);
        Assert.Single(Directory.GetFiles(output.Root));
    }

    [Fact]
    public async Task Export_fails_closed_before_publication_when_private_input_leaks()
    {
        using var output = new TemporaryOutput();
        var path = Path.Combine(output.Root, "report.json");
        const string secret = "private-gate-secret";

        var result = await new TestGateReportExporter().ExportAsync(
            Evidence(path, new[] { secret }) with { GateId = secret },
            CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.False(File.Exists(path));
    }

    private static TestGateReportEvidence Evidence(string path, string[] secrets) => new(
        path,
        "20261016-001",
        "TESTGATE-T03",
        TestGateMode.Task,
        "runner-task",
        "0123456789abcdef0123456789abcdef01234567",
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        new[] { "TestGateOrchestration/net8.0" },
        SourceCount: 4,
        DiscoveredCount: 5,
        ExecutedCount: 5,
        PassedCount: 5,
        FailedCount: 0,
        SkippedCount: 0,
        Duration: TimeSpan.FromSeconds(2),
        TimedOut: false,
        ResourceDenied: false,
        RecoveryRequired: false,
        WatchdogClean: true,
        OwnershipClean: true,
        InventoryComplete: true,
        PrivateValues: secrets);

    private sealed class TemporaryOutput : IDisposable
    {
        public TemporaryOutput()
        {
            Root = Path.Combine(Path.GetTempPath(), "test-gate-report-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
