using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reporting;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Reporting;

public sealed class SqlPerformanceReportExporterTests
{
    [Fact]
    public async Task Exporter_writes_sanitized_deterministic_reports_from_measured_accepted_runs()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MyDmsVn.SqlPerformanceReportExporter",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runs = new List<BenchmarkFrameworkRun>
            {
                WriteRun(root, "warmup-secret", "net8.0", BenchmarkPassKind.Warmup, 1, 999,
                    ("Example.WarmupTests.Leak(secret-warmup)", 999),
                    Timing("warmup-secret", "net8.0", "Example.WarmupTests.Leak", "warmup-secret", null, "warmup", 0, 999)),
                WriteRun(root, "net8-one", "net8.0", BenchmarkPassKind.Measured, 1, 100,
                    ("Example.AlphaTests.Slow(secret-one)", 60),
                    ("Example.ZetaTests.Tie(server-one)", 40),
                    Timing("net8-one", "net8.0", "Example.AlphaTests.Slow", "body", null, "test-body", 0, 100),
                    Timing("net8-one", "net8.0", "Example.AlphaTests.Slow", "migration", "body", "migration", 20, 80)),
                WriteRun(root, "net8-two", "net8.0", BenchmarkPassKind.Measured, 2, 200,
                    ("Example.AlphaTests.Slow(secret-two)", 70),
                    ("Example.BetaTests.Tie(C:\\private\\customer-42)", 40),
                    Timing("net8-two", "net8.0", "Example.AlphaTests.Slow", "database", null, "database", 0, 50),
                    Timing("net8-two", "net8.0", "Example.AlphaTests.Slow", "migration", null, "migration", 40, 100)),
                WriteRun(root, "invalid-secret", "net8.0", BenchmarkPassKind.Measured, 3, 777,
                    ("Example.InvalidTests.Leak(password=secret-invalid)", 777),
                    Timing("invalid-secret", "net8.0", "Example.InvalidTests.Leak", "invalid", null, "invalid", 0, 777),
                    new ExitCode(1)),
                WriteRun(root, "net48-one", "net48", BenchmarkPassKind.Measured, 1, 50,
                    ("Example.LegacyTests.First(secret-legacy)", 50),
                    Timing("net48-one", "net48", "Example.LegacyTests.First", "legacy", null, "migration", 0, 50)),
                WriteRun(root, "net48-two", "net48", BenchmarkPassKind.Measured, 2, 70,
                    ("Example.LegacyTests.Second(server-legacy)", 70),
                    Timing("net48-two", "net48", "Example.LegacyTests.Second", "legacy", null,
                        "Server=review-host;Password=stage-secret", 0, 70)),
            };
            var exporter = new SqlPerformanceReportExporter();

            var result = await exporter.ExportAsync(
                new BenchmarkReportEvidence(root, new[] { "net48", "net8.0" }, 2, runs),
                CancellationToken.None);

            Assert.True(result.IsComplete);
            Assert.Equal(4, result.ReportPaths.Count);
            Assert.All(result.ReportPaths, path => Assert.True(File.Exists(path), path));
            var jsonPath = Path.Combine(root, "sqlperf-baseline-net8.0.json");
            var report = JObject.Parse(File.ReadAllText(jsonPath));
            Assert.Equal(2, (int?)report["acceptedRunCount"]);
            Assert.Equal(100m, (decimal?)report["wallClockP50Ms"]);
            Assert.Equal(200m, (decimal?)report["wallClockP95Ms"]);
            Assert.Equal(200m, (decimal?)report["measuredUnionMs"]);
            Assert.Equal(10m, (decimal?)report["overlapMs"]);
            Assert.Equal(100m, (decimal?)report["residualMs"]);
            Assert.Equal(50m, (decimal?)report["exclusiveStageMs"]?["database"]);
            Assert.Equal(120m, (decimal?)report["exclusiveStageMs"]?["migration"]);
            Assert.Equal(40m, (decimal?)report["exclusiveStageMs"]?["test-body"]);
            Assert.Equal("unavailable", (string?)report["dmvStatus"]);
            Assert.Equal(
                new[] { "Example.AlphaTests.Slow", "Example.BetaTests.Tie", "Example.ZetaTests.Tie" },
                report["rankedTests"]!.Select(row => (string?)row["identity"]));
            Assert.Equal(new[] { 130m, 40m, 40m },
                report["rankedTests"]!.Select(row => (decimal)row["totalDurationMs"]!));

            var shareable = string.Join(
                Environment.NewLine,
                result.ReportPaths.Select(File.ReadAllText));
            foreach (var sensitive in new[]
                     {
                         "secret-warmup", "secret-one", "server-one", "secret-two",
                         "customer-42", "secret-invalid", "secret-legacy", "server-legacy",
                         "review-host", "stage-secret",
                         root,
                     })
            {
                Assert.DoesNotContain(sensitive, shareable, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Exporter_reports_three_pass_test_and_class_statistics_with_stage_reconciliation()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ReportStatistics", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string test = "Example.InventoryTests.Creates_receipt";
            var runs = new[]
            {
                StatisticalRun(root, "pass-1", 1, 100, 150, test),
                StatisticalRun(root, "pass-2", 2, 200, 250, test),
                StatisticalRun(root, "pass-3", 3, 300, 350, test),
            };

            var result = await new SqlPerformanceReportExporter().ExportAsync(
                new BenchmarkReportEvidence(root, new[] { "net8.0" }, 3, runs),
                CancellationToken.None);

            Assert.True(result.IsComplete);
            var report = JObject.Parse(File.ReadAllText(Path.Combine(root, "sqlperf-baseline-net8.0.json")));
            var testRow = Assert.Single(report["rankedTests"]!);
            Assert.Equal(3, (int?)testRow["count"]);
            Assert.Equal(600m, (decimal?)testRow["totalDurationMs"]);
            Assert.Equal(200m, (decimal?)testRow["p50DurationMs"]);
            Assert.Equal(300m, (decimal?)testRow["p95DurationMs"]);
            Assert.Equal(90m, (decimal?)testRow["setupDurationMs"]);
            Assert.Equal(30m, (decimal?)testRow["teardownDurationMs"]);
            Assert.Equal(480m, (decimal?)testRow["exclusiveStageMs"]?["test-body"]);
            Assert.Equal(600m, (decimal?)testRow["measuredExclusiveDurationMs"]);
            Assert.Equal(0m, (decimal?)testRow["residualDurationMs"]);
            Assert.Equal(80m, (decimal?)testRow["wallClockContributionPercent"]);

            var classRow = Assert.Single(report["rankedClasses"]!);
            Assert.Equal("Example.InventoryTests", (string?)classRow["identity"]);
            Assert.Equal(3, (int?)classRow["count"]);
            Assert.Equal(600m, (decimal?)classRow["totalDurationMs"]);
            Assert.Equal(200m, (decimal?)classRow["p50DurationMs"]);
            Assert.Equal(300m, (decimal?)classRow["p95DurationMs"]);
            Assert.Equal(90m, (decimal?)classRow["setupDurationMs"]);
            Assert.Equal(30m, (decimal?)classRow["teardownDurationMs"]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Exporter_rejects_malformed_timing_event_instead_of_omitting_it()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ReportMalformed", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var run = WriteRun(root, "malformed", "net8.0", BenchmarkPassKind.Measured, 1, 10,
                ("Example.Tests.One", 10),
                "{\"runId\":\"malformed\",\"tfm\":\"net8.0\",\"stage\":\"migration\"," +
                "\"operationId\":\"broken\",\"startTimestamp\":1}");
            var malformedLines = new[]
            {
                "{\"runId\":\"malformed\",\"tfm\":\"net8.0\",\"stage\":\"migration\"," +
                "\"operationId\":\"missing-end\",\"startTimestamp\":1}",
                Timing("malformed", "net8.0", "Example.Tests.One", "reversed", null, "migration", 10, 0),
                Timing("malformed", "net8.0", "Example.Tests.One", "mismatch", null, "migration", 0, 10)
                    .Replace("\"durationMs\":10", "\"durationMs\":9", StringComparison.Ordinal),
            };

            foreach (var malformed in malformedLines)
            {
                File.WriteAllText(run.TimingPath, malformed);
                var result = await new SqlPerformanceReportExporter().ExportAsync(
                    new BenchmarkReportEvidence(root, new[] { "net8.0" }, 1, new[] { run }),
                    CancellationToken.None);

                Assert.False(result.IsComplete);
                Assert.Empty(result.ReportPaths);
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Exporter_rejects_missing_required_measured_pass()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ReportMissing", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var run = WriteRun(root, "only-one", "net8.0", BenchmarkPassKind.Measured, 1, 10,
                ("Example.Tests.One", 10),
                Timing("only-one", "net8.0", "Example.Tests.One", "one", null, "migration", 0, 10));

            var result = await new SqlPerformanceReportExporter().ExportAsync(
                new BenchmarkReportEvidence(root, new[] { "net8.0" }, 2, new[] { run }),
                CancellationToken.None);

            Assert.False(result.IsComplete);
            Assert.Empty(result.ReportPaths);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Exporter_rejects_create_and_drop_timing_from_a_different_run()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ReportWrongRun", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var run = WriteRun(root, "expected-run", "net8.0", BenchmarkPassKind.Measured, 1, 20,
                ("Example.Tests.One", 20),
                Timing("other-run", "net8.0", "Example.Tests.One", "create", null, "create", 0, 10),
                Timing("other-run", "net8.0", "Example.Tests.One", "drop", null, "drop", 10, 20));

            var result = await new SqlPerformanceReportExporter().ExportAsync(
                new BenchmarkReportEvidence(root, new[] { "net8.0" }, 1, new[] { run }),
                CancellationToken.None);

            Assert.False(result.IsComplete);
            Assert.Empty(result.ReportPaths);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Exporter_accepts_helper_timings_attributed_to_the_public_test()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ReportHelper", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string test = "Example.RepositoryTests.Public_test";
            var run = WriteRun(root, "run-1", "net8.0", BenchmarkPassKind.Measured, 1, 20,
                (test, 20),
                Timing("run-1", "net8.0", test, "helper-create", null, "create", 0, 10),
                Timing("run-1", "net8.0", test, "helper-drop", null, "drop", 10, 20));

            var result = await new SqlPerformanceReportExporter().ExportAsync(
                new BenchmarkReportEvidence(root, new[] { "net8.0" }, 1, new[] { run }),
                CancellationToken.None);

            Assert.True(result.IsComplete);
            var report = JObject.Parse(File.ReadAllText(Path.Combine(root, "sqlperf-baseline-net8.0.json")));
            var row = Assert.Single(report["rankedTests"]!);
            Assert.Equal(test, (string?)row["identity"]);
            Assert.Equal(10m, (decimal?)row["setupDurationMs"]);
            Assert.Equal(10m, (decimal?)row["teardownDurationMs"]);
            Assert.Equal(0m, (decimal?)row["residualDurationMs"]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static BenchmarkFrameworkRun WriteRun(
        string root,
        string runId,
        string tfm,
        BenchmarkPassKind kind,
        int pass,
        decimal durationMs,
        params object[] rows)
    {
        var trxPath = Path.Combine(root, runId + ".trx");
        var timingPath = Path.Combine(root, runId + ".jsonl");
        var trxRows = rows.OfType<ValueTuple<string, int>>().ToArray();
        File.WriteAllText(
            trxPath,
            "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
            string.Join(string.Empty, trxRows.Select(row =>
                "<UnitTestResult testName=\"" + System.Security.SecurityElement.Escape(row.Item1) +
                "\" outcome=\"Passed\" duration=\"00:00:00." +
                (row.Item2 * 10000).ToString("0000000") + "\" />")) +
            "</Results></TestRun>");
        File.WriteAllLines(timingPath, rows.OfType<string>());
        var exitCode = rows.OfType<ExitCode>().Select(value => value.Value).DefaultIfEmpty(0).Single();
        return new BenchmarkFrameworkRun(
            runId,
            tfm,
            trxPath,
            timingPath,
            kind,
            pass,
            new CommandResult(exitCode, false, 1, string.Empty, string.Empty, Array.Empty<int>(),
                TimeSpan.FromMilliseconds((double)durationMs)));
    }

    private static string Timing(
        string runId,
        string tfm,
        string testCaseId,
        string operationId,
        string? parentOperationId,
        string stage,
        long startMs,
        long endMs)
    {
        var parent = parentOperationId is null ? "null" : "\"" + parentOperationId + "\"";
        return "{\"runId\":\"" + runId + "\",\"testCaseId\":\"" + testCaseId + "\"," +
               "\"operationId\":\"" + operationId + "\",\"parentOperationId\":" + parent + "," +
               "\"stage\":\"" + stage + "\",\"startTimestamp\":" +
               (startMs * Stopwatch.Frequency / 1000) + ",\"endTimestamp\":" +
               (endMs * Stopwatch.Frequency / 1000) + ",\"durationMs\":" + (endMs - startMs) +
               ",\"outcome\":\"Succeeded\",\"tfm\":\"" + tfm +
               "\",\"anonymizedDatabaseId\":\"db\"}";
    }

    private static BenchmarkFrameworkRun StatisticalRun(
        string root,
        string runId,
        int pass,
        int trxDurationMs,
        decimal wallClockMs,
        string test)
    {
        return WriteRun(
            root,
            runId,
            "net8.0",
            BenchmarkPassKind.Measured,
            pass,
            wallClockMs,
            (test, trxDurationMs),
            Timing(runId, "net8.0", test, "create", null, "create", 0, 10),
            Timing(runId, "net8.0", test, "migration", null, "migration", 10, 30),
            Timing(runId, "net8.0", test, "body", null, "test-body", 30, trxDurationMs - 10),
            Timing(runId, "net8.0", test, "drop", null, "drop", trxDurationMs - 10, trxDurationMs));
    }

    private readonly record struct ExitCode(int Value);
}
