using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class BootstrapGitWorktree
{
    private readonly BootstrapToolchain.BootstrapTools _tools;

    internal BootstrapGitWorktree(BootstrapToolchain.BootstrapTools tools) => _tools = tools;

    internal async Task RequireCleanRepositoryAsync(string root, CancellationToken token)
    {
        var gitRoot = (await GitAsync(root, token, "rev-parse", "--show-toplevel").ConfigureAwait(false)).Replace('/', '\\');
        if (!BootstrapJson.SamePath(root, gitRoot)) throw new IOException("Bootstrap source path is not the repository root.");
        var settings = await GitAsync(root, token, "config", "--no-includes", "--list", "--name-only").ConfigureAwait(false);
        if (settings.Split('\n').Any(name => name.Trim().StartsWith("filter.", StringComparison.OrdinalIgnoreCase) ||
            name.Trim().StartsWith("include", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Bootstrap repository has external checkout configuration.");
        var status = await GitAsync(root, token, "status", "--porcelain", "--untracked-files=all", "--ignore-submodules=none").ConfigureAwait(false);
        if (status.Length != 0) throw new IOException("Bootstrap source checkout is dirty.");
    }

    internal async Task<BootstrapSource> CreateAsync(string repository, string worktree, string sha, CancellationToken token)
    {
        await GitAsync(repository, token, "worktree", "add", "--detach", worktree, sha).ConfigureAwait(false);
        return await MeasureAsync(worktree, sha, token).ConfigureAwait(false);
    }

    internal async Task<BootstrapSource> MeasureAsync(string root, string expectedSha, CancellationToken token)
    {
        await RequireCleanRepositoryAsync(root, token).ConfigureAwait(false);
        var branch = await GitAsync(root, token, "rev-parse", "--abbrev-ref", "HEAD").ConfigureAwait(false);
        var sha = await GitAsync(root, token, "rev-parse", "--verify", "HEAD").ConfigureAwait(false);
        var tree = await GitAsync(root, token, "rev-parse", "--verify", "HEAD^{tree}").ConfigureAwait(false);
        if (branch != "HEAD" || sha != expectedSha || !BootstrapJson.IsLowerHex(tree, 40))
            throw new IOException("Bootstrap detached worktree identity differs from the approved commit.");
        return new BootstrapSource(root, sha, tree);
    }

    internal async Task VerifyAsync(BootstrapSource source, CancellationToken token)
    {
        var current = await MeasureAsync(source.Path, source.Sha, token).ConfigureAwait(false);
        if (current.Tree != source.Tree) throw new IOException("Bootstrap Git source tree changed.");
    }

    private Task<string> GitAsync(string root, CancellationToken token, params string[] arguments) => _tools.RunAsync("git", root, token,
        new[] { "--no-optional-locks", "-c", "core.hooksPath=NUL", "-c", "core.fsmonitor=false", "-c", "core.ignoreStat=false",
            "-c", "core.trustctime=true", "-c", "core.checkStat=default", "-c", "core.excludesFile=NUL", "-c", "submodule.recurse=false" }
            .Concat(arguments).ToArray());
}

internal sealed class BootstrapSource
{
    internal BootstrapSource(string path, string sha, string tree) { Path = path; Sha = sha; Tree = tree; }
    internal string Path { get; }
    internal string Sha { get; }
    internal string Tree { get; }
}
