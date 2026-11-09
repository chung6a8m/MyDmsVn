using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;

namespace MyDmsVn.WebOperation.Operator;

[Flags]
internal enum CompactionCleanupFailure { None = 0, Rollback = 1, Close = 2, TransactionDispose = 4, ConnectionDispose = 8 }

internal sealed class CompactionTransactionResult(MaintenanceTransactionCertainty certainty,
    Exception? primaryFailure, CompactionCleanupFailure cleanupFailures, bool commitStarted = false)
{
    internal bool CommitStarted { get; } = commitStarted;
    internal MaintenanceTransactionCertainty Certainty { get; } = certainty;
    internal Exception? PrimaryFailure { get; } = primaryFailure;
    internal CompactionCleanupFailure CleanupFailures { get; } = cleanupFailures;
}

internal static class OperatorCompactionExecution
{
    internal static async Task<OperatorCompactionResult> RunAsync(CompactOptions options, IMaintenanceEvidenceStore evidence,
        Func<CancellationToken, Task<OperatorCompactionResult>> preview,
        Func<CancellationToken, Task<string>> readLogin,
        Func<MaintenanceEvidenceReceipt, string, CancellationToken, Task<CompactionTransactionResult>> execute,
        Func<CancellationToken, Task<bool>> readback, CancellationToken cancellationToken)
    {
        if (options == null || options.HelpRequested || !options.Execute)
            throw new ArgumentException("Exact execute options are required.", nameof(options));
        var candidate = await preview(cancellationToken).ConfigureAwait(false);
        if (candidate.Outcome == OperatorCompactionOutcome.NoChange) return candidate;
        if (candidate.Outcome != OperatorCompactionOutcome.Eligible)
            return new OperatorCompactionResult(OperatorCompactionOutcome.Unknown);
        var login = await readLogin(cancellationToken).ConfigureAwait(false);
        MaintenanceEvidenceReceipt attempt;
        try
        {
            attempt = await evidence.WriteAttemptAsync(Record(MaintenanceEvidencePhase.Attempt,
                MaintenanceTransactionCertainty.NotStarted), cancellationToken).ConfigureAwait(false);
            if (!ValidEvent(attempt, options.CaseId)) return new OperatorCompactionResult(OperatorCompactionOutcome.Unknown);
        }
        catch { return new OperatorCompactionResult(OperatorCompactionOutcome.Unknown); }

        CompactionTransactionResult transaction;
        try { transaction = await execute(attempt, login, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception)
        {
            // The execute seam owns provider cleanup and must report its certainty.
            // An unclassified exception cannot prove rollback or absence of Commit.
            transaction = new CompactionTransactionResult(MaintenanceTransactionCertainty.Unknown, exception, CompactionCleanupFailure.None);
        }
        var committed = transaction.Certainty == MaintenanceTransactionCertainty.Committed;
        var matched = false;
        if (committed)
        {
            try { matched = await readback(cancellationToken).ConfigureAwait(false); }
            catch { /* A known Commit remains known, but readback is unverified. */ }
        }

        var delivered = true;
        try
        {
            delivered = ValidEvent(await evidence.WriteOutcomeAsync(Record(MaintenanceEvidencePhase.Transaction,
                transaction.Certainty), cancellationToken).ConfigureAwait(false), options.CaseId);
            if (committed && delivered)
                delivered = ValidEvent(await evidence.WriteOutcomeAsync(Record(MaintenanceEvidencePhase.Readback,
                    transaction.Certainty, matched ? MaintenanceReadback.Verified : MaintenanceReadback.Unknown),
                    cancellationToken).ConfigureAwait(false), options.CaseId);
        }
        catch { delivered = false; }
        // Preserve any durable partial record even if a later outcome write failed.
        try
        {
            var final = await evidence.FinalizeAndBackupAsync(options.CaseId, cancellationToken).ConfigureAwait(false);
            delivered &= final != null && final.CaseId == options.CaseId && final.Delivery == MaintenanceEvidenceDelivery.Delivered &&
                MaintenanceEvidenceRecord.IsHash(final.PrimaryManifestHash) && MaintenanceEvidenceRecord.IsHash(final.BackupManifestHash);
        }
        catch { delivered = false; }
        if (committed)
            return new OperatorCompactionResult(!delivered ? OperatorCompactionOutcome.AuditDeliveryFailed :
                matched ? OperatorCompactionOutcome.Completed : OperatorCompactionOutcome.Unknown);
        if (transaction.Certainty == MaintenanceTransactionCertainty.Unknown || !delivered)
            return new OperatorCompactionResult(OperatorCompactionOutcome.Unknown);
        if (transaction.PrimaryFailure != null) ExceptionDispatchInfo.Capture(transaction.PrimaryFailure).Throw();
        return new OperatorCompactionResult(OperatorCompactionOutcome.Unknown);

        MaintenanceEvidenceRecord Record(MaintenanceEvidencePhase phase, MaintenanceTransactionCertainty certainty,
            MaintenanceReadback observed = MaintenanceReadback.NotRun) =>
            MaintenanceEvidenceRecord.FromCompaction(options, login, phase, certainty, observed);
    }

    internal static bool ValidEvent(MaintenanceEvidenceReceipt? receipt, string caseId) =>
        receipt != null && receipt.CaseId == caseId && receipt.Delivery == MaintenanceEvidenceDelivery.PrimaryOnly &&
        MaintenanceEvidenceRecord.IsHash(receipt.EventHash);
}

internal static class OperatorCompactionCommitBoundary
{
    internal static async Task<CompactionTransactionResult> RunAsync(Func<CancellationToken, Task<int>> mutate,
        Action commit, Action rollback, Action close, Action disposeTransaction, Action disposeConnection,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await mutate(cancellationToken).ConfigureAwait(false) != 1)
                throw new WebOperationException("Operation.ResolutionConflict");
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception exception)
        {
            var failures = Cleanup(rollback, CompactionCleanupFailure.Rollback) | AfterCommit();
            return new CompactionTransactionResult(failures == CompactionCleanupFailure.None
                ? MaintenanceTransactionCertainty.RolledBack : MaintenanceTransactionCertainty.Unknown, exception, failures);
        }
        try { commit(); }
        catch (Exception exception)
        {
            return new CompactionTransactionResult(MaintenanceTransactionCertainty.Unknown, exception, AfterCommit(), commitStarted: true);
        }
        return new CompactionTransactionResult(MaintenanceTransactionCertainty.Committed, null, AfterCommit(), commitStarted: true);

        CompactionCleanupFailure AfterCommit() => Cleanup(close, CompactionCleanupFailure.Close) |
            Cleanup(disposeTransaction, CompactionCleanupFailure.TransactionDispose) |
            Cleanup(disposeConnection, CompactionCleanupFailure.ConnectionDispose);
    }

    internal static CompactionCleanupFailure Cleanup(Action action, CompactionCleanupFailure failure)
    {
        try { action(); return CompactionCleanupFailure.None; }
        catch { return failure; }
    }
}
