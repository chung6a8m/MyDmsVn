using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CutoverCanonicalPathTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "w6-canonical-" + Guid.NewGuid().ToString("N"));
    private string Backend => Path.Combine(_root, "backend");
    private string Frontend => Path.Combine(_root, "frontend");
    private string Runtime => Path.Combine(_root, "runtime");
    private string Host => Path.Combine(Runtime, "host");
    private string Configuration => Path.Combine(Runtime, "applicationhost.config");
    private string Dist => Path.Combine(Frontend, "dist");
    private string Published => Path.Combine(Host, "wwwroot");
    private static readonly string[] Contracts = { "web4-v1", "web5-resolution-v1", "web6-resend-eligibility-v1" };
    private static readonly string[] ContractKeys = { "web4ContractSha256", "web5ContractSha256", "web6ContractSha256" };
    private static readonly string[] Locks = { Path.Combine("src", "packages.lock.json"), "packages.lock.json" };

    public Web6CutoverCanonicalPathTests()
    {
        foreach (var contract in Contracts)
            Write(Path.Combine(Backend, "docs", "contracts", contract, "fixture.json"), contract);
        foreach (var file in Locks) Write(Path.Combine(Backend, file), file);
        Write(Path.Combine(Frontend, "package-lock.json"), "frontend-lock");
        Write(Path.Combine(Host, "bin", "MyDmsVn.Server.HttpHost.dll"), "host-dll");
        Write(Path.Combine(Host, "Web.config"), "web-config");
        Write(Configuration, "iis-config");
        Write(Path.Combine(Dist, "assets", "app.js"), "candidate");
        Write(Path.Combine(Published, "assets", "app.js"), "candidate");
    }

    [Fact]
    public void Published_candidate_seal_accepts_the_exact_native_paths_and_hashes()
    {
        var control = Path.Combine(_root, "record");
        var inputs = new Web6CandidateInputs("fixture-01", (new string('1', 40), new string('2', 40)),
            (new string('3', 40), new string('4', 40)), new string('5', 64), new Uri("https://localhost:44321/"),
            new string('6', 64), "Web6CutoverPreflight", 600, 60, "web6-cutover-live.spec.ts");
        using var manifest = Web6CandidateManifest.CreateNew(control, inputs);
        using (var identity = WindowsIdentity.GetCurrent())
        using (new FileStream(manifest.AuditPath, FileMode.CreateNew, FileSystemRights.FullControl, FileShare.Read,
            4096, FileOptions.WriteThrough, SqlTestPrivateRecord.FileAcl(identity.User!))) { }

        var files = Web6CandidateFileSet.ForPublishedHost(Host, Configuration, Backend, Locks, Frontend, Dist);
        manifest.SealArtifacts(files, new string('8', 64));
        var sealedText = File.ReadAllText(Path.Combine(control, "candidate.manifest"));
        var expected = new Web6CandidateFileSet(Path.Combine(Host, "bin", "MyDmsVn.Server.HttpHost.dll"),
            Path.Combine(Host, "Web.config"), Configuration, Backend, Locks, Path.Combine(Frontend, "package-lock.json"),
            Path.Combine(Backend, "docs", "contracts", Contracts[0]), Path.Combine(Backend, "docs", "contracts", Contracts[1]),
            Path.Combine(Backend, "docs", "contracts", Contracts[2]), Dist, Published);
        Assert.Contains(expected.Measure(), sealedText, StringComparison.Ordinal);
        for (var i = 0; i < Contracts.Length; i++)
        {
            var tree = Path.Combine(Backend, "docs", "contracts", Contracts[i]);
            AssertCanonical(tree, Backend, directory: true);
            Assert.Contains(ContractKeys[i] + "=" + Web6CandidateFileSet.MeasureTree(tree) + "\n", sealedText, StringComparison.Ordinal);
        }
        AssertCanonical(Path.Combine(Host, "bin", "MyDmsVn.Server.HttpHost.dll"), Host, directory: false);
        AssertCanonical(Path.Combine(Host, "Web.config"), Host, directory: false);
        AssertCanonical(Configuration, Runtime, directory: false);
        AssertCanonical(Path.Combine(Frontend, "package-lock.json"), Frontend, directory: false);
        foreach (var path in Locks) AssertCanonical(Path.Combine(Backend, path), Backend, directory: false);
        AssertCanonical(Dist, Frontend, directory: true);
        AssertCanonical(Published, Host, directory: true);
    }

    [Theory]
    [InlineData("embedded-alternate-separator")]
    [InlineData("embedded-empty-segment")]
    [InlineData("embedded-dot-segment")]
    [InlineData("traversal")]
    [InlineData("rooted")]
    public void Published_candidate_seal_refuses_noncanonical_or_uncontained_lock_paths(string change)
    {
        Write(Path.Combine(_root, "packages.lock.json"), "outside");
        var path = change switch
        {
            "embedded-alternate-separator" => "src/packages.lock.json",
            "embedded-empty-segment" => @"src\\packages.lock.json",
            "embedded-dot-segment" => @"src\.\packages.lock.json",
            "traversal" => @"..\packages.lock.json",
            _ => Path.Combine(_root, "packages.lock.json"),
        };
        Assert.Throws<IOException>(() => Web6CandidateFileSet.ForPublishedHost(
            Host, Configuration, Backend, new[] { path }, Frontend, Dist));
    }

    private static void AssertCanonical(string path, string root, bool directory)
    {
        Assert.Equal(Path.GetFullPath(path), path);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar, path);
        Assert.InRange(path.Length, 4, 240);
        Assert.StartsWith(root + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(path.Split(Path.DirectorySeparatorChar).Skip(1), segment => segment is "" or "." or "..");
        if (directory) Web6CandidateFileSystem.RequireDirectories(path);
        else Web6CandidateFileSystem.RequireRegularFile(path);
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
