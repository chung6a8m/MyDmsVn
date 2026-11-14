using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class BootstrapRunExecution : IDisposable
{
    private readonly BootstrapToolchain _toolchain;
    private BootstrapToolchain.BootstrapTools? _tools;
    private BootstrapAuthority? _authority;
    private ProtectedJsonFile? _approval;
    internal BootstrapRunExecution(BootstrapToolchain toolchain) => _toolchain = toolchain;

    internal async Task PrepareAsync(Web6SelectorPlan plan, CancellationToken token)
    {
        _authority = BootstrapAuthority.ReadProtected(plan.Text("authorityPath"), plan.Text("authoritySha256"), DateTimeOffset.UtcNow);
        if (_authority.CandidateId != plan.Text("candidateId") || _authority.BackendSha != plan.Text("backendSha") ||
            _authority.FrontendSha != plan.Text("frontendSha") || _authority.OldBackendSha != plan.Text("oldBackendSha") ||
            _authority.OldFrontendSha != plan.Text("oldFrontendSha") || Path.GetDirectoryName(plan.TaskRoot) != _authority.TaskParent ||
            _authority.SqlControlParent != plan.Text("sqlControlParent") ||
            !_authority.DrillClasses.SequenceEqual(new[] { "cutover", "pair", "recovery", "workload" }) ||
            !_authority.Origins.SequenceEqual(new[] { "https://localhost:44321", "https://localhost:44322" }))
            throw new IOException("Bootstrap prepared candidate exceeds protected authority.");
        var records = (JObject)plan.Json["tools"]!;
        _tools = new BootstrapToolchain.BootstrapTools(_toolchain,
            records.Properties().ToDictionary(p => p.Name, p => BootstrapJson.AbsolutePath((string)p.Value["path"]!)), plan.Text("certificateSha256"));
        if (!JToken.DeepEquals(records, _tools.ToolRecords)) throw new IOException("Bootstrap pinned tool bytes changed.");
        _approval = ProtectedJsonFile.Open(plan.Text("sqlApprovalPath"), plan.Text("sqlApprovalSha256"));
        foreach (var gate in plan.Gates)
        {
            BootstrapDisposition.RequireAllowedResource(plan.GateRoot(gate.Name), plan.TaskRoot);
            if (File.Exists(plan.GateRoot(gate.Name)) || Directory.Exists(plan.GateRoot(gate.Name)))
                throw new IOException("Bootstrap selector root was already used.");
        }
        await VerifyAsync(plan, token).ConfigureAwait(false);
        await BuildSelectorAsync(plan, _tools, token).ConfigureAwait(false);
        await VerifyAsync(plan, token).ConfigureAwait(false);
    }

    internal static async Task BuildSelectorAsync(Web6SelectorPlan plan, BootstrapToolchain.BootstrapTools tools, CancellationToken token)
    {
        await tools.RunAsync("dotnet", plan.Text("backendWorktree"), token,
            new[] { "restore", plan.Project, "--locked-mode", "--disable-build-servers", "-p:Configuration=Release", "-p:TargetFramework=net48" }, build: true).ConfigureAwait(false);
        await tools.RunAsync("dotnet", plan.Text("backendWorktree"), token,
            new[] { "build", plan.Project, "--configuration", "Release", "--framework", "net48", "--no-restore", "--disable-build-servers", "-nr:false" }, build: true).ConfigureAwait(false);
        _ = BootstrapPathPolicy.HashFile(plan.SelectorDll);
    }

    private async Task VerifyAsync(Web6SelectorPlan plan, CancellationToken token)
    {
        using var prepared = ProtectedJsonFile.Open(plan.Prepared.Path, plan.Prepared.Sha256);
        _authority!.Verify(DateTimeOffset.UtcNow); _tools!.Verify(); _approval!.Verify();
        _toolchain.VerifyPreparedCertificate(plan.Text("tlsThumbprint"), plan.Text("certificateSha256"));
        var sql = await _toolchain.VerifySqlAsync(_approval, token).ConfigureAwait(false);
        if (sql.ServerDigest != plan.Text("sqlServerSha256")) throw new IOException("Bootstrap SQL server changed.");
        var git = new BootstrapGitWorktree(_tools);
        foreach (var prefix in new[] { "backend", "oldBackend", "frontend", "oldFrontend" })
        {
            var path = plan.Text(prefix + "Worktree");
            BootstrapDisposition.RequireAllowedResource(path, plan.TaskRoot);
            await git.VerifyAsync(new BootstrapSource(path, plan.Text(prefix + "Sha"), plan.Text(prefix + "Tree")), token).ConfigureAwait(false);
        }
        foreach (var artifact in new[] { "ordinaryDist", "candidateDist", "oldFrontendDist", "currentPublish", "oldPublish" })
        {
            var path = plan.Text(artifact);
            BootstrapDisposition.RequireAllowedResource(path, plan.TaskRoot);
            if (new BootstrapTree(path).Digest != plan.Text(artifact + "Sha256")) throw new IOException("Bootstrap artifact bytes changed.");
        }
        foreach (var mode in new[] { "ordinary", "candidate" })
            if (BootstrapPathPolicy.HashFile(Path.Combine(plan.TaskRoot, "frontend-" + mode + "-manifest.json")) != plan.Text(mode + "ManifestSha256"))
                throw new IOException("Bootstrap frontend artifact manifest changed.");
        using var build = ProtectedJsonFile.Open(plan.Text("cutoverBuildAuthorityPath"), plan.Text("cutoverBuildAuthoritySha256"));
        var expectedIis = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express", "iisexpress.exe");
        if (!BootstrapJson.SamePath(plan.Tool("iisExpress"), expectedIis))
            throw new IOException("The existing selector requires its exact reviewed IIS Express installation.");
    }

    internal async Task<SelectorResult> InvokeAsync(Web6SelectorPlan plan, SelectorInvocation invocation, CancellationToken token)
    {
        await VerifyAsync(plan, token).ConfigureAwait(false);
        return await new SelectorProcessRunner(plan.Tool("dotnet"), plan.Tool("git"), plan.Tool("git", "sha256"),
            plan.Text("backendWorktree"), plan.Project).RunAsync(invocation, token).ConfigureAwait(false);
    }

    public void Dispose() { _approval?.Dispose(); _tools?.Dispose(); _authority?.Dispose(); }
}
