using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness.Timing;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Trx;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MyDmsVn.SqlTestRunner.Reporting;

public interface IBenchmarkReportExporter
{
    Task<BenchmarkReportExportResult> ExportAsync(
        BenchmarkReportEvidence evidence,
        CancellationToken cancellationToken);
}

public sealed record BenchmarkReportEvidence(
    string ArtifactRoot,
    IReadOnlyCollection<string> TargetFrameworks,
    int RequiredMeasuredPassCount,
    IReadOnlyCollection<BenchmarkFrameworkRun> Runs);

public sealed record BenchmarkReportExportResult(
    bool IsComplete,
    IReadOnlyList<string> ReportPaths)
{
    public static BenchmarkReportExportResult Incomplete() =>
        new BenchmarkReportExportResult(false, Array.Empty<string>());
}

public sealed class SqlPerformanceReportExporter : IBenchmarkReportExporter
{
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Converters = { new StringEnumConverter() },
        Formatting = Formatting.Indented,
    };

    public Task<BenchmarkReportExportResult> ExportAsync(
        BenchmarkReportEvidence evidence,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (evidence.RequiredMeasuredPassCount <= 0 ||
                evidence.TargetFrameworks.Count == 0)
            {
                return Task.FromResult(BenchmarkReportExportResult.Incomplete());
            }

            var outputs = new List<string>();
            var reports = new List<(string Tfm, SqlPerformanceReport Report)>();
            foreach (var tfm in evidence.TargetFrameworks.OrderBy(value => value, StringComparer.Ordinal))
            {
                var accepted = evidence.Runs
                    .Where(run => run.Kind == BenchmarkPassKind.Measured &&
                                  run.Command.ExitCode == 0 &&
                                  !run.Command.TimedOut &&
                                  string.Equals(run.Tfm, tfm, StringComparison.Ordinal))
                    .OrderBy(run => run.PassNumber)
                    .ToArray();
                if (accepted.Length != evidence.RequiredMeasuredPassCount ||
                    accepted.Select(run => run.PassNumber).Distinct().Count() !=
                    evidence.RequiredMeasuredPassCount)
                {
                    return Task.FromResult(BenchmarkReportExportResult.Incomplete());
                }

                var samples = accepted.Select(run => new BenchmarkSample(
                    run.RunId,
                    run.Tfm,
                    false,
                    true,
                    (decimal)run.Command.Duration.TotalMilliseconds)).ToArray();
                var timings = new List<SqlTestTimingEvent>();
                var testCosts = new Dictionary<string, decimal>(StringComparer.Ordinal);
                foreach (var run in accepted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!File.Exists(run.TrxPath) || !File.Exists(run.TimingPath))
                    {
                        return Task.FromResult(BenchmarkReportExportResult.Incomplete());
                    }

                    timings.AddRange(ReadTimings(run.TimingPath, run.RunId, tfm));
                    var trx = TrxResultParser.TryParse(run.TrxPath, tfm);
                    if (!trx.IsComplete || trx.Results.Count == 0 ||
                        trx.Results.Any(row => row.Outcome != TestOutcome.Passed))
                    {
                        return Task.FromResult(BenchmarkReportExportResult.Incomplete());
                    }

                    foreach (var row in trx.Results)
                    {
                        var identity = SqlPerformanceReportBuilder.RedactShareable(row.MethodName);
                        testCosts.TryGetValue(identity, out var current);
                        testCosts[identity] = current + (decimal)row.Duration.TotalMilliseconds;
                    }
                }

                var rankedTests = Rank(testCosts);
                var classCosts = new Dictionary<string, decimal>(StringComparer.Ordinal);
                foreach (var row in rankedTests)
                {
                    var separator = row.Identity.LastIndexOf('.');
                    var identity = separator > 0 ? row.Identity.Substring(0, separator) : row.Identity;
                    identity = SqlPerformanceReportBuilder.RedactShareable(identity);
                    classCosts.TryGetValue(identity, out var current);
                    classCosts[identity] = current + row.TotalDurationMs;
                }

                var baseReport = SqlPerformanceReportBuilder.Build(samples, timings, null);
                reports.Add((tfm, new SqlPerformanceReport(
                    baseReport.AcceptedRunCount,
                    baseReport.WallClockP50Ms,
                    baseReport.WallClockP95Ms,
                    baseReport.MeasuredUnionMs,
                    baseReport.OverlapMs,
                    baseReport.ResidualMs,
                    new SortedDictionary<string, decimal>(
                        baseReport.ExclusiveStageMs.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value,
                            StringComparer.Ordinal),
                        StringComparer.Ordinal),
                    baseReport.DmvStatus,
                    rankedTests,
                    Rank(classCosts))));
            }

            Directory.CreateDirectory(evidence.ArtifactRoot);
            foreach (var item in reports)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileTfm = SafeFileComponent(item.Tfm);
                var jsonPath = Path.Combine(evidence.ArtifactRoot, "sqlperf-baseline-" + fileTfm + ".json");
                var markdownPath = Path.Combine(evidence.ArtifactRoot, "sqlperf-baseline-" + fileTfm + ".md");
                WriteAtomic(jsonPath, JsonConvert.SerializeObject(item.Report, JsonSettings));
                WriteAtomic(markdownPath, BuildMarkdown(item.Tfm, item.Report));
                outputs.Add(jsonPath);
                outputs.Add(markdownPath);
            }

            return Task.FromResult(new BenchmarkReportExportResult(true, outputs));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Task.FromResult(BenchmarkReportExportResult.Incomplete());
        }
    }

    private static IReadOnlyList<SqlTestTimingEvent> ReadTimings(
        string path,
        string runId,
        string tfm)
    {
        var result = new List<SqlTestTimingEvent>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var value = JObject.Parse(line);
            var hasStart = value["startTimestamp"] is not null;
            var hasEnd = value["endTimestamp"] is not null;
            if (!hasStart && !hasEnd &&
                string.Equals(
                    (string?)value["recordType"],
                    "migration-invocation",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!hasStart || !hasEnd)
            {
                throw new InvalidDataException("A timing record is missing interval timestamps.");
            }

            var startTimestamp = (long)value["startTimestamp"]!;
            var endTimestamp = (long)value["endTimestamp"]!;
            var durationMs = (decimal)value["durationMs"]!;
            var expectedDurationMs =
                (decimal)(endTimestamp - startTimestamp) * 1000m / Stopwatch.Frequency;
            if (endTimestamp < startTimestamp ||
                durationMs < 0m ||
                durationMs != expectedDurationMs)
            {
                throw new InvalidDataException("A timing record contains an invalid interval.");
            }

            var timing = new SqlTestTimingEvent(
                RequiredString(value, "runId"),
                RequiredString(value, "testCaseId"),
                RequiredString(value, "operationId"),
                (string?)value["parentOperationId"],
                SqlPerformanceReportBuilder.RedactShareable(RequiredString(value, "stage")),
                startTimestamp,
                endTimestamp,
                durationMs,
                Enum.Parse<SqlTestTimingOutcome>(RequiredString(value, "outcome"), ignoreCase: false),
                RequiredString(value, "tfm"),
                RequiredString(value, "anonymizedDatabaseId"));
            if (string.Equals(timing.RunId, runId, StringComparison.Ordinal) &&
                string.Equals(timing.Tfm, tfm, StringComparison.Ordinal))
            {
                result.Add(timing);
            }
        }

        return result;
    }

    private static string RequiredString(JObject value, string name) =>
        (string?)value[name] ?? throw new InvalidDataException("Timing field is missing: " + name);

    private static IReadOnlyList<SqlPerformanceCost> Rank(
        IReadOnlyDictionary<string, decimal> costs) =>
        costs.Select(pair => new SqlPerformanceCost(pair.Key, pair.Value))
            .OrderByDescending(row => row.TotalDurationMs)
            .ThenBy(row => row.Identity, StringComparer.Ordinal)
            .ToArray();

    private static string BuildMarkdown(string tfm, SqlPerformanceReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# SQL performance baseline — " +
                           SqlPerformanceReportBuilder.RedactShareable(tfm));
        builder.AppendLine();
        builder.AppendLine("- Accepted measured runs: " + report.AcceptedRunCount);
        builder.AppendLine("- Wall clock p50 (ms): " + Format(report.WallClockP50Ms));
        builder.AppendLine("- Wall clock p95 (ms): " + Format(report.WallClockP95Ms));
        builder.AppendLine("- Measured union (ms): " + Format(report.MeasuredUnionMs));
        builder.AppendLine("- Overlap (ms): " + Format(report.OverlapMs));
        builder.AppendLine("- Residual (ms): " + Format(report.ResidualMs));
        builder.AppendLine("- DMV status: " + report.DmvStatus);
        builder.AppendLine();
        builder.AppendLine("## Ranked tests");
        builder.AppendLine();
        foreach (var row in report.RankedTests)
        {
            builder.AppendLine("- " + SqlPerformanceReportBuilder.RedactShareable(row.Identity) +
                               ": " + Format(row.TotalDurationMs) + " ms");
        }

        builder.AppendLine();
        builder.AppendLine("## Ranked classes");
        builder.AppendLine();
        foreach (var row in report.RankedClasses)
        {
            builder.AppendLine("- " + SqlPerformanceReportBuilder.RedactShareable(row.Identity) +
                               ": " + Format(row.TotalDurationMs) + " ms");
        }

        return builder.ToString();
    }

    private static string Format(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string SafeFileComponent(string value) =>
        new string(value.Where(character =>
            char.IsLetterOrDigit(character) || character == '.' || character == '-').ToArray());

    private static void WriteAtomic(string path, string content)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }
}
