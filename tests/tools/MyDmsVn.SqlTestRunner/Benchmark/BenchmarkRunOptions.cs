using System;
using System.Collections.Generic;

namespace MyDmsVn.SqlTestRunner.Benchmark;

public sealed record BenchmarkRunOptions(
    string RepositoryRoot,
    string TestProject,
    string ArtifactRoot,
    string ApprovalFile,
    IReadOnlyList<string> TargetFrameworks,
    TimeSpan CommandTimeout);
