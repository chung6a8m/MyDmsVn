using System;
using System.IO;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// A candidate allocator consumes a root/source admitted before any SQL mutation.
// Later checks reuse that root; they never search for a replacement checkout.
internal sealed class Web6CandidateBackendSource
{
    private const string Marker = "MyDmsVn.sln";
    private readonly Func<string, (string Root, string Sha, string Tree)> _readIdentity;
    private readonly (string Sha, string Tree) _identity;
    private Web6CandidateBackendSource(string root, (string Sha, string Tree) identity,
        Func<string, (string Root, string Sha, string Tree)> readIdentity)
    { Root = root; _identity = identity; _readIdentity = readIdentity; }

    internal string Root { get; }

    internal static string ResolveRoot(string startDirectory)
    {
        if (!Directory.Exists(startDirectory)) throw new DirectoryNotFoundException("Candidate source directory is absent.");
        for (var directory = new DirectoryInfo(startDirectory); directory != null; directory = directory.Parent)
        {
            var marker = Path.Combine(directory.FullName, Marker);
            if (File.Exists(marker)) { Web6CandidateFileSystem.RequireRegularFile(marker); return directory.FullName; }
            // A missing marker in this checkout must not adopt an ancestor repo.
            if (File.Exists(Path.Combine(directory.FullName, ".git")) || Directory.Exists(Path.Combine(directory.FullName, ".git"))) break;
        }
        throw new FileNotFoundException("Current backend requires the MyDmsVn.sln solution marker.");
    }

    internal static Web6CandidateBackendSource Admit(string startDirectory,
        Func<string, (string Root, string Sha, string Tree)> readIdentity,
        string? expectedRoot = null, string? expectedSha = null, string? expectedTree = null)
    {
        var root = ResolveRoot(startDirectory);
        if (expectedRoot != null && !SamePath(root, expectedRoot)) throw new IOException("Current backend root differs from the prepared source.");
        var observed = readIdentity(root);
        RequireIdentity(root, observed, expectedSha, expectedTree);
        return new Web6CandidateBackendSource(root, (observed.Sha, observed.Tree), readIdentity);
    }

    internal (string Sha, string Tree) Verify()
    {
        Web6CandidateFileSystem.RequireRegularFile(Path.Combine(Root, Marker));
        var observed = _readIdentity(Root);
        RequireIdentity(Root, observed, _identity.Sha, _identity.Tree);
        return _identity;
    }

    internal Task<T> AllocateAsync<T>(Func<Task<T>> allocate) { Verify(); return allocate(); }
    internal Task AllocateAsync(Func<Task> allocate) { Verify(); return allocate(); }

    private static void RequireIdentity(string root, (string Root, string Sha, string Tree) observed, string? sha, string? tree)
    {
        if (!SamePath(root, observed.Root) || (sha != null && observed.Sha != sha) || (tree != null && observed.Tree != tree))
            throw new IOException("Current backend Git root/source differs from its admitted identity.");
        Web6CandidateInputs.RequireHash(observed.Sha, 40); Web6CandidateInputs.RequireHash(observed.Tree, 40);
    }

    private static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'),
        Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
