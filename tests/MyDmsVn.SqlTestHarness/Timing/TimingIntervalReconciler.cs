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
        IReadOnlyDictionary<string, decimal> exclusiveDurationByOperation,
        IReadOnlyDictionary<string, decimal> exclusiveDurationByStage)
    {
        MeasuredUnionMs = measuredUnionMs;
        OverlapMs = overlapMs;
        ResidualMs = residualMs;
        _exclusiveDurationByOperation = exclusiveDurationByOperation;
        ExclusiveDurationByStage = exclusiveDurationByStage;
    }

    public decimal MeasuredUnionMs { get; }

    public decimal OverlapMs { get; }

    public decimal ResidualMs { get; }

    public bool HasNegativeResidual => ResidualMs < 0m;

    public IReadOnlyDictionary<string, decimal> ExclusiveDurationByStage { get; }

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

        var instances = events.Select((timingEvent, index) =>
                new OperationInstance(timingEvent, index))
            .ToArray();
        foreach (var instance in instances)
        {
            instance.Parent = FindParent(instance, instances);
        }

        var roots = instances.Where(instance => instance.Parent is null).ToArray();
        var measuredUnionTicks = UnionLength(roots.Select(instance => ToInterval(instance.Event)));
        var exclusiveByOperation = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var exclusiveIntervalsByStage = new Dictionary<string, List<Interval>>(StringComparer.Ordinal);
        decimal overlapTicks = roots.Sum(root =>
            (decimal)Math.Max(0, root.Event.EndTimestamp - root.Event.StartTimestamp)) - measuredUnionTicks;

        foreach (var instance in instances)
        {
            var timingEvent = instance.Event;
            var children = instances
                .Where(candidate => ReferenceEquals(candidate.Parent, instance))
                .Select(candidate => ToInterval(candidate.Event))
                .ToArray();
            var childUnion = UnionLength(children);
            var childSum = children.Sum(interval => (decimal)(interval.End - interval.Start));
            overlapTicks += childSum - childUnion;
            var durationTicks = Math.Max(0, timingEvent.EndTimestamp - timingEvent.StartTimestamp);
            var exclusiveMs = Math.Max(0m, durationTicks - childUnion) * 1000m / timestampFrequency;
            Add(exclusiveByOperation, timingEvent.OperationId, exclusiveMs);
            if (!exclusiveIntervalsByStage.TryGetValue(timingEvent.Stage, out var stageIntervals))
            {
                stageIntervals = new List<Interval>();
                exclusiveIntervalsByStage[timingEvent.Stage] = stageIntervals;
            }

            stageIntervals.AddRange(SubtractIntervals(ToInterval(timingEvent), children));
        }

        var exclusiveByStage = exclusiveIntervalsByStage.ToDictionary(
            item => item.Key,
            item => UnionLength(item.Value) * 1000m / timestampFrequency,
            StringComparer.Ordinal);

        var measuredUnionMs = measuredUnionTicks * 1000m / timestampFrequency;
        var residualMs = (decimal)trxDuration.TotalMilliseconds - measuredUnionMs;
        return new TimingIntervalReconciliation(
            measuredUnionMs,
            overlapTicks * 1000m / timestampFrequency,
            residualMs,
            exclusiveByOperation,
            exclusiveByStage);
    }

    private static OperationInstance? FindParent(
        OperationInstance child,
        IReadOnlyCollection<OperationInstance> instances)
    {
        if (child.Event.ParentOperationId is null)
        {
            return null;
        }

        return instances
            .Where(candidate =>
                candidate.Index != child.Index &&
                SameScope(candidate.Event, child.Event) &&
                string.Equals(
                    candidate.Event.OperationId,
                    child.Event.ParentOperationId,
                    StringComparison.Ordinal) &&
                candidate.Event.StartTimestamp <= child.Event.StartTimestamp &&
                candidate.Event.EndTimestamp >= child.Event.EndTimestamp)
            .OrderBy(candidate => candidate.Event.EndTimestamp - candidate.Event.StartTimestamp)
            .ThenBy(candidate => candidate.Index)
            .FirstOrDefault();
    }

    private static bool SameScope(SqlTestTimingEvent left, SqlTestTimingEvent right) =>
        string.Equals(left.RunId, right.RunId, StringComparison.Ordinal) &&
        string.Equals(left.Tfm, right.Tfm, StringComparison.Ordinal) &&
        string.Equals(left.TestCaseId, right.TestCaseId, StringComparison.Ordinal);

    private static void Add(IDictionary<string, decimal> values, string key, decimal value)
    {
        values.TryGetValue(key, out var current);
        values[key] = current + value;
    }

    private static Interval ToInterval(SqlTestTimingEvent timingEvent) =>
        new Interval(timingEvent.StartTimestamp, timingEvent.EndTimestamp);

    private static IEnumerable<Interval> SubtractIntervals(
        Interval source,
        IEnumerable<Interval> exclusions)
    {
        var cursor = source.Start;
        foreach (var exclusion in MergeIntervals(exclusions))
        {
            var start = Math.Max(source.Start, exclusion.Start);
            var end = Math.Min(source.End, exclusion.End);
            if (end <= cursor || start >= source.End)
            {
                continue;
            }

            if (start > cursor)
            {
                yield return new Interval(cursor, start);
            }

            cursor = Math.Max(cursor, end);
        }

        if (cursor < source.End)
        {
            yield return new Interval(cursor, source.End);
        }
    }

    private static IEnumerable<Interval> MergeIntervals(IEnumerable<Interval> intervals)
    {
        var ordered = intervals
            .Where(interval => interval.End > interval.Start)
            .OrderBy(interval => interval.Start)
            .ThenBy(interval => interval.End)
            .ToArray();
        if (ordered.Length == 0)
        {
            yield break;
        }

        var current = ordered[0];
        for (var index = 1; index < ordered.Length; index++)
        {
            var next = ordered[index];
            if (next.Start <= current.End)
            {
                current = new Interval(current.Start, Math.Max(current.End, next.End));
                continue;
            }

            yield return current;
            current = next;
        }

        yield return current;
    }

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

    private sealed class OperationInstance
    {
        public OperationInstance(SqlTestTimingEvent timingEvent, int index)
        {
            Event = timingEvent;
            Index = index;
        }

        public SqlTestTimingEvent Event { get; }

        public int Index { get; }

        public OperationInstance? Parent { get; set; }
    }
}
