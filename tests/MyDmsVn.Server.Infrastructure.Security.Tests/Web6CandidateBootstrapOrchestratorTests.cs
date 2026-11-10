using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Web6.CandidateBootstrap;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateBootstrapOrchestratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "web6-orch-" + Guid.NewGuid().ToString("N").Substring(0, 8));
    private readonly List<string> _calls = new();
    private const string Backend = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly string Hash = new string('c', 64);
    private string? _mutation;
    private bool _acceptedDigestRejected;
    private PreparedCandidateReceipt Prepared()
    {
        Directory.CreateDirectory(_root);
        var json = new JObject
        {
            ["version"] = 1, ["state"] = "Prepared", ["cutoverAdmission"] = "PIN_MATCHED", ["candidateId"] = "candidate-1",
            ["taskRoot"] = _root, ["backendSha"] = Backend, ["frontendSha"] = PreparedCandidateReceipt.ReviewedFrontendSha,
            ["candidateDistSha256"] = Hash, ["currentPublishSha256"] = Hash, ["expiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"),
            ["sqlApprovalPath"] = Path.Combine(_root, "approval.json"), ["sqlControlParent"] = Path.Combine(_root, "sql"),
            ["tlsThumbprint"] = new string('A', 40), ["actorCohort"] = "web.smoke", ["workloadSeconds"] = 60, ["recoveryAuthorized"] = true,
            ["tools"] = new JObject { ["node"] = new JObject { ["path"] = Path.Combine(_root, "node.exe"), ["sha256"] = Hash },
                ["git"] = new JObject { ["path"] = Path.Combine(_root, "git.exe"), ["sha256"] = Hash } },
        };
        foreach (var key in new[] { "backendWorktree", "frontendWorktree", "oldBackendWorktree", "oldFrontendWorktree", "candidateDist", "oldFrontendDist", "currentPublish", "oldPublish" })
            json[key] = Path.Combine(_root, key);
        foreach (var gate in new[] { "pairChromium", "pairEdge", "cutover", "workload", "recovery" })
            json[gate + "Root"] = Path.Combine(_root, gate);
        json["cutoverBuildAuthorityPath"] = Path.Combine(_root, "build-authority.json");
        json["cutoverBuildAuthoritySha256"] = Hash;
        var path = Path.Combine(_root, "prepared.json");
        var digest = ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(json));
        ProtectedJsonFile.WriteNew(Path.Combine(_root, "prepared.sha256"), Encoding.ASCII.GetBytes(digest));
        return PreparedCandidateReceipt.Read(path, digest);
    }

    [Fact]
    public void Exact_plan_contains_only_current_gate_opt_ins_and_required_inputs()
    {
        var plan = Web6SelectorPlan.Create(Prepared());
        Assert.Equal(new[] { "pair-chromium", "pair-edge", "cutover", "workload", "recovery" }, plan.Gates.Select(g => g.Name));
        foreach (var gate in plan.Gates)
        {
            var env = plan.EnvironmentFor(gate.Name, "run-1", "private-sql", "task4-path", Hash);
            Assert.Equal("private-sql", env["MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING"]);
            Assert.Equal("run-1", env["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"]);
            Assert.DoesNotContain("MYDMSVN_WEB_JWT_KEYS", env.Keys);
            Assert.Equal(gate.Name == "cutover", env.ContainsKey("MYDMSVN_WEB6_CUTOVER_ENABLE"));
            Assert.Equal(gate.Name == "workload", env.ContainsKey("MYDMSVN_WEB6_WORKLOAD_ENABLE"));
            Assert.Equal(gate.Name == "recovery", env.ContainsKey("MYDMSVN_WEB6_RECOVERY_ENABLE"));
            Assert.Equal(Path.Combine(_root, "git.exe"), env["MYDMSVN_WEB6_BOOTSTRAP_GIT_PATH"]);
            if (gate.Name.StartsWith("pair-"))
            {
                Assert.Equal(gate.Name.Substring(5), env["MYDMSVN_WEB_PILOT_PROJECT"]);
                Web6CandidateAcceptanceGate.RequireLive(env.ToDictionary(k => k.Key, k => (string?)k.Value));
            }
            if (gate.Name == "cutover") Web6CutoverAcceptanceGate.RequireLive(env.ToDictionary(k => k.Key, k => (string?)k.Value));
            if (gate.Name == "workload") Web6WorkloadAcceptanceGate.RequireLive(env.ToDictionary(k => k.Key, k => (string?)k.Value));
            if (gate.Name == "recovery") Web6CandidateRecoveryAcceptanceGate.RequireLive(env.ToDictionary(k => k.Key, k => (string?)k.Value));
        }
    }

    [Fact]
    public async Task Serial_run_writes_Task4_only_after_both_browsers_and_cutover_then_fresh_disposition()
    {
        var result = await Run();
        Assert.Equal(BootstrapRunStage.CandidateVerified, result.Stage);
        Assert.Equal(new[] { "pair-chromium", "pair-edge", "cutover", "workload", "recovery", "disposition" }, _calls);
        var task4 = Web6WorkloadTask4Receipt.ReadProtected(Path.Combine(_root, "evidence", "task4.json"),
            BootstrapPathPolicy.HashFile(Path.Combine(_root, "evidence", "task4.json")));
        Assert.Equal("candidate-1", task4.CandidateId);
        Assert.Equal(PreparedCandidateReceipt.ReviewedFrontendSha, task4.FrontendSha);
    }

    [Theory]
    [InlineData("workload")]
    [InlineData("recovery")]
    public async Task Accepted_native_receipt_mutation_before_final_observation_is_retained(string gate)
    {
        _mutation = "accepted-" + gate;
        var result = await Run();
        Assert.Equal(BootstrapRunStage.FailedRetained, result.Stage);
        Assert.True(_acceptedDigestRejected);
        Assert.Equal(new[] { "pair-chromium", "pair-edge", "cutover", "workload", "recovery", "disposition" }, _calls);
        Assert.DoesNotContain(Directory.GetFiles(_root, "*.json"), path =>
            File.ReadAllText(path).Contains("\"state\":\"CandidateVerified\""));
    }

    [Theory]
    [InlineData("pair-edge-candidate", 2)]
    [InlineData("pair-edge-browser", 2)]
    [InlineData("cutover-stale", 3)]
    [InlineData("cutover-task4", 3)]
    [InlineData("workload-samples", 4)]
    [InlineData("workload-profiles", 4)]
    [InlineData("recovery-rpo", 5)]
    [InlineData("recovery-rto", 5)]
    [InlineData("first-failure", 1)]
    [InlineData("inventory", 1)]
    [InlineData("process", 6)]
    [InlineData("queue", 6)]
    [InlineData("sql", 6)]
    public async Task Evidence_mismatch_or_uncertain_disposition_stops_once_and_retains(string mutation, int calls)
    {
        _mutation = mutation;
        Assert.Equal(BootstrapRunStage.FailedRetained, (await Run()).Stage);
        Assert.Equal(calls, _calls.Count);
        Assert.Equal(_calls.Count, _calls.Distinct().Count());
        if (calls <= 3) Assert.False(File.Exists(Path.Combine(_root, "evidence", "task4.json")));
    }

    [Theory]
    [InlineData("WEB5-HISTORICAL")]
    [InlineData("WEB6_QUARANTINE")]
    [InlineData("20261109-b03")]
    public void Historical_resource_records_are_never_adopted(string resource) =>
        Assert.Throws<IOException>(() => BootstrapDisposition.RequireAllowedResource(Path.Combine(_root, resource), _root));

    [Theory]
    [InlineData("existing-output")]
    [InlineData("existing-digest")]
    [InlineData("shared-parent")]
    public void Selector_output_refuses_reuse_or_nonprivate_parent(string failure)
    {
        Directory.CreateDirectory(_root);
        var parent = Path.Combine(_root, "private");
        using var pinned = new BootstrapPathPolicy.PinnedDirectory(parent, create: true);
        var output = Path.Combine(parent, "receipt.json"); var digest = Path.Combine(parent, "receipt.sha256");
        if (failure == "existing-output") ProtectedJsonFile.WriteNew(output, Encoding.ASCII.GetBytes("retained"));
        if (failure == "existing-digest") ProtectedJsonFile.WriteNew(digest, Encoding.ASCII.GetBytes("retained"));
        if (failure == "shared-parent")
        {
            var acl = Directory.GetAccessControl(parent);
            acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null),
                System.Security.AccessControl.FileSystemRights.Read, System.Security.AccessControl.AccessControlType.Allow));
            Directory.SetAccessControl(parent, acl);
        }
        var env = new Dictionary<string, string> { ["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH"] = output, ["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_SHA_PATH"] = digest };
        Assert.Throws<IOException>(() => Web6SelectorOutput.Write(env, new JObject { ["version"] = 1 }));
        if (failure == "existing-output") Assert.Equal("retained", File.ReadAllText(output));
        if (failure == "existing-digest") Assert.Equal("retained", File.ReadAllText(digest));
    }

    [Theory]
    [InlineData("foreign-task4")]
    [InlineData("changed-pair")]
    [InlineData("changed-cutover")]
    [InlineData("changed-manifest")]
    public async Task Task4_binding_rechecks_both_pair_receipts_cutover_and_retained_identity(string failure)
    {
        Assert.Equal(BootstrapRunStage.CandidateVerified, (await Run()).Stage);
        var preparedPath = Path.Combine(_root, "prepared.json");
        var plan = Web6SelectorPlan.Create(PreparedCandidateReceipt.Read(preparedPath, BootstrapPathPolicy.HashFile(preparedPath)));
        var path = Path.Combine(plan.EvidenceRoot, "task4.json");
        var hash = BootstrapPathPolicy.HashFile(path);
        Web6SelectorOutput.VerifyTask4(plan, "run-1", path, hash);
        if (failure == "foreign-task4")
        {
            var foreign = Path.Combine(plan.EvidenceRoot, "foreign.json");
            ProtectedJsonFile.WriteNew(foreign, File.ReadAllBytes(path)); path = foreign;
        }
        else File.AppendAllText(Path.Combine(plan.EvidenceRoot, failure switch
        { "changed-pair" => "pair-edge.json", "changed-cutover" => "cutover.json", _ => "candidate.manifest" }), "drift");
        Assert.ThrowsAny<IOException>(() => Web6SelectorOutput.VerifyTask4(plan, "run-1", path, hash));
    }

    [Theory]
    [InlineData("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY")]
    [InlineData("MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY")]
    [InlineData("MYDMSVN_WEB6_CANDIDATE_ROOT")]
    [InlineData("MYDMSVN_WEB6_CANDIDATE_ID")]
    [InlineData("MYDMSVN_WEB6_BOOTSTRAP_GIT_PATH")]
    public void Native_selector_output_cannot_relabel_different_actual_inputs(string name)
    {
        var plan = Web6SelectorPlan.Create(Prepared());
        var env = plan.EnvironmentFor("pair-chromium", "run-1", "sql", null, null);
        Web6SelectorOutput.RequireEnvironment(plan, "pair-chromium", "run-1", env);
        env[name] += "-other";
        Assert.Throws<IOException>(() => Web6SelectorOutput.RequireEnvironment(plan, "pair-chromium", "run-1", env));
    }

    [Theory]
    [InlineData("")]
    [InlineData("The command completed successfully.")]
    [InlineData("Snapshot of HTTP service state (Request Queue View):\n-----------------------------------------------------\nRequest queue name: owned\n")]
    public void Final_queue_observation_requires_a_complete_parseable_snapshot(string snapshot) =>
        Assert.ThrowsAny<IOException>(() => BootstrapDisposition.RequireQueuesAbsent(snapshot, new[] { "owned" }));

    [Fact]
    public void Final_queue_observation_accepts_only_observed_absence()
    {
        const string empty = "Snapshot of HTTP service state (Request Queue View):\n-----------------------------------------------------\n";
        BootstrapDisposition.RequireQueuesAbsent(empty, new[] { "owned" });
        var owned = empty + "Request queue name: owned\n    State: Active\n    Number of active processes attached: 1\n" +
            "    Controller process:\n        ID: 17, image: fixture.exe\n    Processes:\n        ID: 17, image: fixture.exe\n" +
            "    URL groups:\n    URL group ID: 111\n        State: Active\n        Request queue name: owned\n" +
            "        Properties:\n            Number of registered URLs: 1\n            Registered URLs:\n" +
            "                HTTPS://LOCALHOST:44321/\n        Server session ID: 222\n";
        Assert.Throws<IOException>(() => BootstrapDisposition.RequireQueuesAbsent(owned, new[] { "owned" }));
    }

    [Fact]
    public async Task Bootstrap_cutover_cleanup_stops_before_a_second_deletion_after_uncertainty()
    {
        var deleted = new List<int>();
        await Assert.ThrowsAsync<IOException>(() => Web6CutoverLifetime.Run(() => Task.CompletedTask,
            () => Task.FromResult(true), new Func<Task>[]
            {
                () => { deleted.Add(1); throw new IOException("Uncertain first disposition."); },
                () => { deleted.Add(2); return Task.CompletedTask; },
            }, stopOnCleanupFailure: true));
        Assert.Equal(new[] { 1 }, deleted);
    }

    [Fact]
    public async Task Selector_build_uses_locked_restore_and_exact_Release_net48_project_before_launch()
    {
        var plan = Web6SelectorPlan.Create(Prepared());
        Directory.CreateDirectory(Path.GetDirectoryName(plan.Project)!);
        var commands = new List<BootstrapProcessRequest>();
        var files = new Dictionary<string, string>();
        foreach (var name in new[] { "git", "dotnet", "node" })
        {
            var path = Path.Combine(_root, name + ".exe"); File.WriteAllText(path, name); files.Add(name, path);
        }
        var owner = new BootstrapToolchain((request, _) =>
        {
            commands.Add(request);
            if (request.Arguments[0] == "build")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(plan.SelectorDll)!); File.WriteAllText(plan.SelectorDll, "exact selector assembly");
            }
            return Task.FromResult("");
        }, _ => Array.Empty<string>(), _ => Array.Empty<BootstrapCertificate>(), () => Array.Empty<string>(), () => null,
            (_, _) => Task.FromResult(("master", "fixture")));
        using var tools = new BootstrapToolchain.BootstrapTools(owner, files, Hash);
        Directory.CreateDirectory(plan.Text("backendWorktree"));
        await BootstrapRunExecution.BuildSelectorAsync(plan, tools, CancellationToken.None);
        Assert.Equal(new[] { "restore", "build" }, commands.Select(c => c.Arguments[0]));
        Assert.All(commands, c => Assert.Equal(plan.Project, c.Arguments[1]));
        Assert.Contains("--locked-mode", commands[0].Arguments);
        Assert.Contains("--no-restore", commands[1].Arguments);
        Assert.Contains("Release", commands[1].Arguments); Assert.Contains("net48", commands[1].Arguments);
        Assert.All(commands, c => Assert.DoesNotContain("MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING", c.Environment.Keys));
    }

    private Task<RunTransitionReceipt> Run()
    {
        var prepared = Prepared();
        var plan = Web6SelectorPlan.Create(prepared);
        return new Web6CandidateBootstrapOrchestrator(
            (_, _) => Task.CompletedTask, Invoke,
            async (p, state, recovery, receipts, token) =>
            {
                _calls.Add("disposition");
                if (_mutation is "accepted-workload" or "accepted-recovery")
                {
                    Assert.Equal(BootstrapRunStage.RecoveryRecorded, recovery.Stage);
                    var gate = _mutation.Substring("accepted-".Length);
                    var nativePath = Path.Combine(_root, "evidence", gate + ".json");
                    var native = PreparedCandidateReceipt.ReadCanonical(File.ReadAllBytes(nativePath));
                    if (gate == "workload") native["profiles"]![0]!["classes"]!["SalesCreate"]!["count"] = 1001;
                    else native["recoveryRtoMilliseconds"] = 51;
                    var changed = BootstrapJson.Serialize(native);
                    File.WriteAllBytes(nativePath, changed);
                    File.WriteAllText(Path.Combine(_root, "evidence", gate + ".sha256"), ProtectedJsonFile.Digest(changed), Encoding.ASCII);
                    var accepted = receipts.Single(r => r.Receipt.Gate == gate);
                    try { accepted.Read(p); }
                    catch (IOException) { _acceptedDigestRejected = accepted.Receipt.NativeSha256 != ProtectedJsonFile.Digest(changed); }
                }
                await Task.Delay(20, token); // Separate real observation after the recorded recovery clock tick.
                var path = Path.Combine(_root, "evidence", "disposition-native.json");
                var sha = ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(new JObject { ["observed"] = DateTimeOffset.UtcNow.ToString("O") }));
                return ProtectedDispositionReceipt.Write(Path.Combine(_root, "evidence", "disposition.json"), state.RunId,
                    state.CandidateId, path, sha, recovery.Sha256, DateTimeOffset.UtcNow,
                    _mutation != "process", _mutation != "queue", _mutation != "sql", true);
            }).RunAsync(plan, "run-1", "private-sql", CancellationToken.None);
    }

    private Task<SelectorResult> Invoke(Web6SelectorPlan plan, SelectorInvocation invocation, CancellationToken token)
    {
        _calls.Add(invocation.Name);
        var task4Path = Path.Combine(_root, "evidence", "task4.json");
        Assert.Equal(invocation.Name is "workload" or "recovery", File.Exists(task4Path));
        var started = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(Path.GetDirectoryName(invocation.TrxPath)!);
        File.WriteAllText(invocation.TrxPath, "fixture exact selector result");
        if (_mutation != "first-failure")
        {
            var payload = Web6SelectorOutput.CreatePayload(plan, invocation.Name, "run-1");
            payload["processDrained"] = true; payload["httpQueuesAbsent"] = true; payload["sqlQuiescent"] = true; payload["resourcesVerified"] = true;
            var allocationPath = Path.Combine(plan.EvidenceRoot, invocation.Name + "-allocation.json");
            var allocationHash = ProtectedJsonFile.WriteNew(allocationPath, BootstrapJson.Serialize(new JObject
            { ["version"] = 1, ["runId"] = "run-1", ["gate"] = invocation.Name, ["originalAllocation"] = "fixture" }));
            payload["resources"] = new JArray(new JObject { ["kind"] = "database", ["name"] = "fixture-database-" + invocation.Name,
                ["ownershipSha256"] = allocationHash, ["ownershipPath"] = allocationPath });
            var inventoryPath = Path.Combine(plan.EvidenceRoot, invocation.Name + "-cleanup-inventory.json");
            payload["cleanupInventoryPath"] = inventoryPath;
            payload["cleanupInventorySha256"] = ProtectedJsonFile.WriteNew(inventoryPath, BootstrapJson.Serialize(new JObject
            { ["version"] = 1, ["runId"] = "run-1", ["gate"] = invocation.Name, ["deletionAuthority"] = false, ["resources"] = payload["resources"]!.DeepClone() }));
            if (_mutation == "inventory") payload["resources"]![0]!["name"] = "changed-resource";
            payload["sqlOwnershipReceiptSha256"] = Hash;
            if (invocation.Name == "pair-edge" && _mutation == "pair-edge-candidate") payload["candidateId"] = "foreign";
            if (invocation.Name == "pair-edge" && _mutation == "pair-edge-browser") payload["browser"] = "chromium";
            if (invocation.Name == "cutover")
            {
                var manifestPath = Path.Combine(_root, "evidence", "candidate.manifest");
                payload["retainedManifestPath"] = manifestPath;
                payload["retainedManifestSha256"] = ProtectedJsonFile.WriteNew(manifestPath, Encoding.UTF8.GetBytes("immutable identity only"));
                if (_mutation == "cutover-stale") payload["completedAtUtc"] = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
                if (_mutation == "cutover-task4") payload["preparedSha256"] = new string('d', 64);
            }
            if (invocation.Name == "workload")
                payload["profiles"] = new JArray(new[] { 1, 4, 8, 16 }.Where(n => _mutation != "workload-profiles" || n != 16)
                    .Select(n => new JObject { ["concurrency"] = n, ["classes"] = new JObject(Web6CandidateWorkloadPlan.CriticalClasses.Select(c =>
                        new JProperty(c.ToString(), new JObject { ["count"] = _mutation == "workload-samples" ? 999 : 1000, ["errors"] = 0, ["timeouts"] = 0 }))) }));
            if (invocation.Name == "recovery")
            {
                payload["recoveryRpo"] = _mutation == "recovery-rpo" ? null : (long?)0;
                payload["recoveryRtoMilliseconds"] = _mutation == "recovery-rto" ? null : (long?)50;
            }
            Web6SelectorOutput.Write(invocation.Environment, payload);
        }
        var result = new SelectorResult(invocation, _mutation == "first-failure" ? SelectorDisposition.NonzeroExit : SelectorDisposition.Passed,
            started, DateTimeOffset.UtcNow, true, _mutation == "first-failure" ? 1 : 0,
            new SelectorTrx(1, 1, 1, 0, 0, BootstrapPathPolicy.HashFile(invocation.TrxPath)));
        if (_mutation == null) Assert.Null(Record.Exception(() => Web6CandidateBootstrapOrchestrator.ReadAccepted(plan, invocation, result)));
        return Task.FromResult(result);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
