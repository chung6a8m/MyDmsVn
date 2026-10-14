namespace MyDmsVn.SqlTestHarness.Ownership;

public enum DatabaseRecoveryClassification
{
    Absent,
    PositivelyOwned,
    Ambiguous,
    Unowned,
    ActiveOtherRun,
}

public sealed class DatabaseRecoveryResult
{
    public DatabaseRecoveryResult(
        DatabaseRecoveryClassification classification,
        string reason)
    {
        Classification = classification;
        Reason = reason;
    }

    public DatabaseRecoveryClassification Classification { get; }

    public string Reason { get; }

    public bool MayAutomaticallyClean =>
        Classification == DatabaseRecoveryClassification.PositivelyOwned;
}
