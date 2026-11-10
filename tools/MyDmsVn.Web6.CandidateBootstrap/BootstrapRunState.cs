using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class BootstrapRunState
{
    private readonly object _sync = new();
    private readonly DateTimeOffset _expiry;
    private readonly RunTransitionReceipt _prepared;
    private RunTransitionReceipt _current;
    private DateTimeOffset _lastSelectorEndedAtUtc;
    private (SelectorResult Result, ProtectedGateReceipt Receipt)? _chromium;
    private string? _workloadStatus;
    private long? _recoveryRpo;
    private long? _recoveryRtoMilliseconds;

    private BootstrapRunState(string runId, ProtectedJsonFile prepared, JObject payload)
    {
        RunId = BootstrapJson.RequireCandidateId(runId);
        CandidateId = BootstrapJson.CandidateId(payload);
        TaskRoot = BootstrapJson.AbsolutePath(BootstrapJson.Text(payload, "taskRoot"));
        BackendSha = BootstrapJson.Commit(payload, "backendSha");
        FrontendSha = BootstrapJson.Commit(payload, "frontendSha");
        FrontendDistSha256 = RequireDigest(payload, "candidateDistSha256");
        BackendPublishSha256 = RequireDigest(payload, "currentPublishSha256");
        if (!BootstrapJson.SamePath(prepared.Path, Path.Combine(TaskRoot, "prepared.json")) ||
            BootstrapJson.Text(payload, "state") != "Prepared" ||
            BootstrapJson.Text(payload, "cutoverAdmission") != "PIN_MATCHED" ||
            !DateTimeOffset.TryParseExact(BootstrapJson.Text(payload, "expiresAtUtc"), "O", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _expiry) || _expiry <= DateTimeOffset.UtcNow)
            throw new IOException("Bootstrap prepared receipt is not admitted for a run.");

        _current = _prepared = new RunTransitionReceipt(BootstrapRunStage.Prepared, prepared.Path, prepared.Sha256);
        _lastSelectorEndedAtUtc = DateTimeOffset.UtcNow;
        prepared.Verify();
        // Reserve the prepared root before any selector can be launched. A
        // crashed or failed run cannot be reopened as a new attempt.
        ProtectedJsonFile.WriteNew(Path.Combine(TaskRoot, "run-attempt.json"), BootstrapJson.Serialize(new JObject
        {
            ["version"] = 1, ["runId"] = RunId, ["candidateId"] = CandidateId, ["preparedSha256"] = prepared.Sha256,
            ["createdAtUtc"] = Utc(_lastSelectorEndedAtUtc),
        }));
    }

    internal string RunId { get; }
    internal string CandidateId { get; }
    internal string TaskRoot { get; }
    internal string BackendSha { get; }
    internal string FrontendSha { get; }
    internal string FrontendDistSha256 { get; }
    internal string BackendPublishSha256 { get; }
    internal BootstrapRunStage Stage { get { lock (_sync) return _current.Stage; } }

    internal static BootstrapRunState Open(PreparedCandidateReceipt receipt, string runId) =>
        Open(receipt.Path, receipt.Sha256, runId);

    internal static BootstrapRunState Open(string preparedPath, string preparedSha256, string runId)
    {
        using var prepared = ProtectedJsonFile.Open(preparedPath, preparedSha256);
        return new BootstrapRunState(runId, prepared, ReadPrepared(prepared.Bytes));
    }

    internal RunTransitionReceipt? Accept(SelectorResult result, ProtectedGateReceipt receipt)
    {
        lock (_sync)
        {
            var (expected, next) = NextGate();
            if (result.Name != expected || receipt.Gate != expected || !result.PassedMandatoryTests ||
                receipt.RunId != RunId || receipt.CandidateId != CandidateId || receipt.BackendSha != BackendSha ||
                receipt.FrontendSha != FrontendSha || receipt.FrontendDistSha256 != FrontendDistSha256 ||
                (_chromium is { } first && expected == "pair-edge" &&
                    (first.Result.Filter != result.Filter || BootstrapJson.SamePath(first.Receipt.NativePath, receipt.NativePath))))
                throw new IOException("Bootstrap selector or gate receipt did not match the expected transition.");
            RequireResult(result, verifyTrx: true);
            using var prepared = OpenPrepared(requireCurrent: true);
            using var previous = ProtectedJsonFile.Open(_current.Path, _current.Sha256);
            using var envelope = OpenPrivate(receipt.PrivatePath, receipt.Sha256);
            using var native = OpenPrivate(receipt.NativePath, receipt.NativeSha256);

            if (expected == "pair-chromium")
            {
                _chromium = (result, receipt);
                _lastSelectorEndedAtUtc = result.EndedAtUtc;
                return null;
            }

            var payload = Transition(next, result, result.SanitizedDisposition);
            payload["gatePrivatePath"] = receipt.PrivatePath;
            payload["gateSha256"] = receipt.Sha256;
            payload["ownedResourcePrivatePath"] = receipt.NativePath;
            payload["ownedResourceSha256"] = receipt.NativeSha256;
            payload["workloadStatus"] = receipt.WorkloadStatus;
            payload["recoveryRpo"] = receipt.RecoveryRpo;
            payload["recoveryRtoMilliseconds"] = receipt.RecoveryRtoMilliseconds;
            if (next == BootstrapRunStage.PairPassed && _chromium is { } chromium)
            {
                using var chromiumEnvelope = OpenPrivate(chromium.Receipt.PrivatePath, chromium.Receipt.Sha256);
                using var chromiumNative = OpenPrivate(chromium.Receipt.NativePath, chromium.Receipt.NativeSha256);
                VerifyTrx(chromium.Result);
                AddChromium(payload);
                var pair = Commit(next, payload);
                _lastSelectorEndedAtUtc = result.EndedAtUtc;
                return pair;
            }

            var transition = Commit(next, payload);
            _lastSelectorEndedAtUtc = result.EndedAtUtc;
            if (next == BootstrapRunStage.WorkloadRecorded) _workloadStatus = receipt.WorkloadStatus;
            if (next == BootstrapRunStage.RecoveryRecorded)
            {
                _recoveryRpo = receipt.RecoveryRpo;
                _recoveryRtoMilliseconds = receipt.RecoveryRtoMilliseconds;
            }
            return transition;
        }
    }

    internal RunTransitionReceipt Complete(ProtectedDispositionReceipt receipt)
    {
        lock (_sync)
        {
            if (_current.Stage != BootstrapRunStage.RecoveryRecorded || _workloadStatus != "Measured" ||
                _recoveryRpo != 0 || _recoveryRtoMilliseconds is not > 0 ||
                receipt.RunId != RunId || receipt.CandidateId != CandidateId ||
                !receipt.ProcessDrained || !receipt.HttpQueuesAbsent || !receipt.SqlQuiescent || !receipt.ResourcesVerified)
                throw new IOException("Bootstrap final verification requires measured workload, recovery and verified resource disposition.");

            using var prepared = OpenPrepared(requireCurrent: true);
            using var previous = ProtectedJsonFile.Open(_current.Path, _current.Sha256);
            using var envelope = OpenPrivate(receipt.PrivatePath, receipt.Sha256);
            using var native = OpenPrivate(receipt.NativePath, receipt.NativeSha256);
            var payload = Transition(BootstrapRunStage.CandidateVerified, null, "Verified");
            payload["gatePrivatePath"] = receipt.PrivatePath;
            payload["gateSha256"] = receipt.Sha256;
            payload["ownedResourcePrivatePath"] = receipt.NativePath;
            payload["ownedResourceSha256"] = receipt.NativeSha256;
            payload["processTreeDrained"] = receipt.ProcessDrained;
            payload["httpQueuesAbsent"] = receipt.HttpQueuesAbsent;
            payload["sqlQuiescent"] = receipt.SqlQuiescent;
            payload["resourcesVerified"] = receipt.ResourcesVerified;
            payload["workloadStatus"] = _workloadStatus;
            payload["recoveryRpo"] = _recoveryRpo;
            payload["recoveryRtoMilliseconds"] = _recoveryRtoMilliseconds;
            return Commit(BootstrapRunStage.CandidateVerified, payload);
        }
    }

    internal RunTransitionReceipt Fail(SelectorResult? result = null)
    {
        lock (_sync)
        {
            if (_current.Stage is BootstrapRunStage.CandidateVerified or BootstrapRunStage.FailedRetained or BootstrapRunStage.BlockedNoAllocation)
                throw new IOException("Bootstrap run already has a terminal disposition.");
            if (result != null)
            {
                if (result.Name != NextGate().Name)
                    throw new IOException("Bootstrap failure belongs to a different selector.");
                RequireResult(result, verifyTrx: false);
            }

            using var prepared = OpenPrepared(requireCurrent: false);
            using var previous = ProtectedJsonFile.Open(_current.Path, _current.Sha256);
            var disposition = result == null || result.PassedMandatoryTests ? "EvidenceOrDispositionUnverified" : result.SanitizedDisposition;
            var payload = Transition(BootstrapRunStage.FailedRetained, result, disposition);
            payload["requiresDispositionProof"] = true;
            if (_current.Stage == BootstrapRunStage.Prepared) AddChromium(payload);
            return Commit(BootstrapRunStage.FailedRetained, payload);
        }
    }

    internal RunTransitionReceipt BlockBeforeAllocation()
    {
        lock (_sync)
        {
            if (_current.Stage != BootstrapRunStage.Prepared || _chromium != null)
                throw new IOException("Bootstrap run can no longer record a preallocation block.");
            using var prepared = OpenPrepared(requireCurrent: false);
            return Commit(BootstrapRunStage.BlockedNoAllocation,
                Transition(BootstrapRunStage.BlockedNoAllocation, null, "BlockedBeforeAllocation"));
        }
    }

    private (string Name, BootstrapRunStage Next) NextGate() => _current.Stage switch
    {
        BootstrapRunStage.Prepared when _chromium == null => ("pair-chromium", BootstrapRunStage.Prepared),
        BootstrapRunStage.Prepared => ("pair-edge", BootstrapRunStage.PairPassed),
        BootstrapRunStage.PairPassed => ("cutover", BootstrapRunStage.CutoverPassed),
        BootstrapRunStage.CutoverPassed => ("workload", BootstrapRunStage.WorkloadRecorded),
        BootstrapRunStage.WorkloadRecorded => ("recovery", BootstrapRunStage.RecoveryRecorded),
        _ => throw new IOException("Bootstrap run has no next selector."),
    };

    private void RequireResult(SelectorResult result, bool verifyTrx)
    {
        SelectorProcessRunner.RequireExactFilter(result.Filter);
        RequirePrivatePath(result.TrxPath);
        if (result.RunId != RunId || result.StartedAtUtc > result.EndedAtUtc || result.StartedAtUtc < _lastSelectorEndedAtUtc)
            throw new IOException("Bootstrap selector result is not bound to this run.");
        if (verifyTrx) VerifyTrx(result);
    }

    private static void VerifyTrx(SelectorResult result)
    {
        if (result.TrxSha256 == null || BootstrapPathPolicy.HashFile(result.TrxPath) != result.TrxSha256)
            throw new IOException("Bootstrap selector TRX digest differs.");
    }

    private ProtectedJsonFile OpenPrepared(bool requireCurrent)
    {
        if (requireCurrent && _expiry <= DateTimeOffset.UtcNow)
            throw new IOException("Bootstrap prepared receipt expired during the run.");
        return ProtectedJsonFile.Open(_prepared.Path, _prepared.Sha256);
    }

    private ProtectedJsonFile OpenPrivate(string path, string digest) => ProtectedJsonFile.Open(RequirePrivatePath(path), digest);

    private string RequirePrivatePath(string path)
    {
        path = BootstrapJson.AbsolutePath(path);
        if (!BootstrapJson.IsChild(path, TaskRoot)) throw new IOException("Bootstrap evidence is outside the prepared task root.");
        return path;
    }

    private JObject Transition(BootstrapRunStage next, SelectorResult? result, string disposition)
    {
        var now = DateTimeOffset.UtcNow;
        return new JObject
        {
            ["version"] = 1, ["state"] = next.ToString(), ["runId"] = RunId, ["candidateId"] = CandidateId,
            ["previousSha256"] = _current.Sha256, ["backendSha"] = BackendSha, ["frontendSha"] = FrontendSha,
            ["frontendDistSha256"] = FrontendDistSha256, ["backendPublishSha256"] = BackendPublishSha256,
            ["selector"] = result?.Name, ["filter"] = result?.Filter, ["exitCode"] = result?.ExitCode,
            ["discovered"] = result?.Discovered ?? 0, ["executed"] = result?.Executed ?? 0, ["passed"] = result?.Passed ?? 0,
            ["failed"] = result?.Failed ?? 0, ["skipped"] = result?.Skipped ?? 0,
            ["startedAtUtc"] = Utc(result?.StartedAtUtc ?? now), ["endedAtUtc"] = Utc(result?.EndedAtUtc ?? now),
            ["recordedAtUtc"] = Utc(now), ["disposition"] = disposition,
            ["processTreeDrained"] = result?.ProcessTreeDrained, ["requiresDispositionProof"] = result?.RequiresDispositionProof ?? false,
            ["privateTaskRoot"] = TaskRoot, ["trxPrivatePath"] = result?.TrxPath, ["trxSha256"] = result?.TrxSha256,
        };
    }

    private void AddChromium(JObject payload)
    {
        if (_chromium is not { } chromium) return;
        payload["pairChromiumPrivatePath"] = chromium.Receipt.PrivatePath;
        payload["pairChromiumSha256"] = chromium.Receipt.Sha256;
        payload["pairChromiumTrxPrivatePath"] = chromium.Result.TrxPath;
        payload["pairChromiumTrxSha256"] = chromium.Result.TrxSha256;
        payload["pairChromiumResult"] = new JObject
        {
            ["selector"] = chromium.Result.Name, ["filter"] = chromium.Result.Filter,
            ["discovered"] = chromium.Result.Discovered, ["executed"] = chromium.Result.Executed,
            ["passed"] = chromium.Result.Passed, ["failed"] = chromium.Result.Failed, ["skipped"] = chromium.Result.Skipped,
            ["startedAtUtc"] = Utc(chromium.Result.StartedAtUtc), ["endedAtUtc"] = Utc(chromium.Result.EndedAtUtc),
        };
    }

    private RunTransitionReceipt Commit(BootstrapRunStage next, JObject payload)
    {
        var path = Path.Combine(TaskRoot, "run-" + next.ToString() + ".json");
        var digest = ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(payload));
        return _current = new RunTransitionReceipt(next, path, digest);
    }

    private static string Utc(DateTimeOffset date) => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string RequireDigest(JObject obj, string key)
    {
        var digest = BootstrapJson.Text(obj, key);
        if (!BootstrapJson.IsLowerHex(digest, 64)) throw new IOException("Bootstrap prepared artifact digest is invalid.");
        return digest;
    }

    private static JObject ReadPrepared(byte[] bytes)
    {
        try
        {
            using var reader = new JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(bytes)))
            { DateParseHandling = DateParseHandling.None, MaxDepth = 8 };
            var obj = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (obj["version"] is not JValue { Type: JTokenType.Integer, Value: 1L } || !bytes.SequenceEqual(BootstrapJson.Serialize(obj)))
                throw new IOException("Bootstrap prepared receipt is not canonical.");
            return obj;
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException)
        {
            throw new IOException("Bootstrap prepared receipt syntax is invalid.");
        }
    }
}
