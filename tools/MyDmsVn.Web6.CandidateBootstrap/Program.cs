using System;
using System.Threading;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal static class Program
{
    private static int Main(string[] args)
    {
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += cancel;
        try
        {
            return BootstrapCommand.RunAsync(args, Console.Out, Console.Error, stop.Token).GetAwaiter().GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }
}
