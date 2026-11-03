using System;
using System.Threading;

namespace MyDmsVn.WebOperation.Operator;

internal static class Program
{
    private static int Main(string[] args)
    {
        OperatorProcessDeadline? hardDeadline = null;
        var inspectParsed = InspectOptions.TryParse(args, out var inspectOptions, out _) && !inspectOptions!.HelpRequested;
        var decisionParsed = RecordNotExecutedOptions.TryParse(args, out var decisionOptions, out _) && !decisionOptions!.HelpRequested;
        var timeoutSeconds = inspectParsed ? inspectOptions!.TimeoutSeconds : decisionParsed ? decisionOptions!.TimeoutSeconds : 0;
        if (timeoutSeconds > 0)
        {
            hardDeadline = new OperatorProcessDeadline(TimeSpan.FromSeconds(timeoutSeconds), () =>
            {
                try { Console.Error.WriteLine("Operator.Timeout: the process deadline expired; SQL outcome is unknown."); }
                finally { Environment.Exit((int)OperatorExitCode.Timeout); }
            });
        }
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            if (args.Length > 0 && args[0] == "record-not-executed")
                return OperatorDecisionApplication.RunAsync(args,
                    _ => new SqlOperatorDecisionClient(new OperatorSqlConnectionFactory(
                        Environment.GetEnvironmentVariable(OperatorInspectApplication.ConnectionStringEnvironmentVariable),
                        Environment.GetEnvironmentVariable(OperatorInspectApplication.DatabaseUserEnvironmentVariable),
                        Environment.GetEnvironmentVariable(OperatorInspectApplication.LoginEnvironmentVariable))),
                    Console.Out, Console.Error, stop.Token).GetAwaiter().GetResult();
            return OperatorInspectApplication.RunAsync(args,
                _ => new SqlOperatorInspectionClient(new OperatorSqlConnectionFactory(
                    Environment.GetEnvironmentVariable(OperatorInspectApplication.ConnectionStringEnvironmentVariable),
                    Environment.GetEnvironmentVariable(OperatorInspectApplication.DatabaseUserEnvironmentVariable),
                    Environment.GetEnvironmentVariable(OperatorInspectApplication.LoginEnvironmentVariable))),
                Console.Out, Console.Error, stop.Token).GetAwaiter().GetResult();
        }
        finally
        {
            hardDeadline?.Dispose();
            Console.CancelKeyPress -= handler;
        }
    }
}
