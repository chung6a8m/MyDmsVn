using System;

namespace MyDmsVn.SqlTestHarness.Preflight;

public sealed class SqlPreflightResult
{
    private SqlPreflightResult(
        SqlMutationAuthorization? authorization,
        SqlPreflightFailure? failure)
    {
        Authorization = authorization;
        Failure = failure;
    }

    public bool IsSuccess => Authorization is not null;

    public SqlMutationAuthorization? Authorization { get; }

    public SqlPreflightFailure? Failure { get; }

    public static SqlPreflightResult Success(SqlMutationAuthorization authorization) =>
        new SqlPreflightResult(
            authorization ?? throw new ArgumentNullException(nameof(authorization)),
            null);

    public static SqlPreflightResult Failed(SqlPreflightFailure failure) =>
        new SqlPreflightResult(
            null,
            failure ?? throw new ArgumentNullException(nameof(failure)));
}
