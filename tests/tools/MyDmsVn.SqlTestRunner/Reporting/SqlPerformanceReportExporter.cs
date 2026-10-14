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
                var testCosts = new Dictionary<string, CostAccumulator>(StringComparer.Ordinal);
                foreach (var run in accepted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!File.Exists(run.TrxPath) || !File.Exists(run.TimingPath))
                    {
                        return Task.FromResult(BenchmarkReportExportResult.Incomplete());
                    }

                    var runTimings = ReadTimings(run.TimingPath, run.RunId, tfm);
                    timings.AddRange(runTimings);
                    var trx = TrxResultParser.TryParse(run.TrxPath, tfm);
                    if (!trx.IsComplete || trx.Results.Count == 0 ||
                        trx.Results.Any(row => row.Outcome != TestOutcome.Passed))
                    {
                        return Task.FromResult(BenchmarkReportExportResult.Incomplete());
                    }

                    var methodGroups = trx.Results
                        .GroupBy(row => row.MethodName, StringComparer.Ordinal)
                        .ToArray();
                    foreach (var timing in runTimings)
                    {
                        if (methodGroups.Count(group => TestCaseMatches(
                                timing.TestCaseId,
                                group.Key)) != 1)
                        {
                            throw new InvalidDataException(
                                "A timing event cannot be attributed to exactly one TRX test.");
                        }
                    }

                    foreach (var group in methodGroups)
                    {
                        var identity = SqlPerformanceReportBuilder.RedactShareable(group.Key);
                        if (!testCosts.TryGetValue(identity, out var accumulator))
                        {
                            accumulator = new CostAccumulator(identity);
                            testCosts.Add(identity, accumulator);
                        }

                        var groupEvents = runTimings
                            .Where(timing => TestCaseMatches(timing.TestCaseId, group.Key))
                            .ToArray();
                        var duration = group.Sum(row => row.Duration.TotalMilliseconds);
                        var reconciliation = TimingIntervalReconciler.Reconcile(
                            TimeSpan.FromMilliseconds(duration),
                            groupEvents);
                        accumulator.Add(
                            group.Select(row => (decimal)row.Duration.TotalMilliseconds),
                            reconciliation);
                    }
                }

                var totalWallClockMs = samples.Sum(sample => sample.WallClockMs);
                var rankedTests = Rank(testCosts, totalWallClockMs);
                var classCosts = new Dictionary<string, CostAccumulator>(StringComparer.Ordinal);
                foreach (var pair in testCosts)
                {
                    var separator = pair.Key.LastIndexOf('.');
                    var identity = separator > 0 ? pair.Key.Substring(0, separator) : pair.Key;
                    identity = SqlPerformanceReportBuilder.RedactShareable(identity);
                    if (!classCosts.TryGetValue(identity, out var accumulator))
                    {
                        accumulator = new CostAccumulator(identity);
                        classCosts.Add(identity, accumulator);
                    }

                    accumulator.Merge(pair.Value);
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
                    Rank(classCosts, totalWallClockMs))));
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
            if (!string.Equals(RequiredString(value, "runId"), runId, StringComparison.Ordinal) ||
                !string.Equals(RequiredString(value, "tfm"), tfm, StringComparison.Ordinal))
            {
                throw new InvalidDataException("A timing record has unexpected run provenance.");
            }

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
            result.Add(timing);
        }

        return result;
    }

    private static string RequiredString(JObject value, string name) =>
        (string?)value[name] ?? throw new InvalidDataException("Timing field is missing: " + name);

    private static bool TestCaseMatches(string testCaseId, string methodName) =>
        string.Equals(testCaseId, methodName, StringComparison.Ordinal) ||
        methodName.EndsWith("." + testCaseId, StringComparison.Ordinal);

    private static IReadOnlyList<SqlPerformanceCost> Rank(
        IReadOnlyDictionary<string, CostAccumulator> costs,
        decimal totalWallClockMs) =>
        costs.Select(pair => pair.Value.Build(totalWallClockMs))
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
                               ": count=" + row.Count +
                               ", total=" + Format(row.TotalDurationMs) +
                               " ms, p50=" + Format(row.P50DurationMs) +
                               " ms, p95=" + Format(row.P95DurationMs) +
                               " ms, setup=" + Format(row.SetupDurationMs) +
                               " ms, teardown=" + Format(row.TeardownDurationMs) +
                               " ms, exclusive=" + Format(row.MeasuredExclusiveDurationMs) +
                               " ms, overlap=" + Format(row.OverlapDurationMs) +
                               " ms, residual=" + Format(row.ResidualDurationMs) +
                               " ms, stages=" + FormatStages(row.ExclusiveStageMs) +
                               ", wall-clock contribution=" +
                               Format(row.WallClockContributionPercent) + "%");
        }

        builder.AppendLine();
        builder.AppendLine("## Ranked classes");
        builder.AppendLine();
        foreach (var row in report.RankedClasses)
        {
            builder.AppendLine("- " + SqlPerformanceReportBuilder.RedactShareable(row.Identity) +
                               ": count=" + row.Count +
                               ", total=" + Format(row.TotalDurationMs) +
                               " ms, p50=" + Format(row.P50DurationMs) +
                               " ms, p95=" + Format(row.P95DurationMs) +
                               " ms, setup=" + Format(row.SetupDurationMs) +
                               " ms, teardown=" + Format(row.TeardownDurationMs) +
                               " ms, exclusive=" + Format(row.MeasuredExclusiveDurationMs) +
                               " ms, overlap=" + Format(row.OverlapDurationMs) +
                               " ms, residual=" + Format(row.ResidualDurationMs) +
                               " ms, stages=" + FormatStages(row.ExclusiveStageMs) +
                               ", wall-clock contribution=" +
                               Format(row.WallClockContributionPercent) + "%");
        }

        return builder.ToString();
    }

    private static string Format(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatStages(IReadOnlyDictionary<string, decimal> stages) =>
        "[" + string.Join(
            ", ",
            stages.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => SqlPerformanceReportBuilder.RedactShareable(pair.Key) +
                                "=" + Format(pair.Value))) + "]";

    private static string SafeFileComponent(string value) =>
        new string(value.Where(character =>
            char.IsLetterOrDigit(character) || character == '.' || character == '-').ToArray());

    private static void WriteAtomic(string path, string content)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }

    private sealed class CostAccumulator
    {
        private static readonly HashSet<string> SetupStages = new HashSet<string>(
            new[] { "create", "migration", "seed", "permission-sync" },
            StringComparer.Ordinal);
        private static readonly HashSet<string> TeardownStages = new HashSet<string>(
            new[] { "pool-cleanup", "drop" },
            StringComparer.Ordinal);
        private readonly List<decimal> _durations = new List<decimal>();
        private readonly Dictionary<string, decimal> _exclusiveStages =
            new Dictionary<string, decimal>(StringComparer.Ordinal);

        public CostAccumulator(string identity) => Identity = identity;

        public string Identity { get; }

        public decimal OverlapDurationMs { get; private set; }

        public decimal ResidualDurationMs { get; private set; }

        public void Add(
            IEnumerable<decimal> durations,
            TimingIntervalReconciliation reconciliation)
        {
            _durations.AddRange(durations);
            OverlapDurationMs += reconciliation.OverlapMs;
            ResidualDurationMs += reconciliation.ResidualMs;
            foreach (var stage in reconciliation.ExclusiveDurationByStage)
            {
                AddStage(stage.Key, stage.Value);
            }
        }

        public void Merge(CostAccumulator other)
        {
            _durations.AddRange(other._durations);
            OverlapDurationMs += other.OverlapDurationMs;
            ResidualDurationMs += other.ResidualDurationMs;
            foreach (var stage in other._exclusiveStages)
            {
                AddStage(stage.Key, stage.Value);
            }
        }

        public SqlPerformanceCost Build(decimal totalWallClockMs)
        {
            var ordered = _durations.OrderBy(value => value).ToArray();
            var total = ordered.Sum();
            var setup = _exclusiveStages
                .Where(pair => SetupStages.Contains(pair.Key))
                .Sum(pair => pair.Value);
            var teardown = _exclusiveStages
                .Where(pair => TeardownStages.Contains(pair.Key))
                .Sum(pair => pair.Value);
            return new SqlPerformanceCost(
                Identity,
                ordered.Length,
                total,
                Percentile(ordered, 0.50m),
                Percentile(ordered, 0.95m),
                setup,
                teardown,
                _exclusiveStages.Values.Sum(),
                OverlapDurationMs,
                ResidualDurationMs,
                totalWallClockMs == 0m ? 0m : total * 100m / totalWallClockMs,
                new SortedDictionary<string, decimal>(_exclusiveStages, StringComparer.Ordinal));
        }

        private void AddStage(string stage, decimal duration)
        {
            _exclusiveStages.TryGetValue(stage, out var current);
            _exclusiveStages[stage] = current + duration;
        }

        private static decimal Percentile(IReadOnlyList<decimal> ordered, decimal percentile)
        {
            if (ordered.Count == 0)
            {
                return 0m;
            }

            var rank = (int)Math.Ceiling(ordered.Count * percentile);
            return ordered[Math.Max(0, rank - 1)];
        }
    }
}
