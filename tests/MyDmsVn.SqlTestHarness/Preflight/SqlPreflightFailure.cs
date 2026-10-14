using System;

namespace MyDmsVn.SqlTestHarness.Preflight;

public enum SqlPreflightFailureKind
{
    InvalidConfiguration,
    ApprovalExpired,
    ConnectionFailure,
    WrongEffectiveDatabase,
    IdentityUnavailable,
    ApprovalMismatch,
    VersionUnavailable,
    MissingPermission,
}

public sealed class SqlPreflightFailure
{
    public SqlPreflightFailure(SqlPreflightFailureKind kind, string message, Exception? exception = null)
    {
        Kind = kind;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Exception = exception;
    }

    public SqlPreflightFailureKind Kind { get; }

    public string Message { get; }

    public Exception? Exception { get; }
}
