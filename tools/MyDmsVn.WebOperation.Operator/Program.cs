using System;
using System.Threading;

namespace MyDmsVn.WebOperation.Operator;

internal static class Program
{
    private static int Main(string[] args)
    {
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            return OperatorInspectApplication.RunAsync(args,
                _ => new SqlOperatorInspectionClient(new OperatorSqlConnectionFactory(
                    Environment.GetEnvironmentVariable(OperatorInspectApplication.ConnectionStringEnvironmentVariable),
                    Environment.GetEnvironmentVariable(OperatorInspectApplication.DatabaseUserEnvironmentVariable),
                    Environment.GetEnvironmentVariable(OperatorInspectApplication.LoginEnvironmentVariable))),
                Console.Out, Console.Error, stop.Token).GetAwaiter().GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
