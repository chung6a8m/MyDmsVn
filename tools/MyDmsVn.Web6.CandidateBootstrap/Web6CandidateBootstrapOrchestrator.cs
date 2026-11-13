using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class Web6CandidateBootstrapOrchestrator
{
    private readonly Func<Web6SelectorPlan, CancellationToken, Task> _prepare;
    private readonly Func<Web6SelectorPlan, SelectorInvocation, CancellationToken, Task<SelectorResult>> _invoke;
    private readonly Func<Web6SelectorPlan, BootstrapRunState, RunTransitionReceipt, IReadOnlyList<AcceptedSelectorReceipt>, CancellationToken, Task<ProtectedDispositionReceipt>> _disposition;

    internal Web6CandidateBootstrapOrchestrator(Func<Web6SelectorPlan, CancellationToken, Task> prepare,
        Func<Web6SelectorPlan, SelectorInvocation, CancellationToken, Task<SelectorResult>> invoke,
        Func<Web6SelectorPlan, BootstrapRunState, RunTransitionReceipt, IReadOnlyList<AcceptedSelectorReceipt>, CancellationToken, Task<ProtectedDispositionReceipt>> disposition)
    { _prepare = prepare; _invoke = invoke; _disposition = disposition; }

    internal async Task<RunTransitionReceipt> RunAsync(Web6SelectorPlan plan, string runId, string sql, CancellationToken token)
    {
        var state = BootstrapRunState.Open(plan.Prepared, runId);
        using var root = new BootstrapPathPolicy.PinnedDirectory(plan.TaskRoot);
        using var evidence = new BootstrapPathPolicy.PinnedDirectory(plan.EvidenceRoot, create: true);
        try { await _prepare(plan, token).ConfigureAwait(false); }
        catch (Exception)
        { return state.BlockBeforeAllocation(); }
        var receipts = new List<AcceptedSelectorReceipt>();
        string? task4Path = null, task4Hash = null;
        RunTransitionReceipt? transition = null;
        foreach (var gate in plan.Gates)
        {
            SelectorResult? result = null;
            try
            {
                token.ThrowIfCancellationRequested(); root.Verify(); evidence.Verify();
                if (gate.Name == "cutover" && transition?.Stage != BootstrapRunStage.PairPassed)
                    throw new IOException("Cutover requires the accepted PairPassed transition.");
                var env = plan.EnvironmentFor(gate.Name, runId, sql, task4Path, task4Hash,
                    gate.Name == "cutover" ? transition!.Sha256 : null);
                var invocation = new SelectorInvocation(gate.Name, gate.Filter, env, Web6CandidatePairBudgets.GateTimeout(gate.Name),
                    Path.Combine(plan.TaskRoot, "trx-" + gate.Name, "result.trx"), plan.SelectorDll);
                result = await _invoke(plan, invocation, token).ConfigureAwait(false);
                if (!result.PassedMandatoryTests) return state.Fail(result);
                var digest = Encoding.ASCII.GetString(ProtectedJsonFile.ReadInput(env["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_SHA_PATH"]));
                var native = ReadAccepted(plan, invocation, result, digest);
                var path = env["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH"];
                var receipt = ProtectedGateReceipt.Write(Path.Combine(plan.EvidenceRoot, gate.Name + "-envelope.json"), gate.Name, runId,
                    plan.Text("candidateId"), plan.Text("backendSha"), plan.Text("frontendSha"), plan.Text("candidateDistSha256"),
                    path, digest, gate.Name == "workload" ? "Measured" : null,
                    (long?)native["recoveryRpo"], (long?)native["recoveryRtoMilliseconds"]);
                transition = state.Accept(result, receipt);
                receipts.Add(new AcceptedSelectorReceipt(invocation, result, receipt));
                result = null; // The next state owns any subsequent evidence failure.
                if (gate.Name == "cutover") (task4Path, task4Hash) = WriteTask4(plan, state, receipts);
            }
            catch (Exception)
            { return state.Fail(result); }
        }
        var pinned = new List<ProtectedJsonFile>();
        try
        {
            var disposition = await _disposition(plan, state, transition!, receipts, token).ConfigureAwait(false);
            // Keep the exact accepted evidence immutable through the final state
            // transition, including changes during independent observation.
            foreach (var accepted in receipts)
            {
                pinned.Add(ProtectedJsonFile.Open(accepted.Receipt.PrivatePath, accepted.Receipt.Sha256));
                pinned.Add(ProtectedJsonFile.Open(accepted.Receipt.NativePath, accepted.Receipt.NativeSha256));
                accepted.Read(plan);
            }
            return state.Complete(disposition);
        }
        catch (Exception)
        { return state.Fail(); }
        finally { foreach (var file in pinned) file.Dispose(); }
    }

    internal static JObject ReadAccepted(Web6SelectorPlan plan, SelectorInvocation invocation, SelectorResult result, string? acceptedDigest = null)
    {
        var path = invocation.Environment["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH"];
        var digest = acceptedDigest ?? Encoding.ASCII.GetString(ProtectedJsonFile.ReadInput(invocation.Environment["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_SHA_PATH"]));
        using var file = ProtectedJsonFile.Open(path, digest);
        var json = PreparedCandidateReceipt.ReadCanonical(file.Bytes);
        var expected = Web6SelectorOutput.CreatePayload(plan, invocation.Name, result.RunId ?? throw new IOException("Bootstrap run identity is missing."));
        if (!json.Properties().Select(p => p.Name).OrderBy(n => n).SequenceEqual(expected.Properties().Select(p => p.Name).OrderBy(n => n)))
            throw new IOException("Bootstrap native receipt schema differs.");
        Web6SelectorOutput.RequireAcceptedNativeIdentity(json, expected);
        if (!DateTimeOffset.TryParseExact((string?)json["completedAtUtc"], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var completed) ||
            completed.Offset != TimeSpan.Zero || completed < result.StartedAtUtc || completed > result.EndedAtUtc)
            throw new IOException("Bootstrap native receipt is stale or outside the selector execution.");
        BootstrapDisposition.RequireOwnershipEvidence(plan, result.RunId!, json);
        if (invocation.Name == "cutover")
        {
            var manifest = BootstrapJson.Text(json, "retainedManifestPath");
            if (manifest != Path.Combine(plan.EvidenceRoot, "candidate.manifest")) throw new IOException("Cutover retained evidence is outside the output root.");
            using var copy = ProtectedJsonFile.Open(manifest, BootstrapJson.Text(json, "retainedManifestSha256"));
        }
        if (invocation.Name == "workload") RequireMeasured(json);
        if (invocation.Name == "recovery" && ((long?)json["recoveryRpo"] != 0 || (long?)json["recoveryRtoMilliseconds"] is not > 0))
            throw new IOException("Bootstrap recovery lacks observed zero-loss RPO and positive RTO.");
        return json;
    }

    internal static void RequireMeasured(JObject receipt)
    {
        if (receipt["profiles"] is not JArray profiles || !profiles.Select(p => (int?)p["concurrency"]).SequenceEqual(new int?[] { 1, 4, 8, 16 }))
            throw new IOException("Bootstrap workload requires the four exact measured profiles.");
        foreach (var profile in profiles)
            foreach (var name in Web6SelectorPlan.CriticalClasses)
                if ((int?)profile["classes"]?[name]?["count"] is not >= 1000 || (int?)profile["classes"]?[name]?["errors"] != 0 ||
                    (int?)profile["classes"]?[name]?["timeouts"] != 0)
                    throw new IOException("Bootstrap workload has insufficient or unsuccessful critical samples.");
    }

    private static (string Path, string Hash) WriteTask4(Web6SelectorPlan plan, BootstrapRunState state, IReadOnlyList<AcceptedSelectorReceipt> receipts)
    {
        if (state.Stage != BootstrapRunStage.CutoverPassed || receipts.Count != 3 ||
            !receipts.Select(r => r.Receipt.Gate).SequenceEqual(new[] { "pair-chromium", "pair-edge", "cutover" }))
            throw new IOException("Task4 requires both browsers and cutover.");
        var native = receipts.Select(r => r.Read(plan)).ToArray();
        var cutover = native[2];
        var task4 = new JObject
        {
            ["version"] = 1, ["mode"] = "web6-cutover", ["status"] = "ACCEPTED", ["candidateId"] = state.CandidateId,
            ["backendSha"] = state.BackendSha, ["frontendSha"] = state.FrontendSha, ["frontendDistManifestSha256"] = state.FrontendDistSha256,
            ["sqlOwnershipReceiptSha256"] = cutover["sqlOwnershipReceiptSha256"]!.DeepClone(),
            ["manifestSha256"] = cutover["retainedManifestSha256"]!.DeepClone(), ["completedAtUtc"] = cutover["completedAtUtc"]!.DeepClone(),
        };
        var path = Path.Combine(plan.EvidenceRoot, "task4.json");
        var bytes = BootstrapJson.Serialize(task4);
        ProtectedJsonFile.WriteNew(Path.Combine(plan.EvidenceRoot, "task4-bindings.json"), BootstrapJson.Serialize(new JObject
        {
            ["version"] = 1, ["runId"] = state.RunId, ["candidateId"] = state.CandidateId, ["preparedSha256"] = plan.Prepared.Sha256,
            ["task4Sha256"] = ProtectedJsonFile.Digest(bytes), ["retainedEvidenceOnly"] = true,
            ["gates"] = new JArray(receipts.Select((r, index) => new JObject { ["gate"] = r.Receipt.Gate,
                ["sha256"] = r.Receipt.NativeSha256,
                ["sqlOwnershipReceiptSha256"] = native[index]["sqlOwnershipReceiptSha256"]!.DeepClone() })),
        }));
        return (path, ProtectedJsonFile.WriteNew(path, bytes));
    }
}

internal sealed class AcceptedSelectorReceipt
{
    private readonly SelectorInvocation _invocation;
    private readonly SelectorResult _result;
    internal AcceptedSelectorReceipt(SelectorInvocation invocation, SelectorResult result, ProtectedGateReceipt receipt)
    { _invocation = invocation; _result = result; Receipt = receipt; }
    internal ProtectedGateReceipt Receipt { get; }
    internal JObject Read(Web6SelectorPlan plan)
    {
        if (Receipt.NativePath != _invocation.Environment["MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH"] || Receipt.Gate != _invocation.Name)
            throw new IOException("Accepted native evidence differs from its selector binding.");
        using var envelope = ProtectedJsonFile.Open(Receipt.PrivatePath, Receipt.Sha256);
        return Web6CandidateBootstrapOrchestrator.ReadAccepted(plan, _invocation, _result, Receipt.NativeSha256);
    }
}
