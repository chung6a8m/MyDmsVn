using System;

namespace MyDmsVn.SqlTestHarness.Timing;

public sealed class SqlTestMeasurementContext
{
    private SqlTestMeasurementContext(
        string runId,
        string testCaseId,
        string tfm,
        string anonymizedDatabaseId,
        ISqlTestTimingSink sink,
        bool enabled)
    {
        RunId = runId;
        TestCaseId = testCaseId;
        Tfm = tfm;
        AnonymizedDatabaseId = anonymizedDatabaseId;
        Sink = sink;
        Enabled = enabled;
    }

    public string RunId { get; }

    public string TestCaseId { get; }

    public string Tfm { get; }

    public string AnonymizedDatabaseId { get; }

    internal ISqlTestTimingSink Sink { get; }

    internal bool Enabled { get; }

    public static SqlTestMeasurementContext Create(
        string runId,
        string testCaseId,
        string tfm,
        string anonymizedDatabaseId,
        ISqlTestTimingSink sink,
        bool? enabled = null) =>
        new SqlTestMeasurementContext(
            Require(runId, nameof(runId)),
            Require(testCaseId, nameof(testCaseId)),
            Require(tfm, nameof(tfm)),
            Require(anonymizedDatabaseId, nameof(anonymizedDatabaseId)),
            sink ?? throw new ArgumentNullException(nameof(sink)),
            enabled ?? string.Equals(
                Environment.GetEnvironmentVariable("MYDMSVN_SQL_TEST_TIMINGS"),
                "1",
                StringComparison.Ordinal));

    public SqlTestOperationScope StartOperation(
        string stage,
        string operationId,
        string? parentOperationId = null) =>
        new SqlTestOperationScope(
            this,
            Require(stage, nameof(stage)),
            Require(operationId, nameof(operationId)),
            parentOperationId);

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty measurement value is required.", parameterName);
        }

        return value;
    }
}
