using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MyDmsVn.SqlTestHarness.Timing;

namespace MyDmsVn.SqlTestRunner.Reporting;

public static class SqlPerformanceReportBuilder
{
    private static readonly Regex Secret = new Regex(
        "(?i)(server|data source|password|pwd)\\s*=\\s*[^;]+",
        RegexOptions.Compiled);
    private static readonly Regex WindowsPath = new Regex(
        "[A-Za-z]:\\\\[^\\r\\n]+",
        RegexOptions.Compiled);
    private static readonly Regex TheoryValues = new Regex(
        "\\([^\\r\\n]*\\)$",
        RegexOptions.Compiled);

    public static SqlPerformanceReport Build(
        IReadOnlyCollection<BenchmarkSample> runs,
        IReadOnlyCollection<SqlTestTimingEvent> timingEvents,
        SqlDmvSnapshot? dmv)
    {
        var accepted = runs.Where(run => !run.IsWarmup && run.IsValid).ToArray();
        var durations = accepted.Select(run => run.WallClockMs).OrderBy(value => value).ToArray();
        var trxDuration = TimeSpan.FromMilliseconds(
            accepted.Length == 0 ? 0d : (double)accepted[0].WallClockMs);
        var timing = TimingIntervalReconciler.Reconcile(trxDuration, timingEvents);
        var exclusiveByStage = timingEvents
            .GroupBy(item => item.Stage, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => TimingIntervalReconciler.Reconcile(trxDuration, group.ToArray()).MeasuredUnionMs,
                StringComparer.Ordinal);
        return new SqlPerformanceReport(
            accepted.Length,
            Percentile(durations, 0.50m),
            Percentile(durations, 0.95m),
            timing.MeasuredUnionMs,
            timing.OverlapMs,
            timing.ResidualMs,
            exclusiveByStage,
            dmv is null ? "unavailable" : "available");
    }

    public static string RedactShareable(string value)
    {
        var redacted = Secret.Replace(value, match =>
            match.Value.Substring(0, match.Value.IndexOf('=') + 1) + "[redacted]");
        redacted = WindowsPath.Replace(redacted, "[path]");
        return TheoryValues.Replace(redacted, "([row-redacted])");
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
