using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CSharp;
using MyDmsVn.Web6.CandidateBootstrap;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateBootstrapPreflightTests : IDisposable
{
    private const string Candidate = "fixture-01";
    private const string Server = "fixture-nonproduction";
    private const string Thumbprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string BackendSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FrontendSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string OldBackendSha = Web6CandidateCutoverPlan.BackendArchiveSha;
    private const string OldFrontendSha = Web6CandidateCutoverPlan.FrontendArchiveSha;
    private const string Tree = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private const string SourceWebConfig = """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <!-- Retain the host configuration. -->
          <appSettings><add key="fixture" value="source" /></appSettings>
          <system.web><compilation targetFramework="4.8" /></system.web>
          <runtime />
        </configuration>
        """;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "web6 preflight " + Guid.NewGuid().ToString("N").Substring(0, 8));
    private readonly List<BootstrapProcessRequest> _processes = new();
    private readonly Dictionary<string, string> _checkout = new(StringComparer.Ordinal);
    private string _failure = "";
    private string _currentBackend = BackendSha;
    private string _currentFrontend = FrontendSha;
    private string _oldBackend = OldBackendSha;
    private string _oldFrontend = OldFrontendSha;
    private int _masterChecks;

    private string At(string path) => Path.Combine(_root, path);
    private string TaskRoot => At(@"tasks\attempt");

    public Web6CandidateBootstrapPreflightTests()
    {
        foreach (var directory in new[] { "tasks", "backend", "frontend", "sql", "node", @"node\node_modules\npm\bin" })
            Directory.CreateDirectory(At(directory));
        foreach (var path in new[] { "git.exe", "dotnet.exe", "iisexpress.exe", "chrome.exe", "headless.exe", "msedge.exe", @"node\node.exe", @"node\node_modules\npm\bin\npm-cli.js" })
            File.WriteAllBytes(At(path), new byte[] { 1, 2, 3 });
    }

    [Theory]
    [InlineData("5f355e4ed3d0ca0778e195b940ecbed67b410dc0", "PIN_MATCHED")]
    [InlineData("c1e07ba7d03297ea0061cd5a0800b2f6df302bf1", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("0dc8a2ff0d88218af9bc156298d971fd3e60e715", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("53f9d48b4cd8789c4aad0fa232407797a060db43", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("56da5b4112366277f85d56fc51bf93bbd3c0c5a8", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("0b6cefd64046e7f8e83641e71c264b069db3c7ee", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("989d7dda72915cfaa31644e714ef0ac3ae2126af", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("9a1a2ba17414fabadace5edfb16e85ade6ecffbc", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("4a6541c419b1293a04c57d67bc6faaf1254ba391", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("ed96b91be42f3155c845d41e67e44b5e80cad701", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("6db4326b17b8da1ba0d02bc089e0cea1c3ea7f34", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("7ab16026ca2cf963ef378a843c609fa5a4b69cd0", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("d7a2ac1662b1eceb7dd674cc0bf273083ca84082", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("8e6a771083d0832cec168706c68bbbbeb5faa5b4", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("b91aba84b8e42b8aac78bd113c732ad9c5bff715", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("a8afa96dd81e3a282c6351fd6d1786d331a6479d", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("03205766a8e361872f1f8dee7fe83b506a6cb08f", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("13179f56a3381275c0652f6edaf29258f18927f2", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("5f7c0051497e8631925b68127ff556d2d066cd5c", "BLOCKED_REVIEWED_FRONTEND_PIN")]
    public async Task FreshPrepareRequiresTheReviewedArtifactCommandPin(string pin, string admission)
    {
        _currentFrontend = pin;
        using var authority = Authority();
        var request = Request(authority);
        var receipt = await new BootstrapPreflight(Toolchain()).PrepareAsync(request, CancellationToken.None);
        Assert.Equal(admission, receipt.CutoverAdmission);
        Assert.Equal(_currentFrontend, receipt.Payload["frontendSha"]);
        Assert.Empty(Directory.GetFileSystemEntries(At("sql")));
    }

    [Fact]
    public async Task PublishesArtifactManifestsUnderHeldParentWithExactBuildEnvironment()
    {
        using var authority = Authority();
        var variable = BootstrapToolchain.CandidateVariable;
        var previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, "ambient-wrong-candidate");
            var receipt = await new BootstrapPreflight(Toolchain()).PrepareAsync(Request(authority), CancellationToken.None);
            AssertManifest(receipt, "ordinary", null);
            AssertManifest(receipt, "candidate", Candidate);
        }
        finally { Environment.SetEnvironmentVariable(variable, previous); }
    }

    private void AssertManifest(PreparedCandidateReceipt receipt, string mode, string? id)
    {
        var root = Path.Combine(TaskRoot, mode == "ordinary" ? "frontend-ordinary" : "frontend-current");
        var build = Assert.Single(_processes, process => process.WorkingDirectory == root && process.Arguments.Contains("build"));
        Assert.Equal(id, build.Environment.TryGetValue(BootstrapToolchain.CandidateVariable, out var buildId) ? buildId : null);
        var script = Path.Combine(root, "scripts", "web6-candidate-artifact.mjs");
        var verifier = Assert.Single(_processes, process => process.Arguments[0] == script);
        Assert.Equal(At(@"node\node.exe"), verifier.Executable);
        Assert.True(verifier.ExactUtf8Stdout);
        var expected = new[] { script, "--dist", Path.Combine(root, "dist"), "--mode", mode, "--stdout" };
        Assert.Equal(id == null ? expected : expected.Concat(new[] { "--candidate-id", id }), verifier.Arguments);
        Assert.DoesNotContain(BootstrapToolchain.CandidateVariable, verifier.Environment.Keys);

        var path = Path.Combine(TaskRoot, "frontend-" + mode + "-manifest.json");
        using var manifest = ProtectedJsonFile.Open(path, (string)receipt.Payload[mode + "ManifestSha256"]!);
        Assert.Equal(Encoding.UTF8.GetBytes(Manifest(Path.Combine(root, "dist"), mode, id)), manifest.Bytes);
    }

    [Theory]
    [InlineData("stdout BOM")]
    [InlineData("stdout newline")]
    [InlineData("stdout banner")]
    [InlineData("stdout truncated")]
    [InlineData("ordinary with ID")]
    [InlineData("wrong candidate ID")]
    public async Task InvalidStdoutLeavesTheCorrespondingManifestUnpublished(string failure)
    {
        _failure = failure;
        using var authority = Authority();
        var mode = failure == "wrong candidate ID" ? "candidate" : "ordinary";
        await Assert.ThrowsAnyAsync<IOException>(() => new BootstrapPreflight(Toolchain()).PrepareAsync(Request(authority), CancellationToken.None));
        Assert.Contains(_processes, process => process.Arguments.Contains("--stdout") && process.Arguments.Contains(mode));
        Assert.False(File.Exists(Path.Combine(TaskRoot, "frontend-" + mode + "-manifest.json")));
        Assert.False(File.Exists(Path.Combine(TaskRoot, "prepared.json")));
    }

    [Theory]
    [InlineData("framing")]
    [InlineData("invalid UTF-8")]
    [InlineData("capture limit")]
    public async Task OwnedExactStdoutPreservesFramingAndRefusesUndecodableOrTruncatedBytes(string probe)
    {
        CompileFakeTools();
        var command = new BootstrapProcessRequest(At(@"node\node.exe"), _root, new[] { "--probe-stdout", probe },
            new Dictionary<string, string>(), TimeSpan.FromSeconds(10), exactUtf8Stdout: true);
        if (probe == "framing")
            Assert.Equal("\uFEFFété\r\n", await BootstrapToolchain.RunOwnedAsync(command, CancellationToken.None));
        else
            await Assert.ThrowsAnyAsync<IOException>(() => BootstrapToolchain.RunOwnedAsync(command, CancellationToken.None));
    }

    [Theory]
    [InlineData("dirty checkout")]
    [InlineData("wrong commit")]
    [InlineData("missing IIS Express")]
    [InlineData("missing Node")]
    [InlineData("missing browser")]
    [InlineData("ambiguous browser")]
    [InlineData("missing certificate")]
    [InlineData("ambiguous certificate")]
    [InlineData("no private key")]
    [InlineData("wrong catalog")]
    [InlineData("effective catalog drift")]
    [InlineData("expired SQL approval")]
    [InlineData("wrong SQL server")]
    [InlineData("build failed")]
    [InlineData("build timeout")]
    [InlineData("dist drift")]
    [InlineData("publish drift")]
    [InlineData("same-byte artifact swap")]
    [InlineData("missing generated config")]
    [InlineData("malformed generated config")]
    [InlineData("missing generated runtime")]
    [InlineData("duplicate generated runtime")]
    [InlineData("missing source runtime")]
    [InlineData("duplicate source runtime")]
    public async Task RefusesInvalidInputsWithoutPublishingPreparedReceipt(string failure)
    {
        _failure = failure;
        using var authority = Authority();
        var request = Request(authority);
        await Assert.ThrowsAnyAsync<IOException>(() => new BootstrapPreflight(Toolchain()).PrepareAsync(request, CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(TaskRoot, "prepared.json")));
        Assert.All(_processes, process => Assert.Contains(Path.GetFileName(process.Executable), new[] { "git.exe", "dotnet.exe", "node.exe" }));
        if (failure is "wrong catalog" or "expired SQL approval") Assert.Equal(0, _masterChecks);
    }

    [Theory]
    [InlineData("outside authority")]
    [InlineData("overlapping repository")]
    [InlineData("quarantine")]
    public void RefusesUnsafeRootsBeforeToolDiscovery(string failure)
    {
        _failure = failure;
        using var authority = Authority();
        Assert.ThrowsAny<IOException>(() => Request(authority));
        Assert.Empty(_processes);
    }

    [Fact]
    public async Task RefusesExistingTaskRootWithoutAdoptingIt()
    {
        Directory.CreateDirectory(TaskRoot);
        using var authority = Authority();
        await Assert.ThrowsAnyAsync<IOException>(() => new BootstrapPreflight(Toolchain()).PrepareAsync(Request(authority), CancellationToken.None));
        Assert.Empty(_processes);
        Assert.Empty(Directory.GetFileSystemEntries(TaskRoot));
    }

    [Fact]
    public async Task RefusesReparsePointInApprovedInput()
    {
        var redirected = At("redirected");
        File.Move(At("iisexpress.exe"), redirected);
        Directory.CreateDirectory(At("iisexpress.exe"));
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        using (var link = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(cmd,
            "/d /c mklink /j \"" + At(@"iisexpress.exe\linked") + "\" \"" + At("node") + "\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
        {
            Assert.NotNull(link);
            Assert.True(link!.WaitForExit(5000));
            Assert.Equal(0, link.ExitCode);
        }
        try
        {
            _failure = "junction";
            using var authority = Authority();
            await Assert.ThrowsAnyAsync<IOException>(() => new BootstrapPreflight(Toolchain()).PrepareAsync(Request(authority), CancellationToken.None));
            Assert.Empty(_processes);
        }
        finally
        {
            Directory.Delete(At(@"iisexpress.exe\linked"));
        }
    }

    [Fact]
    public async Task WritesProtectedReceiptAndExactSelectorAuthorityWithZeroLiveAllocation()
    {
        using var authority = Authority();
        var receipt = await new BootstrapPreflight(Toolchain()).PrepareAsync(Request(authority), CancellationToken.None);
        Assert.Equal(1, _masterChecks);
        Assert.All(_processes, process => Assert.Contains(Path.GetFileName(process.Executable), new[] { "git.exe", "dotnet.exe", "node.exe" }));
        using var protectedReceipt = ProtectedJsonFile.Open(receipt.Path, receipt.Sha256);
        var json = JObject.Parse(Encoding.UTF8.GetString(protectedReceipt.Bytes));
        Assert.Equal(Candidate, (string?)json["candidateId"]);
        Assert.Equal(FrontendSha, (string?)json["frontendSha"]);
        Assert.Equal("BLOCKED_REVIEWED_FRONTEND_PIN", (string?)json["cutoverAdmission"]);
        Assert.DoesNotContain(Server, Encoding.UTF8.GetString(protectedReceipt.Bytes));
        Assert.Equal(receipt.Sha256, File.ReadAllText(Path.Combine(TaskRoot, "prepared.sha256")));
        var buildAuthority = Web6CutoverBuildAuthority.ReadProtected(receipt.BuildAuthorityPath, receipt.BuildAuthoritySha256);
        var cutover = JObject.Parse(File.ReadAllText(receipt.BuildAuthorityPath));
        Assert.Equal(new[] { "version", "frontendSha", "frontendTree", "frontendBuildSha256", "backendSha", "backendTree", "backendBuildSha256", "currentPublishedSha256", "oldPublishedSha256" },
            cutover.Properties().Select(property => property.Name));
        Assert.Equal(Web6CandidateFileSet.MeasureTree(Path.Combine(TaskRoot, "current-publish")), (string?)cutover["currentPublishedSha256"]);
        Assert.Equal(Web6CandidateFileSet.MeasureTree(Path.Combine(TaskRoot, "old-publish")), (string?)cutover["oldPublishedSha256"]);
        Assert.Equal(Web6CandidateFileSet.MeasureTree(Path.Combine(TaskRoot, "frontend-old", "dist")), (string?)cutover["frontendBuildSha256"]);
        buildAuthority.Verify(new Web6CutoverBuildObservation(
            (string)json["oldFrontendSha"]!, (string)json["oldFrontendTree"]!, true,
            Web6CandidateFileSet.MeasureTree((string)json["oldFrontendDist"]!),
            (string)json["oldBackendSha"]!, (string)json["oldBackendTree"]!, true,
            Web6CandidateFileSet.MeasureTree((string)json["oldPublish"]!),
            Web6CandidateFileSet.MeasureTree((string)json["currentPublish"]!),
            Web6CandidateFileSet.MeasureTree((string)json["oldPublish"]!)));
        Assert.False(Directory.Exists(Path.Combine(TaskRoot, "pair")));
        Assert.Empty(Directory.GetFileSystemEntries(At("sql")));
    }

    [Fact]
    public async Task PublishesEachExactBuildRuntimeAndPreservesTheRestOfWebConfig()
    {
        using var authority = Authority();
        await new BootstrapPreflight(Toolchain()).PrepareAsync(Request(authority), CancellationToken.None);

        AssertPublishedRuntime("current-publish", "13.0.0.0");
        AssertPublishedRuntime("old-publish", "12.0.0.0");
    }

    private void AssertPublishedRuntime(string publish, string version)
    {
        var published = XElement.Load(Path.Combine(TaskRoot, publish, "Web.config"));
        var runtime = Assert.Single(published.Elements("runtime"));
        XNamespace bindings = "urn:schemas-microsoft-com:asm.v1";
        var redirect = Assert.Single(runtime.Descendants(bindings + "bindingRedirect"));
        Assert.Equal(version, (string?)redirect.Attribute("newVersion"));
        Assert.Equal("0.0.0.0-" + version, (string?)redirect.Attribute("oldVersion"));
        Assert.Equal("Newtonsoft.Json", (string?)redirect.Parent?.Element(bindings + "assemblyIdentity")?.Attribute("name"));

        var expected = XElement.Parse(SourceWebConfig);
        expected.Element("runtime")!.Remove();
        runtime.Remove();
        Assert.True(XNode.DeepEquals(expected, published));
    }

    [Fact]
    public async Task PrepareCommandBuildsDetachedTemporaryRepositoriesWithFakeExecutables()
    {
        var git = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')
            .Select(path => Path.Combine(path.Trim().Trim('"'), "git.exe")).First(File.Exists);
        // Git for Windows resolves its distribution relative to git.exe. Use the actual installation.
        var processEnvironment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "SystemRoot", "WINDIR", "COMSPEC", "TEMP", "TMP", "PATH", "USERPROFILE", "LOCALAPPDATA" })
            if (Environment.GetEnvironmentVariable(name) is { } value) processEnvironment.Add(name, value);
        // Use the production Git configuration boundary during fixture creation too.
        // Inheriting global autocrlf at add/commit then disabling it at preflight
        // makes the same CRLF bytes appear dirty, depending on index stat timing.
        processEnvironment["GIT_CONFIG_NOSYSTEM"] = "1";
        processEnvironment["GIT_CONFIG_GLOBAL"] = "NUL";
        processEnvironment["GIT_TERMINAL_PROMPT"] = "0";
        Task<string> Git(string root, params string[] args) => BootstrapToolchain.RunOwnedAsync(
            new BootstrapProcessRequest(git, root, args, processEnvironment, TimeSpan.FromSeconds(30)), CancellationToken.None);
        async Task<(string Old, string Current)> Repository(string root)
        {
            await Git(root, "init", "--quiet");
            File.WriteAllText(Path.Combine(root, ".gitignore"), "dist/\nnode_modules/\n");
            if (root == At("backend")) SeedBackend(root);
            var tracked = Path.Combine(root, "source.txt");
            File.WriteAllText(tracked, "archive");
            await Git(root, "add", "--all");
            await Git(root, "-c", "user.name=WEB6 Fixture", "-c", "user.email=fixture@example.invalid", "commit", "--quiet", "-m", "archive");
            var old = await Git(root, "rev-parse", "HEAD");
            File.WriteAllText(tracked, "current");
            await Git(root, "add", "--all");
            await Git(root, "-c", "user.name=WEB6 Fixture", "-c", "user.email=fixture@example.invalid", "commit", "--quiet", "-m", "current");
            return (old, await Git(root, "rev-parse", "HEAD"));
        }
        (_oldBackend, _currentBackend) = await Repository(At("backend"));
        (_oldFrontend, _currentFrontend) = await Repository(At("frontend"));
        CompileFakeTools();

        var toolchain = Toolchain(async (process, token) =>
        {
            _processes.Add(process);
            Assert.DoesNotContain("MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING", process.Environment.Keys);
            Assert.Contains(Path.GetFileName(process.Executable), new[] { "git.exe", "dotnet.exe", "node.exe" });
            if (process.Arguments.Contains("-e"))
                return new JArray(At("chrome.exe"), At("headless.exe"), At("msedge.exe")).ToString(Formatting.None);
            return await BootstrapToolchain.RunOwnedAsync(process, token);
        }, git);

        using var authority = Authority();
        _ = Request(authority);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = await BootstrapCommand.RunAsync(new[] { "prepare", "--request", At("request.json"), "--authority", authority.Path,
            "--authority-sha256", authority.Sha256 }, stdout, stderr, CancellationToken.None, toolchain);
        Assert.True(code == 0, "Prepare failed after child commands " + string.Join(" | ", _processes.Select(process =>
            Path.GetFileName(process.Executable) + " " + string.Join(" ", process.Arguments))));
        Assert.Empty(stderr.ToString());
        var preparedPath = Path.Combine(TaskRoot, "prepared.json");
        Assert.StartsWith("status=PREPARED candidate=" + Candidate + " sha256=" + Hash(File.ReadAllBytes(preparedPath)), stdout.ToString());
        Assert.Contains("cutover=BLOCKED_REVIEWED_FRONTEND_PIN", stdout.ToString());
        var beforeRun = _processes.Count;
        Assert.Equal(4, await BootstrapCommand.RunAsync(new[] { "run", "--prepared", preparedPath,
            "--prepared-sha256", Hash(File.ReadAllBytes(preparedPath)) }, TextWriter.Null, TextWriter.Null, CancellationToken.None, toolchain));
        Assert.Equal(beforeRun, _processes.Count);
        Assert.Equal(1, _masterChecks);
        Assert.Empty(Directory.GetFileSystemEntries(At("sql")));
        Assert.Empty(await Git(At("backend"), "status", "--porcelain"));
        Assert.Empty(await Git(At("frontend"), "status", "--porcelain"));
        Assert.Equal("HEAD", await Git(Path.Combine(TaskRoot, "frontend-current"), "rev-parse", "--abbrev-ref", "HEAD"));
        Assert.Equal(_oldFrontend, await Git(Path.Combine(TaskRoot, "frontend-old"), "rev-parse", "HEAD"));
        Assert.False(Directory.Exists(Path.Combine(TaskRoot, "cutover")));
    }

    private BootstrapAuthority Authority()
    {
        var path = At("authority.json");
        var input = new JObject
        {
            ["version"] = 1, ["candidateId"] = Candidate, ["taskParent"] = At("tasks"), ["sqlControlParent"] = At("sql"),
            ["origins"] = new JArray("https://localhost:44321", "https://localhost:44322"),
            ["drillClasses"] = new JArray("cutover", "pair", "recovery", "workload"),
            ["backendSha"] = _currentBackend, ["frontendSha"] = _currentFrontend, ["oldBackendSha"] = _oldBackend, ["oldFrontendSha"] = _oldFrontend,
            ["expiresAtUtc"] = DateTimeOffset.UtcNow.AddHours(3).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        };
        File.WriteAllText(At("authority-input.json"), input.ToString());
        return BootstrapAuthority.ReadProtected(path, BootstrapAuthority.Authorize(At("authority-input.json"), path, CancellationToken.None), DateTimeOffset.UtcNow);
    }

    private BootstrapRequest Request(BootstrapAuthority authority)
    {
        var approval = new JObject
        {
            ["version"] = 1, ["purpose"] = "web6-cutover", ["serverSha256"] = Hash(Encoding.UTF8.GetBytes(Server)),
            ["expiresUtc"] = DateTimeOffset.UtcNow.AddHours(_failure == "expired SQL approval" ? -1 : 1).ToString("O"),
        };
        ProtectedJsonFile.WriteNew(At("approval.json"), Encoding.UTF8.GetBytes(approval.ToString()));
        var request = new JObject
        {
            ["version"] = 1, ["candidateId"] = Candidate, ["backendRepository"] = _failure == "overlapping repository" ? At("tasks") : At("backend"),
            ["frontendRepository"] = At("frontend"), ["taskRoot"] = _failure switch
            {
                "outside authority" => At("outside"), "quarantine" => At(@"tasks\WEB5-HISTORICAL"), _ => TaskRoot,
            },
            ["sqlControlParent"] = At("sql"), ["sqlApprovalFile"] = At("approval.json"), ["tlsThumbprint"] = Thumbprint,
            ["nodePath"] = At(_failure == "missing Node" ? "missing.exe" : @"node\node.exe"),
            ["iisExpressPath"] = At(_failure switch { "missing IIS Express" => "missing.exe", "junction" => @"iisexpress.exe\linked\node.exe", _ => "iisexpress.exe" }),
            ["browserProjects"] = new JArray("chromium", "edge"), ["actorCohort"] = "web.smoke", ["workloadSeconds"] = 60,
            ["recoveryAuthorized"] = true, ["authorityPath"] = authority.Path, ["authoritySha256"] = authority.Sha256,
        };
        File.WriteAllText(At("request.json"), request.ToString());
        return BootstrapRequest.ReadStrict(At("request.json"), authority);
    }

    private BootstrapToolchain Toolchain(Func<BootstrapProcessRequest, CancellationToken, Task<string>>? run = null, string? git = null) => new(
        run ?? RunFake, name => new[] { name == "git" && git != null ? git : At(name + ".exe") },
        _ => _failure switch
        {
            "missing certificate" => Array.Empty<BootstrapCertificate>(),
            "ambiguous certificate" => new[] { Certificate(true), Certificate(true) },
            _ => new[] { Certificate(_failure != "no private key") },
        },
        () => _failure == "ambiguous browser" ? new[] { At("msedge.exe"), At("chrome.exe") } : new[] { At("msedge.exe") },
        () => "Data Source=fixture;Initial Catalog=" + (_failure == "wrong catalog" ? "production" : "master") + ";Integrated Security=True",
        (_, _) =>
        {
            _masterChecks++;
            return Task.FromResult((_failure == "effective catalog drift" ? "app" : "master", _failure == "wrong SQL server" ? "other" : Server));
        });

    private static BootstrapCertificate Certificate(bool key) => new(key, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), new string('c', 64), new[] { "localhost" });

    private Task<string> RunFake(BootstrapProcessRequest process, CancellationToken token)
    {
        _processes.Add(process);
        var args = process.Arguments.ToArray();
        var root = process.WorkingDirectory;
        switch (Path.GetFileName(process.Executable))
        {
            case "git.exe":
                if (args.Contains("worktree"))
                {
                    var checkout = args[args.Length - 2];
                    _checkout.Add(checkout, args.Last());
                    if (Path.GetFileName(checkout).StartsWith("backend-", StringComparison.Ordinal))
                        SeedBackend(checkout, _failure);
                }
                if (args.Contains("status") && _failure == "dirty checkout") return Task.FromResult(" M tracked.txt");
                if (args.Contains("--show-toplevel")) return Task.FromResult(root.Replace('\\', '/'));
                if (args.Contains("--abbrev-ref")) return Task.FromResult("HEAD");
                if (args.Last() == "HEAD") return Task.FromResult(_failure == "wrong commit" ? Tree : _checkout.TryGetValue(root, out var sha) ? sha : BackendSha);
                if (args.Last() == "HEAD^{tree}") return Task.FromResult(Tree);
                if (args.Contains("symbolic-ref")) throw new InvalidOperationException("Use a checked Git command for detached verification.");
                break;
            case "dotnet.exe":
                if (args.Contains("--version")) return Task.FromResult("8.0.427");
                if (args.Contains("build"))
                {
                    if (_failure == "build failed") throw new IOException("Fake nonzero exit.");
                    if (_failure == "build timeout") throw new TimeoutException("Fake timeout.");
                    var bin = args[Array.IndexOf(args, "--output") + 1];
                    Directory.CreateDirectory(bin);
                    File.WriteAllText(Path.Combine(bin, "MyDmsVn.Server.HttpHost.dll"), "fixture dll");
                    var version = root.EndsWith("backend-old", StringComparison.Ordinal) ? "12.0.0.0" : "13.0.0.0";
                    if (_failure != "missing generated config")
                        File.WriteAllText(Path.Combine(bin, "MyDmsVn.Server.HttpHost.dll.config"), _failure switch
                        {
                            "malformed generated config" => "<configuration><runtime>",
                            "missing generated runtime" => "<configuration><appSettings /></configuration>",
                            "duplicate generated runtime" => "<configuration><runtime /><runtime /></configuration>",
                            _ => GeneratedWebConfig(version),
                        });
                    if (_failure == "publish drift" && root.EndsWith("backend-old", StringComparison.Ordinal))
                        File.AppendAllText(Path.Combine(TaskRoot, "current-publish", "bin", "MyDmsVn.Server.HttpHost.dll"), "drift");
                }
                break;
            case "node.exe":
                if (args.Length == 1 && args[0] == "--version") return Task.FromResult("v22.14.0");
                if (args.Last() == "--version") return Task.FromResult("11.6.0");
                if (args[0] == "-e") return Task.FromResult(new JArray(At("chrome.exe"), At(_failure == "missing browser" ? "missing.exe" : "headless.exe"), At("msedge.exe")).ToString(Formatting.None));
                process.Environment.TryGetValue("VITE_MYDMSVN_WEB6_CANDIDATE_ID", out var candidate);
                if (args.Contains("build"))
                {
                    var dist = Path.Combine(root, "dist");
                    Directory.CreateDirectory(dist);
                    File.WriteAllText(Path.Combine(dist, "index.html"), "fixture html");
                    if (!root.EndsWith("frontend-old", StringComparison.Ordinal))
                    {
                        var marker = new JObject { ["version"] = 1, ["candidateId"] = candidate }.ToString(Formatting.None);
                        File.WriteAllText(Path.Combine(dist, "app.js"), "app\n/* MyDmsVn WEB6 build marker " + marker + " */\n");
                        File.WriteAllText(Path.Combine(dist, "web6-candidate-build.json"), marker);
                    }
                }
                if (args.Contains("web6:artifact"))
                {
                    Assert.ThrowsAny<IOException>(() => Directory.Move(TaskRoot, TaskRoot + "-moved"));
                    throw new IOException("EBUSY: frontend hard-link output cannot publish into the held parent.");
                }
                if (args[0] == Path.Combine(root, "scripts", "web6-candidate-artifact.mjs"))
                {
                    var dist = args[Array.IndexOf(args, "--dist") + 1];
                    var mode = args[Array.IndexOf(args, "--mode") + 1];
                    Assert.Contains("--stdout", args);
                    Assert.DoesNotContain("--output", args);
                    Assert.ThrowsAny<IOException>(() => Directory.Move(TaskRoot, TaskRoot + "-moved"));
                    Assert.False(File.Exists(Path.Combine(TaskRoot, "frontend-" + mode + "-manifest.json")));
                    var manifest = Manifest(dist, mode, mode == "ordinary" ? (_failure == "ordinary with ID" ? Candidate : null) : (_failure == "wrong candidate ID" ? "other" : Candidate));
                    if (_failure == "dist drift") File.AppendAllText(Path.Combine(dist, "app.js"), "drift");
                    if (_failure == "same-byte artifact swap")
                    {
                        var path = Path.Combine(dist, "app.js");
                        var bytes = File.ReadAllBytes(path);
                        File.Move(path, Path.Combine(TaskRoot, "retained.js"));
                        File.WriteAllBytes(path, bytes);
                    }
                    return Task.FromResult(_failure switch
                    {
                        "stdout BOM" => "\uFEFF" + manifest,
                        "stdout newline" => manifest + "\r\n",
                        "stdout banner" => "artifact complete\n" + manifest,
                        "stdout truncated" => manifest.Substring(0, manifest.Length - 1),
                        _ => manifest,
                    });
                }
                break;
            default:
                throw new InvalidOperationException("An allocator, browser or host process was called.");
        }
        return Task.FromResult("");
    }

    private static void SeedBackend(string root, string failure = "")
    {
        var host = Path.Combine(root, "src", "MyDmsVn.Server.HttpHost");
        Directory.CreateDirectory(host);
        File.WriteAllText(Path.Combine(host, "Web.config"), failure switch
        {
            "missing source runtime" => SourceWebConfig.Replace("<runtime />", ""),
            "duplicate source runtime" => SourceWebConfig.Replace("<runtime />", "<runtime /><runtime />"),
            _ => SourceWebConfig,
        });
    }

    private static string GeneratedWebConfig(string version) => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <runtime>
            <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
              <dependentAssembly>
                <assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />
                <bindingRedirect oldVersion="0.0.0.0-{{version}}" newVersion="{{version}}" />
              </dependentAssembly>
            </assemblyBinding>
          </runtime>
          <appSettings><add key="fixture" value="generated" /></appSettings>
        </configuration>
        """;

    private void CompileFakeTools()
    {
        const string source = """
            using System;
            using System.IO;
            using System.Linq;
            using System.Reflection;
            using System.Security.Cryptography;
            using System.Text;

            static class Program
            {
                static string Value(string[] args, string name) { return args[Array.IndexOf(args, name) + 1]; }
                static string Hash(byte[] bytes)
                {
                    using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                }
                static int Main(string[] args)
                {
                    var tool = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
                    if (args[0] == "--probe-stdout")
                    {
                        var bytes = args[1] == "invalid UTF-8" ? new byte[] { 0xc3, 0x28 } :
                            args[1] == "capture limit" ? new byte[32768] : Encoding.UTF8.GetBytes("\uFEFF\u00E9t\u00E9\r\n");
                        using (var output = Console.OpenStandardOutput()) output.Write(bytes, 0, bytes.Length);
                        return 0;
                    }
                    if (args.Contains("--version"))
                    {
                        Console.WriteLine(tool == "dotnet.exe" ? "8.0.427" : args.Length == 1 ? "v22.14.0" : "11.6.0");
                        return 0;
                    }
                    if (tool == "dotnet.exe")
                    {
                        if (args.Contains("build"))
                        {
                            var output = Value(args, "--output");
                            File.WriteAllText(Path.Combine(output, "MyDmsVn.Server.HttpHost.dll"), "fixture dll");
                            File.WriteAllText(Path.Combine(output, "MyDmsVn.Server.HttpHost.dll.config"),
                                "<configuration><runtime><assemblyBinding xmlns=\"urn:schemas-microsoft-com:asm.v1\">" +
                                "<dependentAssembly><assemblyIdentity name=\"Newtonsoft.Json\" publicKeyToken=\"30ad4fe6b2a6aeed\" culture=\"neutral\" />" +
                                "<bindingRedirect oldVersion=\"0.0.0.0-13.0.0.0\" newVersion=\"13.0.0.0\" /></dependentAssembly>" +
                                "</assemblyBinding></runtime></configuration>");
                        }
                        return 0;
                    }
                    if (args.Contains("build"))
                    {
                        Directory.CreateDirectory("dist");
                        File.WriteAllText("dist/index.html", "fixture html");
                        if (!Directory.GetCurrentDirectory().EndsWith("frontend-old", StringComparison.Ordinal))
                        {
                            var candidate = Environment.GetEnvironmentVariable("VITE_MYDMSVN_WEB6_CANDIDATE_ID");
                            var marker = "{\"version\":1,\"candidateId\":" + (candidate == null ? "null" : "\"" + candidate + "\"") + "}";
                            File.WriteAllText("dist/web6-candidate-build.json", marker);
                            File.WriteAllText("dist/app.js", "fixture\n/* MyDmsVn WEB6 build marker " + marker + " */\n");
                        }
                    }
                    if (args.Contains("web6:artifact")) return 16; // Simulate EBUSY on legacy hard-link publication.
                    if (args.Contains("--stdout"))
                    {
                        var mode = Value(args, "--mode");
                        var id = mode == "candidate" ? "\"" + Value(args, "--candidate-id") + "\"" : "null";
                        var files = Directory.GetFiles(Value(args, "--dist")).OrderBy(path => path, StringComparer.Ordinal)
                            .Select(path => "{\"path\":\"" + Path.GetFileName(path) + "\",\"sha256\":\"" + Hash(File.ReadAllBytes(path)) + "\"}");
                        var payload = "{\"version\":1,\"mode\":\"" + mode + "\",\"candidateId\":" + id + ",\"files\":[" + string.Join(",", files) + "]}";
                        Console.Write(payload.Substring(0, payload.Length - 1) +
                            ",\"manifestSha256\":\"" + Hash(Encoding.UTF8.GetBytes(payload)) + "\"}");
                    }
                    return 0;
                }
            }
            """;
        File.Delete(At("dotnet.exe"));
        File.Delete(At(@"node\node.exe"));
        using var compiler = new CSharpCodeProvider();
        var options = new CompilerParameters(new[] { "System.Core.dll" }, At("dotnet.exe")) { GenerateExecutable = true };
        var result = compiler.CompileAssemblyFromSource(options, source);
        Assert.False(result.Errors.HasErrors, string.Join(Environment.NewLine, result.Errors.Cast<CompilerError>()));
        File.Copy(At("dotnet.exe"), At(@"node\node.exe"));
    }

    private static string Manifest(string dist, string mode, string? candidate)
    {
        var manifest = new JObject
        {
            ["version"] = 1, ["mode"] = mode, ["candidateId"] = candidate,
            ["files"] = new JArray(Directory.GetFiles(dist).OrderBy(path => path, StringComparer.Ordinal).Select(path =>
                new JObject { ["path"] = Path.GetFileName(path), ["sha256"] = Hash(File.ReadAllBytes(path)) })),
        };
        manifest["manifestSha256"] = Hash(Encoding.UTF8.GetBytes(manifest.ToString(Formatting.None)));
        return manifest.ToString(Formatting.None);
    }

    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            if ((File.GetAttributes(file) & FileAttributes.ReadOnly) != 0) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }
}
