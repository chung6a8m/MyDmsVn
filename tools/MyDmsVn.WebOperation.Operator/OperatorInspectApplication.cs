using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;

namespace MyDmsVn.WebOperation.Operator;

public static class OperatorInspectApplication
{
    public const string ConnectionStringEnvironmentVariable = "MYDMSVN_WEB_OPERATION_OPERATOR_CONNECTION_STRING";
    public const string DatabaseUserEnvironmentVariable = "MYDMSVN_WEB_OPERATION_OPERATOR_DATABASE_USER";
    public const string LoginEnvironmentVariable = "MYDMSVN_WEB_OPERATION_OPERATOR_LOGIN";
    public const string Usage = "Usage: inspect --environment <label> --database <name> --epoch <guid> --actor <id> --operation <guid-v4> --fingerprint <sha256> [--timeout-seconds <1..25>]";

    public static async Task<int> RunAsync(string[] args, Func<InspectOptions, IOperatorInspectionClient> clientFactory,
        TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        if (clientFactory == null) throw new ArgumentNullException(nameof(clientFactory));
        if (stdout == null) throw new ArgumentNullException(nameof(stdout));
        if (stderr == null) throw new ArgumentNullException(nameof(stderr));
        if (!InspectOptions.TryParse(args, out var options, out var error))
        {
            stderr.WriteLine(error);
            stderr.WriteLine(Usage);
            return (int)OperatorExitCode.InvalidArguments;
        }
        if (options!.HelpRequested)
        {
            stdout.WriteLine(Usage);
            stdout.WriteLine("Connection source: " + ConnectionStringEnvironmentVariable + " (never printed). Inspect is read-only.");
            stdout.WriteLine("Expected restricted identity: " + DatabaseUserEnvironmentVariable + " and " + LoginEnvironmentVariable + ".");
            return (int)OperatorExitCode.Completed;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var inspection = await clientFactory(options).InspectAsync(options, linked.Token).ConfigureAwait(false);
            if (inspection == null ||
                (inspection.Disposition != "COMMITTED" && inspection.Disposition != "EXPIRED" &&
                 inspection.Disposition != "CANCELLED" && inspection.Disposition != "INDETERMINATE"))
                throw new WebOperationException("Operation.Unavailable");
            WriteInspection(stdout, options, inspection);
            return inspection.Disposition == "INDETERMINATE"
                ? (int)OperatorExitCode.Indeterminate
                : (int)OperatorExitCode.Completed;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                stderr.WriteLine("Operator.Cancelled: inspection was cancelled; no state change was requested.");
                return (int)OperatorExitCode.Cancelled;
            }
            stderr.WriteLine("Operator.Timeout: inspection did not complete within the bounded timeout; outcome is unknown.");
            return (int)OperatorExitCode.Timeout;
        }
        catch (TimeoutException)
        {
            stderr.WriteLine("Operator.Timeout: inspection did not complete within the bounded timeout; outcome is unknown.");
            return (int)OperatorExitCode.Timeout;
        }
        catch (SqlException exception) when (exception.Number == -2)
        {
            stderr.WriteLine("Operator.Timeout: inspection did not complete within the bounded timeout; outcome is unknown.");
            return (int)OperatorExitCode.Timeout;
        }
        catch (WebOperationException exception) when (exception.Code == "Auth.Forbidden" ||
            exception.Code == "Deployment.EpochMismatch" || exception.Code == "Idempotency.FingerprintMismatch")
        {
            stderr.WriteLine("Operator.Denied: authority or exact operation identity was not accepted.");
            return (int)OperatorExitCode.Denied;
        }
        catch (Exception)
        {
            stderr.WriteLine("Operator.EnvironmentFailure: inspection was unavailable; no state change was requested.");
            return (int)OperatorExitCode.EnvironmentFailure;
        }
    }

    private static void WriteInspection(TextWriter output, InspectOptions options, WebOperationInspection inspection)
    {
        output.WriteLine("status=" + inspection.Disposition);
        output.WriteLine("environmentLabel=" + options.EnvironmentName);
        output.WriteLine("environmentLabelSource=operator-supplied");
        output.WriteLine("database=" + options.DatabaseName);
        output.WriteLine("epoch=" + inspection.DeploymentEpoch.ToString("D"));
        output.WriteLine("actor=" + inspection.ActorUserId.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("operation=" + inspection.OperationId.ToString("D"));
        output.WriteLine("kind=" + (inspection.Kind?.ToString() ?? "UNKNOWN"));
        output.WriteLine("target=" + (inspection.RequestTargetId?.ToString(CultureInfo.InvariantCulture) ?? "NONE"));
        output.WriteLine("originalResultRetained=" + inspection.OriginalResultRetained.ToString().ToLowerInvariant());
        output.WriteLine("originalVersionRetained=" + inspection.OriginalVersionRetained.ToString().ToLowerInvariant());
        output.WriteLine("originalLineIdsRetained=" + inspection.OriginalLineIdsRetained.ToString().ToLowerInvariant());
    }
}
