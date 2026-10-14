using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MyDmsVn.SqlTestHarness.Timing;

public sealed class TimingIntervalReconciliation
{
    private readonly IReadOnlyDictionary<string, decimal> _exclusiveDurationByOperation;

    internal TimingIntervalReconciliation(
        decimal measuredUnionMs,
        decimal overlapMs,
        decimal residualMs,
        IReadOnlyDictionary<string, decimal> exclusiveDurationByOperation)
    {
        MeasuredUnionMs = measuredUnionMs;
        OverlapMs = overlapMs;
        ResidualMs = residualMs;
        _exclusiveDurationByOperation = exclusiveDurationByOperation;
    }

    public decimal MeasuredUnionMs { get; }

    public decimal OverlapMs { get; }

    public decimal ResidualMs { get; }

    public bool HasNegativeResidual => ResidualMs < 0m;

    public decimal GetExclusiveDurationMs(string operationId) =>
        _exclusiveDurationByOperation[operationId];
}

public static class TimingIntervalReconciler
{
    public static TimingIntervalReconciliation Reconcile(
        TimeSpan trxDuration,
        IReadOnlyCollection<SqlTestTimingEvent> events) =>
        Reconcile(trxDuration, events, Stopwatch.Frequency);

    public static TimingIntervalReconciliation Reconcile(
        TimeSpan trxDuration,
        IReadOnlyCollection<SqlTestTimingEvent> events,
        long timestampFrequency)
    {
        if (events is null)
        {
            throw new ArgumentNullException(nameof(events));
        }

        if (timestampFrequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timestampFrequency));
        }

        var byId = events.ToDictionary(timingEvent => timingEvent.OperationId, StringComparer.Ordinal);
        var roots = events
            .Where(timingEvent =>
                timingEvent.ParentOperationId is null ||
                !byId.ContainsKey(timingEvent.ParentOperationId))
            .ToArray();
        var measuredUnionTicks = UnionLength(roots.Select(ToInterval));
        var exclusive = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal overlapTicks = roots.Sum(root =>
            (decimal)Math.Max(0, root.EndTimestamp - root.StartTimestamp)) - measuredUnionTicks;

        foreach (var timingEvent in events)
        {
            var children = events
                .Where(candidate => string.Equals(
                    candidate.ParentOperationId,
                    timingEvent.OperationId,
                    StringComparison.Ordinal))
                .Select(ToInterval)
                .ToArray();
            var childUnion = UnionLength(children);
            var childSum = children.Sum(interval => (decimal)(interval.End - interval.Start));
            overlapTicks += childSum - childUnion;
            var durationTicks = Math.Max(0, timingEvent.EndTimestamp - timingEvent.StartTimestamp);
            exclusive[timingEvent.OperationId] =
                Math.Max(0m, durationTicks - childUnion) * 1000m / timestampFrequency;
        }

        var measuredUnionMs = measuredUnionTicks * 1000m / timestampFrequency;
        var residualMs = (decimal)trxDuration.TotalMilliseconds - measuredUnionMs;
        return new TimingIntervalReconciliation(
            measuredUnionMs,
            overlapTicks * 1000m / timestampFrequency,
            residualMs,
            exclusive);
    }

    private static Interval ToInterval(SqlTestTimingEvent timingEvent) =>
        new Interval(timingEvent.StartTimestamp, timingEvent.EndTimestamp);

    private static decimal UnionLength(IEnumerable<Interval> intervals)
    {
        var ordered = intervals
            .Where(interval => interval.End >= interval.Start)
            .OrderBy(interval => interval.Start)
            .ThenBy(interval => interval.End)
            .ToArray();
        if (ordered.Length == 0)
        {
            return 0m;
        }

        long currentStart = ordered[0].Start;
        long currentEnd = ordered[0].End;
        decimal total = 0m;
        for (var index = 1; index < ordered.Length; index++)
        {
            var interval = ordered[index];
            if (interval.Start <= currentEnd)
            {
                currentEnd = Math.Max(currentEnd, interval.End);
            }
            else
            {
                total += currentEnd - currentStart;
                currentStart = interval.Start;
                currentEnd = interval.End;
            }
        }

        return total + currentEnd - currentStart;
    }

    private readonly struct Interval
    {
        public Interval(long start, long end)
        {
            Start = start;
            End = end;
        }

        public long Start { get; }

        public long End { get; }
    }
}
