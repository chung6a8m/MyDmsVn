using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.WebOperation.Operator;

internal enum OperatorDecisionCommitOutcome { Committed, Unknown }

[Flags]
internal enum OperatorDecisionCleanupFailure
{
    None = 0, Rollback = 1, Close = 2, TransactionDispose = 4, ConnectionDispose = 8
}

internal enum OperatorDecisionTransactionUnknownReason { None, PreCommitCleanupUnverified, CommitUnknown }

internal sealed class OperatorDecisionTransactionResult
{
    private OperatorDecisionTransactionResult(OperatorDecisionCommitOutcome outcome, Exception? primaryFailure,
        OperatorDecisionCleanupFailure cleanupFailures, OperatorDecisionTransactionUnknownReason unknownReason)
    {
        Outcome = outcome; PrimaryFailure = primaryFailure; CleanupFailures = cleanupFailures; UnknownReason = unknownReason;
    }
    internal OperatorDecisionCommitOutcome Outcome { get; }
    internal Exception? PrimaryFailure { get; }
    internal OperatorDecisionCleanupFailure CleanupFailures { get; }
    internal OperatorDecisionTransactionUnknownReason UnknownReason { get; }
    internal static OperatorDecisionTransactionResult Committed(OperatorDecisionCleanupFailure cleanupFailures = OperatorDecisionCleanupFailure.None) =>
        new OperatorDecisionTransactionResult(OperatorDecisionCommitOutcome.Committed, null, cleanupFailures, OperatorDecisionTransactionUnknownReason.None);
    internal static OperatorDecisionTransactionResult Unknown(Exception? primaryFailure = null,
        OperatorDecisionCleanupFailure cleanupFailures = OperatorDecisionCleanupFailure.None,
        OperatorDecisionTransactionUnknownReason reason = OperatorDecisionTransactionUnknownReason.CommitUnknown) =>
        new OperatorDecisionTransactionResult(OperatorDecisionCommitOutcome.Unknown, primaryFailure, cleanupFailures, reason);
}

internal static class OperatorDecisionExecution
{
    internal static async Task<OperatorDecisionResult> RunAsync(RecordNotExecutedOptions options,
        Func<CancellationToken, Task<WebOperationInspection>> inspect,
        Func<CancellationToken, Task<OperatorDecisionTransactionResult>> executeTransaction,
        CancellationToken cancellationToken)
    {
        var preview = await inspect(cancellationToken).ConfigureAwait(false);
        if (preview.Disposition != "INDETERMINATE")
            throw new WebOperationException("Operation.ResolutionConflict");

        var transaction = await executeTransaction(cancellationToken).ConfigureAwait(false);
        if (transaction.Outcome == OperatorDecisionCommitOutcome.Unknown)
            return OperatorDecisionResult.Unknown(transaction.UnknownReason == OperatorDecisionTransactionUnknownReason.PreCommitCleanupUnverified
                ? OperatorDecisionUnknownReason.PreCommitCleanupUnverified : OperatorDecisionUnknownReason.CommitOrReadback);

        try
        {
            var verified = await inspect(cancellationToken).ConfigureAwait(false);
            if (verified.DeploymentEpoch != options.DeploymentEpoch || verified.ActorUserId != options.ActorUserId ||
                verified.OperationId != options.OperationId || verified.Disposition != "CANCELLED" ||
                verified.Kind != options.Kind || verified.RequestTargetId != options.RequestTargetId ||
                verified.Terminal?.DecisionId != options.DecisionId || verified.Terminal.CaseId != options.CaseId)
                return OperatorDecisionResult.Unknown();
            return OperatorDecisionResult.Completed(verified);
        }
        catch
        {
            return OperatorDecisionResult.Unknown();
        }
    }
}

internal static class OperatorDecisionCommitBoundary
{
    internal static async Task<OperatorDecisionTransactionResult> RunAsync(Func<CancellationToken, Task> mutate,
        Action commit, Action rollback, Action close, Action disposeTransaction, Action disposeConnection,
        CancellationToken cancellationToken)
    {
        try
        {
            await mutate(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception exception)
        {
            var cleanupFailures = CleanupPreCommit(rollback, close, disposeTransaction, disposeConnection);
            if (cleanupFailures != OperatorDecisionCleanupFailure.None)
                return OperatorDecisionTransactionResult.Unknown(exception, cleanupFailures,
                    OperatorDecisionTransactionUnknownReason.PreCommitCleanupUnverified);
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("The guarded SQL decision transaction was cancelled before Commit.", exception, cancellationToken);
            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }

        try
        {
            commit();
        }
        catch (Exception exception)
        {
            // Commit certainty is now lost. Cleanup is still attempted, but can no longer replace UNKNOWN.
            var cleanupFailures = CleanupAfterCommit(close, disposeTransaction, disposeConnection);
            return OperatorDecisionTransactionResult.Unknown(exception, cleanupFailures,
                OperatorDecisionTransactionUnknownReason.CommitUnknown);
        }

        // Commit returned successfully. Cleanup cannot turn a known Commit into success without readback,
        // nor can it replace the post-Commit verification phase with an environment error.
        var committedCleanupFailures = CleanupAfterCommit(close, disposeTransaction, disposeConnection);
        return OperatorDecisionTransactionResult.Committed(committedCleanupFailures);
    }

    private static OperatorDecisionCleanupFailure CleanupPreCommit(Action rollback, Action close,
        Action disposeTransaction, Action disposeConnection)
    {
        var failures = TryCleanup(rollback, OperatorDecisionCleanupFailure.Rollback);
        failures |= TryCleanup(close, OperatorDecisionCleanupFailure.Close);
        failures |= TryCleanup(disposeTransaction, OperatorDecisionCleanupFailure.TransactionDispose);
        failures |= TryCleanup(disposeConnection, OperatorDecisionCleanupFailure.ConnectionDispose);
        return failures;
    }

    private static OperatorDecisionCleanupFailure CleanupAfterCommit(Action close, Action disposeTransaction, Action disposeConnection)
    {
        var failures = TryCleanup(close, OperatorDecisionCleanupFailure.Close);
        failures |= TryCleanup(disposeTransaction, OperatorDecisionCleanupFailure.TransactionDispose);
        failures |= TryCleanup(disposeConnection, OperatorDecisionCleanupFailure.ConnectionDispose);
        return failures;
    }

    private static OperatorDecisionCleanupFailure TryCleanup(Action cleanup, OperatorDecisionCleanupFailure failure)
    {
        try { cleanup(); return OperatorDecisionCleanupFailure.None; }
        catch { return failure; }
    }
}
