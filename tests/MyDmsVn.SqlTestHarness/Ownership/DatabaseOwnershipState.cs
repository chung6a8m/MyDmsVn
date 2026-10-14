namespace MyDmsVn.SqlTestHarness.Ownership;

public enum DatabaseOwnershipState
{
    CreateIntent,
    Created,
    DropIntent,
    Disposed,
    CleanupFailed,
    Quarantined,
}
