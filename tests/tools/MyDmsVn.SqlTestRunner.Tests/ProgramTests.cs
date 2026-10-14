using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests;

public sealed class ProgramTests
{
    [Fact]
    public async Task Baseline_required_report_failure_maps_to_exit_code_three_without_SQL()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ProgramTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var called = false;
            var result = await Program.RunAsync(
                new[]
                {
                    "baseline",
                    "--repo", root,
                    "--project", Path.Combine(root, "tests.csproj"),
                    "--artifacts", Path.Combine(root, "artifacts"),
                    "--approval", Path.Combine(root, "approval.json"),
                },
                (options, cancellationToken) =>
                {
                    called = true;
                    return Task.FromResult(new BenchmarkRunResult(
                        false,
                        Array.Empty<BenchmarkFrameworkRun>()));
                },
                CancellationToken.None);

            Assert.True(called);
            Assert.Equal(3, result);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
