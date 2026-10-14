using System;
using MyDmsVn.SqlTestHarness.Timing;
using Xunit;

namespace MyDmsVn.SqlTestHarness.Tests.Timing;

public sealed class TimingIntervalReconcilerTests
{
    [Fact]
    public void Repeated_logical_operation_ids_in_different_tests_remain_distinct_instances()
    {
        var events = new[]
        {
            Event("migration", parent: null, start: 0, end: 40, testCaseId: "Tests.First"),
            Event("migration", parent: null, start: 50, end: 80, testCaseId: "Tests.Second"),
        };

        var result = TimingIntervalReconciler.Reconcile(
            TimeSpan.FromMilliseconds(100),
            events,
            timestampFrequency: 1000);

        Assert.Equal(70m, result.MeasuredUnionMs);
        Assert.Equal(70m, result.GetExclusiveDurationMs("migration"));
    }

    [Fact]
    public void Nested_intervals_are_subtracted_once_and_residual_uses_union_time()
    {
        var events = new[]
        {
            Event("parent", parent: null, start: 0, end: 100),
            Event("child", parent: "parent", start: 20, end: 50),
        };

        var result = TimingIntervalReconciler.Reconcile(
            TimeSpan.FromMilliseconds(150),
            events,
            timestampFrequency: 1000);

        Assert.Equal(100m, result.MeasuredUnionMs);
        Assert.Equal(0m, result.OverlapMs);
        Assert.Equal(50m, result.ResidualMs);
        Assert.Equal(70m, result.GetExclusiveDurationMs("parent"));
        Assert.Equal(30m, result.GetExclusiveDurationMs("child"));
    }

    [Fact]
    public void Overlapping_siblings_are_labeled_instead_of_added_to_elapsed_time()
    {
        var events = new[]
        {
            Event("parent", parent: null, start: 0, end: 100),
            Event("child-a", parent: "parent", start: 20, end: 70),
            Event("child-b", parent: "parent", start: 50, end: 90),
        };

        var result = TimingIntervalReconciler.Reconcile(
            TimeSpan.FromMilliseconds(100),
            events,
            timestampFrequency: 1000);

        Assert.Equal(100m, result.MeasuredUnionMs);
        Assert.Equal(20m, result.OverlapMs);
        Assert.Equal(0m, result.ResidualMs);
        Assert.Equal(30m, result.GetExclusiveDurationMs("parent"));
    }

    [Fact]
    public void Negative_sampling_residual_is_preserved_and_labeled()
    {
        var result = TimingIntervalReconciler.Reconcile(
            TimeSpan.FromMilliseconds(90),
            new[] { Event("operation", parent: null, start: 0, end: 100) },
            timestampFrequency: 1000);

        Assert.Equal(-10m, result.ResidualMs);
        Assert.True(result.HasNegativeResidual);
    }

    private static SqlTestTimingEvent Event(
        string operation,
        string? parent,
        long start,
        long end,
        string testCaseId = "Tests.Class.Method",
        string stage = "migration") =>
        new SqlTestTimingEvent(
            "run-1",
            testCaseId,
            operation,
            parent,
            stage,
            start,
            end,
            end - start,
            SqlTestTimingOutcome.Succeeded,
            "net8.0",
            "db-opaque");
}
