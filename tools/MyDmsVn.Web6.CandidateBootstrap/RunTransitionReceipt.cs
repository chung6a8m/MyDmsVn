using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal enum BootstrapRunStage
{
    Requested,
    Prepared,
    PairPassed,
    CutoverPassed,
    WorkloadRecorded,
    RecoveryRecorded,
    CandidateVerified,
    FailedRetained,
    BlockedNoAllocation,
}

internal sealed class RunTransitionReceipt
{
    internal RunTransitionReceipt(BootstrapRunStage stage, string path, string sha256)
    {
        Stage = stage;
        Path = path;
        Sha256 = sha256;
    }

    internal BootstrapRunStage Stage { get; }
    internal string Path { get; }
    internal string Sha256 { get; }
}

// Task 5 adapts the selector-native evidence after validating its gate-specific
// schema. This envelope binds the validated private evidence to one run.
internal sealed class ProtectedGateReceipt
{
    private ProtectedGateReceipt(string gate, string runId, string candidateId, string backendSha, string frontendSha,
        string frontendDistSha256, string privatePath, string sha256, string nativePath, string nativeSha256,
        string? workloadStatus, long? recoveryRpo, long? recoveryRtoMilliseconds)
    {
        Gate = gate;
        RunId = runId;
        CandidateId = candidateId;
        BackendSha = backendSha;
        FrontendSha = frontendSha;
        FrontendDistSha256 = frontendDistSha256;
        PrivatePath = privatePath;
        Sha256 = sha256;
        NativePath = nativePath;
        NativeSha256 = nativeSha256;
        WorkloadStatus = workloadStatus;
        RecoveryRpo = recoveryRpo;
        RecoveryRtoMilliseconds = recoveryRtoMilliseconds;
    }

    internal string Gate { get; }
    internal string RunId { get; }
    internal string CandidateId { get; }
    internal string BackendSha { get; }
    internal string FrontendSha { get; }
    internal string FrontendDistSha256 { get; }
    internal string PrivatePath { get; }
    internal string Sha256 { get; }
    internal string NativePath { get; }
    internal string NativeSha256 { get; }
    internal string? WorkloadStatus { get; }
    internal long? RecoveryRpo { get; }
    internal long? RecoveryRtoMilliseconds { get; }

    internal static ProtectedGateReceipt Write(string path, string gate, string runId, string candidateId, string backendSha,
        string frontendSha, string frontendDistSha256, string nativePath, string nativeSha256,
        string? workloadStatus = null, long? recoveryRpo = null, long? recoveryRtoMilliseconds = null)
    {
        if ((gate == "workload" ? workloadStatus is not ("Measured" or "InsufficientSample" or "NotMeasured") : workloadStatus != null) ||
            (gate == "recovery" ? recoveryRpo is < 0 || recoveryRtoMilliseconds is <= 0 : recoveryRpo.HasValue || recoveryRtoMilliseconds.HasValue))
            throw new IOException("Bootstrap gate measurements are invalid for this gate.");
        var payload = new JObject
        {
            ["version"] = 1, ["gate"] = gate, ["runId"] = runId, ["candidateId"] = candidateId,
            ["backendSha"] = backendSha, ["frontendSha"] = frontendSha, ["frontendDistSha256"] = frontendDistSha256,
            ["nativePath"] = nativePath, ["nativeSha256"] = nativeSha256,
            ["workloadStatus"] = workloadStatus, ["recoveryRpo"] = recoveryRpo, ["recoveryRtoMilliseconds"] = recoveryRtoMilliseconds,
        };
        var digest = ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(payload));
        return new ProtectedGateReceipt(gate, runId, candidateId, backendSha, frontendSha, frontendDistSha256,
            path, digest, nativePath, nativeSha256, workloadStatus, recoveryRpo, recoveryRtoMilliseconds);
    }
}

internal sealed class ProtectedDispositionReceipt
{
    private ProtectedDispositionReceipt(string privatePath, string sha256, string runId, string candidateId,
        string nativePath, string nativeSha256, bool processDrained, bool httpQueuesAbsent, bool sqlQuiescent, bool resourcesVerified)
    {
        PrivatePath = privatePath;
        Sha256 = sha256;
        RunId = runId;
        CandidateId = candidateId;
        NativePath = nativePath;
        NativeSha256 = nativeSha256;
        ProcessDrained = processDrained;
        HttpQueuesAbsent = httpQueuesAbsent;
        SqlQuiescent = sqlQuiescent;
        ResourcesVerified = resourcesVerified;
    }

    internal string PrivatePath { get; }
    internal string Sha256 { get; }
    internal string RunId { get; }
    internal string CandidateId { get; }
    internal string NativePath { get; }
    internal string NativeSha256 { get; }
    internal bool ProcessDrained { get; }
    internal bool HttpQueuesAbsent { get; }
    internal bool SqlQuiescent { get; }
    internal bool ResourcesVerified { get; }

    internal static ProtectedDispositionReceipt Write(string path, string runId, string candidateId, string nativePath,
        string nativeSha256, bool processDrained, bool httpQueuesAbsent, bool sqlQuiescent, bool resourcesVerified)
    {
        var payload = new JObject
        {
            ["version"] = 1, ["runId"] = runId, ["candidateId"] = candidateId,
            ["nativePath"] = nativePath, ["nativeSha256"] = nativeSha256,
            ["processDrained"] = processDrained, ["httpQueuesAbsent"] = httpQueuesAbsent,
            ["sqlQuiescent"] = sqlQuiescent, ["resourcesVerified"] = resourcesVerified,
        };
        var digest = ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(payload));
        return new ProtectedDispositionReceipt(path, digest, runId, candidateId, nativePath, nativeSha256,
            processDrained, httpQueuesAbsent, sqlQuiescent, resourcesVerified);
    }
}
