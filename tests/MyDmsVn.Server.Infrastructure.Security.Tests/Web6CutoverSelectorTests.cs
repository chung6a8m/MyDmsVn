using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using MyDmsVn.Web6.CandidateBootstrap;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CutoverSelectorTests : IDisposable
{
    [Theory]
    [InlineData("exact", 3600)]
    [InlineData("name", 1500)]
    [InlineData("filter", 1500)]
    [InlineData("project", 1500)]
    [InlineData("directory", 1500)]
    [InlineData("enable-missing", 1500)]
    [InlineData("enable-value", 1500)]
    [InlineData("run-missing", 1500)]
    [InlineData("run-empty", 1500)]
    [InlineData("pair-missing", 1500)]
    [InlineData("pair-invalid", 1500)]
    [InlineData("candidate", 1500)]
    [InlineData("browser", 1500)]
    [InlineData("workload", 1500)]
    [InlineData("recovery", 1500)]
    public void Extended_gate_requires_the_entire_protected_cutover_tuple(string drift, int seconds)
    {
        var directory = Path.GetFullPath(_root);
        var project = Path.Combine(directory, "tests", Web6SelectorPlan.Owner, Web6SelectorPlan.Owner + ".csproj");
        var env = CutoverEnvironment();
        var name = drift == "name" ? "default" : "cutover";
        var filter = drift == "filter" ? Web6CandidatePairBudgets.PairFilter : Web6CutoverSelectorBudgets.Filter;
        if (drift == "project") project = Path.Combine(directory, "different.csproj");
        if (drift == "directory") directory = Path.Combine(directory, "other");
        if (drift == "enable-missing") env.Remove("MYDMSVN_WEB6_CUTOVER_ENABLE");
        if (drift == "enable-value") env["MYDMSVN_WEB6_CUTOVER_ENABLE"] = "true";
        if (drift == "run-missing") env.Remove("MYDMSVN_WEB6_BOOTSTRAP_RUN_ID");
        if (drift == "run-empty") env["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"] = "";
        if (drift == "pair-missing") env.Remove(Web6SelectorPlan.PairPassedDigestVariable);
        if (drift == "pair-invalid") env[Web6SelectorPlan.PairPassedDigestVariable] = "invalid";
        if (drift == "candidate") env["MYDMSVN_WEB6_CANDIDATE_ENABLE"] = "1";
        if (drift == "browser") env["MYDMSVN_WEB_PILOT_PROJECT"] = "chromium";
        if (drift == "workload") env["MYDMSVN_WEB6_WORKLOAD_ENABLE"] = "1";
        if (drift == "recovery") env["MYDMSVN_WEB6_RECOVERY_ENABLE"] = "1";
        Assert.Equal(drift == "exact", Web6CutoverSelectorBudgets.IsExact(name, filter, env, project, directory));
        Assert.Equal(TimeSpan.FromSeconds(seconds), Web6CutoverSelectorBudgets.GateTimeout(name, filter, env, project, directory));
        Assert.Equal(TimeSpan.FromSeconds(3660), Web6CutoverSelectorBudgets.ProcessMaximum);
    }

    [Theory]
    [InlineData("pair-chromium", 2640, 2640)]
    [InlineData("pair-edge", 2640, 2640)]
    [InlineData("workload", 1500, 1800)]
    [InlineData("recovery", 1500, 1800)]
    [InlineData("default", 1500, 1800)]
    [InlineData("legacy", 1500, 1800)]
    [InlineData("cutover", 1500, 1800)]
    public void Neighbor_gate_and_process_ceilings_are_unchanged(string name, int gate, int process)
    {
        var env = new Dictionary<string, string>();
        if (name.StartsWith("pair-", StringComparison.Ordinal))
        {
            env["MYDMSVN_WEB6_CANDIDATE_ENABLE"] = "1";
            env["MYDMSVN_WEB_PILOT_PROJECT"] = name.Substring(5);
            env["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"] = "fixture-run";
        }
        Assert.Equal(TimeSpan.FromSeconds(gate), Web6CutoverSelectorBudgets.GateTimeout(name, Web6CandidatePairBudgets.PairFilter, env, Path.Combine(_root, "other.csproj"), _root));
        Assert.Equal(TimeSpan.FromSeconds(process), Web6CandidatePairBudgets.SelectorMaximum(name, Web6CandidatePairBudgets.PairFilter, env));
    }

    [Fact]
    public async Task Browser_primary_is_first_then_stop_and_drain_and_no_allocations_are_disposed()
    {
        var primary = new IOException("browser primary");
        var stop = new IOException("controller stop");
        var drain = new IOException("host drain");
        var disposals = 0;
        var error = await Assert.ThrowsAsync<AggregateException>(() => Web6CutoverLifetime.Run(
            () => Web6CutoverLifetime.RunWithStop<int>(() => Task.FromException<int>(primary), () => Task.FromException(stop)),
            () => Task.FromException<bool>(drain), new Func<Task>[] { () => { disposals++; return Task.CompletedTask; } }));
        Assert.Equal(new Exception[] { primary, stop, drain }, error.InnerExceptions);
        Assert.Equal(0, disposals);
    }

    [Fact]
    public async Task Unjoined_stop_is_sticky_and_refuses_all_later_cleanup_and_success()
    {
        var gate = new Web6CutoverCleanupGate();
        var primary = new IOException("browser primary");
        var calls = new List<string>();
        var error = await Assert.ThrowsAsync<AggregateException>(() => Web6CutoverLifetime.RunWithStop<int>(
            () => Task.FromException<int>(primary),
            () => gate.Stop(() => { calls.Add("stop"); return Task.CompletedTask; }, () => false)));
        Assert.Same(primary, error.InnerExceptions[0]);
        Assert.True(gate.RetainForContainment);
        foreach (var action in new[] { "drain", "disposition", "allocation", "control", "manifest", "next-browser", "success" })
            Assert.Throws<IOException>(() => { gate.ThrowIfRetained(); calls.Add(action); });
        await Assert.ThrowsAsync<IOException>(() => gate.Stop(() => { calls.Add("second-stop"); return Task.CompletedTask; }, () => true));
        Assert.Equal(new[] { "stop" }, calls);
    }

    [Fact]
    public void Source_routes_preserve_separate_controllers_owned_termination_and_sticky_cleanup_guards()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        string Source(string path) => File.ReadAllText(Path.Combine(root!.FullName, path));
        const string tests = "tests/MyDmsVn.Server.Infrastructure.Security.Tests/";
        var live = Source(tests + "WebHostIisExpressTests.Cutover.cs");
        var run = live.Substring(live.IndexOf("public async Task<Web6CutoverBrowserResult> RunAndValidate", StringComparison.Ordinal));
        Assert.Contains("StartCutover(_plan.CurrentEpoch, _session!.Handle, closeResponse: null, parent: token)", run);
        Assert.Contains("Web6CutoverLifetime.RunWithStop", run);
        Assert.Contains("drained => { _browserDrained = drained; return StopController(); }, () => RetainForContainment", run);
        foreach (var method in new[] { "internal async Task<bool> Cleanup()", "internal void VerifyFinalDisposition()", "internal void DisposeManifests(bool allowDeletion)" })
        {
            var body = live.Substring(live.IndexOf(method, StringComparison.Ordinal) + method.Length).TrimStart();
            Assert.StartsWith("{\r\n            _cleanupGate.ThrowIfRetained();", body);
        }
        var orchestration = Source(tests + "Web6CandidateManifest.cs");
        Assert.Contains("new Web6CutoverDeadline(Web6CutoverBudgets.AggregateParent, _clock)", orchestration);
        Assert.Contains("foreach (var browser in new[] { \"chromium\", \"edge\" })", orchestration);
        Assert.Contains("if (started && !_operations.RetainForContainment)", orchestration);
        var browser = Source(tests + "WebHostIisExpressTests.cs");
        Assert.Contains("if (operations?.RetainForContainment != true)", browser);
        Assert.Contains("cutoverCleanup.Finish(browserDrained, DisposeBrowserArtifacts)", browser);
        Assert.Contains("GuiOwnedProcess.RunWeb6CutoverAsync(start, Web6CutoverBudgets.BrowserPack, selectorProcess: false, cancellationToken: cutoverParent)", browser);
        var runner = Source("tools/MyDmsVn.Web6.CandidateBootstrap/SelectorProcessRunner.cs");
        Assert.Contains("Web6CutoverSelectorBudgets.IsExact(invocation.Name, invocation.Filter, invocation.Environment, _project, _directory)", runner);
        Assert.Contains("command.Web6Cutover", runner);
        Assert.Contains("GuiOwnedProcess.RunWeb6CutoverAsync(info, command.Timeout, selectorProcess: true", runner);
        var owned = Source("tests/MyDmsVn.Desktop.Tests/GuiOwnedProcess.cs");
        Assert.Contains("#if WEB6_BOOTSTRAP || WEB6_SECURITY", owned);
        Assert.Contains("selectorProcess ? CutoverBudgets.SelectorGate : CutoverBudgets.BrowserPack", owned);
        Assert.Contains("cleanupBudget: selectorProcess ? CutoverBudgets.SelectorProcessMaximum - CutoverBudgets.SelectorGate : CleanupBudget", owned);
        Assert.Contains("child.Terminate();", owned);
        Assert.Contains("while (child.ActiveProcesses != 0 || !child.RootExited)", owned);
        Assert.Contains("await drains.ConfigureAwait(false);", owned);
        Assert.Contains("TerminateJobObject(_job", owned);
        Assert.DoesNotContain("Process.GetProcessById", owned);
    }

    private static Dictionary<string, string> CutoverEnvironment() => new Dictionary<string, string>
    {
        ["MYDMSVN_WEB6_CUTOVER_ENABLE"] = "1", ["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"] = "fixture-run",
        [Web6SelectorPlan.PairPassedDigestVariable] = new string('a', 64),
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Browser_cleanup_decision_stops_once_and_first_uncertainty_is_sticky(bool unjoined)
    {
        var gate = new Web6CutoverCleanupGate();
        var calls = 0; var disposed = 0;
        var failure = new IOException("first controller failure");
        var cleanup = new Web6CutoverBrowserCleanup(_ => gate.Stop(() =>
        { calls++; return Task.FromException(failure); }, () => !unjoined), () => gate.RetainForContainment, () => !unjoined);
        var observed = await Assert.ThrowsAsync<IOException>(() => cleanup.Stop(true));
        Assert.Same(failure, observed);
        await cleanup.Finish(true, () => disposed++);
        Assert.Equal(1, calls);
        Assert.Equal(unjoined, gate.RetainForContainment);
        Assert.Equal(unjoined ? 0 : 1, disposed);
    }

    [Fact]
    public async Task Browser_deadline_retention_prevents_private_disposal_even_after_cooperative_join()
    {
        var retained = true; var stopped = 0; var disposed = 0;
        var cleanup = new Web6CutoverBrowserCleanup(_ => { stopped++; return Task.CompletedTask; }, () => retained, () => true);
        await Assert.ThrowsAsync<IOException>(() => cleanup.Finish(true, () => disposed++));
        Assert.Equal(1, stopped);
        Assert.Equal(0, disposed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Active_browser_or_stop_deadline_is_sticky_before_any_disposal(bool cancel, bool duringStop)
    {
        var now = TimeSpan.Zero;
        using var parent = new CancellationTokenSource();
        var gate = new Web6CutoverCleanupGate();
        gate.BindDeadline(new Web6CutoverDeadline(TimeSpan.FromSeconds(3300), () => now), parent.Token);
        void Expire() { if (cancel) parent.Cancel(); else now = TimeSpan.FromSeconds(3300); }
        var stops = 0; var disposals = 0; var drains = 0; var joined = false;
        var cleanup = new Web6CutoverBrowserCleanup(_ => gate.Stop(() =>
        {
            stops++;
            if (duringStop) Expire();
            joined = true;
            return Task.CompletedTask;
        }, () => joined), () => gate.RetainForContainment, () => joined);
        if (!duringStop) Expire();
        await Assert.ThrowsAsync<IOException>(() => cleanup.Finish(true, () => disposals++));
        await cleanup.Finish(true, () => disposals++); // consumed first Stop never retries
        await Assert.ThrowsAsync<IOException>(() => gate.DrainHost(() => { drains++; return Task.CompletedTask; }));
        Assert.True(joined);
        Assert.True(gate.RetainForContainment);
        Assert.Equal(1, stops);
        Assert.Equal(0, disposals);
        Assert.Equal(0, drains);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Browser_cleanup_error_order_contains_each_original_once(bool browserFailsFirst)
    {
        var browser = new IOException("browser"); var controller = new IOException("controller");
        var stops = 0;
        var cleanup = new Web6CutoverBrowserCleanup(_ => { stops++; return Task.FromException(controller); }, () => false, () => true);
        var result = await Record.ExceptionAsync(() => Web6CutoverLifetime.RunWithStop(async () =>
        {
            if (browserFailsFirst) throw browser;
            await cleanup.Stop(true); // the in-body decision uses the exact same consumed owner as finally
            return true;
        }, () => cleanup.Finish(true, () => { })));
        if (browserFailsFirst)
            Assert.Equal(new Exception[] { browser, controller }, Assert.IsType<AggregateException>(result).InnerExceptions);
        else Assert.Same(controller, result);
        Assert.Equal(1, stops);
    }

    [Theory]
    [InlineData("mydmsvn_web6_candidate_enable")]
    [InlineData("mydmsvn_web_pilot_project")]
    [InlineData("MyDmsVn_Web6_Workload_Enable")]
    [InlineData("mydmsvn_web6_recovery_enable")]
    public void Windows_environment_aliases_cannot_acquire_the_extended_gate(string flag)
    {
        var env = CutoverEnvironment(); env[flag] = "1";
        var project = Path.Combine(_root, "tests", Web6SelectorPlan.Owner, Web6SelectorPlan.Owner + ".csproj");
        Assert.False(Web6CutoverSelectorBudgets.IsExact("cutover", Web6CutoverSelectorBudgets.Filter, env, project, _root));
        Assert.Equal(TimeSpan.FromMinutes(25), Web6CutoverSelectorBudgets.GateTimeout("cutover", Web6CutoverSelectorBudgets.Filter, env, project, _root));
    }

    [Fact]
    public void Duplicate_windows_aliases_are_rejected_consistently_before_admission()
    {
        var env = CutoverEnvironment(); env["mydmsvn_web6_cutover_enable"] = "1";
        var project = Path.Combine(_root, "tests", Web6SelectorPlan.Owner, Web6SelectorPlan.Owner + ".csproj");
        Assert.Throws<IOException>(() => Web6CutoverSelectorBudgets.IsExact("cutover", Web6CutoverSelectorBudgets.Filter, env, project, _root));
        Assert.Throws<IOException>(() => new SelectorInvocation("cutover", Web6CutoverSelectorBudgets.Filter, env,
            TimeSpan.FromMinutes(60), Path.Combine(_root, "result.trx"), Path.Combine(_root, "fixture.dll")));
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "w6-selector-" + Guid.NewGuid().ToString("N"));
    private const string Tree = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private string FrontendRoot => Path.Combine(_root, "frontend");
    private string CurrentFrontendRoot => Path.Combine(_root, "frontend-current");
    private string BackendRoot => Path.Combine(_root, "backend");
    private string ArchivedDist => Path.Combine(FrontendRoot, "dist");
    private string OldPublish => Path.Combine(_root, "old-publish");
    private string CurrentPublish => Path.Combine(_root, "current-publish");
    private string CandidateDist => Path.Combine(CurrentPublish, "wwwroot");
    private string Asset => Path.Combine(CandidateDist, "index.html");
    private string Evidence => Path.Combine(_root, "evidence");
    private readonly Web6CutoverBuildAuthority _authority;
    private readonly string _publishHash, _pairHash;

    public Web6CutoverSelectorTests()
    {
        Put(ArchivedDist, "index.html", "archived frontend");
        Put(OldPublish, "bin/host.dll", "archived host");
        Put(CurrentPublish, "bin/host.dll", "current host");
        Put(CandidateDist, "index.html", "paired frontend");
        Put(CurrentFrontendRoot, "dist/index.html", "paired frontend");
        Directory.CreateDirectory(BackendRoot);
        Directory.CreateDirectory(Evidence);
        _publishHash = Digest(CurrentPublish);
        _pairHash = Digest(CandidateDist);
        var oldHash = Digest(OldPublish);
        _authority = new Web6CutoverBuildAuthority(Web6CandidateCutoverPlan.FrontendArchiveSha, Tree, Digest(ArchivedDist),
            Web6CandidateCutoverPlan.BackendArchiveSha, Tree, oldHash, _publishHash, oldHash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Drift_before_each_callback_refuses_and_preserves_every_prior_allocation(int ordinal)
    {
        var calls = new List<Web6CutoverPair>();
        var refusal = (Web6CutoverPair)ordinal;
        var records = new List<(string Path, string Hash)>();
        var selector = Selector();
        if (refusal == Web6CutoverPair.Current) File.WriteAllText(Asset, "pre-lease drift");

        await Assert.ThrowsAsync<IOException>(() => selector.AllocateAsync(() => { }, pair =>
        {
            calls.Add(pair);
            return Task.FromResult("allocation-" + pair);
        }, (pair, identity) =>
        {
            records.Add(Web6BootstrapSelectorEvidence.CutoverOwnership(Evidence, "fixture-run", pair, "database", "confirmed",
                identity, new JObject { ["databaseId"] = 10 + (int)pair }));
            return Task.CompletedTask;
        }, prepare: pair =>
        {
            Assert.Equal((int)pair, records.Count);
            if (pair == refusal && pair != Web6CutoverPair.Current) File.WriteAllText(Asset, "between allocation steps");
        }));

        var expected = new[] { Web6CutoverPair.Current, Web6CutoverPair.Old, Web6CutoverPair.Restored }
            .TakeWhile(pair => pair != refusal).ToArray();
        Assert.Equal(expected, calls);
        Assert.Equal(expected.Length, records.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            using var record = ProtectedJsonFile.Open(records[i].Path, records[i].Hash);
            var json = PreparedCandidateReceipt.ReadCanonical(record.Bytes);
            Assert.Equal("allocation-" + expected[i], (string?)json["name"]);
            Assert.Equal(10 + i, (int?)json["identity"]!["databaseId"]);
            Assert.False((bool?)json["deletionAuthority"]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Login_failure_keeps_independent_create_new_identity_for_each_attempt(int failureAt)
    {
        var calls = new List<Web6CutoverPair>();
        var error = new IOException("synthetic login creation failure");
        var task = Web6CutoverSelector.CreateLoginsAsync(pair =>
            Web6BootstrapSelectorEvidence.CreateCutoverLogin(Evidence, "fixture-run", pair, "fixture-" + pair, "fixture-server",
                new string((char)('a' + (int)pair), 32), async () =>
                {
                    calls.Add(pair);
                    Assert.True(File.Exists(Path.Combine(Evidence, $"cutover-{pair.ToString().ToLowerInvariant()}-login-intent.json")));
                    await Task.Yield();
                    if ((int)pair == failureAt) throw error;
                }));
        if (failureAt == 3) await task;
        else Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => task));
        Assert.Equal(Math.Min(failureAt + 1, 3), calls.Count);
        Assert.Equal(Web6CutoverSelector.Pairs.Take(calls.Count), calls);
        foreach (var pair in calls)
        {
            var prefix = Path.Combine(Evidence, $"cutover-{pair.ToString().ToLowerInvariant()}-login-");
            var intentBytes = ProtectedJsonFile.ReadInput(prefix + "intent.json");
            var intent = PreparedCandidateReceipt.ReadCanonical(intentBytes);
            Assert.Equal("fixture-" + pair, (string?)intent["name"]);
            Assert.Equal(new string((char)('a' + (int)pair), 32), (string?)intent["identity"]!["sid"]);
            Assert.False((bool?)intent["deletionAuthority"]);
            if ((int)pair == failureAt)
            {
                Assert.False(File.Exists(prefix + "confirmed.json"));
                continue;
            }
            var confirmationBytes = ProtectedJsonFile.ReadInput(prefix + "confirmed.json");
            using var record = ProtectedJsonFile.Open(prefix + "confirmed.json", ProtectedJsonFile.Digest(confirmationBytes));
            var confirmation = PreparedCandidateReceipt.ReadCanonical(record.Bytes);
            Assert.Equal(ProtectedJsonFile.Digest(intentBytes), (string?)confirmation["identity"]!["intentSha256"]);
            Assert.Equal((string?)intent["identity"]!["sid"], (string?)confirmation["identity"]!["sid"]);
            Assert.False((bool?)confirmation["deletionAuthority"]);
        }
        Assert.Equal(calls.Count + failureAt, Directory.GetFiles(Evidence).Length);
    }

    [Fact]
    public async Task Sealed_files_resist_concurrent_write_and_replacement_through_allocator()
    {
        var writes = new List<Exception?>();
        var calls = new List<Web6CutoverPair>();
        var observations = 0;
        await Selector().AllocateAsync(() =>
        {
            observations++;
            var peer = Task.Run(() =>
            {
                writes.Add(Record.Exception(() => File.WriteAllText(Asset, "changed")));
                writes.Add(Record.Exception(() => File.Move(Asset, Asset + ".moved")));
                writes.Add(Record.Exception(() => Directory.Move(CandidateDist, CandidateDist + ".moved")));
            });
            peer.GetAwaiter().GetResult();
        }, async pair =>
        {
            calls.Add(pair);
            var during = await Task.Run(() => Record.Exception(() => File.WriteAllText(Asset, "changed during allocator")));
            Assert.IsType<IOException>(during);
            Assert.Equal("paired frontend", File.ReadAllText(Asset));
            return pair;
        }, (_, _) => Task.CompletedTask);

        Assert.Equal(3, observations);
        Assert.Equal(new[] { Web6CutoverPair.Current, Web6CutoverPair.Old, Web6CutoverPair.Restored }, calls);
        Assert.Equal(9, writes.Count);
        Assert.All(writes, error => Assert.IsType<IOException>(error));
    }

    [Fact]
    public async Task Archive_wrong_git_root_is_refused_before_allocator()
    {
        var calls = 0;
        var selector = Selector(root => (root == FrontendRoot ? _root : root, Sha(root), Tree));
        await Assert.ThrowsAsync<IOException>(() => selector.AllocateAsync(() => { },
            pair => { calls++; return Task.FromResult(pair); }, (_, _) => Task.CompletedTask));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Attributes_only_junction_drift_during_source_verification_refuses_allocator()
    {
        var empty = Directory.CreateDirectory(Path.Combine(CurrentPublish, "empty")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(_root, "outside")).FullName;
        var redirected = Path.Combine(target, "foreign.txt");
        File.WriteAllText(redirected, "foreign");
        var installed = false;
        var calls = 0;
        try
        {
            await Assert.ThrowsAsync<IOException>(() => Selector().AllocateAsync(() =>
            {
                Task.Run(() =>
                {
                    const uint writeAttributes = 0x100, openReparsePoint = 0x00200000, backupSemantics = 0x02000000;
                    using var directory = CreateFile(empty, writeAttributes, 7, IntPtr.Zero, 3,
                        openReparsePoint | backupSemantics, IntPtr.Zero);
                    Assert.False(directory.IsInvalid);
                    var substitute = @"\??\" + target;
                    var paths = Encoding.Unicode.GetBytes(substitute + "\0" + target + "\0");
                    using var buffer = new MemoryStream();
                    using var writer = new BinaryWriter(buffer);
                    writer.Write(0xa0000003u);
                    writer.Write((ushort)(paths.Length + 8));
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((ushort)(substitute.Length * 2));
                    writer.Write((ushort)((substitute.Length + 1) * 2));
                    writer.Write((ushort)(target.Length * 2));
                    writer.Write(paths);
                    var reparse = buffer.ToArray();
                    installed = DeviceIoControl(directory, 0x000900a4, reparse, reparse.Length,
                        IntPtr.Zero, 0, out _, IntPtr.Zero);
                    Assert.True(installed);
                }).GetAwaiter().GetResult();
            }, pair => { calls++; return Task.FromResult(pair); }, (_, _) => Task.CompletedTask));
            Assert.True(installed);
            Assert.Equal(0, calls);
            Assert.Equal("foreign", File.ReadAllText(redirected));
        }
        finally { if (installed) Directory.Delete(empty); }
    }

    [Fact]
    public async Task Archive_junction_is_refused_before_git_and_allocator()
    {
        var original = FrontendRoot + "-original";
        Directory.Move(FrontendRoot, original);
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/d /c mklink /J \"" + FrontendRoot + "\" \"" + original + "\"") { UseShellExecute = false, CreateNoWindow = true });
        Assert.NotNull(process);
        Assert.True(process!.WaitForExit(5000));
        Assert.Equal(0, process.ExitCode);
        try
        {
            var calls = 0;
            var selector = Selector(_ => throw new InvalidOperationException("Git must not consume a junction."));
            await Assert.ThrowsAsync<IOException>(() => selector.AllocateAsync(() => { },
                pair => { calls++; return Task.FromResult(pair); }, (_, _) => Task.CompletedTask));
            Assert.Equal(0, calls);
        }
        finally { Directory.Delete(FrontendRoot); }
    }

    [Fact]
    public async Task Failure_does_not_retry_or_start_a_later_callback()
    {
        var calls = new List<string>();
        var failure = new IOException("synthetic allocator failure");
        var error = await Assert.ThrowsAsync<IOException>(() => Selector().AllocateAsync(() => { }, pair =>
        {
            calls.Add("allocate " + pair);
            return pair == Web6CutoverPair.Old ? Task.FromException<Web6CutoverPair>(failure) : Task.FromResult(pair);
        }, (pair, _) =>
        {
            calls.Add("seal " + pair);
            return Task.CompletedTask;
        }));
        Assert.Same(failure, error);
        Assert.Equal(new[] { "allocate Current", "seal Current", "allocate Old" }, calls);
    }

    [Fact]
    public async Task Current_frontend_must_match_pair_before_allocation_and_composition()
    {
        var selector = Selector(pairHash: Digest(ArchivedDist));
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => selector.AllocateAsync(() => { },
            pair => { calls++; return Task.FromResult(pair); }, (_, _) => Task.CompletedTask));
        Assert.Equal(0, calls);
        using var runtime = Web6PrivateDirectoryLease.CreateNew(Path.Combine(_root, "deployment"));
        Assert.Throws<IOException>(() => selector.Compose(runtime, cached: false, old: false));
        Assert.False(Directory.Exists(Path.Combine(runtime.Path, "host")));
    }

    [Fact]
    public void Deployment_composes_below_the_live_retained_delete_owner()
    {
        var runtime = Path.Combine(_root, "owned-runtime");
        using var owner = Web6PrivateDirectoryLease.CreateNew(runtime);
        var destination = Path.Combine(runtime, "host");

        var deployment = Selector().Compose(owner, cached: false, old: false);

        owner.Verify();
        Assert.Equal(deployment.Hash, Web6CandidateFileSet.MeasureTree(destination));
        Assert.Equal("paired frontend", File.ReadAllText(Path.Combine(destination, "wwwroot", "index.html")));
        Assert.True(Directory.GetAccessControl(destination).AreAccessRulesProtected);
        Assert.True(File.GetAccessControl(Path.Combine(destination, "wwwroot", "index.html")).AreAccessRulesProtected);
        Assert.Throws<IOException>(() => Selector().Compose(owner, cached: false, old: false));
        owner.DeleteTreeVerified();
        Assert.False(Directory.Exists(runtime));
    }

    [Fact]
    public void Deployment_uses_paired_publish_and_cached_archive_after_worktree_dist_changes()
    {
        Put(CurrentFrontendRoot, "dist/index.html", "late worktree dist mutation");
        var selector = Selector();
        foreach (var (name, cached, old, frontend, host) in new[]
        {
            ("current", false, false, "paired frontend", "current host"),
            ("restored", false, false, "paired frontend", "current host"),
            ("old", false, true, "paired frontend", "archived host"),
            ("cached", true, false, "archived frontend", "current host")
        })
        {
            using var runtime = Web6PrivateDirectoryLease.CreateNew(Path.Combine(_root, name));
            var destination = Path.Combine(runtime.Path, "host");
            selector.Compose(runtime, cached, old);
            Assert.Equal(frontend, File.ReadAllText(Path.Combine(destination, "wwwroot", "index.html")));
            Assert.Equal(host, File.ReadAllText(Path.Combine(destination, "bin", "host.dll")));
        }
        File.WriteAllText(Asset, "sealed publish drift");
        using var refused = Web6PrivateDirectoryLease.CreateNew(Path.Combine(_root, "refused"));
        Assert.Throws<IOException>(() => selector.Compose(refused, cached: false, old: false));
        Assert.False(Directory.Exists(Path.Combine(refused.Path, "host")));
    }

    internal Web6CutoverSelector Selector(Func<string, (string Root, string Sha, string Tree)>? git = null, string? pairHash = null) =>
        new Web6CutoverSelector(_authority, FrontendRoot, BackendRoot, ArchivedDist, OldPublish, CurrentPublish,
            _publishHash, pairHash ?? _pairHash, git ?? (root => (root.Replace('\\', '/'), Sha(root), Tree)));
    private string Sha(string root) => root == FrontendRoot ? Web6CandidateCutoverPlan.FrontendArchiveSha : Web6CandidateCutoverPlan.BackendArchiveSha;
    private static string Digest(string root) => new BootstrapTree(root).Digest;
    private static void Put(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint control, byte[] input, int inputSize,
        IntPtr output, int outputSize, out uint returned, IntPtr overlapped);
}
