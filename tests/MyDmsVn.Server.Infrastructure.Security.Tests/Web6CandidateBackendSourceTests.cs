using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CSharp;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Web6.CandidateBootstrap;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateBackendSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "web6-source-" + Guid.NewGuid().ToString("N"));
    private static readonly string Sha = new string('a', 40), Tree = new string('b', 40);

    public Web6CandidateBackendSourceTests() => Directory.CreateDirectory(Path.Combine(_root, ".git"));

    [Theory]
    [InlineData(null)]
    [InlineData("CleanArchitecture.sln")]
    [InlineData("MyDmsVn.UiLab.sln")]
    public async Task Missing_or_wrong_solution_marker_prevents_identity_read_and_allocator(string? marker)
    {
        if (marker != null) File.WriteAllText(Path.Combine(_root, marker), "wrong solution marker");
        var reads = 0; var allocations = 0;
        await Assert.ThrowsAnyAsync<IOException>(async () =>
        {
            var source = Web6CandidateBackendSource.Admit(_root, root => { reads++; return (root, Sha, Tree); });
            await source.AllocateAsync(() => { allocations++; return Task.FromResult(1); });
        });
        Assert.Equal(0, reads); Assert.Equal(0, allocations);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("sha")]
    [InlineData("tree")]
    public async Task Prepared_source_mismatch_prevents_allocator(string mismatch)
    {
        File.WriteAllText(Path.Combine(_root, "MyDmsVn.sln"), "solution");
        var allocations = 0;
        await Assert.ThrowsAnyAsync<IOException>(async () =>
        {
            var source = Web6CandidateBackendSource.Admit(_root, root => (root, Sha, Tree),
                mismatch == "root" ? Path.Combine(_root, "other") : _root,
                mismatch == "sha" ? new string('c', 40) : Sha, mismatch == "tree" ? new string('d', 40) : Tree);
            await source.AllocateAsync(() => { allocations++; return Task.FromResult(1); });
        });
        Assert.Equal(0, allocations);
    }

    [Theory]
    [InlineData("marker")]
    [InlineData("git-root")]
    [InlineData("sha")]
    [InlineData("tree")]
    [InlineData("dirty")]
    public async Task Source_change_after_admission_stops_allocator(string change)
    {
        var marker = Path.Combine(_root, "MyDmsVn.sln"); File.WriteAllText(marker, "solution");
        var changed = false; var allocations = 0;
        var source = Web6CandidateBackendSource.Admit(_root, root =>
        {
            if (changed && change == "dirty") throw new IOException("Source dirty.");
            return (changed && change == "git-root" ? Path.GetDirectoryName(root)! : root,
                changed && change == "sha" ? new string('c', 40) : Sha,
                changed && change == "tree" ? new string('d', 40) : Tree);
        });
        changed = true;
        if (change == "marker") File.Move(marker, Path.Combine(_root, "CleanArchitecture.sln"));
        await Assert.ThrowsAnyAsync<IOException>(() => source.AllocateAsync(() => { allocations++; return Task.FromResult(1); }));
        Assert.Equal(0, allocations);
    }

    [Fact]
    public async Task Verified_source_is_reused_and_checked_before_allocator()
    {
        File.WriteAllText(Path.Combine(_root, "MyDmsVn.sln"), "solution");
        var calls = new List<string>();
        var source = Web6CandidateBackendSource.Admit(_root, root => { calls.Add("source"); return (root, Sha, Tree); });
        var result = await source.AllocateAsync(() => { calls.Add("allocate"); return Task.FromResult(42); });
        Assert.Equal(42, result); Assert.Equal(_root, source.Root);
        Assert.Equal(new[] { "source", "source", "allocate" }, calls);
    }

    [Fact]
    public void Actual_checkout_resolves_MyDmsVn_solution_marker()
    {
        var root = Web6CandidateBackendSource.ResolveRoot(AppContext.BaseDirectory);
        Assert.True(File.Exists(Path.Combine(root, "MyDmsVn.sln")));
        Assert.True(File.Exists(Path.Combine(root, "src", "MyDmsVn.Server.HttpHost", "MyDmsVn.Server.HttpHost.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "tests", "MyDmsVn.Server.Infrastructure.Security.Tests", "MyDmsVn.Server.Infrastructure.Security.Tests.csproj")));
    }

    [Fact]
    public void Paired_source_ignores_a_disposable_replacement_ref()
    {
        var original = Encoding.UTF8.GetBytes("verified bytes with CRLF\r\n");
        var oid = SeedPairedBlob(original);
        File.WriteAllText(Path.Combine(_root, "other.txt"), "separate fixture object");
        var other = Git(_root, "hash-object -w other.txt");
        var marker = "refs/replace/" + oid;
        Git(_root, "update-ref " + marker + " " + other);
        Assert.Equal(other, Git(_root, "rev-parse --verify " + marker));

        var settings = PairedGitStart(_root, "cat-file blob " + oid);
        Assert.Contains("--no-replace-objects", settings.Arguments);
        Assert.Contains("-c core.fsmonitor=false", settings.Arguments);
        Assert.Contains("-c core.hooksPath=NUL", settings.Arguments);
        Assert.Equal("1", settings.EnvironmentVariables["GIT_NO_REPLACE_OBJECTS"]);
        Assert.Equal("1", settings.EnvironmentVariables["GIT_CONFIG_NOSYSTEM"]);
        Assert.Equal("NUL", settings.EnvironmentVariables["GIT_CONFIG_SYSTEM"]);
        Assert.Equal("NUL", settings.EnvironmentVariables["GIT_CONFIG_GLOBAL"]);
        Assert.Equal(original, ReadPairedBlob(_root, oid));
    }

    [Fact]
    public void Paired_source_refuses_bytes_with_a_different_blob_identity()
    {
        var original = new byte[] { 0xff, 0x00, 0x0d, 0x0a };
        var oid = SeedPairedBlob(original);
        Assert.Equal(original, RequirePairedBlobIdentity(oid, original));
        Assert.Equal(original, ReadPairedBlob(_root, oid));
        Assert.Throws<IOException>(() => RequirePairedBlobIdentity(oid, Encoding.UTF8.GetBytes("different\r\n")));
    }

    [Theory]
    [InlineData("CoRe.FsMoNiToR", "disabled-fixture-path")]
    [InlineData("core.hooksPath", "unused-fixture-path")]
    [InlineData("AlIaS.StAtUs", "version")]
    [InlineData("include.path", "absent-fixture-config")]
    [InlineData("filter.fixture.clean", "unused-fixture-command")]
    [InlineData("core.worktree", ".")]
    public void Paired_source_refuses_external_git_configuration(string key, string value)
    {
        SeedPairedBlob(Encoding.UTF8.GetBytes("fixture"));
        Git(_root, "config --local " + key + " " + value);
        Assert.Throws<IOException>(() => RequirePairedGitConfig(_root));
    }

    [Fact]
    public void Paired_source_refuses_worktree_git_configuration()
    {
        SeedPairedBlob(Encoding.UTF8.GetBytes("fixture"));
        Git(_root, "config --local extensions.worktreeConfig true");
        Git(_root, "config --worktree core.fsmonitor false");
        Assert.Throws<IOException>(() => RequirePairedGitConfig(_root));
    }

    [Fact]
    public void Paired_source_does_not_execute_external_node_or_typescript()
    {
        var backend = Web6CandidateBackendSource.ResolveRoot(AppContext.BaseDirectory);
        var path = Path.Combine(backend, "tests", "MyDmsVn.Server.Infrastructure.Security.Tests", nameof(Web6CandidateBackendSourceTests) + ".cs");
        var source = File.ReadAllText(path);
        var prohibited = @"\b(?:BootstrapToolchain\s*\.\s*RunOwnedAsync|transpile" + @"Module|node[.]exe|node_" + @"modules)\b";
        Assert.DoesNotMatch(prohibited, source);
    }

    private string SeedPairedBlob(byte[] bytes)
    {
        Git(_root, "init --quiet");
        File.WriteAllBytes(Path.Combine(_root, "source.txt"), bytes);
        return Git(_root, "hash-object -w source.txt");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Paired_git_join_waits_for_both_delayed_readers_even_on_termination_failure(bool failTermination)
    {
        var stdout = new TaskCompletionSource<bool>();
        var stderr = new TaskCompletionSource<bool>();
        var terminationFailure = new IOException("fixture termination observation failed");
        var joined = GuiOwnedProcess.JoinPairedGitAsync(
            () => failTermination ? Task.FromException(terminationFailure) : Task.CompletedTask, stdout.Task, stderr.Task);
        var returnedBeforeOutput = joined.IsCompleted;
        stdout.SetResult(true);
        var returnedBeforeError = joined.IsCompleted;
        stderr.SetResult(true);
        if (failTermination)
            Assert.Same(terminationFailure, await Assert.ThrowsAsync<IOException>(() => joined));
        else
            await joined;
        Assert.False(returnedBeforeOutput);
        Assert.False(returnedBeforeError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Paired_git_runner_terminates_and_drains_the_disposable_child(bool cancel)
    {
        var start = CompilePairedGitFixture("hang");
        using var cancellation = new CancellationTokenSource();
        var work = GuiOwnedProcess.RunPairedGitAsync(start, TimeSpan.FromSeconds(cancel ? 10 : 3), cancellation.Token, PairedGitBytes);
        var pidFile = Path.Combine(_root, "child.pid");
        var clock = Stopwatch.StartNew();
        while (!File.Exists(pidFile) && clock.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(20);
        if (cancel) cancellation.Cancel();
        var failure = await Assert.ThrowsAsync<GuiProcessException>(() => work);
        Assert.True(failure.CleanupVerified);
        Assert.Equal(cancel ? "Canceled" : "TimedOut", failure.Reason);
        Assert.Equal("first-second", failure.StandardOutput);
        Assert.Equal("error-later", failure.StandardError);
        var pid = int.Parse(File.ReadAllText(pidFile), CultureInfo.InvariantCulture);
        Assert.False(IsPairedFixtureRunning(pid));
    }

    [Fact]
    public async Task Paired_git_runner_joins_success_and_reports_start_failure()
    {
        var start = CompilePairedGitFixture("success");
        var result = await GuiOwnedProcess.RunPairedGitAsync(start, TimeSpan.FromSeconds(5), default, PairedGitBytes);
        Assert.True(result.CleanupVerified);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("first-second", result.StandardOutput);
        Assert.Equal("error-later", result.StandardError);

        var invalid = Path.Combine(_root, "invalid.exe");
        File.WriteAllText(invalid, "synthetic invalid executable");
        var failure = new ProcessStartInfo(invalid) { WorkingDirectory = _root, UseShellExecute = false };
        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() =>
            GuiOwnedProcess.RunPairedGitAsync(failure, TimeSpan.FromSeconds(5), default, PairedGitBytes));
    }

    private ProcessStartInfo CompilePairedGitFixture(string mode)
    {
        var executable = Path.Combine(_root, "paired-git-fixture.exe");
        using var compiler = new CSharpCodeProvider();
        var result = compiler.CompileAssemblyFromSource(new CompilerParameters(new[] { "System.dll" }, executable)
        {
            GenerateExecutable = true,
        }, """
            using System;
            using System.Diagnostics;
            using System.IO;
            using System.Threading;
            public static class Fixture
            {
                public static void Main(string[] args)
                {
                    Console.Write("first");
                    Console.Error.Write("error");
                    Thread.Sleep(50);
                    Console.Write("-second");
                    Console.Error.Write("-later");
                    Console.Out.Flush();
                    Console.Error.Flush();
                    File.WriteAllText("child.pid.tmp", Process.GetCurrentProcess().Id.ToString());
                    File.Move("child.pid.tmp", "child.pid");
                    if (args[0] == "hang") Thread.Sleep(Timeout.Infinite);
                }
            }
            """);
        Assert.False(result.Errors.HasErrors);
        return new ProcessStartInfo(executable, mode) { WorkingDirectory = _root, UseShellExecute = false };
    }

    private static bool IsPairedFixtureRunning(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    [Web6PairedSourceFact]
    public async Task Prepared_frontend_cutover_matches_backend_wire_and_deadline_hierarchy()
    {
        const string reviewedSha = "fa032ece3d47af029d5095ae39b21fb43d0f1afc";
        const string reviewedTree = "e0cb0950f78d072e73850392b6bf496e574775ae";
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable(Web6PairedSourceFactAttribute.RootVariable)!);
        using var sourceRoot = new BootstrapPathPolicy.PinnedDirectory(root);
        RequirePairedGitConfig(root);
        Assert.Equal(root, Path.GetFullPath(PairedGitText(root, "rev-parse --show-toplevel")), ignoreCase: true);
        Assert.Equal(reviewedSha, PairedGitText(root, "rev-parse --verify HEAD"));
        Assert.Equal(reviewedTree, PairedGitText(root, "rev-parse --verify HEAD^{tree}"));
        Assert.Empty(PairedGitText(root, "status --porcelain --untracked-files=all --ignore-submodules=all"));

        const string liveSpec = "tests/pilot/web6-cutover-live.spec.ts";
        const string cutoverHelpers = "tests/pilot/web6-cutover-helpers.ts";
        const string deadlineSource = "tests/pilot/web6-cutover-deadline.ts";
        const string controlSource = "tests/pilot/web6-cutover-control.ts";
        var sources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in new[] { liveSpec, cutoverHelpers, deadlineSource, controlSource })
        {
            // Read the verified commit's blobs so index flags or a later edit cannot change the tested source.
            var blob = PairedGitText(root, "rev-parse --verify " + reviewedSha + ":" + path);
            sources.Add(path, ReadPairedBlob(root, blob));
        }
        sourceRoot.Verify();

        using var fixture = new Web6CandidateBootstrapPreflightTests();
        var receipt = await fixture.PrepareWithFrontendSources(reviewedSha, reviewedTree, sources);
        Assert.Equal("PIN_MATCHED", receipt.CutoverAdmission);
        var plan = Web6SelectorPlan.Create(receipt);
        Assert.Equal(reviewedSha, plan.Text("frontendSha"));
        Assert.Equal(reviewedTree, plan.Text("frontendTree"));
        var preparedRoot = plan.Text("frontendWorktree");
        Assert.Equal(Path.Combine(plan.TaskRoot, "frontend-current"), preparedRoot);
        string ReadPrepared(string path)
        {
            var bytes = File.ReadAllBytes(Path.Combine(preparedRoot, path));
            Assert.Equal(sources[path], bytes);
            return PairedUtf8.GetString(bytes);
        }
        VerifyPairedControlSource(ReadPrepared(deadlineSource), ReadPrepared(controlSource));

        var spec = ReadPrepared(liveSpec);
        var helpers = ReadPrepared(cutoverHelpers);
        Assert.Contains("operation.operationId === operationId", helpers);
        Assert.Contains("operation.frozen.request.operationKind === \"Sales.Create\"", helpers);
        Assert.Contains("operation.state === \"DISPATCHED\"", helpers);
        Assert.Contains("matches.length !== 1", helpers);
        Assert.Contains("selectDispatchedSalesCreateByWire(await readStore<DurableOperationRecord>(page, \"operations\"), wire!.operationId)", spec);
        var budgetRows = Regex.Matches(spec, @"\[\s*(\w+)\s*,\s*\[\s*([0-9_]+)\s*,\s*([0-9_]+)\s*\]\s*\]");
        var expected = new[]
        {
            ("cachedWeb4", Web6CutoverBudgets.FirstScenario, Web6CutoverBudgets.FirstResetReserve),
            ("currentClient", Web6CutoverBudgets.SecondScenario, Web6CutoverBudgets.SecondResetReserve),
            ("restartRevokeRestore", Web6CutoverBudgets.ThirdScenario, Web6CutoverBudgets.ThirdResetReserve),
        };
        var actual = budgetRows.Cast<Match>().Select(row => (row.Groups[1].Value,
            ReadMilliseconds(row.Groups[2].Value), ReadMilliseconds(row.Groups[3].Value))).ToArray();
        Assert.Equal(expected, actual);
        var pack = TimeSpan.FromMilliseconds(actual.Sum(row => row.Item2.TotalMilliseconds));
        Assert.True(pack < Web6CutoverBudgets.BrowserPack);
        Assert.True(Web6CutoverBudgets.BrowserPack < Web6CutoverBudgets.Controller);
        Assert.True(Web6CutoverBudgets.Controller + Web6CutoverBudgets.Controller < Web6CutoverBudgets.AggregateParent);
        Assert.True(Web6CutoverBudgets.AggregateParent < Web6CutoverSelectorBudgets.Gate);
        Assert.True(Web6CutoverSelectorBudgets.Gate < Web6CutoverSelectorBudgets.ProcessMaximum);
        Assert.Equal(TimeSpan.FromSeconds(60), Web6CutoverSelectorBudgets.ProcessMaximum - Web6CutoverSelectorBudgets.Gate);

        Assert.Single(Regex.Matches(spec, @"\btest\s*\.\s*afterEach\s*\(").Cast<Match>());
        var resetHook = Assert.Single(Regex.Matches(spec, @"\btest\s*\.\s*afterEach\s*\((?<body>[\s\S]*?)^\s*\}\s*\)\s*;",
            RegexOptions.Multiline).Cast<Match>()).Groups["body"].Value;
        const string resetFrame = @"\bsendCutoverFrame\s*\(\s*""resetCutover""\s*,";
        Assert.Single(Regex.Matches(spec, resetFrame).Cast<Match>());
        Assert.Matches(@"\bscenario\s*\.\s*reset\s*\([^;]+" + resetFrame, resetHook);
        Assert.Matches(@"\btestInfo\s*\.\s*setTimeout\s*\(\s*Math\s*\.\s*max\s*\(\s*1\s*,\s*scenario\s*\.\s*remainingTotalMs\s*\(\s*\)\s*\)\s*\)", resetHook);
        Assert.DoesNotMatch(@"\b(?:oldClientNewHostOff|newClientOldHostOff|revokeGrantOff)\b", resetHook);
    }

    private static TimeSpan ReadMilliseconds(string value) => TimeSpan.FromMilliseconds(int.Parse(value.Replace("_", ""),
        CultureInfo.InvariantCulture));

    private static void VerifyPairedControlSource(string deadlines, string control)
    {
        var reserve = Assert.Single(Regex.Matches(deadlines, @"\bCUTOVER_RESPONSE_RESERVE_MS\s*=\s*([0-9_]+)\s*;")
            .Cast<Match>());
        Assert.Equal(Web6CutoverBudgets.ResponseReserve, ReadMilliseconds(reserve.Groups[1].Value));
        var map = Assert.Single(Regex.Matches(deadlines, @"\bcutoverActionCaps\s*=\s*\{([^}]*)\}").Cast<Match>());
        var caps = map.Groups[1].Value.Split(',').Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => Assert.Single(Regex.Matches(entry, @"\A\s*(\w+)\s*:\s*([0-9_]+)\s*\z").Cast<Match>()))
            .ToDictionary(entry => entry.Groups[1].Value, entry => ReadMilliseconds(entry.Groups[2].Value), StringComparer.Ordinal);
        Assert.Equal(11, caps.Count);
        Assert.Matches(@"\bbodyDeadline\s*=\s*totalDeadline\s*-\s*resetReserveMs\b", deadlines);
        Assert.Matches(@"\bMath\s*\.\s*min\s*\(\s*cutoverActionCaps\s*\.\s*resetCutover\s*,\s*resetReserveMs\s*\)", deadlines);

        var operationSet = Assert.Single(Regex.Matches(control,
            @"\boperationActions\s*=\s*new\s+Set\s*<CutoverAction>\s*\(\s*\[([^]]*)\]").Cast<Match>());
        var operationActions = Regex.Matches(operationSet.Groups[1].Value, @"""(\w+)""").Cast<Match>()
            .Select(entry => entry.Groups[1].Value).ToArray();
        Assert.All(operationActions, action => Assert.True(caps.ContainsKey(action)));
        Assert.Matches(@"\baction\s*!==\s*""revokeGrantOff""\s*&&\s*operationActions\s*\.\s*has", control);
        Assert.Matches(@"\baction\s*===\s*""committedUnavailable""", control);
        Assert.Matches(@"\bJSON\s*\.\s*stringify\s*\(\s*\{\s*id\s*,\s*token\s*,\s*action\s*,\s*requestAllowanceMs\s*," +
            @"\s*\.\.\.\s*\(\s*hasOperation\s*\?\s*\{\s*operationId\s*\}\s*:\s*\{\s*\}\s*\)\s*," +
            @"\s*\.\.\.\s*\(\s*committedSetup\s*===\s*undefined\s*\?\s*\{\s*\}\s*:\s*\{\s*committedSetup\s*\}\s*\)\s*,?\s*\}\s*\)", control);

        const string token = "fixture-token";
        const string operation = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        foreach (var cap in caps)
        {
            var frame = new JObject
            {
                ["id"] = 7, ["token"] = token, ["action"] = cap.Key, ["requestAllowanceMs"] = (int)cap.Value.TotalMilliseconds,
            };
            if (operationActions.Contains(cap.Key))
                frame["operationId"] = operation;
            if (cap.Key == "committedUnavailable")
                frame["committedSetup"] = new JObject
                {
                    ["operationId"] = operation, ["operationKind"] = "Sales.Create", ["requestFingerprint"] = new string('a', 64),
                    ["authorizedBusinessEffects"] = 1, ["checkId"] = "web6-cutover-authentic-committed-setup",
                };

            var request = Web6CutoverControl.Validate(frame, token);
            Assert.Equal(cap.Key, request.Action);
            Assert.Equal(Web6CutoverBudgets.ActionCap(request.Action), request.RequestAllowance);
            Assert.Equal(request.RequestAllowance - Web6CutoverBudgets.ResponseReserve, request.WorkAllowance);
            Assert.True(request.WorkAllowance < Web6CutoverBudgets.FirstScenario);
            if (cap.Key == "revokeGrantOff")
            {
                Assert.Null(request.Operation);
                frame["operationId"] = operation;
                Assert.Equal(Guid.Parse(operation), Web6CutoverControl.Validate(frame, token).Operation);
            }

            frame["requestAllowanceMs"] = (int)cap.Value.TotalMilliseconds + 1;
            Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, token));
            frame["requestAllowanceMs"] = (int)Web6CutoverBudgets.ResponseReserve.TotalMilliseconds;
            Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, token));
            frame.Remove("requestAllowanceMs");
            frame["remainingMs"] = (int)cap.Value.TotalMilliseconds;
            Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, token));
        }
    }

    [Theory]
    [InlineData("dirty-submodule")]
    [InlineData("untracked")]
    [InlineData("external-config")]
    [InlineData("wrong-git-root")]
    [InlineData("junction-root")]
    public async Task Live_backend_reader_refuses_hidden_or_redirected_source_before_allocator(string change)
    {
        Git(_root, "init --quiet");
        File.WriteAllText(Path.Combine(_root, "MyDmsVn.sln"), "synthetic solution");
        var submodule = Directory.CreateDirectory(Path.Combine(_root, "vendor", "sample")).FullName;
        Git(submodule, "init --quiet");
        File.WriteAllText(Path.Combine(submodule, "tracked.txt"), "tracked");
        Git(submodule, "add tracked.txt");
        Git(submodule, "commit --quiet -m synthetic-submodule");
        Git(_root, "config diff.ignoreSubmodules all");
        Git(_root, "config status.showUntrackedFiles no");
        Git(_root, "update-index --add --cacheinfo 160000," + Git(submodule, "rev-parse HEAD") + ",vendor/sample");
        Git(_root, "add MyDmsVn.sln");
        Git(_root, "commit --quiet -m synthetic-root");
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        var calls = 0;
        Func<Task<int>> allocate = () => { calls++; return Task.FromResult(1); };
        switch (change)
        {
            case "dirty-submodule":
                File.WriteAllText(Path.Combine(submodule, "tracked.txt"), "uncommitted change");
                Assert.Equal("", Git(_root, "status --porcelain"));
                await Assert.ThrowsAnyAsync<IOException>(() => source.AllocateAsync(allocate));
                break;
            case "untracked":
                File.WriteAllText(Path.Combine(_root, "new.txt"), "untracked change");
                Assert.Equal("", Git(_root, "status --porcelain"));
                await Assert.ThrowsAnyAsync<IOException>(() => source.AllocateAsync(allocate));
                break;
            case "external-config":
                Git(_root, "config include.path missing-fixture-config");
                await Assert.ThrowsAnyAsync<IOException>(() => source.AllocateAsync(allocate));
                break;
            case "wrong-git-root":
                var nested = Directory.CreateDirectory(Path.Combine(_root, "wrong")).FullName;
                File.WriteAllText(Path.Combine(nested, "MyDmsVn.sln"), "fake solution marker");
                await Assert.ThrowsAnyAsync<IOException>(() => WebHostIisExpressTests.AdmitCurrentBackend(nested).AllocateAsync(allocate));
                break;
            case "junction-root":
                var alias = _root + "-alias";
                using (var command = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                    "/d /c mklink /J \"" + alias + "\" \"" + _root + "\"") { UseShellExecute = false, CreateNoWindow = true }))
                {
                    Assert.NotNull(command);
                    Assert.True(command!.WaitForExit(5000));
                    Assert.Equal(0, command.ExitCode);
                }
                try { await Assert.ThrowsAnyAsync<IOException>(() => WebHostIisExpressTests.AdmitCurrentBackend(alias).AllocateAsync(allocate)); }
                finally { Directory.Delete(alias); }
                break;
        }
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Live_backend_reader_ignores_ambient_git_directory()
    {
        Git(_root, "init --quiet");
        File.WriteAllText(Path.Combine(_root, "MyDmsVn.sln"), "synthetic solution");
        Git(_root, "add MyDmsVn.sln");
        Git(_root, "commit --quiet -m synthetic-root");
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        var prior = Environment.GetEnvironmentVariable("GIT_DIR");
        try
        {
            Environment.SetEnvironmentVariable("GIT_DIR", Path.Combine(_root, "absent-git-directory"));
            Assert.Equal(1, await source.AllocateAsync(() => Task.FromResult(1)));
        }
        finally { Environment.SetEnvironmentVariable("GIT_DIR", prior); }
    }

    [Theory]
    [InlineData("assume-unchanged", false)]
    [InlineData("skip-worktree", false)]
    [InlineData("assume-unchanged", true)]
    [InlineData("skip-worktree", true)]
    [InlineData("external-include", true)]
    [InlineData("ignore-untracked", true)]
    [InlineData("sparse-checkout", false)]
    public async Task Strict_source_refuses_index_flags_or_nested_config_before_allocator(string change, bool nested)
    {
        var leaf = CreateNestedCheckout();
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        var checkout = nested ? leaf : _root;
        var file = nested ? "tracked.txt" : "deployment/sql/Provision-MyDmsVnWebPrincipals.sql";
        if (change is "assume-unchanged" or "skip-worktree")
        {
            Git(checkout, "update-index --" + change + " -- " + file);
            File.AppendAllText(Path.Combine(checkout, file), "\nchanged");
        }
        else if (change == "external-include") Git(leaf, "config include.path absent-fixture-config");
        else if (change == "ignore-untracked") Git(leaf, "config status.showUntrackedFiles no");
        else Git(_root, "config core.sparseCheckout true");

        Assert.Equal("", Git(_root, "status --porcelain --untracked-files=all --ignore-submodules=none"));
        var indexBefore = Git(checkout, "ls-files -v");
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => source.AllocateAsync(() => { calls++; return Task.FromResult(1); }));
        Assert.Equal(0, calls);
        Assert.Equal(indexBefore, Git(checkout, "ls-files -v"));
    }

    [Fact]
    public async Task Strict_source_admits_clean_initialized_nested_checkouts()
    {
        CreateNestedCheckout();
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        Assert.Equal(42, await source.AllocateAsync(() => Task.FromResult(42)));
    }

    [Fact]
    public void Cutover_provisioning_text_uses_admitted_commit_after_worktree_edits()
    {
        CreateNestedCheckout();
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        var script = WebHostIisExpressTests.ReadCutoverProvisioningScript(source);
        File.WriteAllText(Path.Combine(_root, "deployment", "sql", "Provision-MyDmsVnWebPrincipals.sql"), "SELECT 2;\n");
        Assert.Equal("SELECT 1;\n", script);
        Assert.Equal(script, WebHostIisExpressTests.ReadCutoverProvisioningScript(source));
    }

    [Theory]
    [InlineData("changed-commit")]
    [InlineData("staged-gitlink")]
    [InlineData("junction")]
    public async Task Strict_source_refuses_nested_gitlink_identity_drift(string drift)
    {
        var leaf = CreateNestedCheckout();
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        var calls = 0;
        Func<Task<int>> allocate = () => { calls++; return Task.FromResult(1); };
        if (drift == "junction")
        {
            var moved = Path.Combine(_root, "leaf-outside");
            Directory.Move(leaf, moved);
            using var command = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                "/d /c mklink /J \"" + leaf + "\" \"" + moved + "\"") { UseShellExecute = false, CreateNoWindow = true });
            Assert.NotNull(command);
            Assert.True(command!.WaitForExit(5000));
            Assert.Equal(0, command.ExitCode);
            try { await Assert.ThrowsAsync<IOException>(() => source.AllocateAsync(allocate)); }
            finally { Directory.Delete(leaf); }
        }
        else
        {
            Git(leaf, "commit --allow-empty --quiet -m changed-head");
            if (drift == "staged-gitlink")
                Git(Path.Combine(_root, "vendor", "first"), "update-index --cacheinfo 160000," + Git(leaf, "rev-parse HEAD") + ",vendor/second");
            await Assert.ThrowsAsync<IOException>(() => source.AllocateAsync(allocate));
        }
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Strict_source_leaves_uninitialized_gitlinks_alone()
    {
        var leaf = CreateNestedCheckout();
        var parent = Path.Combine(_root, "vendor", "first");
        var marker = Path.Combine(leaf, ".git");
        Directory.Move(marker, Path.Combine(_root, ".git", "fixture-detached-leaf"));
        File.Move(Path.Combine(leaf, "tracked.txt"), Path.Combine(_root, ".git", "fixture-detached-file"));
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        Assert.Equal(42, await source.AllocateAsync(() => Task.FromResult(42)));
        Assert.Empty(Directory.GetFileSystemEntries(leaf));
        Assert.Equal("160000", Git(parent, "ls-files --stage -- vendor/second").Substring(0, 6));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Cutover_refuses_hidden_current_source_change_before_the_next_live_callback(int ordinal)
    {
        CreateNestedCheckout();
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        using var artifacts = new Web6CutoverSelectorTests();
        var selector = artifacts.Selector();
        var calls = new List<Web6CutoverPair>();
        var refusal = (Web6CutoverPair)ordinal;
        await Assert.ThrowsAsync<IOException>(() => selector.AllocateAsync(() => source.Verify(),
            pair => { calls.Add(pair); return Task.FromResult(pair); }, (_, _) => Task.CompletedTask,
            prepare: pair =>
            {
                if (pair != refusal) return;
                const string script = "deployment/sql/Provision-MyDmsVnWebPrincipals.sql";
                Git(_root, "update-index --assume-unchanged -- " + script);
                File.AppendAllText(Path.Combine(_root, script), "\nchanged between cutover allocations");
            }));
        Assert.Equal(Web6CutoverSelector.Pairs.Take(ordinal), calls);
    }

    [Theory]
    [InlineData("absent", false, 1)]
    [InlineData("absent", false, 2)]
    [InlineData("empty", false, 1)]
    [InlineData("empty", false, 2)]
    [InlineData("absent", true, 1)]
    [InlineData("absent", true, 2)]
    [InlineData("empty", true, 1)]
    [InlineData("empty", true, 2)]
    [InlineData("initialized", false, 1)]
    [InlineData("initialized", false, 2)]
    [InlineData("initialized-empty", true, 1)]
    [InlineData("initialized-empty", true, 2)]
    [InlineData("replacement", false, 1)]
    [InlineData("replacement", false, 2)]
    [InlineData("replacement", true, 1)]
    [InlineData("replacement", true, 2)]
    [InlineData("denied", true, 1)]
    [InlineData("denied", true, 2)]
    [InlineData("junction", true, 1)]
    [InlineData("junction", true, 2)]
    [InlineData("file-parent", true, 1)]
    [InlineData("file-parent", true, 2)]
    [InlineData("sharing", true, 1)]
    [InlineData("sharing", true, 2)]
    public async Task Cutover_pins_initial_gitlink_inventory_before_each_callback(string change, bool nested, int ordinal)
    {
        var leaf = CreateNestedCheckout();
        var child = nested ? leaf : Path.Combine(_root, "vendor", "first");
        var detached = Path.Combine(_root, ".git", "detached-inventory");
        if (change.StartsWith("initialized", StringComparison.Ordinal))
        {
            Directory.Move(child, detached);
            if (change == "initialized-empty") Directory.CreateDirectory(child);
        }
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        using var artifacts = new Web6CutoverSelectorTests();
        var calls = new List<Web6CutoverPair>();
        string? priorAcl = null;
        IDisposable? conflictingHandle = null;
        string? junction = null;
        try
        {
            await Assert.ThrowsAnyAsync<IOException>(() => artifacts.Selector().AllocateAsync(() => source.Verify(),
                pair => { calls.Add(pair); return Task.FromResult(pair); }, (_, _) => Task.CompletedTask,
                prepare: pair =>
                {
                    if ((int)pair != ordinal) return;
                    if (change.StartsWith("initialized", StringComparison.Ordinal))
                    {
                        if (change == "initialized-empty") Directory.Delete(child);
                        Directory.Move(detached, child);
                        return;
                    }
                    if (change == "sharing")
                    {
                        using var parent = Web6CandidateFileSystem.OpenDirectory(Path.GetDirectoryName(child)!);
                        conflictingHandle = Web6CandidateFileSystem.OpenRelativeForDelete(parent, Path.GetFileName(child));
                        return;
                    }
                    if (change == "denied")
                    {
                        priorAcl = Directory.GetAccessControl(child).GetSecurityDescriptorSddlForm(AccessControlSections.Access);
                        var denied = Directory.GetAccessControl(child);
                        denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,
                            FileSystemRights.ListDirectory | FileSystemRights.ReadAttributes, AccessControlType.Deny));
                        Directory.SetAccessControl(child, denied);
                        return;
                    }
                    if (change == "file-parent")
                    {
                        var parent = Path.GetDirectoryName(child)!;
                        Directory.Move(parent, detached);
                        File.WriteAllText(parent, "not a directory");
                        return;
                    }
                    Directory.Move(child, detached);
                    if (change is "empty" or "replacement") Directory.CreateDirectory(child);
                    if (change == "replacement")
                        foreach (var entry in Directory.GetFileSystemEntries(detached))
                        {
                            var destination = Path.Combine(child, Path.GetFileName(entry));
                            if (Directory.Exists(entry)) Directory.Move(entry, destination);
                            else File.Move(entry, destination);
                        }
                    if (change == "junction")
                    {
                        using var command = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                            "/d /c mklink /J \"" + child + "\" \"" + detached + "\"") { UseShellExecute = false, CreateNoWindow = true });
                        Assert.NotNull(command); Assert.True(command!.WaitForExit(5000)); Assert.Equal(0, command.ExitCode);
                        junction = child;
                    }
                }));
            Assert.Equal(new[] { Web6CutoverPair.Current, Web6CutoverPair.Old }.Take(ordinal), calls);
        }
        finally
        {
            conflictingHandle?.Dispose();
            if (priorAcl != null)
            {
                var restored = new DirectorySecurity();
                restored.SetSecurityDescriptorSddlForm(priorAcl, AccessControlSections.Access);
                Directory.SetAccessControl(child, restored);
            }
            if (junction != null) Directory.Delete(junction);
        }
    }

    [Theory]
    [InlineData("initialized")]
    [InlineData("absent")]
    [InlineData("empty")]
    public async Task Cutover_preserves_stable_gitlink_inventory_for_all_three_callbacks(string state)
    {
        var leaf = CreateNestedCheckout();
        if (state != "initialized")
        {
            Directory.Move(leaf, Path.Combine(_root, ".git", "detached-inventory"));
            if (state == "empty") Directory.CreateDirectory(leaf);
        }
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        using var artifacts = new Web6CutoverSelectorTests();
        var calls = new List<Web6CutoverPair>();
        await artifacts.Selector().AllocateAsync(() => source.Verify(),
            pair => { calls.Add(pair); return Task.FromResult(pair); }, (_, _) => Task.CompletedTask);
        Assert.Equal(new[] { Web6CutoverPair.Current, Web6CutoverPair.Old, Web6CutoverPair.Restored }, calls);
    }

    private string CreateNestedCheckout()
    {
        Git(_root, "init --quiet");
        File.WriteAllText(Path.Combine(_root, "MyDmsVn.sln"), "synthetic solution");
        var scripts = Directory.CreateDirectory(Path.Combine(_root, "deployment", "sql")).FullName;
        File.WriteAllText(Path.Combine(scripts, "Provision-MyDmsVnWebPrincipals.sql"), "SELECT 1;\n");
        var child = Directory.CreateDirectory(Path.Combine(_root, "vendor", "first")).FullName;
        var leaf = Directory.CreateDirectory(Path.Combine(child, "vendor", "second")).FullName;
        Git(leaf, "init --quiet");
        File.WriteAllText(Path.Combine(leaf, "tracked.txt"), "original");
        Git(leaf, "add tracked.txt");
        Git(leaf, "commit --quiet -m synthetic-leaf");
        Git(child, "init --quiet");
        Git(child, "update-index --add --cacheinfo 160000," + Git(leaf, "rev-parse HEAD") + ",vendor/second");
        Git(child, "commit --quiet -m synthetic-child");
        Git(_root, "update-index --add --cacheinfo 160000," + Git(child, "rev-parse HEAD") + ",vendor/first");
        Git(_root, "add MyDmsVn.sln deployment");
        Git(_root, "commit --quiet -m synthetic-root");
        return leaf;
    }

    private static readonly UTF8Encoding PairedUtf8 = new UTF8Encoding(false, true);
    private static readonly Encoding PairedGitBytes = Encoding.GetEncoding(28591);

    private static ProcessStartInfo PairedGitStart(string root, string arguments)
    {
        var start = GitStart(root, arguments);
        start.FileName = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')
            .Select(path => path.Trim().Trim('"')).Where(Path.IsPathRooted)
            .Select(path => Path.Combine(path, "git.exe")).First(File.Exists);
        start.Arguments = "--no-replace-objects --no-optional-locks -c core.fsmonitor=false -c core.autocrlf=false " +
            "-c core.ignoreStat=false -c core.excludesFile=NUL -c core.attributesFile=NUL -c submodule.recurse=false " + start.Arguments;
        start.EnvironmentVariables["GIT_NO_REPLACE_OBJECTS"] = "1";
        start.EnvironmentVariables["GIT_NO_LAZY_FETCH"] = "1";
        start.EnvironmentVariables["GIT_CONFIG_SYSTEM"] = "NUL";
        start.EnvironmentVariables["GIT_ATTR_NOSYSTEM"] = "1";
        start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
        return start;
    }

    private static byte[] RequirePairedBlobIdentity(string oid, byte[] bytes)
    {
        var header = Encoding.ASCII.GetBytes("blob " + bytes.Length.ToString(CultureInfo.InvariantCulture) + "\0");
        using var sha = SHA1.Create(); // Git SHA-1 object identity, not an application signing algorithm.
        sha.TransformBlock(header, 0, header.Length, null, 0);
        sha.TransformFinalBlock(bytes, 0, bytes.Length);
        var actual = BitConverter.ToString(sha.Hash!).Replace("-", "").ToLowerInvariant();
        if (!string.Equals(actual, oid, StringComparison.Ordinal))
            throw new IOException("Paired Git blob bytes differ from the expected object ID.");
        return bytes;
    }

    private static byte[] ReadPairedBlob(string root, string oid)
    {
        if (!BootstrapJson.IsLowerHex(oid, 40)) throw new IOException("Paired Git returned an invalid blob object ID.");
        return RequirePairedBlobIdentity(oid, ReadPairedGit(root, "cat-file blob " + oid));
    }

    private static string PairedGitText(string root, string arguments) => PairedUtf8.GetString(ReadPairedGit(root, arguments)).Trim();

    private static byte[] ReadPairedGit(string root, string arguments)
    {
        GuiProcessResult result;
        try
        {
            result = GuiOwnedProcess.RunPairedGitAsync(PairedGitStart(root, arguments), TimeSpan.FromSeconds(10), default, PairedGitBytes)
                .GetAwaiter().GetResult();
        }
        catch (Exception failure) when (failure is GuiProcessException or System.ComponentModel.Win32Exception)
        {
            throw new IOException("Paired Git source verification did not complete with verified cleanup.");
        }
        if (result.ExitCode != 0 || result.StandardError.Length != 0 || result.StandardOutput.Length >= GuiOwnedProcess.OutputLimit)
            throw new IOException("Paired Git source verification failed.");
        return PairedGitBytes.GetBytes(result.StandardOutput);
    }

    private static void RequirePairedGitConfig(string root)
    {
        var local = PairedGitText(root, "config --local --no-includes --null --list");
        var worktree = PairedGitText(root, "config --worktree --no-includes --null --list");
        foreach (var entry in (local + "\0" + worktree).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var newline = entry.IndexOf('\n');
            var key = (newline < 0 ? entry : entry.Substring(0, newline)).ToLowerInvariant();
            var value = newline < 0 ? "" : entry.Substring(newline + 1);
            var harmless = key is "core.filemode" or "core.bare" or "core.logallrefupdates" or "core.symlinks" or
                "core.ignorecase" or "codex.localenvironmentconfigpath" ||
                (key == "core.repositoryformatversion" && value == "0") || key == "extensions.worktreeconfig" ||
                key.StartsWith("branch.", StringComparison.Ordinal) ||
                (key.StartsWith("remote.", StringComparison.Ordinal) &&
                    (key.EndsWith(".url", StringComparison.Ordinal) || key.EndsWith(".fetch", StringComparison.Ordinal)));
            if (!harmless) throw new IOException("Paired checkout has unsupported local or worktree Git configuration.");
        }
    }

    private static ProcessStartInfo GitStart(string root, string arguments)
    {
        var start = new ProcessStartInfo("git",
            "-c user.name=Fixture -c user.email=fixture@example.invalid -c core.hooksPath=NUL " + arguments)
        {
            WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var key in new List<string>(start.EnvironmentVariables.Keys.Cast<string>()))
            if (key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)) start.EnvironmentVariables.Remove(key);
        start.EnvironmentVariables["GIT_CONFIG_NOSYSTEM"] = "1";
        start.EnvironmentVariables["GIT_CONFIG_GLOBAL"] = "NUL";
        return start;
    }

    private static string Git(string root, string arguments)
    {
        var start = GitStart(root, arguments);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(5000));
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
        return output.Trim();
    }

    public void Dispose()
    {
        foreach (var path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        Directory.Delete(_root, true);
    }
}

internal sealed class Web6PairedSourceFactAttribute : FactAttribute
{
    internal const string RootVariable = "MYDMSVN_WEB6_PAIRED_SOURCE_ROOT";

    public Web6PairedSourceFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(RootVariable)))
            Skip = "Set " + RootVariable + " to the reviewed frontend checkout.";
    }
}
