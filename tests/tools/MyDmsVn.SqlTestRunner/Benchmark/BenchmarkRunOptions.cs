using System;
using System.Collections.Generic;

namespace MyDmsVn.SqlTestRunner.Benchmark;

public sealed record BenchmarkRunOptions(
    string RepositoryRoot,
    string TestProject,
    string ArtifactRoot,
    string ApprovalFile,
    IReadOnlyList<string> TargetFrameworks,
    TimeSpan CommandTimeout,
    int WarmupPasses = 1,
    int MeasuredPasses = 3,
    TimeSpan? OverallRunTimeout = null)
{
    public TimeSpan OverallTimeout { get; init; } = OverallRunTimeout ?? TimeSpan.FromHours(4);
}
