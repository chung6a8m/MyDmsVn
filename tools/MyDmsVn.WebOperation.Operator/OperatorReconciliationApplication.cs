using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.WebOperation.Operator;

public static class OperatorReconciliationApplication
{
    public static async Task<int> RunAsync(string[] args, Func<ReconcileCommittedOptions, IOperatorReconciliationClient> clientFactory,
        TextWriter stdout, TextWriter stderr, CancellationToken token)
    {
        if (clientFactory == null) throw new ArgumentNullException(nameof(clientFactory));
        if (stdout == null) throw new ArgumentNullException(nameof(stdout));
        if (stderr == null) throw new ArgumentNullException(nameof(stderr));
        if (!ReconcileCommittedOptions.TryParse(args, out var options, out var error))
        { stderr.WriteLine(error); return (int)OperatorExitCode.InvalidArguments; }
        if (options!.HelpRequested)
        {
            stdout.WriteLine("Usage: reconcile-committed --environment <label> --database <name> --current-epoch <guid> --stored-epoch <guid> --actor <id> --operation <guid-v4> --fingerprint <sha256> --correlation <guid-v4> --case <id> [--timeout-seconds <1..25>]");
            stdout.WriteLine("Read-only support evidence; no browser settlement. Use lowercase GUIDs and SHA-256.");
            stdout.WriteLine("Dedicated connection: " + OperatorCompactionApplication.ConnectionStringEnvironmentVariable);
            return 0;
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            var result = await clientFactory(options).ReconcileAsync(options, deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            switch (result?.Outcome)
            {
                case WebOperationReconciliationOutcome.Committed:
                    stdout.WriteLine("status=BUSINESS_COMMITTED_RESULT_UNAVAILABLE"); return 0;
                case WebOperationReconciliationOutcome.Superseded:
                    stdout.WriteLine("status=BUSINESS_COMMITTED_RESULT_UNAVAILABLE_SUPERSEDED"); return 0;
                default:
                    stderr.WriteLine("status=UNKNOWN/ESCALATE"); return (int)OperatorExitCode.Unknown;
            }
        }
        catch (OperationCanceledException)
        { stderr.WriteLine("status=UNKNOWN/ESCALATE"); return (int)(token.IsCancellationRequested ? OperatorExitCode.Cancelled : OperatorExitCode.Timeout); }
        catch (Exception)
        { stderr.WriteLine("status=UNKNOWN/ESCALATE"); return (int)OperatorExitCode.Unknown; }
    }
}
