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
            return (int)OperatorExitCode.Completed;
        }
        if (options.Execute)
            return WriteStatus(stderr, "DENIED", OperatorExitCode.Denied);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var client = clientFactory(options);
            var result = await client.PreviewAsync(options, linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
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
