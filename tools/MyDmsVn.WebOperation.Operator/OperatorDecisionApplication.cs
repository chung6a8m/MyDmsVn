using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;

namespace MyDmsVn.WebOperation.Operator;

public static class OperatorDecisionApplication
{
    public const string Usage = "Usage: record-not-executed --dry-run|--execute --environment <label> --database <name> --epoch <guid> --actor <id> --operation <guid-v4> --kind <1..6> --target <NONE|positive-id> --fingerprint <sha256> --case <id> --reason <code> --evidence <reference> --decision <guid-v4> [--confirm <exact-composite>] [--timeout-seconds <1..25>]";
    public static async Task<int> RunAsync(string[] args, Func<RecordNotExecutedOptions, IOperatorDecisionClient> clientFactory,
        TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        if (clientFactory == null) throw new ArgumentNullException(nameof(clientFactory));
        if (stdout == null) throw new ArgumentNullException(nameof(stdout));
        if (stderr == null) throw new ArgumentNullException(nameof(stderr));
        if (!RecordNotExecutedOptions.TryParse(args, out var options, out var error))
        {
            stderr.WriteLine(error); stderr.WriteLine(Usage); return (int)OperatorExitCode.InvalidArguments;
        }
        if (options!.HelpRequested)
        {
            stdout.WriteLine(Usage);
            stdout.WriteLine("Dry-run is read-only and never proves NOT_EXECUTED. Execute requires exact typed confirmation.");
            return (int)OperatorExitCode.Completed;
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var client = clientFactory(options);
            var result = options.DryRun ? await client.DryRunAsync(options, linked.Token).ConfigureAwait(false)
                : await client.ExecuteAsync(options, linked.Token).ConfigureAwait(false);
            if (result == null) throw new WebOperationException("Operation.Unavailable");
            if (result.Outcome == OperatorDecisionOutcome.Unknown)
            {
                WriteUnknown(stderr); return (int)OperatorExitCode.Unknown;
            }
            if (options.DryRun && result.Outcome == OperatorDecisionOutcome.Preview && result.Inspection?.Disposition == "INDETERMINATE")
            {
                stdout.WriteLine("status=PREVIEW"); stdout.WriteLine("durableDisposition=INDETERMINATE");
                stdout.WriteLine("notProofOfNotExecuted=true"); stdout.WriteLine("environmentLabel=" + options.EnvironmentName);
                stdout.WriteLine("environmentLabelSource=operator-supplied"); stdout.WriteLine("database=" + options.DatabaseName);
                return (int)OperatorExitCode.Preview;
            }
            if (options.Execute && result.Outcome == OperatorDecisionOutcome.Completed && result.Inspection?.Disposition == "CANCELLED" &&
                result.Inspection.Terminal?.DecisionId == options.DecisionId && result.Inspection.Terminal.CaseId == options.CaseId)
            {
                stdout.WriteLine("status=CANCELLED"); stdout.WriteLine("postCommitVerification=matched");
                stdout.WriteLine("environmentLabel=" + options.EnvironmentName); stdout.WriteLine("environmentLabelSource=operator-supplied");
                stdout.WriteLine("database=" + options.DatabaseName); stdout.WriteLine("operation=" + options.OperationId.ToString("D"));
                stdout.WriteLine("decision=" + options.DecisionId.ToString("D")); return (int)OperatorExitCode.Completed;
            }
            throw new WebOperationException("Operation.Unavailable");
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                stderr.WriteLine("Operator.Cancelled: terminal decision stopped before a known Commit; re-inspect before any further action.");
                return (int)OperatorExitCode.Cancelled;
            }
            stderr.WriteLine("Operator.Timeout: terminal decision exceeded the bounded timeout; SQL outcome is unknown.");
            return (int)OperatorExitCode.Timeout;
        }
        catch (TimeoutException) { stderr.WriteLine("Operator.Timeout: terminal decision exceeded the bounded timeout; SQL outcome is unknown."); return (int)OperatorExitCode.Timeout; }
        catch (SqlException exception) when (exception.Number == -2) { stderr.WriteLine("Operator.Timeout: terminal decision exceeded the bounded timeout; SQL outcome is unknown."); return (int)OperatorExitCode.Timeout; }
        catch (WebOperationException exception) when (exception.Code == "Auth.Forbidden" || exception.Code == "Deployment.EpochMismatch" ||
            exception.Code == "Idempotency.FingerprintMismatch" || exception.Code == "Operation.ResolutionConflict")
        {
            stderr.WriteLine("Operator.Denied: authority, durable disposition, or exact operation identity was not accepted.");
            return (int)OperatorExitCode.Denied;
        }
        catch (Exception) { stderr.WriteLine("Operator.EnvironmentFailure: terminal decision was unavailable; re-inspect before any further action."); return (int)OperatorExitCode.EnvironmentFailure; }
    }
    private static void WriteUnknown(TextWriter stderr)
    {
        stderr.WriteLine("Operator.UNKNOWN/ESCALATE: Commit or post-Commit verification is uncertain; never execute again automatically.");
        stderr.WriteLine("Run an independent inspect command with the same database, epoch, actor, operation, and fingerprint.");
    }
}
