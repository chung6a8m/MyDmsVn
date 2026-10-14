namespace MyDmsVn.SqlTestHarness.Timing;

public interface ISqlTestInvocationSink
{
    void WriteInvocation(SqlTestMigrationInvocation invocation);
}

public sealed class SqlTestMigrationInvocation
{
    public SqlTestMigrationInvocation(
        string runId,
        string testCaseId,
        string invocationId,
        string tfm,
        string anonymizedDatabaseId)
    {
        RunId = runId;
        TestCaseId = testCaseId;
        InvocationId = invocationId;
        Tfm = tfm;
        AnonymizedDatabaseId = anonymizedDatabaseId;
    }

    public string RecordType => "migration-invocation";

    public string RunId { get; }

    public string TestCaseId { get; }

    public string InvocationId { get; }

    public string Tfm { get; }

    public string AnonymizedDatabaseId { get; }
}
