using System;
using System.Collections.Generic;
using System.Diagnostics;
using MyDmsVn.SqlTestHarness.Timing;
using MyDmsVn.SqlTestRunner.Reporting;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Reporting;

public sealed class SqlPerformanceReportBuilderTests
{
    [Fact]
    public void Builder_excludes_warmup_and_invalid_runs_and_computes_literal_percentiles()
    {
        var runs = new[]
        {
            Run("warmup", warmup: true, valid: true, 999),
            Run("invalid", warmup: false, valid: false, 888),
            Run("one", warmup: false, valid: true, 100),
            Run("two", warmup: false, valid: true, 200),
            Run("three", warmup: false, valid: true, 300),
        };

        var report = SqlPerformanceReportBuilder.Build(runs, Array.Empty<SqlTestTimingEvent>(), null);

        Assert.Equal(3, report.AcceptedRunCount);
        Assert.Equal(200m, report.WallClockP50Ms);
        Assert.Equal(300m, report.WallClockP95Ms);
        Assert.Equal("unavailable", report.DmvStatus);
    }

    [Fact]
    public void Nested_and_overlapping_timing_is_reconciled_against_trx_without_addition()
    {
        var events = new[]
        {
            Event("parent", null, 0, 100),
            Event("child", "parent", 20, 80),
            Event("sibling", null, 70, 120),
        };

        var report = SqlPerformanceReportBuilder.Build(
            new[] { Run("one", false, true, 150) },
            events,
            null);

        Assert.Equal(120m, report.MeasuredUnionMs);
        Assert.Equal(30m, report.ResidualMs);
        Assert.Equal(30m, report.OverlapMs);
        Assert.Equal(120m, report.ExclusiveStageMs["migration"]);
    }

    [Theory]
    [InlineData("Server=prod;Password=secret")]
    [InlineData("C:\\Users\\person\\artifact.trx")]
    [InlineData("Theory(value: customer-42)")]
    public void Shareable_text_redacts_secrets_paths_hosts_and_theory_values(string sensitive)
    {
        var text = SqlPerformanceReportBuilder.RedactShareable(sensitive);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prod", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("person", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customer-42", text, StringComparison.OrdinalIgnoreCase);
    }

    private static BenchmarkSample Run(string id, bool warmup, bool valid, decimal durationMs) =>
        new BenchmarkSample(id, "net8.0", warmup, valid, durationMs);

    private static SqlTestTimingEvent Event(
        string id,
        string? parent,
        long start,
        long end) =>
        new SqlTestTimingEvent(
            "run",
            "test",
            id,
            parent,
            "migration",
            start * Stopwatch.Frequency / 1000,
            end * Stopwatch.Frequency / 1000,
            end - start,
            SqlTestTimingOutcome.Succeeded,
            "net8.0",
            "db");
}
