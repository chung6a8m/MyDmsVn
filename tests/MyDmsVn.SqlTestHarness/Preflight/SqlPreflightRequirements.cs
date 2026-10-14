namespace MyDmsVn.SqlTestHarness.Preflight;

public sealed class SqlPreflightRequirements
{
    private SqlPreflightRequirements(bool requireCrossSessionDmvPermission)
    {
        RequireCrossSessionDmvPermission = requireCrossSessionDmvPermission;
    }

    public static SqlPreflightRequirements Mutation { get; } =
        new SqlPreflightRequirements(requireCrossSessionDmvPermission: true);

    public bool RequireCrossSessionDmvPermission { get; }
}
