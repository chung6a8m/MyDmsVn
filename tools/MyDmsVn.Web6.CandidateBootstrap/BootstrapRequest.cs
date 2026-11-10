using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class BootstrapRequest
{
    private static readonly string[] Keys =
    {
        "version", "candidateId", "backendRepository", "frontendRepository", "taskRoot", "sqlControlParent",
        "sqlApprovalFile", "tlsThumbprint", "nodePath", "iisExpressPath", "browserProjects", "actorCohort",
        "workloadSeconds", "recoveryAuthorized", "authorityPath", "authoritySha256",
    };
    private static readonly IReadOnlyList<string> RequiredBrowserProjects = Array.AsReadOnly(new[] { "chromium", "edge" });

    private BootstrapRequest(JObject value, BootstrapAuthority authority)
    {
        CandidateId = BootstrapJson.CandidateId(value);
        BackendRepository = ReadPath(value, "backendRepository");
        FrontendRepository = ReadPath(value, "frontendRepository");
        TaskRoot = ReadPath(value, "taskRoot");
        SqlControlParent = ReadPath(value, "sqlControlParent");
        SqlApprovalFile = ReadPath(value, "sqlApprovalFile");
        NodePath = ReadPath(value, "nodePath");
        IisExpressPath = ReadPath(value, "iisExpressPath");
        AuthorityPath = ReadPath(value, "authorityPath");
        AuthoritySha256 = BootstrapJson.Text(value, "authoritySha256");
        // Preserve the authorized spelling so a case-sensitive Windows directory
        // cannot resolve a case variant outside the authority boundary.
        if (CandidateId != authority.CandidateId || !TaskRoot.StartsWith(authority.TaskParent + "\\", StringComparison.Ordinal) ||
            SqlControlParent != authority.SqlControlParent || AuthorityPath != authority.Path || AuthoritySha256 != authority.Sha256)
            throw new IOException("Bootstrap request exceeds the protected Owner authority.");
        BootstrapJson.RequireSeparateRoots(BackendRepository, FrontendRepository, TaskRoot, SqlControlParent);
        if (BootstrapJson.SamePath(SqlApprovalFile, AuthorityPath))
            throw new IOException("Bootstrap requires a separate SQL approval file.");

        var thumbprint = BootstrapJson.Text(value, "tlsThumbprint");
        if (!BootstrapJson.IsLowerHex(thumbprint.ToLowerInvariant(), 40))
            throw new IOException("Bootstrap TLS thumbprint is invalid.");
        TlsThumbprint = thumbprint.ToUpperInvariant();

        var projects = BootstrapJson.Strings(value, "browserProjects");
        if (!projects.OrderBy(project => project, StringComparer.Ordinal).SequenceEqual(RequiredBrowserProjects))
            throw new IOException("Bootstrap requires the exact Chromium and Edge browser projects.");
        BrowserProjects = RequiredBrowserProjects;

        ActorCohort = BootstrapJson.Text(value, "actorCohort");
        if (ActorCohort != "web.smoke" ||
            value["workloadSeconds"] is not JValue { Type: JTokenType.Integer, Value: 60L })
            throw new IOException("Bootstrap workload cohort or budget differs from the exact selector.");
        WorkloadSeconds = 60;
        if (value["recoveryAuthorized"] is not JValue { Type: JTokenType.Boolean, Value: bool recoveryAuthorized } ||
            (recoveryAuthorized && !authority.DrillClasses.Contains("recovery")))
            throw new IOException("Bootstrap recovery selection exceeds the protected Owner authority.");
        RecoveryAuthorized = recoveryAuthorized;

        BackendSha = authority.BackendSha;
        FrontendSha = authority.FrontendSha;
        OldBackendSha = authority.OldBackendSha;
        OldFrontendSha = authority.OldFrontendSha;
        Origins = authority.Origins;
        DrillClasses = authority.DrillClasses;
    }

    internal string CandidateId { get; }
    internal string BackendRepository { get; }
    internal string FrontendRepository { get; }
    internal string TaskRoot { get; }
    internal string SqlControlParent { get; }
    internal string SqlApprovalFile { get; }
    internal string TlsThumbprint { get; }
    internal string NodePath { get; }
    internal string IisExpressPath { get; }
    internal IReadOnlyList<string> BrowserProjects { get; }
    internal string ActorCohort { get; }
    internal int WorkloadSeconds { get; }
    internal bool RecoveryAuthorized { get; }
    internal string AuthorityPath { get; }
    internal string AuthoritySha256 { get; }
    internal string BackendSha { get; }
    internal string FrontendSha { get; }
    internal string OldBackendSha { get; }
    internal string OldFrontendSha { get; }
    internal IReadOnlyList<string> Origins { get; }
    internal IReadOnlyList<string> DrillClasses { get; }

    internal static BootstrapRequest ReadStrict(string path, BootstrapAuthority authority)
    {
        var request = new BootstrapRequest(BootstrapJson.Parse(ProtectedJsonFile.ReadInput(path), Keys), authority);
        authority.Verify(DateTimeOffset.UtcNow);
        return request;
    }

    private static string ReadPath(JObject value, string key) => BootstrapJson.AbsolutePath(BootstrapJson.Text(value, key));
}
