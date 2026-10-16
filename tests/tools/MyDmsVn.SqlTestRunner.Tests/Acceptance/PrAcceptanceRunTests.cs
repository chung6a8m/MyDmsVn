using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reporting;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Acceptance;

public sealed class PrAcceptanceRunTests
{
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task Profile_runs_one_unfiltered_pass_per_tfm_sequentially()
    {
        using var fixture = new Fixture();
        var process = new RecordingProcessRunner();
        var exporter = new StubAcceptanceReportExporter();
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            process,
            new StubResultGate(),
            new StubOwnershipGate(),
            exporter,
            new StubClassificationGate());

        var result = await coordinator.RunAsync(
            fixture.Options,
            CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.Equal(4, process.Commands.Count);
        Assert.Collection(
            process.Commands,
            command => Assert.Contains("--list-tests", command.Arguments),
            command =>
            {
                Assert.Contains("-f net48", command.Arguments);
                Assert.DoesNotContain("--filter", command.Arguments);
            },
            command => Assert.Contains("--list-tests", command.Arguments),
            command =>
            {
                Assert.Contains("-f net8.0", command.Arguments);
                Assert.DoesNotContain("--filter", command.Arguments);
            });
        Assert.Equal(2, result.Runs.Count);
        Assert.All(result.Runs, run => Assert.Equal(BenchmarkPassKind.Acceptance, run.Kind));
        Assert.Equal("pr-acceptance", Assert.Single(exporter.Evidence).Mode);
    }

    [Fact]
    public async Task Direct_profile_rejects_the_single_framework_candidate_exception()
    {
        using var fixture = new Fixture();
        var process = new RecordingProcessRunner();
        var exporter = new StubAcceptanceReportExporter();
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            process,
            new StubResultGate(),
            new StubOwnershipGate(),
            exporter,
            new StubClassificationGate());

        var result = await coordinator.RunAsync(
            fixture.Options with { TargetFrameworks = new[] { "net48" } },
            CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Empty(process.Commands);
        Assert.Empty(exporter.Evidence);
    }

    [Theory]
    [InlineData(1, 1, "net48,net8.0")]
    [InlineData(0, 2, "net48,net8.0")]
    [InlineData(0, 1, "net8.0,net48")]
    [InlineData(0, 1, "net8.0")]
    public async Task Profile_fails_closed_if_the_exact_single_pass_dual_tfm_contract_changes(
        int warmups,
        int passes,
        string frameworks)
    {
        using var fixture = new Fixture();
        var process = new RecordingProcessRunner();
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            process,
            new StubResultGate(),
            new StubOwnershipGate(),
            new StubAcceptanceReportExporter(),
            new StubClassificationGate());
        var options = fixture.Options with
        {
            WarmupPasses = warmups,
            MeasuredPasses = passes,
            TargetFrameworks = frameworks.Split(','),
        };

        var result = await coordinator.RunAsync(options, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Empty(process.Commands);
    }

    [Fact]
    public async Task Sanitized_export_contains_only_aggregate_counts_and_revision()
    {
        using var fixture = new Fixture();
        var trxPath = Path.Combine(fixture.ArtifactRoot, "accepted.trx");
        File.WriteAllText(
            trxPath,
            "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
            "<UnitTestResult testName=\"Secret.Namespace.First(value: 42)\" outcome=\"Passed\" />" +
            "<UnitTestResult testName=\"Secret.Namespace.Second\" outcome=\"Passed\" />" +
            "</Results></TestRun>");
        var command = new CommandResult(
            0,
            false,
            123,
            string.Empty,
            string.Empty,
            Array.Empty<int>(),
            TimeSpan.FromSeconds(3));
        var evidence = new BenchmarkReportEvidence(
            fixture.ArtifactRoot,
            new[] { "net8.0" },
            1,
            new[]
            {
                new BenchmarkFrameworkRun(
                    "private-run-id",
                    "net8.0",
                    trxPath,
                    Path.Combine(fixture.ArtifactRoot, "private-timing.jsonl"),
                    BenchmarkPassKind.Acceptance,
                    1,
                    command,
                    "The following Tests are available:\n    Secret.Namespace.First(value: 42)\n    Secret.Namespace.Second\n"),
            },
            "pr-acceptance",
            Revision);

        var result = await new SqlAcceptanceReportExporter()
            .ExportAsync(evidence, CancellationToken.None);

        Assert.True(result.IsComplete);
        var path = Assert.Single(result.ReportPaths);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;
        Assert.Equal("pr-acceptance", root.GetProperty("mode").GetString());
        Assert.Equal(Revision, root.GetProperty("revision").GetString());
        Assert.Equal("net8.0", root.GetProperty("tfm").GetString());
        Assert.Equal(2, root.GetProperty("methods").GetInt32());
        Assert.Equal(2, root.GetProperty("discovered").GetInt32());
        Assert.Equal(2, root.GetProperty("executed").GetInt32());
        Assert.Equal(2, root.GetProperty("passed").GetInt32());
        Assert.Equal(0, root.GetProperty("failed").GetInt32());
        Assert.Equal(0, root.GetProperty("skipped").GetInt32());
        Assert.Equal(3000m, root.GetProperty("durationMs").GetDecimal());
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("Secret.Namespace", text, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Root, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-run-id", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-commit")]
    [InlineData("0123456789abcdef0123456789abcdef0123456")]
    public async Task Sanitized_export_rejects_invalid_revision(string revision)
    {
        using var fixture = new Fixture();
        var evidence = new BenchmarkReportEvidence(
            fixture.ArtifactRoot,
            new[] { "net8.0" },
            1,
            Array.Empty<BenchmarkFrameworkRun>(),
            "pr-acceptance",
            revision);

        var result = await new SqlAcceptanceReportExporter()
            .ExportAsync(evidence, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Empty(result.ReportPaths);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "MyDmsVn.PrAcceptance", Guid.NewGuid().ToString("N"));
            ArtifactRoot = Path.Combine(Root, "artifacts");
            Directory.CreateDirectory(ArtifactRoot);
            Options = new BenchmarkRunOptions(
                Root,
                "tests.csproj",
                ArtifactRoot,
                Path.Combine(Root, "approval.json"),
                new[] { "net48", "net8.0" },
                TimeSpan.FromMinutes(1),
                WarmupPasses: 0,
                MeasuredPasses: 1)
            {
                Mode = "pr-acceptance",
                Revision = Revision,
            };
        }

        public string Root { get; }

        public string ArtifactRoot { get; }

        public BenchmarkRunOptions Options { get; }

        public void Dispose() => Directory.Delete(Root, true);
    }

    private sealed class StubPreflightGate : IBenchmarkPreflightGate
    {
        private readonly bool _allowed;

        public StubPreflightGate(bool allowed) => _allowed = allowed;

        public Task<bool> ValidateAsync(CancellationToken cancellationToken) => Task.FromResult(_allowed);
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<CommandSpec> Commands { get; } = new List<CommandSpec>();

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            var output = command.Arguments.Contains("--list-tests", StringComparison.Ordinal)
                ? "The following Tests are available:\n    Example.Tests.First\n"
                : string.Empty;
            return Task.FromResult(new CommandResult(
                0,
                false,
                1,
                output,
                string.Empty,
                Array.Empty<int>(),
                TimeSpan.FromSeconds(1)));
        }
    }

    private sealed class StubResultGate : IBenchmarkResultGate
    {
        public bool IsValid(BenchmarkResultEvidence evidence) => true;
    }

    private sealed class StubOwnershipGate : IBenchmarkOwnershipGate
    {
        public Task<BenchmarkOwnershipResult> ValidateAsync(
            BenchmarkOwnershipEvidence evidence,
            CancellationToken cancellationToken) =>
            Task.FromResult(new BenchmarkOwnershipResult(true, null));
    }

    private sealed class StubClassificationGate : IBenchmarkClassificationGate
    {
        public bool IsValid(string assemblyPath, string repositoryRoot) => true;
    }

    private sealed class StubAcceptanceReportExporter : IBenchmarkReportExporter
    {
        public List<BenchmarkReportEvidence> Evidence { get; } = new List<BenchmarkReportEvidence>();

        public IReadOnlyList<string> ExpectedReportPaths(BenchmarkReportEvidence evidence) =>
            evidence.TargetFrameworks.Select(tfm =>
                Path.Combine(evidence.ArtifactRoot, "sql-pr-acceptance-" + tfm + ".json"))
                .ToArray();

        public Task<BenchmarkReportExportResult> ExportAsync(
            BenchmarkReportEvidence evidence,
            CancellationToken cancellationToken)
        {
            Evidence.Add(evidence);
            var paths = ExpectedReportPaths(evidence);
            foreach (var path in paths)
            {
                File.WriteAllText(path, "{}");
            }

            return Task.FromResult(new BenchmarkReportExportResult(true, paths));
        }
    }
}
