using System;
using System.Collections.Generic;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal static class Web6CandidatePairBudgets
{
    internal const string PairFilter = "FullyQualifiedName=MyDmsVn.Server.Infrastructure.IntegrationTests.WebHostIisExpressTests.Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL";
    // Exact reviewed frontend phase total; see tests/pilot/web6-pair-budgets.ts.
    internal static readonly TimeSpan SequentialChildren = TimeSpan.FromMilliseconds(1797800);
    internal static readonly TimeSpan BrowserAssertionMargin = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan FirstTwoTests = TimeSpan.FromSeconds(240 + 90);
    internal static readonly TimeSpan IpcDrainMargin = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan LaunchReporterDrainMargin = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan FixtureMargin = TimeSpan.FromSeconds(45 + 45 + 30); // host admission, health, SQL/oracle allowance
    private static TimeSpan NextMinute(TimeSpan value) => TimeSpan.FromMinutes(Math.Floor(value.TotalMinutes) + 1);
    internal static readonly TimeSpan ThirdTest = NextMinute(SequentialChildren + BrowserAssertionMargin);
    internal static readonly TimeSpan PairController = NextMinute(ThirdTest + FirstTwoTests + IpcDrainMargin);
    internal static readonly TimeSpan PairBrowser = NextMinute(PairController + LaunchReporterDrainMargin);
    internal static readonly TimeSpan PairGate = NextMinute(PairBrowser + FixtureMargin);
    internal static bool IsPairMode(bool candidate, bool cutover, string? project) => candidate && !cutover && project is "chromium" or "edge";
    internal static TimeSpan ControllerLifetime(bool pair) => pair ? PairController : TimeSpan.FromMinutes(6);
    internal static TimeSpan BrowserTimeout(bool candidate, bool cutover, string? project) => IsPairMode(candidate, cutover, project) ? PairBrowser : TimeSpan.FromMinutes(5);
    internal static TimeSpan GateTimeout(string gate) => gate is "pair-chromium" or "pair-edge" ? PairGate : TimeSpan.FromMinutes(25);
    internal static bool IsPairSelector(string name, string filter, IReadOnlyDictionary<string, string> environment) =>
        name is "pair-chromium" or "pair-edge" && filter == PairFilter &&
        environment.TryGetValue("MYDMSVN_WEB6_CANDIDATE_ENABLE", out var enabled) && enabled == "1" &&
        environment.TryGetValue("MYDMSVN_WEB_PILOT_PROJECT", out var project) && name == "pair-" + project &&
        environment.TryGetValue("MYDMSVN_WEB6_BOOTSTRAP_RUN_ID", out var run) && !string.IsNullOrEmpty(run);
    internal static TimeSpan SelectorMaximum(string name, string filter, IReadOnlyDictionary<string, string> environment) =>
        IsPairSelector(name, filter, environment) ? PairGate : TimeSpan.FromMinutes(30);
}
