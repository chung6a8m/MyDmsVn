using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class SelectorInvocation
{
    internal SelectorInvocation(string name, string filter, IReadOnlyDictionary<string, string> environment, TimeSpan timeout,
        string trxPath, string selectorDllPath)
    {
        Name = name;
        Filter = filter;
        Environment = new ReadOnlyDictionary<string, string>(Web6CutoverSelectorBudgets.NormalizeEnvironment(environment));
        Timeout = timeout;
        TrxPath = trxPath;
        SelectorDllPath = selectorDllPath;
    }

    internal string Name { get; }
    internal string Filter { get; }
    internal IReadOnlyDictionary<string, string> Environment { get; }
    internal TimeSpan Timeout { get; }
    internal string TrxPath { get; }
    internal string SelectorDllPath { get; }
}

internal enum SelectorDisposition
{
    Passed,
    NonzeroExit,
    MissingTrx,
    InvalidTrx,
    MandatoryTestFailed,
    TimedOut,
    Canceled,
    ProcessUncertain,
}

internal sealed class SelectorResult
{
    internal SelectorResult(SelectorInvocation invocation, SelectorDisposition disposition, DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc, bool processTreeDrained, int? exitCode = null, SelectorTrx? trx = null)
    {
        Name = invocation.Name;
        Filter = invocation.Filter;
        TrxPath = invocation.TrxPath;
        RunId = invocation.Environment.TryGetValue("MYDMSVN_WEB6_BOOTSTRAP_RUN_ID", out var runId) ? runId : null;
        Disposition = disposition;
        StartedAtUtc = startedAtUtc;
        EndedAtUtc = endedAtUtc;
        ProcessTreeDrained = processTreeDrained;
        ExitCode = exitCode;
        Discovered = trx?.Discovered ?? 0;
        Executed = trx?.Executed ?? 0;
        Passed = trx?.Passed ?? 0;
        Failed = trx?.Failed ?? 0;
        Skipped = trx?.Skipped ?? 0;
        TrxSha256 = trx?.Sha256;
    }

    internal string Name { get; }
    internal string Filter { get; }
    internal string? RunId { get; }
    internal string TrxPath { get; }
    internal string? TrxSha256 { get; }
    internal SelectorDisposition Disposition { get; }
    internal string SanitizedDisposition => Disposition.ToString();
    internal DateTimeOffset StartedAtUtc { get; }
    internal DateTimeOffset EndedAtUtc { get; }
    internal int? ExitCode { get; }
    internal int Discovered { get; }
    internal int Executed { get; }
    internal int Passed { get; }
    internal int Failed { get; }
    internal int Skipped { get; }
    internal bool ProcessTreeDrained { get; }
    internal bool RequiresDispositionProof => Disposition != SelectorDisposition.Passed;

    internal bool PassedMandatoryTests => Disposition == SelectorDisposition.Passed && ProcessTreeDrained && ExitCode == 0 &&
        Discovered > 0 && Discovered == Executed && Executed == Passed && Failed == 0 && Skipped == 0;
}

internal sealed class SelectorTrx
{
    internal SelectorTrx(int discovered, int executed, int passed, int failed, int skipped, string sha256)
    {
        Discovered = discovered;
        Executed = executed;
        Passed = passed;
        Failed = failed;
        Skipped = skipped;
        Sha256 = sha256;
    }

    internal int Discovered { get; }
    internal int Executed { get; }
    internal int Passed { get; }
    internal int Failed { get; }
    internal int Skipped { get; }
    internal string Sha256 { get; }
}
