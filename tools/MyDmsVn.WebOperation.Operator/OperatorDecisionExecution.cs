using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.WebOperation.Operator;

internal enum OperatorDecisionCommitOutcome { Committed, Unknown }

internal static class OperatorDecisionExecution
{
    internal static async Task<OperatorDecisionResult> RunAsync(RecordNotExecutedOptions options,
        Func<CancellationToken, Task<WebOperationInspection>> inspect,
        Func<CancellationToken, Task<OperatorDecisionCommitOutcome>> executeTransaction,
        CancellationToken cancellationToken)
    {
        var preview = await inspect(cancellationToken).ConfigureAwait(false);
        if (preview.Disposition != "INDETERMINATE")
            throw new WebOperationException("Operation.ResolutionConflict");

        if (await executeTransaction(cancellationToken).ConfigureAwait(false) == OperatorDecisionCommitOutcome.Unknown)
            return OperatorDecisionResult.Unknown();

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
    internal static async Task<OperatorDecisionCommitOutcome> RunAsync(Func<CancellationToken, Task> mutate,
        Action commit, Action rollback, Action close, Action disposeTransaction, Action disposeConnection,
        CancellationToken cancellationToken)
    {
        try
        {
            await mutate(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            BestEffort(rollback); BestEffort(close); BestEffort(disposeTransaction); BestEffort(disposeConnection);
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("The guarded SQL decision transaction was cancelled before Commit.", exception, cancellationToken);
            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }

        try
        {
            commit();
        }
        catch
        {
            // Commit certainty is now lost. Cleanup is still attempted, but can no longer replace UNKNOWN.
            BestEffort(close); BestEffort(disposeTransaction); BestEffort(disposeConnection);
            return OperatorDecisionCommitOutcome.Unknown;
        }

        // Commit returned successfully. Cleanup cannot turn a known Commit into success without readback,
        // nor can it replace the post-Commit verification phase with an environment error.
        BestEffort(disposeTransaction); BestEffort(close); BestEffort(disposeConnection);
        return OperatorDecisionCommitOutcome.Committed;
    }

    private static void BestEffort(Action cleanup)
    {
        try { cleanup(); }
        catch { /* The mutation phase already owns the authoritative outcome. */ }
    }
}
