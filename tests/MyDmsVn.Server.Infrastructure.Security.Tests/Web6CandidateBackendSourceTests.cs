using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

    [Web6PairedSourceFact]
    public async Task Prepared_frontend_cutover_matches_backend_wire_and_deadline_hierarchy()
    {
        const string reviewedSha = "96842a6329191713282b3212e5420872eb0f3a41";
        const string reviewedTree = "a8095060d0c578d8556a58c5301e11df4b4aff97";
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable(Web6PairedSourceFactAttribute.RootVariable)!);
        Assert.Equal(root, Path.GetFullPath(Git(root, "rev-parse --show-toplevel")), ignoreCase: true);
        Assert.Equal(reviewedSha, Git(root, "rev-parse --verify HEAD"));
        Assert.Equal(reviewedTree, Git(root, "rev-parse --verify HEAD^{tree}"));
        Assert.Empty(Git(root, "--no-optional-locks status --porcelain --untracked-files=all --ignore-submodules=none"));

        const string liveSpec = "tests/pilot/web6-cutover-live.spec.ts";
        var sources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in new[] { liveSpec, "tests/pilot/web6-cutover-deadline.ts", "tests/pilot/web6-cutover-control.ts" })
        {
            // Read the verified commit's blobs so index flags or a later edit cannot change the tested source.
            var blob = Git(root, "rev-parse " + reviewedSha + ":" + path);
            sources.Add(path, Encoding.UTF8.GetBytes(Git(root, "cat-file blob " + blob, trim: false)));
        }

        using var fixture = new Web6CandidateBootstrapPreflightTests();
        var receipt = await fixture.PrepareWithFrontendSources(reviewedSha, reviewedTree, sources);
        Assert.Equal("PIN_MATCHED", receipt.CutoverAdmission);
        var plan = Web6SelectorPlan.Create(receipt);
        Assert.Equal(reviewedSha, plan.Text("frontendSha"));
        Assert.Equal(reviewedTree, plan.Text("frontendTree"));
        var preparedRoot = plan.Text("frontendWorktree");
        Assert.Equal(Path.Combine(plan.TaskRoot, "frontend-current"), preparedRoot);
        var frontend = await ReadFrontendWireAsync(root, preparedRoot);
        Assert.Equal(Web6CutoverBudgets.ResponseReserve.TotalMilliseconds, (double)frontend["responseReserve"]!);
        Assert.Equal(3, ((JArray)frontend["scenarios"]!).Count);
        var frames = (JArray)frontend["frames"]!;
        Assert.Equal(11, frames.Select(frame => (string)frame["action"]!).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(12, frames.Count); // revokeGrantOff is exercised both with and without an operation ID.
        foreach (var emitted in frames)
        {
            var bytes = Convert.FromBase64String((string)emitted["frame"]!);
            Assert.Equal(bytes.Length - sizeof(int), BitConverter.ToInt32(bytes, 0));
            var frame = JObject.Parse(Encoding.UTF8.GetString(bytes, sizeof(int), bytes.Length - sizeof(int)));
            var request = Web6CutoverControl.Validate(frame, "fixture-token");
            Assert.Equal((string)emitted["action"]!, request.Action);
            Assert.Equal(Web6CutoverBudgets.ActionCap(request.Action), request.RequestAllowance);
            Assert.Equal(request.RequestAllowance - Web6CutoverBudgets.ResponseReserve, request.WorkAllowance);
            Assert.True(request.WorkAllowance < Web6CutoverBudgets.FirstScenario);

            frame["requestAllowanceMs"] = (int)request.RequestAllowance.TotalMilliseconds + 1;
            Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "fixture-token"));
            frame["requestAllowanceMs"] = (int)Web6CutoverBudgets.ResponseReserve.TotalMilliseconds;
            Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "fixture-token"));
            frame.Remove("requestAllowanceMs");
            frame["remainingMs"] = (int)request.RequestAllowance.TotalMilliseconds;
            Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "fixture-token"));
        }

        var spec = File.ReadAllText(Path.Combine(preparedRoot, liveSpec));
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
        Assert.Equal(actual.Select(row => row.Item3.TotalMilliseconds), ((JArray)frontend["scenarios"]!).Select(row => (double)row!));
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
        System.Globalization.CultureInfo.InvariantCulture));

    private static async Task<JObject> ReadFrontendWireAsync(string frontendRoot, string preparedRoot)
    {
        // Execute only the committed pure deadline module and frame builder. The builder never needs a transport.
        const string script = """
            const fs = require('node:fs');
            const ts = require(process.argv[1]);
            function load(file, imports) {
              const source = fs.readFileSync(`tests/pilot/${file}.ts`, 'utf8');
              const { outputText } = ts.transpileModule(source, { compilerOptions: {
                module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022,
              } });
              const exports = {};
              new Function('exports', 'require', outputText)(exports, name => {
                if (Object.hasOwn(imports, name)) return imports[name];
                throw Error(`Unexpected cutover module import: ${name}`);
              });
              return exports;
            }
            const deadlines = load('web6-cutover-deadline', {});
            const { createCutoverControlFrame } = load('web6-cutover-control', {
              './web6-cutover-deadline': deadlines, './web6-control-transport': {},
            });
            const operationId = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
            const operationActions = new Set(['sameEpochRestart', 'revokeGrantOn', 'revokeGrantOff',
              'restoreEpoch', 'committedUnavailable', 'verifyNoCutoverMutation']);
            const proof = { operationId, operationKind: 'Sales.Create', requestFingerprint: 'a'.repeat(64),
              authorizedBusinessEffects: 1, checkId: 'web6-cutover-authentic-committed-setup' };
            const frames = Object.entries(deadlines.cutoverActionCaps).map(([action, cap]) => ({
              action, frame: createCutoverControlFrame(7, 'fixture-token', action, cap,
                operationActions.has(action) ? operationId : undefined,
                action === 'committedUnavailable' ? proof : undefined).toString('base64'),
            }));
            const off = 'revokeGrantOff';
            frames.push({ action: off, frame: createCutoverControlFrame(7, 'fixture-token', off,
              deadlines.cutoverActionCaps[off]).toString('base64') });
            async function resetAllowances() {
              const scenarios = [];
              for (const [total, reserve] of [[420000, 180000], [420000, 180000], [690000, 60000]]) {
                let now = 0;
                const owner = deadlines.createCutoverScenarioDeadline(total, reserve, () => now);
                now = total - reserve;
                await owner.reset(async allowance => scenarios.push(allowance));
              }
              return scenarios;
            }
            resetAllowances().then(scenarios => process.stdout.write(JSON.stringify({
              responseReserve: deadlines.CUTOVER_RESPONSE_RESERVE_MS, frames, scenarios,
            })));
            """;
        var node = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')
            .Select(path => Path.Combine(path.Trim().Trim('"'), "node.exe")).First(File.Exists);
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP" })
            if (Environment.GetEnvironmentVariable(name) is { } value) environment.Add(name, value);
        var output = await BootstrapToolchain.RunOwnedAsync(new BootstrapProcessRequest(node, preparedRoot,
            new[] { "--input-type=commonjs", "-e", script, Path.Combine(frontendRoot, "node_modules", "typescript") },
            environment, TimeSpan.FromSeconds(30)), CancellationToken.None);
        return JObject.Parse(output);
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

    private static string Git(string root, string arguments, bool trim = true)
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
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(5000));
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
        return trim ? output.Trim() : output;
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
            Skip = "Set " + RootVariable + " to the reviewed frontend checkout with its locked dependencies installed.";
    }
}
