using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class BootstrapPreflight
{
    private readonly BootstrapToolchain _toolchain;

    internal BootstrapPreflight(BootstrapToolchain toolchain) => _toolchain = toolchain;

    internal async Task<PreparedCandidateReceipt> PrepareAsync(BootstrapRequest request, CancellationToken token)
    {
        try
        {
            using var authority = BootstrapAuthority.ReadProtected(request.AuthorityPath, request.AuthoritySha256, DateTimeOffset.UtcNow);
            using var paths = new BootstrapPathPolicy(request, authority);
            if (!request.Origins.SequenceEqual(new[] { "https://localhost:44321", "https://localhost:44322" }))
                throw new IOException("Bootstrap authority must cover the existing pair and cutover origins.");
            using var tools = _toolchain.Bind(request);
            using var approval = ProtectedJsonFile.OpenSqlApproval(request.SqlApprovalFile);
            var (serverDigest, sqlExpiry) = await _toolchain.VerifySqlAsync(approval, token).ConfigureAwait(false);
            var git = new BootstrapGitWorktree(tools);
            await git.RequireCleanRepositoryAsync(request.BackendRepository, token).ConfigureAwait(false);
            await git.RequireCleanRepositoryAsync(request.FrontendRepository, token).ConfigureAwait(false);
            var nodeVersion = await tools.RunAsync("node", request.FrontendRepository, token, new[] { "--version" }).ConfigureAwait(false);
            var npmVersion = await tools.NpmAsync(request.FrontendRepository, token, new[] { "--version" }).ConfigureAwait(false);
            if (!nodeVersion.StartsWith("v", StringComparison.Ordinal) || !Version.TryParse(nodeVersion.Substring(1), out var node) ||
                node.Major != 22 || node < new Version(22, 14, 0) || npmVersion != "11.6.0")
                throw new IOException("Bootstrap Node or npm version differs from the pinned frontend toolchain.");

            token.ThrowIfCancellationRequested();
            paths.Verify();
            paths.CreateTaskRoot();
            var backend = await git.CreateAsync(request.BackendRepository, paths.CreateChild("backend-current"), request.BackendSha, token).ConfigureAwait(false);
            var oldBackend = await git.CreateAsync(request.BackendRepository, paths.CreateChild("backend-old"), request.OldBackendSha, token).ConfigureAwait(false);
            var frontend = await git.CreateAsync(request.FrontendRepository, paths.CreateChild("frontend-current"), request.FrontendSha, token).ConfigureAwait(false);
            var ordinaryFrontend = await git.CreateAsync(request.FrontendRepository, paths.CreateChild("frontend-ordinary"), request.FrontendSha, token).ConfigureAwait(false);
            var oldFrontend = await git.CreateAsync(request.FrontendRepository, paths.CreateChild("frontend-old"), request.OldFrontendSha, token).ConfigureAwait(false);
            var dotnetVersion = await tools.RunAsync("dotnet", backend.Path, token, new[] { "--version" }).ConfigureAwait(false);
            if (!Version.TryParse(dotnetVersion, out var sdk) || sdk.Major != 8 || sdk < new Version(8, 0, 400))
                throw new IOException("Bootstrap backend SDK differs from the pinned net48 build toolchain.");

            var builder = new BootstrapArtifactBuilder(tools);
            await builder.RestoreFrontendAsync(frontend, token).ConfigureAwait(false);
            await tools.ValidateBrowsersAsync(frontend.Path, token).ConfigureAwait(false);
            await builder.RestoreFrontendAsync(ordinaryFrontend, token).ConfigureAwait(false);
            var ordinaryManifest = Path.Combine(request.TaskRoot, "frontend-ordinary-manifest.json");
            var ordinary = await builder.BuildFrontendAsync(ordinaryFrontend, null, ordinaryManifest, token).ConfigureAwait(false);
            var candidateManifest = Path.Combine(request.TaskRoot, "frontend-candidate-manifest.json");
            var candidate = await builder.BuildFrontendAsync(frontend, request.CandidateId, candidateManifest, token).ConfigureAwait(false);
            await builder.RestoreFrontendAsync(oldFrontend, token).ConfigureAwait(false);
            var archive = await builder.BuildFrontendAsync(oldFrontend, null, null, token).ConfigureAwait(false);
            var currentPublish = await builder.BuildBackendAsync(backend, paths.CreateChild("current-publish"), candidate.Tree, token).ConfigureAwait(false);
            var oldPublish = await builder.BuildBackendAsync(oldBackend, paths.CreateChild("old-publish"), null, token).ConfigureAwait(false);

            foreach (var source in new[] { backend, oldBackend, frontend, ordinaryFrontend, oldFrontend })
                await git.VerifyAsync(source, token).ConfigureAwait(false);
            foreach (var tree in new[] { currentPublish, oldPublish, ordinary.Tree, candidate.Tree, archive.Tree }) tree.Verify();
            if (new BootstrapTree(Path.Combine(currentPublish.Root, "wwwroot")).Digest != candidate.Tree.Digest ||
                BootstrapPathPolicy.HashFile(ordinaryManifest) != ordinary.ManifestDigest ||
                BootstrapPathPolicy.HashFile(candidateManifest) != candidate.ManifestDigest)
                throw new IOException("Bootstrap final artifact or manifest digest differs.");

            paths.Verify();
            paths.VerifyRunRootsAreNew();
            tools.Verify();
            _toolchain.VerifyCertificate(request, tools.CertificateDigest);
            approval.Verify();
            var now = DateTimeOffset.UtcNow;
            authority.Verify(now);
            if (sqlExpiry <= now) throw new IOException("Bootstrap SQL approval expired while preparing artifacts.");
            token.ThrowIfCancellationRequested();
            return PreparedCandidateReceipt.Create(request, authority, approval, serverDigest, sqlExpiry,
                backend, oldBackend, frontend, oldFrontend, ordinary.Tree, ordinary.ManifestDigest,
                candidate.Tree, candidate.ManifestDigest, archive.Tree, currentPublish, oldPublish,
                tools, nodeVersion, npmVersion, dotnetVersion, now);
        }
        catch (Exception error) when (error is SqlException or JsonException or CryptographicException or
            SecurityException or Win32Exception or ArgumentException or InvalidOperationException or TimeoutException ||
            (error is OperationCanceledException && !token.IsCancellationRequested))
        {
            // Provider/process errors can contain connection details and private paths.
            throw new IOException("Bootstrap preflight prerequisite could not be verified.");
        }
    }
}
