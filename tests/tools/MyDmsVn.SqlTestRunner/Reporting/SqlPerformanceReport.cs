using System.Collections.Generic;

namespace MyDmsVn.SqlTestRunner.Reporting;

public sealed record BenchmarkSample(
    string RunId,
    string Tfm,
    bool IsWarmup,
    bool IsValid,
    decimal WallClockMs);

public sealed class SqlPerformanceReport
{
    public SqlPerformanceReport(
        int acceptedRunCount,
        decimal wallClockP50Ms,
        decimal wallClockP95Ms,
        decimal measuredUnionMs,
        decimal overlapMs,
        decimal residualMs,
        IReadOnlyDictionary<string, decimal> exclusiveStageMs,
        string dmvStatus)
    {
        AcceptedRunCount = acceptedRunCount;
        WallClockP50Ms = wallClockP50Ms;
        WallClockP95Ms = wallClockP95Ms;
        MeasuredUnionMs = measuredUnionMs;
        OverlapMs = overlapMs;
        ResidualMs = residualMs;
        ExclusiveStageMs = exclusiveStageMs;
        DmvStatus = dmvStatus;
    }

    public int AcceptedRunCount { get; }
    public decimal WallClockP50Ms { get; }
    public decimal WallClockP95Ms { get; }
    public decimal MeasuredUnionMs { get; }
    public decimal OverlapMs { get; }
    public decimal ResidualMs { get; }
    public IReadOnlyDictionary<string, decimal> ExclusiveStageMs { get; }
    public string DmvStatus { get; }
}

public sealed record SqlDmvSnapshot(
    decimal? CpuMs,
    decimal? IoMs,
    decimal? WaitMs,
    decimal? MemoryGrantKb);
