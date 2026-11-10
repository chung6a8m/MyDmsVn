using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

// Output handles are evidence destinations. Existing selector opt-ins and SQL
// authority remain mandatory; these handles never authorize a live operation.
internal sealed class Web6SelectorOutput : IDisposable
{
    private readonly BootstrapPathPolicy.PinnedDirectory? _parent;
    private readonly ProtectedJsonFile _prepared;
    private readonly BootstrapAuthority _authority;
    private readonly Dictionary<string, string> _environment;
    private readonly string _parentAcl;
    private string? _inventoryHash;
    internal Web6SelectorPlan Plan { get; }
    internal string Gate { get; }
    internal string RunId { get; }
    internal string GitPath => Plan.Tool("git");

    private Web6SelectorOutput(Dictionary<string, string> environment, string gate)
    {
        _environment = environment; Gate = gate;
        var path = BootstrapJson.AbsolutePath(environment["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH"]);
        var root = Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;
        var digest = Encoding.ASCII.GetString(ProtectedJsonFile.ReadInput(Path.Combine(root, "prepared.sha256")));
        _prepared = ProtectedJsonFile.Open(Path.Combine(root, "prepared.json"), digest);
        try
        {
            Plan = Web6SelectorPlan.Create(PreparedCandidateReceipt.Read(_prepared.Path, digest));
            RunId = BootstrapJson.RequireCandidateId(environment["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"]);
            _authority = BootstrapAuthority.ReadProtected(Plan.Text("authorityPath"), Plan.Text("authoritySha256"), DateTimeOffset.UtcNow);
            try
            {
                var attempt = PreparedCandidateReceipt.ReadCanonical(ProtectedJsonFile.ReadInput(Path.Combine(root, "run-attempt.json")));
                if ((string?)attempt["runId"] != RunId || (string?)attempt["preparedSha256"] != digest ||
                    _authority.CandidateId != Plan.Text("candidateId") || _authority.FrontendSha != Plan.Text("frontendSha") ||
                    _authority.BackendSha != Plan.Text("backendSha")) throw new IOException("Bootstrap selector run binding changed.");
                RequireTargets(environment, Plan, gate);
                RequireEnvironment(Plan, gate, RunId, Environment.GetEnvironmentVariables().Keys.Cast<string>()
                    .Where(n => n.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(n => n, n => Environment.GetEnvironmentVariable(n)!, StringComparer.OrdinalIgnoreCase));
                _parent = new BootstrapPathPolicy.PinnedDirectory(Plan.EvidenceRoot);
                _parentAcl = RequirePrivateParent(Plan.EvidenceRoot);
                if (gate is "workload" or "recovery")
                    VerifyTask4(Plan, RunId, Environment.GetEnvironmentVariable("MYDMSVN_WEB6_TASK4_ACCEPTANCE_PATH") ?? "",
                        Environment.GetEnvironmentVariable("MYDMSVN_WEB6_TASK4_ACCEPTANCE_SHA256") ?? "");
            }
            catch { _parent?.Dispose(); _authority.Dispose(); throw; }
        }
        catch { _prepared.Dispose(); throw; }
    }

    internal static Web6SelectorOutput? Open(string gate)
    {
        var names = new[] { "MYDMSVN_WEB6_BOOTSTRAP_RUN_ID", "MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH", "MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_SHA_PATH" };
        var values = names.ToDictionary(n => n, Environment.GetEnvironmentVariable);
        if (values.Values.All(v => v == null)) return null;
        if (values.Values.Any(string.IsNullOrWhiteSpace)) throw new IOException("Bootstrap output handle is incomplete.");
        return new Web6SelectorOutput(values.ToDictionary(v => v.Key, v => v.Value!), gate);
    }

    internal JObject Payload() => CreatePayload(Plan, Gate, RunId);
    internal void SealCleanupInventory(JArray resources)
    {
        Verify();
        if (_inventoryHash != null || resources.Count == 0) throw new IOException("Bootstrap cleanup inventory must be sealed once.");
        _inventoryHash = ProtectedJsonFile.WriteNew(Path.Combine(Plan.EvidenceRoot, Gate + "-cleanup-inventory.json"),
            BootstrapJson.Serialize(new JObject { ["version"] = 1, ["runId"] = RunId, ["gate"] = Gate,
                ["deletionAuthority"] = false, ["resources"] = resources.DeepClone() }));
        Verify();
    }

    internal void Complete(JObject payload)
    {
        Verify();
        if (_inventoryHash == null) throw new IOException("Bootstrap cleanup inventory was not sealed before cleanup.");
        payload["cleanupInventoryPath"] = Path.Combine(Plan.EvidenceRoot, Gate + "-cleanup-inventory.json");
        payload["cleanupInventorySha256"] = _inventoryHash;
        payload = PreparedCandidateReceipt.ReadCanonical(BootstrapJson.Serialize(payload));
        BootstrapDisposition.RequireCleanupInventory(Plan, RunId, payload);
        Write(_environment, payload);
        Verify();
    }

    internal static JObject CreatePayload(Web6SelectorPlan plan, string gate, string runId) => new()
    {
        ["version"] = 1, ["gate"] = gate, ["runId"] = runId, ["candidateId"] = plan.Text("candidateId"),
        ["backendSha"] = plan.Text("backendSha"), ["frontendSha"] = plan.Text("frontendSha"),
        ["frontendDistSha256"] = plan.Text("candidateDistSha256"), ["backendPublishSha256"] = plan.Text("currentPublishSha256"),
        ["preparedSha256"] = plan.Prepared.Sha256, ["status"] = "ACCEPTED",
        ["browser"] = gate.StartsWith("pair-", StringComparison.Ordinal) ? gate.Substring(5) : null,
        ["completedAtUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        ["processDrained"] = false, ["httpQueuesAbsent"] = false, ["sqlQuiescent"] = false, ["resourcesVerified"] = false,
        ["resources"] = new JArray(), ["sqlOwnershipReceiptSha256"] = null,
        ["cleanupInventoryPath"] = null, ["cleanupInventorySha256"] = null,
        ["retainedManifestPath"] = null, ["retainedManifestSha256"] = null,
        ["profiles"] = null, ["recoveryRpo"] = null, ["recoveryRtoMilliseconds"] = null,
    };

    internal static void Write(IReadOnlyDictionary<string, string> environment, JObject payload)
    {
        var path = BootstrapJson.AbsolutePath(environment["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH"]);
        var shaPath = BootstrapJson.AbsolutePath(environment["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_SHA_PATH"]);
        if (Path.GetDirectoryName(path) != Path.GetDirectoryName(shaPath) || path == shaPath || File.Exists(path) || File.Exists(shaPath))
            throw new IOException("Bootstrap selector evidence targets must be distinct and create-new.");
        using var parent = new BootstrapPathPolicy.PinnedDirectory(Path.GetDirectoryName(path)!);
        var acl = RequirePrivateParent(Path.GetDirectoryName(path)!);
        var bytes = BootstrapJson.Serialize(payload);
        // A partial pair is never accepted: the native file is created last and
        // acceptance requires both protected files and the exact detached digest.
        var digest = ProtectedJsonFile.Digest(bytes);
        ProtectedJsonFile.WriteNew(shaPath, Encoding.ASCII.GetBytes(digest));
        ProtectedJsonFile.WriteNew(path, bytes);
        parent.Verify();
        if (RequirePrivateParent(Path.GetDirectoryName(path)!) != acl) throw new IOException("Bootstrap output parent ACL changed.");
    }

    private void Verify()
    {
        _parent!.Verify(); _prepared.Verify(); _authority.Verify(DateTimeOffset.UtcNow);
        if (RequirePrivateParent(Plan.EvidenceRoot) != _parentAcl) throw new IOException("Bootstrap output parent ACL changed.");
    }

    private static string RequirePrivateParent(string path)
    {
        var acl = Directory.GetAccessControl(path, AccessControlSections.Owner | AccessControlSections.Access);
        var owner = ProtectedJsonFile.CurrentOwner();
        var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        if (!acl.AreAccessRulesProtected || !owner.Equals(acl.GetOwner(typeof(SecurityIdentifier))) || rules.Length != 1 ||
            !owner.Equals(rules[0].IdentityReference) || rules[0].AccessControlType != AccessControlType.Allow ||
            rules[0].FileSystemRights != FileSystemRights.FullControl || rules[0].IsInherited)
            throw new IOException("Bootstrap output parent is not private.");
        return acl.GetSecurityDescriptorSddlForm(AccessControlSections.Owner | AccessControlSections.Access);
    }

    private static void RequireTargets(IReadOnlyDictionary<string, string> env, Web6SelectorPlan plan, string gate)
    {
        if (!plan.Gates.Any(g => g.Name == gate) ||
            env["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH"] != Path.Combine(plan.EvidenceRoot, gate + ".json") ||
            env["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_SHA_PATH"] != Path.Combine(plan.EvidenceRoot, gate + ".sha256") ||
            File.Exists(env["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH"]) || File.Exists(env["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_SHA_PATH"]))
            throw new IOException("Bootstrap selector output differs from its protected plan.");
    }

    internal static void VerifyTask4(Web6SelectorPlan plan, string runId, string path, string digest)
    {
        if (path != Path.Combine(plan.EvidenceRoot, "task4.json")) throw new IOException("Task4 is not this attempt's acceptance file.");
        using var task4File = ProtectedJsonFile.Open(path, digest);
        var task4 = PreparedCandidateReceipt.ReadCanonical(task4File.Bytes);
        using var bindingFile = ProtectedJsonFile.OpenSqlApproval(Path.Combine(plan.EvidenceRoot, "task4-bindings.json"));
        var binding = PreparedCandidateReceipt.ReadCanonical(bindingFile.Bytes);
        if ((string?)binding["runId"] != runId || (string?)binding["candidateId"] != plan.Text("candidateId") ||
            (string?)binding["preparedSha256"] != plan.Prepared.Sha256 || (string?)binding["task4Sha256"] != digest ||
            (bool?)binding["retainedEvidenceOnly"] != true || (string?)task4["mode"] != "web6-cutover" || (string?)task4["status"] != "ACCEPTED" ||
            (string?)task4["candidateId"] != plan.Text("candidateId") || (string?)task4["backendSha"] != plan.Text("backendSha") ||
            (string?)task4["frontendSha"] != plan.Text("frontendSha") || (string?)task4["frontendDistManifestSha256"] != plan.Text("candidateDistSha256") ||
            binding["gates"] is not JArray gates || !gates.Select(g => (string?)g["gate"]).SequenceEqual(new[] { "pair-chromium", "pair-edge", "cutover" }))
            throw new IOException("Task4 acceptance is not bound to the prepared run and all prerequisite gates.");
        foreach (var gate in gates)
        {
            var name = (string)gate["gate"]!;
            using var file = ProtectedJsonFile.Open(Path.Combine(plan.EvidenceRoot, name + ".json"), (string)gate["sha256"]!);
            var native = PreparedCandidateReceipt.ReadCanonical(file.Bytes);
            if ((string?)native["runId"] != runId || (string?)native["gate"] != name ||
                (string?)native["candidateId"] != plan.Text("candidateId") || (string?)native["preparedSha256"] != plan.Prepared.Sha256 ||
                (string?)native["sqlOwnershipReceiptSha256"] != (string?)gate["sqlOwnershipReceiptSha256"])
                throw new IOException("Task4 prerequisite native evidence changed.");
            if (name == "cutover")
            {
                if ((string?)task4["sqlOwnershipReceiptSha256"] != (string?)native["sqlOwnershipReceiptSha256"] ||
                    (string?)task4["manifestSha256"] != (string?)native["retainedManifestSha256"])
                    throw new IOException("Task4 cutover ownership or manifest binding differs.");
                using var manifest = ProtectedJsonFile.Open(Path.Combine(plan.EvidenceRoot, "candidate.manifest"), (string)task4["manifestSha256"]!);
            }
        }
    }

    internal static void RequireEnvironment(Web6SelectorPlan plan, string gate, string runId, IReadOnlyDictionary<string, string> actual)
    {
        string Value(string name) => actual.TryGetValue(name, out var value) ? value : "";
        var expected = plan.EnvironmentFor(gate, runId, Value("MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING"),
            gate is "workload" or "recovery" ? Path.Combine(plan.EvidenceRoot, "task4.json") : null,
            Value("MYDMSVN_WEB6_TASK4_ACCEPTANCE_SHA256"));
        foreach (var item in expected)
        {
            // These are fresh per-run epochs, independently constrained by the
            // cutover owner. Source, artifact, root and executable inputs are exact.
            if (item.Key is "MYDMSVN_WEB6_CUTOVER_CURRENT_EPOCH" or "MYDMSVN_WEB6_CUTOVER_RESTORED_EPOCH") continue;
            if (Value(item.Key) != item.Value) throw new IOException("Bootstrap selector input differs from the protected prepared plan.");
        }
        foreach (var enable in new[] { "MYDMSVN_WEB6_CANDIDATE_ENABLE", "MYDMSVN_WEB6_CUTOVER_ENABLE", "MYDMSVN_WEB6_WORKLOAD_ENABLE", "MYDMSVN_WEB6_RECOVERY_ENABLE" })
            if (!expected.ContainsKey(enable) && Value(enable).Length != 0) throw new IOException("An unrelated bootstrap selector is enabled.");
    }

    public void Dispose() { _parent?.Dispose(); _authority.Dispose(); _prepared.Dispose(); }
}
