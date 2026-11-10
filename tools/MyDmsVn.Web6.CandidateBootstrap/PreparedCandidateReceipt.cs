using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class PreparedCandidateReceipt
{
    internal const string ReviewedFrontendSha = "6db4326b17b8da1ba0d02bc089e0cea1c3ea7f34";
    private const string SelectorOldFrontendSha = "ea104582a5aa4bcdf0d04999296a30a3c43e450e";
    private const string SelectorOldBackendSha = "a3f3db16381a38bdf0b76e8ab36cb55bec02bbd2";

    private PreparedCandidateReceipt(string path, string sha256, string buildAuthorityPath, string buildAuthoritySha256, string cutoverAdmission)
    {
        Path = path;
        Sha256 = sha256;
        BuildAuthorityPath = buildAuthorityPath;
        BuildAuthoritySha256 = buildAuthoritySha256;
        CutoverAdmission = cutoverAdmission;
    }

    internal string Path { get; }
    internal string Sha256 { get; }
    internal string BuildAuthorityPath { get; }
    internal string BuildAuthoritySha256 { get; }
    internal string CutoverAdmission { get; }

    internal JObject Payload
    {
        get
        {
            using var file = ProtectedJsonFile.Open(Path, Sha256);
            return ReadCanonical(file.Bytes);
        }
    }

    internal static PreparedCandidateReceipt Read(string path, string digest)
    {
        using var file = ProtectedJsonFile.Open(path, digest);
        var json = ReadCanonical(file.Bytes);
        return new PreparedCandidateReceipt(file.Path, file.Sha256,
            (string?)json["cutoverBuildAuthorityPath"] ?? "", (string?)json["cutoverBuildAuthoritySha256"] ?? "",
            BootstrapJson.Text(json, "cutoverAdmission"));
    }

    internal static JObject ReadCanonical(byte[] bytes)
    {
        using var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(bytes)))
        { DateParseHandling = Newtonsoft.Json.DateParseHandling.None, MaxDepth = 16 };
        var json = JObject.Load(reader, new Newtonsoft.Json.Linq.JsonLoadSettings
        { DuplicatePropertyNameHandling = Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error });
        if (json["version"] is not JValue { Type: JTokenType.Integer, Value: 1L } || !System.Linq.Enumerable.SequenceEqual(bytes, BootstrapJson.Serialize(json)))
            throw new IOException("Bootstrap protected receipt is not canonical.");
        return json;
    }

    internal static PreparedCandidateReceipt Create(BootstrapRequest request, BootstrapAuthority authority,
        ProtectedJsonFile approval, string sqlServerDigest, DateTimeOffset sqlExpiry,
        BootstrapSource backend, BootstrapSource oldBackend, BootstrapSource frontend, BootstrapSource oldFrontend,
        BootstrapTree ordinary, string ordinaryManifestDigest, BootstrapTree candidate, string candidateManifestDigest,
        BootstrapTree archive, BootstrapTree currentPublish, BootstrapTree oldPublish, BootstrapToolchain.BootstrapTools tools,
        string nodeVersion, string npmVersion, string dotnetVersion, DateTimeOffset now)
    {
        var authorityPath = System.IO.Path.Combine(request.TaskRoot, "cutover-build-authority.json");
        var authorityDigest = ProtectedJsonFile.WriteNew(authorityPath, BootstrapJson.Serialize(new JObject
        {
            ["version"] = 1,
            ["frontendSha"] = oldFrontend.Sha, ["frontendTree"] = oldFrontend.Tree, ["frontendBuildSha256"] = archive.Digest,
            ["backendSha"] = oldBackend.Sha, ["backendTree"] = oldBackend.Tree, ["backendBuildSha256"] = oldPublish.Digest,
            ["currentPublishedSha256"] = currentPublish.Digest, ["oldPublishedSha256"] = oldPublish.Digest,
        }));

        var cutoverAdmission = frontend.Sha != ReviewedFrontendSha ? "BLOCKED_REVIEWED_FRONTEND_PIN" :
            oldFrontend.Sha != SelectorOldFrontendSha || oldBackend.Sha != SelectorOldBackendSha ? "BLOCKED_REVIEWED_ARCHIVE_PIN" : "PIN_MATCHED";
        var preparedPath = System.IO.Path.Combine(request.TaskRoot, "prepared.json");
        var expiry = authority.ExpiresAtUtc < sqlExpiry ? authority.ExpiresAtUtc : sqlExpiry;
        var payload = BootstrapJson.Serialize(new JObject
        {
            ["version"] = 1,
            ["state"] = "Prepared",
            ["cutoverAdmission"] = cutoverAdmission,
            ["candidateId"] = request.CandidateId,
            ["taskRoot"] = request.TaskRoot,
            ["sqlControlParent"] = request.SqlControlParent,
            ["origins"] = new JArray(request.Origins),
            ["drillClasses"] = new JArray(request.DrillClasses),
            ["browserProjects"] = new JArray(request.BrowserProjects),
            ["actorCohort"] = request.ActorCohort,
            ["workloadSeconds"] = request.WorkloadSeconds,
            ["recoveryAuthorized"] = request.RecoveryAuthorized,
            ["authorityPath"] = authority.Path, ["authoritySha256"] = authority.Sha256,
            ["sqlApprovalPath"] = approval.Path, ["sqlApprovalSha256"] = approval.Sha256, ["sqlServerSha256"] = sqlServerDigest,
            ["preparedAtUtc"] = now.ToString("O", CultureInfo.InvariantCulture), ["expiresAtUtc"] = expiry.ToString("O", CultureInfo.InvariantCulture),
            ["tlsThumbprint"] = request.TlsThumbprint, ["certificateSha256"] = tools.CertificateDigest,
            ["nodeVersion"] = nodeVersion, ["npmVersion"] = npmVersion, ["dotnetVersion"] = dotnetVersion,
            ["tools"] = tools.ToolRecords,
            ["backendSha"] = backend.Sha, ["backendTree"] = backend.Tree, ["backendWorktree"] = backend.Path,
            ["oldBackendSha"] = oldBackend.Sha, ["oldBackendTree"] = oldBackend.Tree, ["oldBackendWorktree"] = oldBackend.Path,
            ["frontendSha"] = frontend.Sha, ["frontendTree"] = frontend.Tree, ["frontendWorktree"] = frontend.Path,
            ["oldFrontendSha"] = oldFrontend.Sha, ["oldFrontendTree"] = oldFrontend.Tree, ["oldFrontendWorktree"] = oldFrontend.Path,
            ["ordinaryDist"] = ordinary.Root, ["ordinaryDistSha256"] = ordinary.Digest, ["ordinaryManifestSha256"] = ordinaryManifestDigest,
            ["candidateDist"] = candidate.Root, ["candidateDistSha256"] = candidate.Digest, ["candidateManifestSha256"] = candidateManifestDigest,
            ["oldFrontendDist"] = archive.Root, ["oldFrontendDistSha256"] = archive.Digest,
            ["currentPublish"] = currentPublish.Root, ["currentPublishSha256"] = currentPublish.Digest,
            ["oldPublish"] = oldPublish.Root, ["oldPublishSha256"] = oldPublish.Digest,
            ["cutoverBuildAuthorityPath"] = authorityPath, ["cutoverBuildAuthoritySha256"] = authorityDigest,
            ["pairChromiumRoot"] = System.IO.Path.Combine(request.TaskRoot, "pair-chromium"),
            ["pairEdgeRoot"] = System.IO.Path.Combine(request.TaskRoot, "pair-edge"),
            ["cutoverRoot"] = System.IO.Path.Combine(request.TaskRoot, "cutover"),
            ["workloadRoot"] = System.IO.Path.Combine(request.TaskRoot, "workload"),
            ["recoveryRoot"] = System.IO.Path.Combine(request.TaskRoot, "recovery"),
        });
        var digest = ProtectedJsonFile.Digest(payload);
        ProtectedJsonFile.WriteNew(System.IO.Path.Combine(request.TaskRoot, "prepared.sha256"), Encoding.ASCII.GetBytes(digest));
        if (ProtectedJsonFile.WriteNew(preparedPath, payload) != digest)
            throw new IOException("Bootstrap prepared receipt digest differs from its detached hash.");
        return new PreparedCandidateReceipt(preparedPath, digest, authorityPath, authorityDigest, cutoverAdmission);
    }
}
