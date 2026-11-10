using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using MyDmsVn.Web6.CandidateBootstrap;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateBootstrapAuthorityTests : IDisposable
{
    private const string Acknowledgement = "I AUTHORIZE WEB6 NONPRODUCTION DESTRUCTIVE DRILLS";
    private const string BackendSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FrontendSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string OldBackendSha = "cccccccccccccccccccccccccccccccccccccccc";
    private const string OldFrontendSha = "dddddddddddddddddddddddddddddddddddddddd";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "w6b-" + Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _expires = DateTimeOffset.UtcNow.AddDays(1);

    public Web6CandidateBootstrapAuthorityTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task AuthorizeCreatesOnlyCanonicalOwnerReceiptAndReportsItsDigest()
    {
        var input = Authority();
        var expected = input.ToString(Formatting.None);
        var shuffled = new JObject(input.Properties().Reverse());
        var (code, output, error) = await Authorize(shuffled.ToString(Formatting.Indented));

        Assert.Equal(0, code);
        Assert.Empty(error);
        Assert.Equal(new[] { "authority-input.json", "authority.json" },
            Directory.GetFileSystemEntries(_root).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
        var bytes = File.ReadAllBytes(AuthorityPath);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), bytes);
        Assert.Equal("status=AUTHORIZED sha256=" + Digest(bytes) + Environment.NewLine, output);

        using var user = WindowsIdentity.GetCurrent();
        var acl = File.GetAccessControl(AuthorityPath);
        Assert.True(acl.AreAccessRulesProtected);
        Assert.Equal(user.User, acl.GetOwner(typeof(SecurityIdentifier)));
        var ace = Assert.Single(acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
        Assert.Equal(user.User, ace.IdentityReference);
        Assert.Equal(FileSystemRights.FullControl, ace.FileSystemRights);
        Assert.Equal(AccessControlType.Allow, ace.AccessControlType);
        Assert.False(ace.IsInherited);
    }

    [Fact]
    public async Task AuthorizeRefusesInexactAcknowledgementWithoutWriting()
    {
        var (code, output, error) = await Authorize(Authority().ToString(), Acknowledgement + " ");
        Assert.Equal(3, code);
        Assert.Empty(output);
        Assert.Equal("status=REFUSED_AUTHORITY" + Environment.NewLine, error);
        Assert.False(File.Exists(AuthorityPath));
    }

    [Fact]
    public async Task AuthorizeNeverOverwritesAnExistingReceipt()
    {
        var digest = await CreateAuthority();
        var changed = Authority();
        changed["candidateId"] = "changed-candidate";
        var (code, output, error) = await Authorize(changed.ToString());
        Assert.Equal(3, code);
        Assert.Empty(output);
        Assert.Equal("status=REFUSED_AUTHORITY" + Environment.NewLine, error);
        Assert.Equal(digest, Digest(File.ReadAllBytes(AuthorityPath)));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("escaped duplicate")]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("case")]
    [InlineData("trailing comma")]
    [InlineData("comment")]
    [InlineData("single quotes")]
    [InlineData("second object")]
    [InlineData("noncanonical integer")]
    public async Task AuthorizeRejectsInvalidJsonWithoutCreatingAReceipt(string mutation)
    {
        var (code, output, error) = await Authorize(MutateJson(Authority(), mutation));
        Assert.Equal(3, code);
        Assert.Empty(output);
        Assert.Equal("status=REFUSED_AUTHORITY" + Environment.NewLine, error);
        Assert.False(File.Exists(AuthorityPath));
    }

    [Theory]
    [InlineData("version", "2")]
    [InlineData("version", "\"1\"")]
    [InlineData("candidateId", "\"bad/id\"")]
    [InlineData("candidateId", "\"_candidate\"")]
    [InlineData("candidateId", "\"candidate\\n\"")]
    [InlineData("taskParent", "\"relative\"")]
    [InlineData("sqlControlParent", "\"C:\\\\task\\\\..\\\\sql\"")]
    [InlineData("backendSha", "\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"")]
    [InlineData("frontendSha", "\"bbbbbbb\"")]
    [InlineData("oldBackendSha", "\"cccccccccccccccccccccccccccccccccccccccg\"")]
    [InlineData("oldFrontendSha", "\"ddddddddddddddddddddddddddddddddddddddddd\"")]
    [InlineData("expiresAtUtc", "\"2099-01-01T00:00:00+00:00\"")]
    [InlineData("expiresAtUtc", "\"2099-01-01T00:00:00\"")]
    [InlineData("expiresAtUtc", "\"2020-01-01T00:00:00Z\"")]
    [InlineData("origins", "[]")]
    [InlineData("origins", "[\"http://localhost:44321\"]")]
    [InlineData("origins", "[\"https://localhost\"]")]
    [InlineData("origins", "[\"https://localhost:0\"]")]
    [InlineData("origins", "[\"https://localhost:65536\"]")]
    [InlineData("origins", "[\"https://localhost:44321/path\"]")]
    [InlineData("origins", "[\"https://localhost:44321?x\"]")]
    [InlineData("origins", "[\"https://localhost:44321/\"]")]
    [InlineData("origins", "[\"https://example.invalid:44321\"]")]
    [InlineData("origins", "[\"https://localhost:44321\",\"https://localhost:44321\"]")]
    [InlineData("origins", "[\"https://localhost:44322\",\"https://localhost:44321\"]")]
    [InlineData("drillClasses", "[]")]
    [InlineData("drillClasses", "[\"deploy\"]")]
    [InlineData("drillClasses", "[\"Pair\"]")]
    [InlineData("drillClasses", "[\"pair\",\"pair\"]")]
    [InlineData("drillClasses", "[\"pair\",\"cutover\"]")]
    public async Task AuthorizeRejectsInvalidAuthorityValues(string field, string replacement)
    {
        var input = Authority();
        input[field] = JToken.Parse(replacement);
        Assert.Equal(3, (await Authorize(input.ToString())).Code);
        Assert.False(File.Exists(AuthorityPath));
    }

    [Fact]
    public async Task AuthorizeRejectsOverlappingAuthorityParents()
    {
        var input = Authority();
        input["sqlControlParent"] = Path.Combine((string)input["taskParent"]!, "nested");
        Assert.Equal(3, (await Authorize(input.ToString())).Code);
        Assert.False(File.Exists(AuthorityPath));
    }

    [Fact]
    public async Task ProtectedAuthorityRejectsWrongDigestAndExpiryBoundary()
    {
        var digest = await CreateAuthority();
        Assert.ThrowsAny<IOException>(() => BootstrapAuthority.ReadProtected(AuthorityPath, new string('a', 64), _expires.AddSeconds(-1)));
        Assert.ThrowsAny<IOException>(() => BootstrapAuthority.ReadProtected(AuthorityPath, digest.ToUpperInvariant(), _expires.AddSeconds(-1)));
        Assert.ThrowsAny<IOException>(() => BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires));
    }

    [Fact]
    public async Task ProtectedAuthorityRejectsChangedContent()
    {
        var digest = await CreateAuthority();
        File.AppendAllText(AuthorityPath, " ", Encoding.ASCII);
        Assert.ThrowsAny<IOException>(() => BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1)));
    }

    [Fact]
    public async Task ProtectedAuthorityPinsFileIdentityWhileRequestConsumesIt()
    {
        var digest = await CreateAuthority();
        using (var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1)))
        {
            Assert.ThrowsAny<IOException>(() => File.Move(AuthorityPath, Path.Combine(_root, "replacement.json")));
            Assert.ThrowsAny<IOException>(() => File.AppendAllText(AuthorityPath, " ", Encoding.ASCII));
            Assert.Equal("candidate-01", BootstrapRequest.ReadStrict(WriteRequest(Request(digest)), authority).CandidateId);
        }

        File.Move(AuthorityPath, Path.Combine(_root, "replacement.json"));
    }

    [Fact]
    public async Task ProtectedAuthorityPinsItsContainingDirectoryWhileInUse()
    {
        var digest = await CreateAuthority();
        var moved = _root + "-moved";
        using var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1));
        try
        {
            Assert.ThrowsAny<IOException>(() => Directory.Move(_root, moved));
        }
        finally
        {
            if (Directory.Exists(moved))
                Directory.Move(moved, _root);
        }
    }

    [Fact]
    public async Task RequestRefusesAuthorityNativeIdentityDrift()
    {
        var digest = await CreateAuthority();
        using var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1));
        var creationTime = File.GetCreationTimeUtc(AuthorityPath).AddDays(-1).ToFileTimeUtc();
        using (var metadata = new FileStream(AuthorityPath, FileMode.Open, FileSystemRights.WriteAttributes,
            FileShare.Read | FileShare.Write, 4096, FileOptions.None))
            Assert.True(SetFileTime(metadata.SafeFileHandle, ref creationTime, IntPtr.Zero, IntPtr.Zero));
        Assert.ThrowsAny<IOException>(() => BootstrapRequest.ReadStrict(WriteRequest(Request(digest)), authority));
    }

    [Theory]
    [InlineData("taskRoot", "TASK\\candidate-01")]
    [InlineData("sqlApprovalFile", "authority.json")]
    public async Task RequestRefusesCaseVariantTaskParentAndReusedSqlApproval(string field, string path)
    {
        var digest = await CreateAuthority();
        using var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1));
        var request = Request(digest);
        request[field] = Path.Combine(_root, path);
        Assert.ThrowsAny<IOException>(() => BootstrapRequest.ReadStrict(WriteRequest(request), authority));
    }


    [Fact]
    public async Task RequestRefusesAuthorityAclDriftAfterTheRead()
    {
        var digest = await CreateAuthority();
        using var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1));
        var acl = File.GetAccessControl(AuthorityPath);
        acl.SetAccessRuleProtection(false, false);
        File.SetAccessControl(AuthorityPath, acl);
        Assert.ThrowsAny<IOException>(() => BootstrapRequest.ReadStrict(WriteRequest(Request(digest)), authority));
        Assert.ThrowsAny<IOException>(() => BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1)));
    }

    [Fact]
    public async Task StrictRequestBindsSourcePinsAndOriginsFromAuthority()
    {
        var digest = await CreateAuthority();
        using var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1));
        var request = BootstrapRequest.ReadStrict(WriteRequest(Request(digest)), authority);
        Assert.Equal(BackendSha, request.BackendSha);
        Assert.Equal(FrontendSha, request.FrontendSha);
        Assert.Equal(OldBackendSha, request.OldBackendSha);
        Assert.Equal(OldFrontendSha, request.OldFrontendSha);
        Assert.Equal(new[] { "https://localhost:44321", "https://localhost:44322" }, request.Origins);
        Assert.Equal(new[] { "chromium", "edge" }, request.BrowserProjects);
        Assert.Equal("web.smoke", request.ActorCohort);
        Assert.Equal(60, request.WorkloadSeconds);
        Assert.True(request.RecoveryAuthorized);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("escaped duplicate")]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("case")]
    [InlineData("trailing comma")]
    [InlineData("comment")]
    [InlineData("single quotes")]
    [InlineData("second object")]
    [InlineData("noncanonical integer")]
    public async Task StrictRequestRejectsInvalidJson(string mutation)
    {
        var digest = await CreateAuthority();
        using var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1));
        var path = WriteRequest(MutateJson(Request(digest), mutation));
        Assert.ThrowsAny<IOException>(() => BootstrapRequest.ReadStrict(path, authority));
    }

    [Theory]
    [InlineData("candidateId", "\"candidate-02\"")]
    [InlineData("taskRoot", "\"C:\\\\unapproved\\\\candidate\"")]
    [InlineData("sqlControlParent", "\"C:\\\\unapproved\\\\sql\"")]
    [InlineData("authorityPath", "\"C:\\\\unapproved\\\\authority.json\"")]
    [InlineData("authoritySha256", "\"0000000000000000000000000000000000000000000000000000000000000000\"")]
    [InlineData("backendRepository", "\"C:relative\"")]
    [InlineData("backendRepository", "\"C:\\\\repo\\\\child\\\\..\"")]
    [InlineData("frontendRepository", "\"\\\\\\\\server\\\\share\\\\repo\"")]
    [InlineData("sqlApprovalFile", "\"C:\\\\private\\\\approval.json:alias\"")]
    [InlineData("tlsThumbprint", "\"bad hash\"")]
    [InlineData("nodePath", "\"node.exe\"")]
    [InlineData("iisExpressPath", "null")]
    [InlineData("browserProjects", "[\"chromium\"]")]
    [InlineData("browserProjects", "[\"chromium\",\"chromium\"]")]
    [InlineData("browserProjects", "[\"chromium\",\"firefox\"]")]
    [InlineData("actorCohort", "\"another.cohort\"")]
    [InlineData("workloadSeconds", "0")]
    [InlineData("workloadSeconds", "59")]
    [InlineData("workloadSeconds", "61")]
    [InlineData("recoveryAuthorized", "\"true\"")]
    public async Task StrictRequestRejectsInvalidOrWiderValues(string field, string replacement)
    {
        var digest = await CreateAuthority();
        using var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1));
        var input = Request(digest);
        input[field] = JToken.Parse(replacement);
        Assert.ThrowsAny<IOException>(() => BootstrapRequest.ReadStrict(WriteRequest(input), authority));
    }

    [Theory]
    [InlineData("backendSha", "\"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee\"")]
    [InlineData("origins", "[\"https://localhost:44323\"]")]
    [InlineData("drillClasses", "[\"recovery\"]")]
    public async Task StrictRequestRejectsAttemptsToOverrideAuthorityFields(string field, string replacement)
    {
        var digest = await CreateAuthority();
        using var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1));
        var request = Request(digest);
        request[field] = JToken.Parse(replacement);
        Assert.ThrowsAny<IOException>(() => BootstrapRequest.ReadStrict(WriteRequest(request), authority));
    }

    [Fact]
    public async Task StrictRequestRequiresExplicitRecoveryAuthorityForOptIn()
    {
        var input = Authority();
        input["drillClasses"] = new JArray("pair");
        var digest = await CreateAuthority(input);
        using var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1));
        var request = Request(digest);
        Assert.ThrowsAny<IOException>(() => BootstrapRequest.ReadStrict(WriteRequest(request), authority));

        request["recoveryAuthorized"] = false;
        request["browserProjects"] = new JArray("edge", "chromium");
        var admitted = BootstrapRequest.ReadStrict(WriteRequest(request), authority);
        Assert.False(admitted.RecoveryAuthorized);
        Assert.Equal(new[] { "chromium", "edge" }, admitted.BrowserProjects);
    }

    [Fact]
    public async Task StrictRequestRejectsReusedRepositoryAndTaskRoots()
    {
        var digest = await CreateAuthority();
        using var authority = BootstrapAuthority.ReadProtected(AuthorityPath, digest, _expires.AddSeconds(-1));
        var request = Request(digest);
        request["frontendRepository"] = (string)request["backendRepository"]!;
        Assert.ThrowsAny<IOException>(() => BootstrapRequest.ReadStrict(WriteRequest(request), authority));

        request = Request(digest);
        request["backendRepository"] = (string)request["taskRoot"]!;
        Assert.ThrowsAny<IOException>(() => BootstrapRequest.ReadStrict(WriteRequest(request), authority));
    }

    [Fact]
    public async Task PrepareAndRunFailClosedBeforePreflightIsImplemented()
    {
        var digest = await CreateAuthority();
        var (prepareCode, prepareOutput, prepareError) = await Run(
            "prepare", "--request", WriteRequest(Request(digest)), "--authority", AuthorityPath, "--authority-sha256", digest);
        Assert.Equal(4, prepareCode);
        Assert.Empty(prepareOutput);
        Assert.Equal("status=BLOCKED_PREFLIGHT" + Environment.NewLine, prepareError);

        var (runCode, runOutput, runError) = await Run("run", "--prepared", AuthorityPath, "--prepared-sha256", digest);
        Assert.Equal(4, runCode);
        Assert.Empty(runOutput);
        Assert.Equal("status=BLOCKED_PREFLIGHT" + Environment.NewLine, runError);
        Assert.False(Directory.Exists(TaskRoot));
    }

    [Fact]
    public async Task CommandRejectsUnknownDuplicateAndIncompleteOptions()
    {
        foreach (var args in new[]
        {
            new[] { "build" },
            new[] { "authorize", "--input", "private", "--output", AuthorityPath, "--input", "private" },
            new[] { "run", "--prepared", AuthorityPath, "--prepared-sha256" },
            new[] { "run", "--prepared", AuthorityPath, "--authority-sha256", new string('a', 64) },
            new[] { "run", "--prepared", AuthorityPath, "--prepared-sha256", "bad-digest" },
        })
        {
            var (code, output, error) = await Run(args);
            Assert.Equal(2, code);
            Assert.Empty(output);
            Assert.DoesNotContain(_root, error);
            Assert.DoesNotContain("private", error);
        }
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    private string AuthorityPath => Path.Combine(_root, "authority.json");
    private string TaskParent => Path.Combine(_root, "task");
    private string TaskRoot => Path.Combine(TaskParent, "candidate-01");
    private string SqlParent => Path.Combine(_root, "sql");

    private JObject Authority() => new JObject
    {
        ["version"] = 1,
        ["candidateId"] = "candidate-01",
        ["taskParent"] = TaskParent,
        ["sqlControlParent"] = SqlParent,
        ["origins"] = new JArray("https://localhost:44321", "https://localhost:44322"),
        ["drillClasses"] = new JArray("cutover", "pair", "recovery", "workload"),
        ["backendSha"] = BackendSha,
        ["frontendSha"] = FrontendSha,
        ["oldBackendSha"] = OldBackendSha,
        ["oldFrontendSha"] = OldFrontendSha,
        ["expiresAtUtc"] = _expires.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture),
    };

    private JObject Request(string digest) => new JObject
    {
        ["version"] = 1,
        ["candidateId"] = "candidate-01",
        ["backendRepository"] = Path.Combine(_root, "backend"),
        ["frontendRepository"] = Path.Combine(_root, "frontend"),
        ["taskRoot"] = TaskRoot,
        ["sqlControlParent"] = SqlParent,
        ["sqlApprovalFile"] = Path.Combine(_root, "sql-approval.json"),
        ["tlsThumbprint"] = new string('A', 40),
        ["nodePath"] = Path.Combine(_root, "node.exe"),
        ["iisExpressPath"] = Path.Combine(_root, "iisexpress.exe"),
        ["browserProjects"] = new JArray("chromium", "edge"),
        ["actorCohort"] = "web.smoke",
        ["workloadSeconds"] = 60,
        ["recoveryAuthorized"] = true,
        ["authorityPath"] = AuthorityPath,
        ["authoritySha256"] = digest,
    };

    private static string MutateJson(JObject value, string mutation)
    {
        var json = value.ToString(Formatting.None);
        return mutation switch
        {
            "duplicate" => "{\"version\":1," + json.Substring(1),
            "escaped duplicate" => "{\"\\u0076ersion\":1," + json.Substring(1),
            "unknown" => "{\"unknown\":1," + json.Substring(1),
            "missing" => new JObject(value.Properties().Where(property => property.Name != "version")).ToString(Formatting.None),
            "case" => json.Replace("\"version\"", "\"Version\""),
            "trailing comma" => json.Substring(0, json.Length - 1) + ",}",
            "comment" => "/* comment */" + json,
            "single quotes" => json.Replace("\"version\"", "'version'"),
            "second object" => json + "{}",
            "noncanonical integer" => json.Replace("\"version\":1", "\"version\":01"),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
    }

    private string WriteRequest(JObject request) => WriteRequest(request.ToString(Formatting.None));

    private string WriteRequest(string json)
    {
        var path = Path.Combine(_root, "request.json");
        File.WriteAllText(path, json);
        return path;
    }

    private async Task<string> CreateAuthority(JObject? input = null)
    {
        var (code, output, error) = await Authorize((input ?? Authority()).ToString(Formatting.None));
        Assert.Equal(0, code);
        Assert.Empty(error);
        var digest = Digest(File.ReadAllBytes(AuthorityPath));
        Assert.Equal("status=AUTHORIZED sha256=" + digest + Environment.NewLine, output);
        return digest;
    }

    private Task<(int Code, string Output, string Error)> Authorize(string json, string acknowledge = Acknowledgement)
    {
        var input = Path.Combine(_root, "authority-input.json");
        File.WriteAllText(input, json);
        return Run("authorize", "--input", input, "--output", AuthorityPath, "--acknowledge", acknowledge);
    }

    private static async Task<(int Code, string Output, string Error)> Run(params string[] args)
    {
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        var code = await BootstrapCommand.RunAsync(args, stdout, stderr, CancellationToken.None);
        return (code, stdout.ToString(), stderr.ToString());
    }

    private static string Digest(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileTime(SafeFileHandle handle, ref long creationTime, IntPtr accessTime, IntPtr writeTime);

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
