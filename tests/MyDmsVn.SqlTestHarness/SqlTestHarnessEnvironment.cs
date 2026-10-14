namespace MyDmsVn.SqlTestHarness;

public static class SqlTestHarnessEnvironment
{
    public const string ConnectionStringVariable =
        "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING";

    public const string ApprovalFileVariable =
        "MYDMSVN_SQL_TEST_APPROVAL_FILE";

    public const string JournalDirectoryVariable =
        "MYDMSVN_SQL_TEST_JOURNAL_DIRECTORY";

    public const string RunIdVariable =
        "MYDMSVN_SQL_TEST_RUN_ID";

    public const string TimingEnabledVariable =
        "MYDMSVN_SQL_TEST_TIMINGS";

    public const string TimingOutputVariable =
        "MYDMSVN_SQL_TEST_TIMING_OUTPUT";
}
