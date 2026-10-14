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
    public void Builder_reconciles_only_matching_accepted_run_and_tfm_events_before_aggregation()
    {
        var runs = new[]
        {
            Run("warmup", warmup: true, valid: true, 90),
            Run("invalid", warmup: false, valid: false, 80),
            Run("one", warmup: false, valid: true, 100),
            Run("two", warmup: false, valid: true, 200),
        };
        var events = new[]
        {
            Event("warmup-op", null, 0, 90, runId: "warmup"),
            Event("invalid-op", null, 0, 80, runId: "invalid"),
            Event("one-op", null, 0, 20, runId: "one"),
            Event("two-op", null, 0, 30, runId: "two"),
            Event("wrong-tfm", null, 0, 70, runId: "one", tfm: "net48"),
        };

        var report = SqlPerformanceReportBuilder.Build(runs, events, null);

        Assert.Equal(2, report.AcceptedRunCount);
        Assert.Equal(50m, report.MeasuredUnionMs);
        Assert.Equal(250m, report.ResidualMs);
    }

    [Fact]
    public void Exclusive_stage_duration_subtracts_children_from_a_different_stage()
    {
        var events = new[]
        {
            Event("body", null, 0, 100, stage: "test-body"),
            Event("migration", "body", 20, 80, stage: "migration"),
        };

        var report = SqlPerformanceReportBuilder.Build(
            new[] { Run("run", warmup: false, valid: true, 100) },
            events,
            null);

        Assert.Equal(40m, report.ExclusiveStageMs["test-body"]);
        Assert.Equal(60m, report.ExclusiveStageMs["migration"]);
    }

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
            new[] { Run("run", false, true, 150) },
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
        long end,
        string runId = "run",
        string tfm = "net8.0",
        string stage = "migration") =>
        new SqlTestTimingEvent(
            runId,
            "test",
            id,
            parent,
            stage,
            start * Stopwatch.Frequency / 1000,
            end * Stopwatch.Frequency / 1000,
            end - start,
            SqlTestTimingOutcome.Succeeded,
            tfm,
            "db");
}
