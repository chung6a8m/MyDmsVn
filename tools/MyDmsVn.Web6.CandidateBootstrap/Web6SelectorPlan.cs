using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class Web6SelectorPlan
{
    private const string Namespace = "FullyQualifiedName=MyDmsVn.Server.Infrastructure.IntegrationTests.";
    internal const string Owner = "MyDmsVn.Server.Infrastructure.Security.Tests";
    internal static readonly string[] CriticalClasses = { "SalesCreate", "SalesUpdate", "SalesPost", "ReceiptCreate", "ReceiptUpdate", "ReceiptPost" };
    private readonly JObject _json;
    private readonly string _currentEpoch = Guid.NewGuid().ToString("D"), _restoredEpoch = Guid.NewGuid().ToString("D");
    private Web6SelectorPlan(PreparedCandidateReceipt prepared)
    {
        Prepared = prepared; _json = prepared.Payload;
        TaskRoot = BootstrapJson.AbsolutePath(Text("taskRoot"));
        if (prepared.CutoverAdmission != "PIN_MATCHED" || Text("frontendSha") != PreparedCandidateReceipt.ReviewedFrontendSha ||
            !BootstrapJson.SamePath(prepared.Path, Path.Combine(TaskRoot, "prepared.json")) || (bool?)_json["recoveryAuthorized"] != true)
            throw new IOException("Bootstrap requires a fresh reviewed prepared receipt and recovery authority.");
        Gates = Array.AsReadOnly(new[]
        {
            new Web6SelectorGate("pair-chromium", Namespace + "WebHostIisExpressTests.Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL"),
            new Web6SelectorGate("pair-edge", Namespace + "WebHostIisExpressTests.Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL"),
            new Web6SelectorGate("cutover", Namespace + "WebHostIisExpressTests.Web6_pinned_cutover_runs_protected_IIS_and_browser_lifecycle"),
            new Web6SelectorGate("workload", Namespace + "WebHostIisExpressTests.Web6_paired_candidate_runs_bounded_workload_profiles"),
            new Web6SelectorGate("recovery", Namespace + "Web6CandidateRecoveryTests.Web6_candidate_recovery_rehearses_distinct_owned_restore_before_service_admission"),
        });
    }

    internal PreparedCandidateReceipt Prepared { get; }
    internal string TaskRoot { get; }
    internal string EvidenceRoot => Path.Combine(TaskRoot, "evidence");
    internal string Project => Path.Combine(Text("backendWorktree"), "tests", Owner, Owner + ".csproj");
    internal string SelectorDll => Path.Combine(Text("backendWorktree"), "tests", Owner, "bin", "Release", "net48", Owner + ".dll");
    internal IReadOnlyList<Web6SelectorGate> Gates { get; }
    internal string Text(string key) => BootstrapJson.Text(_json, key);
    internal JObject Json => (JObject)_json.DeepClone();
    internal string Tool(string name, string field = "path") => BootstrapJson.Text((JObject)_json["tools"]![name]!, field);
    internal static Web6SelectorPlan Create(PreparedCandidateReceipt receipt) => new(receipt);

    internal string GateRoot(string gate) => Text(gate switch
    { "pair-chromium" => "pairChromiumRoot", "pair-edge" => "pairEdgeRoot", _ => gate + "Root" });

    internal Dictionary<string, string> EnvironmentFor(string gate, string runId, string sql, string? task4Path, string? task4Hash)
    {
        if (!Gates.Any(g => g.Name == gate)) throw new IOException("Unknown bootstrap selector.");
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING"] = sql, ["MYDMSVN_SQL_TEST_APPROVAL_FILE"] = Text("sqlApprovalPath"),
            ["MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY"] = Text("currentPublish"), ["MYDMSVN_WEB_TEST_TLS_THUMBPRINT"] = Text("tlsThumbprint"),
            ["MYDMSVN_WEB_TEST_BROWSER_DIRECTORY"] = Text("frontendWorktree"), ["MYDMSVN_WEB_TEST_NODE"] = Tool("node"),
            ["MYDMSVN_WEB6_CANDIDATE_ID"] = Text("candidateId"),
            ["MYDMSVN_WEB6_CANDIDATE_ROOT"] = gate == "recovery" ? EvidenceRoot : GateRoot(gate),
            ["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"] = BootstrapJson.RequireCandidateId(runId),
            ["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH"] = Path.Combine(EvidenceRoot, gate + ".json"),
            ["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_SHA_PATH"] = Path.Combine(EvidenceRoot, gate + ".sha256"),
            ["MYDMSVN_WEB6_BOOTSTRAP_GIT_PATH"] = Tool("git"), ["MYDMSVN_WEB6_BOOTSTRAP_GIT_SHA256"] = Tool("git", "sha256"),
        };
        if (gate.StartsWith("pair-", StringComparison.Ordinal) || gate == "workload")
        {
            env["MYDMSVN_WEB6_CANDIDATE_ENABLE"] = "1";
            env["MYDMSVN_WEB_PILOT_PROJECT"] = gate == "pair-edge" ? "edge" : "chromium";
        }
        if (gate == "cutover")
        {
            env["MYDMSVN_WEB6_CUTOVER_ENABLE"] = "1";
            foreach (var item in new[] { ("FRONTEND_ARCHIVE_DIRECTORY", "oldFrontendWorktree"), ("BACKEND_ARCHIVE_DIRECTORY", "oldBackendWorktree"),
                ("FRONTEND_DIST_DIRECTORY", "oldFrontendDist"), ("OLD_PUBLISH_DIRECTORY", "oldPublish"), ("CURRENT_PUBLISH_DIRECTORY", "currentPublish"),
                ("BUILD_AUTHORITY_PATH", "cutoverBuildAuthorityPath"), ("BUILD_AUTHORITY_SHA256", "cutoverBuildAuthoritySha256"), ("SQL_CONTROL_PARENT", "sqlControlParent") })
                env["MYDMSVN_WEB6_CUTOVER_" + item.Item1] = Text(item.Item2);
            env["MYDMSVN_WEB6_CUTOVER_CURRENT_EPOCH"] = _currentEpoch;
            env["MYDMSVN_WEB6_CUTOVER_RESTORED_EPOCH"] = _restoredEpoch;
        }
        if (gate is "workload" or "recovery")
        {
            env["MYDMSVN_WEB6_TASK4_ACCEPTANCE_PATH"] = task4Path ?? throw new IOException("Task4 acceptance is absent.");
            env["MYDMSVN_WEB6_TASK4_ACCEPTANCE_SHA256"] = task4Hash ?? throw new IOException("Task4 acceptance hash is absent.");
            env["MYDMSVN_WEB6_" + gate.ToUpperInvariant() + "_ENABLE"] = "1";
            env["MYDMSVN_WEB6_" + gate.ToUpperInvariant() + "_ROOT"] = gate == "workload" ? GateRoot(gate) + "-measurements" : GateRoot(gate);
            if (gate == "workload") env["MYDMSVN_WEB6_WORKLOAD_ACTOR_COHORT"] = Text("actorCohort");
        }
        return env;
    }
}

internal sealed class Web6SelectorGate
{
    internal Web6SelectorGate(string name, string filter) { Name = name; Filter = filter; }
    internal string Name { get; }
    internal string Filter { get; }
}
