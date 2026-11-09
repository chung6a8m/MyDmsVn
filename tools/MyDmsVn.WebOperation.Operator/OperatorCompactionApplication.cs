using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;

namespace MyDmsVn.WebOperation.Operator;

public static class OperatorCompactionApplication
{
    public const string ConnectionStringEnvironmentVariable = "MYDMSVN_WEB_OPERATION_MAINTENANCE_CONNECTION_STRING";
    public const string DatabaseUserEnvironmentVariable = "MYDMSVN_WEB_OPERATION_MAINTENANCE_DATABASE_USER";
    public const string LoginEnvironmentVariable = "MYDMSVN_WEB_OPERATION_MAINTENANCE_LOGIN";
    public const string TargetIdentityEnvironmentVariable = "MYDMSVN_WEB_OPERATION_MAINTENANCE_TARGET_IDENTITY";
    public const string EvidenceRootEnvironmentVariable = "MYDMSVN_WEB_OPERATION_MAINTENANCE_EVIDENCE_ROOT";
    public const string EvidenceBackupRootEnvironmentVariable = "MYDMSVN_WEB_OPERATION_MAINTENANCE_EVIDENCE_BACKUP_ROOT";
    private const string TargetUsage = " --environment <label> --database <name> --current-epoch <guid> --stored-epoch <guid> --actor <id> --operation <guid-v4> --fingerprint <sha256> --correlation <guid-v4> --case <id> [--timeout-seconds <1..25>]";

    public static async Task<int> RunAsync(string[] args, Func<CompactOptions, IOperatorCompactionClient> clientFactory,
        TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        if (clientFactory == null) throw new ArgumentNullException(nameof(clientFactory));
        if (stdout == null) throw new ArgumentNullException(nameof(stdout));
        if (stderr == null) throw new ArgumentNullException(nameof(stderr));
        if (!CompactOptions.TryParse(args, out var options, out var error))
        {
            stderr.WriteLine(error);
            return (int)OperatorExitCode.InvalidArguments;
        }
        if (options!.HelpRequested)
        {
            stdout.WriteLine("Usage: " + options.Command + TargetUsage +
                (options.Execute ? " --confirm <exact-composite>" : string.Empty));
            stdout.WriteLine("Use lowercase GUIDs and SHA-256.");
            stdout.WriteLine("Dedicated connection: " + ConnectionStringEnvironmentVariable);
            stdout.WriteLine("Expected identity: " + DatabaseUserEnvironmentVariable + " and " + LoginEnvironmentVariable);
            stdout.WriteLine("Expected owned target: " + TargetIdentityEnvironmentVariable);
            return (int)OperatorExitCode.Completed;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var client = clientFactory(options);
            var result = options.Execute
                ? await client.ExecuteAsync(options, linked.Token).ConfigureAwait(false)
                : await client.PreviewAsync(options, linked.Token).ConfigureAwait(false);
            // A cancellation observed after execution cannot erase known Commit or audit-delivery status.
            if (!options.Execute) linked.Token.ThrowIfCancellationRequested();
            if (options.Execute)
                return result?.Outcome switch
                {
                    OperatorCompactionOutcome.Completed => WriteStatus(stdout, "COMPLETED", OperatorExitCode.Completed),
                    OperatorCompactionOutcome.NoChange => WriteStatus(stdout, "NO_CHANGE", OperatorExitCode.Preview),
                    OperatorCompactionOutcome.AuditDeliveryFailed => WriteStatus(stderr,
                        "COMMITTED/AUDIT_DELIVERY_FAILED/ESCALATE; inspect independently", OperatorExitCode.AuditDeliveryFailed),
                    _ => WriteStatus(stderr, "UNKNOWN/ESCALATE", OperatorExitCode.Unknown)
                };
            return result?.Outcome switch
            {
                OperatorCompactionOutcome.Eligible => WriteStatus(stdout, "ELIGIBLE", OperatorExitCode.Preview),
                OperatorCompactionOutcome.NoChange => WriteStatus(stdout, "NO_CHANGE", OperatorExitCode.Preview),
                _ => WriteStatus(stderr, "UNKNOWN/ESCALATE", OperatorExitCode.Unknown)
            };
        }
        catch (OperationCanceledException)
        {
            return WriteStatus(stderr, "UNKNOWN/ESCALATE",
                cancellationToken.IsCancellationRequested ? OperatorExitCode.Cancelled : OperatorExitCode.Timeout);
        }
        catch (TimeoutException)
        {
            return WriteStatus(stderr, "UNKNOWN/ESCALATE", OperatorExitCode.Timeout);
        }
        catch (SqlException exception) when (exception.Number == -2)
        {
            return WriteStatus(stderr, "UNKNOWN/ESCALATE", OperatorExitCode.Timeout);
        }
        catch (WebOperationException exception) when (exception.Code == "Auth.Forbidden" ||
            exception.Code == "Deployment.EpochMismatch" || exception.Code == "Operation.ResolutionConflict")
        {
            return WriteStatus(stderr, "DENIED", OperatorExitCode.Denied);
        }
        catch (Exception)
        {
            return WriteStatus(stderr, "UNKNOWN/ESCALATE", OperatorExitCode.EnvironmentFailure);
        }
    }

    private static int WriteStatus(TextWriter output, string label, OperatorExitCode exitCode)
    {
        output.WriteLine("status=" + label);
        return (int)exitCode;
    }
}
