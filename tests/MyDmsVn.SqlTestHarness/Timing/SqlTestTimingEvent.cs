using System;

namespace MyDmsVn.SqlTestHarness.Timing;

public enum SqlTestTimingOutcome
{
    Succeeded,
    Failed,
    Canceled,
}

public sealed class SqlTestTimingEvent
{
    public SqlTestTimingEvent(
        string runId,
        string testCaseId,
        string operationId,
        string? parentOperationId,
        string stage,
        long startTimestamp,
        long endTimestamp,
        decimal durationMs,
        SqlTestTimingOutcome outcome,
        string tfm,
        string anonymizedDatabaseId)
    {
        RunId = Require(runId, nameof(runId));
        TestCaseId = Require(testCaseId, nameof(testCaseId));
        OperationId = Require(operationId, nameof(operationId));
        ParentOperationId = parentOperationId;
        Stage = Require(stage, nameof(stage));
        StartTimestamp = startTimestamp;
        EndTimestamp = endTimestamp;
        DurationMs = durationMs;
        Outcome = outcome;
        Tfm = Require(tfm, nameof(tfm));
        AnonymizedDatabaseId = Require(anonymizedDatabaseId, nameof(anonymizedDatabaseId));
    }

    public string RunId { get; }

    public string TestCaseId { get; }

    public string OperationId { get; }

    public string? ParentOperationId { get; }

    public string Stage { get; }

    public long StartTimestamp { get; }

    public long EndTimestamp { get; }

    public decimal DurationMs { get; }

    public SqlTestTimingOutcome Outcome { get; }

    public string Tfm { get; }

    public string AnonymizedDatabaseId { get; }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty timing value is required.", parameterName);
        }

        return value;
    }
}
