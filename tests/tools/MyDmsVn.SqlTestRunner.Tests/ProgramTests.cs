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
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";

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

    [Fact]
    public async Task Pr_acceptance_maps_to_a_single_pass_profile_for_both_tfms()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ProgramTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            BenchmarkRunOptions? received = null;
            var result = await Program.RunAsync(
                new[]
                {
                    "pr-acceptance",
                    "--repo", root,
                    "--project", Path.Combine(root, "tests.csproj"),
                    "--artifacts", Path.Combine(root, "artifacts"),
                    "--approval", Path.Combine(root, "approval.json"),
                    "--revision", Revision,
                },
                (options, cancellationToken) => throw new InvalidOperationException("Baseline must not run."),
                (options, cancellationToken) =>
                {
                    received = options;
                    return Task.FromResult(new BenchmarkRunResult(
                        true,
                        Array.Empty<BenchmarkFrameworkRun>()));
                },
                CancellationToken.None);

            Assert.Equal(0, result);
            Assert.NotNull(received);
            Assert.Equal(0, received.WarmupPasses);
            Assert.Equal(1, received.MeasuredPasses);
            Assert.Equal("pr-acceptance", received.Mode);
            Assert.Equal(Revision, received.Revision);
            Assert.Equal(new[] { "net48", "net8.0" }, received.TargetFrameworks);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-commit")]
    public async Task Pr_acceptance_rejects_missing_or_invalid_revision(string revision)
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ProgramTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var called = false;
            var result = await Program.RunAsync(
                new[]
                {
                    "pr-acceptance",
                    "--repo", root,
                    "--project", Path.Combine(root, "tests.csproj"),
                    "--artifacts", Path.Combine(root, "artifacts"),
                    "--approval", Path.Combine(root, "approval.json"),
                    "--revision", revision,
                },
                (options, cancellationToken) => throw new InvalidOperationException("Baseline must not run."),
                (options, cancellationToken) =>
                {
                    called = true;
                    return Task.FromResult(new BenchmarkRunResult(
                        true,
                        Array.Empty<BenchmarkFrameworkRun>()));
                },
                CancellationToken.None);

            Assert.Equal(64, result);
            Assert.False(called);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
