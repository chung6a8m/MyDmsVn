using System;
using System.IO;
using System.Threading.Tasks;
using MyDmsVn.Web6.CandidateBootstrap;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class Web6CutoverSelector
{
    internal static readonly System.Collections.Generic.IReadOnlyList<Web6CutoverPair> Pairs =
        Array.AsReadOnly(new[] { Web6CutoverPair.Current, Web6CutoverPair.Old, Web6CutoverPair.Restored });
    private readonly Web6CutoverBuildAuthority _authority;
    private readonly string _frontendRoot, _backendRoot, _archivedDist, _oldPublish, _currentPublish;
    private readonly string _publishHash, _pairHash;
    private readonly Func<string, (string Root, string Sha, string Tree)> _readGit;

    internal Web6CutoverSelector(Web6CutoverBuildAuthority authority, string frontendRoot, string backendRoot,
        string archivedDist, string oldPublish, string currentPublish, string publishHash, string pairHash,
        Func<string, (string Root, string Sha, string Tree)> readGit)
    {
        _authority = authority;
        _frontendRoot = frontendRoot;
        _backendRoot = backendRoot;
        _archivedDist = archivedDist;
        _oldPublish = oldPublish;
        _currentPublish = currentPublish;
        _publishHash = publishHash;
        _pairHash = pairHash;
        _readGit = readGit;
    }

    internal async Task AllocateAsync<T>(Action verifyCurrentBackend, Func<Web6CutoverPair, Task<T>> allocate,
        Func<Web6CutoverPair, T, Task> seal, Action<Web6CutoverPair>? prepare = null)
    {
        foreach (var pair in Pairs)
        {
            prepare?.Invoke(pair);
            using (OpenVerifiedArtifacts(verifyCurrentBackend))
                await seal(pair, await allocate(pair).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }

    internal static async Task CreateLoginsAsync(Func<Web6CutoverPair, Task> create)
    {
        foreach (var pair in Pairs)
            await create(pair).ConfigureAwait(false);
    }

    internal Web6CutoverBuildObservation Verify()
    {
        using var artifacts = OpenVerifiedArtifacts();
        return artifacts.Observation;
    }

    private Artifacts OpenVerifiedArtifacts(Action? verifyCurrentBackend = null)
    {
        var artifacts = new Artifacts();
        try
        {
            artifacts.FrontendRoot = new BootstrapPathPolicy.PinnedDirectory(_frontendRoot);
            artifacts.BackendRoot = new BootstrapPathPolicy.PinnedDirectory(_backendRoot);
            artifacts.ArchivedDist = new Web6CutoverArtifactLease(_archivedDist);
            artifacts.OldPublish = new Web6CutoverArtifactLease(_oldPublish);
            artifacts.CurrentPublish = new Web6CutoverArtifactLease(_currentPublish);
            artifacts.Observation = Observe(artifacts.ArchivedDist, artifacts.OldPublish, artifacts.CurrentPublish);
            verifyCurrentBackend?.Invoke();
            artifacts.FrontendRoot.Verify();
            artifacts.BackendRoot.Verify();
            artifacts.ArchivedDist.Verify();
            artifacts.OldPublish.Verify();
            artifacts.CurrentPublish.Verify();
            return artifacts;
        }
        catch
        {
            artifacts.Dispose();
            throw;
        }
    }

    private sealed class Artifacts : IDisposable
    {
        internal BootstrapPathPolicy.PinnedDirectory FrontendRoot { get; set; } = null!;
        internal BootstrapPathPolicy.PinnedDirectory BackendRoot { get; set; } = null!;
        internal Web6CutoverArtifactLease ArchivedDist { get; set; } = null!;
        internal Web6CutoverArtifactLease OldPublish { get; set; } = null!;
        internal Web6CutoverArtifactLease CurrentPublish { get; set; } = null!;
        internal Web6CutoverBuildObservation Observation { get; set; } = null!;

        public void Dispose()
        {
            CurrentPublish?.Dispose();
            OldPublish?.Dispose();
            ArchivedDist?.Dispose();
            BackendRoot?.Dispose();
            FrontendRoot?.Dispose();
        }
    }

    private Web6CutoverBuildObservation Observe(Web6CutoverArtifactLease archivedDist,
        Web6CutoverArtifactLease oldPublish, Web6CutoverArtifactLease currentPublish)
    {
        var frontend = ReadExactGit(_frontendRoot);
        var backend = ReadExactGit(_backendRoot);
        var oldDigest = oldPublish.Digest();
        var observation = new Web6CutoverBuildObservation(frontend.Sha, frontend.Tree, true, archivedDist.Digest(),
            backend.Sha, backend.Tree, true, oldDigest, currentPublish.Digest(), oldDigest);
        _authority.Verify(observation);
        if (observation.CurrentPublishedHash != _publishHash || currentPublish.Digest("wwwroot/") != _pairHash)
            throw new IOException("Cutover publication does not match the paired backend and frontend.");
        return observation;
    }

    private (string Sha, string Tree) ReadExactGit(string root)
    {
        var identity = _readGit(root);
        if (!BootstrapJson.SamePath(root, identity.Root.Replace('/', '\\')))
            throw new IOException("Cutover archive path is not the exact Git checkout root.");
        return (identity.Sha, identity.Tree);
    }

    internal (string FrontendSource, string Hash) Compose(Web6PrivateDirectoryLease runtime, bool cached, bool old)
    {
        using var artifacts = OpenVerifiedArtifacts();
        var backend = old ? artifacts.OldPublish : artifacts.CurrentPublish;
        var frontend = cached ? artifacts.ArchivedDist : artifacts.CurrentPublish;
        var frontendPrefix = cached ? "" : "wwwroot/";
        runtime.Verify();
        var destination = Path.Combine(runtime.Path, "host");
        var acl = SqlTestPrivateRecord.DirectoryAcl(ProtectedJsonFile.CurrentOwner());
        using var target = ProtectedJsonFile.CreateChildDirectory(runtime.RetainedHandle, destination, acl);
        backend.CopyFiles(destination, target, omitPrefix: "wwwroot/");
        var wwwroot = Path.Combine(destination, "wwwroot");
        using var frontendTarget = ProtectedJsonFile.CreateChildDirectory(target, wwwroot, acl);
        frontend.CopyFiles(wwwroot, frontendTarget, frontendPrefix);
        ProtectedJsonFile.Inspect(target, destination, directory: true);
        ProtectedJsonFile.Inspect(frontendTarget, wwwroot, directory: true);
        runtime.Verify();
        var digest = backend.DeploymentDigest(frontend, frontendPrefix);
        if (Web6CandidateFileSet.MeasureTree(destination) != digest)
            throw new IOException("Cutover deployment differs from its sealed inputs.");
        return (cached ? _archivedDist : Path.Combine(_currentPublish, "wwwroot"), digest);
    }
}
