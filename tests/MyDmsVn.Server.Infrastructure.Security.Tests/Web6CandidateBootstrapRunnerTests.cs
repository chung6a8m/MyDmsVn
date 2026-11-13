using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CSharp;
using MyDmsVn.Web6.CandidateBootstrap;
using MyDmsVn.Web6.CandidateBootstrap.Processes;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateBootstrapRunnerTests : IDisposable
{
    private const string Candidate = "fixture-candidate";
    private const string RunId = "fixture-run";
    private const string Backend = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Frontend = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Dist = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string Publish = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string Filter = "FullyQualifiedName=Fixture.Namespace.Selectors.Required";
    private const string TrxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    private const string TestId = "d0e55b21-80c2-437d-8175-679079248c2d";
    private const string ExecutionId = "f7028eef-f8b7-4063-a663-c57ac8aa6418";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "web6 selector " + Guid.NewGuid().ToString("N"));
    private readonly string _gitSha;
    private string Executable => Path.Combine(_root, "fixture.exe");
    private string GitExecutable => Path.Combine(_root, "pinned-git", "git.exe");
    private string Project => Path.Combine(_root, "fixture.csproj");
    private string SelectorDll => Path.Combine(_root, "fixture.dll");

    public Web6CandidateBootstrapRunnerTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Executable, "fake tool");
        File.WriteAllText(Project, "fake project");
        File.WriteAllText(SelectorDll, "fixture assembly");
        Directory.CreateDirectory(Path.GetDirectoryName(GitExecutable)!);
        File.WriteAllText(GitExecutable, "pinned git");
        _gitSha = ProtectedJsonFile.Digest(File.ReadAllBytes(GitExecutable));
    }

    [Fact]
    public async Task Exact_selector_uses_one_private_results_directory_and_an_allowlisted_child_environment()
    {
        var invocation = Invocation("pair-chromium", environment: new Dictionary<string, string>
        {
            ["MYDMSVN_WEB6_CANDIDATE_ENABLE"] = "1",
            ["MYDMSVN_WEB_JWT_KEYS"] = "unrelated-secret",
            ["DOTNET_STARTUP_HOOKS"] = "unexpected-hook",
        });
        var runner = Runner((command, _) =>
        {
            Assert.Equal(Executable, command.Executable);
            Assert.Equal(_root, command.WorkingDirectory);
            Assert.Equal("test", command.Arguments[0]);
            Assert.Equal(Project, command.Arguments[1]);
            Assert.Contains("--no-restore", command.Arguments);
            Assert.Contains("--no-build", command.Arguments);
            Assert.Contains(Filter, command.Arguments);
            Assert.Contains("trx;LogFileName=result.trx", command.Arguments);
            Assert.Contains(Path.GetDirectoryName(invocation.TrxPath), command.Arguments);
            Assert.Equal(Path.GetDirectoryName(GitExecutable), command.Environment["PATH"].Split(';')[0]);
            Assert.Equal("1", command.Environment["MYDMSVN_WEB6_CANDIDATE_ENABLE"]);
            Assert.DoesNotContain("MYDMSVN_WEB_JWT_KEYS", command.Environment.Keys);
            Assert.DoesNotContain("DOTNET_STARTUP_HOOKS", command.Environment.Keys);
            Assert.Throws<IOException>(() => File.WriteAllText(GitExecutable, "replaced during execution"));
            Assert.Throws<IOException>(() => File.WriteAllText(SelectorDll, "replaced during execution"));
            File.WriteAllText(invocation.TrxPath, Trx());
            return Task.FromResult(new GuiProcessResult(0, "success", ""));
        });
        var result = await runner.RunAsync(invocation, CancellationToken.None);
        Assert.Equal(SelectorDisposition.Passed, result.Disposition);
        Assert.Equal((1, 1, 1, 0, 0), (result.Discovered, result.Executed, result.Passed, result.Failed, result.Skipped));
        Assert.Equal(64, result.TrxSha256?.Length);
        await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(invocation, CancellationToken.None));
    }

    [Fact]
    public async Task Missing_prebuilt_selector_dll_is_refused_before_launch()
    {
        File.Delete(SelectorDll);
        var runner = Runner((_, _) => throw new InvalidOperationException("selector launched"));
        await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(Invocation("cutover"), CancellationToken.None));
    }

    [Theory]
    [InlineData("missing file")]
    [InlineData("relative path")]
    [InlineData("wrong executable name")]
    [InlineData("wrong digest")]
    public async Task Missing_or_wrong_prepared_git_is_refused_before_launch(string defect)
    {
        if (defect == "missing file") File.Delete(GitExecutable);
        if (defect == "wrong digest") File.WriteAllText(GitExecutable, "substituted git");
        var git = defect switch
        {
            "relative path" => "git.exe",
            "wrong executable name" => Executable,
            _ => GitExecutable,
        };
        await Assert.ThrowsAsync<IOException>(async () =>
        {
            var runner = new SelectorProcessRunner(Executable, git, _gitSha, _root, Project,
                (_, _) => throw new InvalidOperationException("selector launched"));
            await runner.RunAsync(Invocation("cutover"), CancellationToken.None);
        });
    }

    [Theory]
    [InlineData("FullyQualifiedName~Required")]
    [InlineData("FullyQualifiedName=Fixture.Required|Category=FastSQL")]
    [InlineData("FullyQualifiedName=Fixture.Required\n")]
    public async Task Refuses_filters_other_than_one_exact_fully_qualified_name(string filter)
    {
        var runner = Runner((_, _) => throw new InvalidOperationException("selector launched"));
        await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(Invocation("cutover", filter: filter), CancellationToken.None));
    }

    [Theory]
    [InlineData("missing", "MissingTrx")]
    [InlineData("malformed", "InvalidTrx")]
    [InlineData("dtd", "InvalidTrx")]
    [InlineData("wrong FQN", "InvalidTrx")]
    [InlineData("foreign storage", "InvalidTrx")]
    [InlineData("foreign codeBase", "InvalidTrx")]
    [InlineData("missing codeBase", "InvalidTrx")]
    [InlineData("aliased storage", "InvalidTrx")]
    [InlineData("discovery mismatch", "InvalidTrx")]
    [InlineData("executed mismatch", "InvalidTrx")]
    [InlineData("foreign row", "InvalidTrx")]
    [InlineData("foreign result type", "InvalidTrx")]
    [InlineData("skipped", "MandatoryTestFailed")]
    [InlineData("failed", "MandatoryTestFailed")]
    [InlineData("nonzero", "NonzeroExit")]
    public async Task Refuses_incomplete_failed_or_foreign_TRX_without_retrying(string defect, string expected)
    {
        var invocation = Invocation("cutover");
        var calls = 0;
        var runner = Runner((_, _) =>
        {
            Interlocked.Increment(ref calls);
            var trx = defect switch
            {
                "missing" => null,
                "malformed" => "<TestRun",
                "dtd" => "<!DOCTYPE TestRun [<!ENTITY embedded 'secret'>]>" + Trx(),
                "wrong FQN" => Trx().Replace("Fixture.Namespace.Selectors", "Unrelated.Selectors"),
                "foreign storage" => Trx().Replace($"storage=\"{SelectorDll.ToLowerInvariant()}\"", $"storage=\"{Path.Combine(_root, "foreign.dll")}\""),
                "foreign codeBase" => Trx().Replace($"codeBase=\"{SelectorDll}\"", $"codeBase=\"{Path.Combine(_root, "foreign.dll")}\""),
                "missing codeBase" => Trx().Replace($" codeBase=\"{SelectorDll}\"", ""),
                "aliased storage" => Trx().Replace($"storage=\"{SelectorDll.ToLowerInvariant()}\"", $"storage=\"{_root}\\child\\..\\fixture.dll\""),
                "discovery mismatch" => Trx().Replace("total=\"1\"", "total=\"2\""),
                "executed mismatch" => Trx().Replace("executed=\"1\"", "executed=\"0\""),
                "foreign row" => Trx().Replace(
                    "<UnitTestResult testId=\"" + TestId, "<UnitTestResult testId=\"" + Guid.NewGuid()),
                "foreign result type" => Trx().Replace("</Results>", "<GenericTestResult /></Results>"),
                "skipped" => Trx("NotExecuted", 0, 0, 0, 1),
                "failed" => Trx("Failed", 1, 0, 1, 0),
                _ => Trx(),
            };
            if (trx != null) File.WriteAllText(invocation.TrxPath, trx);
            return Task.FromResult(new GuiProcessResult(defect == "nonzero" ? 23 : 0, "", ""));
        });
        var result = await runner.RunAsync(invocation, CancellationToken.None);
        Assert.Equal(expected, result.SanitizedDisposition);
        Assert.Equal(1, calls);
        if (defect == "skipped") Assert.Equal((1, 0, 0, 0, 1), (result.Discovered, result.Executed, result.Passed, result.Failed, result.Skipped));
        if (defect == "failed") Assert.Equal((1, 1, 0, 1, 0), (result.Discovered, result.Executed, result.Passed, result.Failed, result.Skipped));
    }

    [Fact]
    public async Task Combined_launch_and_cleanup_failure_is_a_single_sanitized_uncertain_attempt()
    {
        var calls = 0;
        var runner = Runner((_, _) =>
        {
            Interlocked.Increment(ref calls);
            throw new AggregateException(new IOException("connection-string-secret"), new IOException(_root));
        });
        var result = await runner.RunAsync(Invocation("cutover"), CancellationToken.None);
        Assert.Equal(1, calls);
        Assert.Equal("ProcessUncertain", result.SanitizedDisposition);
        Assert.False(result.ProcessTreeDrained);
        Assert.True(result.RequiresDispositionProof);
    }

    [Fact]
    public async Task Raw_child_output_cannot_escape_through_the_selector_summary()
    {
        const string stdout = "Server=localhost;Database=master;User ID=fixture;Password=fixture-password";
        var stderr = "JWT eyJhbGciOiJIUzI1NiJ9.payload.signature\r\nCookie: refresh=fixture-cookie\r\n" + _root;
        var runner = Runner((_, _) => Task.FromResult(new GuiProcessResult(1, stdout, stderr)));
        var result = await runner.RunAsync(Invocation("cutover"), CancellationToken.None);
        Assert.Equal("NonzeroExit", result.SanitizedDisposition);
        Assert.DoesNotContain("fixture-password", result.SanitizedDisposition);
        Assert.DoesNotContain("eyJ", result.SanitizedDisposition);
        Assert.DoesNotContain("fixture-cookie", result.SanitizedDisposition);
        Assert.DoesNotContain(_root, result.SanitizedDisposition);
    }

    [Fact]
    public async Task Deadline_terminates_the_owned_process_tree_and_requires_independent_disposition()
    {
        File.Delete(Executable);
        using var compiler = new CSharpCodeProvider();
        var compiled = compiler.CompileAssemblyFromSource(new CompilerParameters(new[] { "System.dll" }, Executable)
        {
            GenerateExecutable = true,
        }, """
            using System;
            using System.Diagnostics;
            using System.IO;
            using System.Threading;
            public static class TestChild {
                public static void Main(string[] args) {
                    if (args[0] == "descendant") {
                        File.WriteAllText(Path.Combine(args[1], "descendant.pid"), Process.GetCurrentProcess().Id.ToString());
                    } else {
                        string directory = args[Array.IndexOf(args, "--results-directory") + 1];
                        File.WriteAllText(Path.Combine(directory, "root.pid"), Process.GetCurrentProcess().Id.ToString());
                        Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,
                            "descendant \"" + directory + "\"") { UseShellExecute = false });
                    }
                    Thread.Sleep(Timeout.Infinite);
                }
            }
            """);
        Assert.False(compiled.Errors.HasErrors);
        var invocation = Invocation("cutover", timeout: TimeSpan.FromSeconds(4));
        var result = await new SelectorProcessRunner(Executable, GitExecutable, _gitSha, _root, Project)
            .RunAsync(invocation, CancellationToken.None);
        Assert.Equal(SelectorDisposition.TimedOut, result.Disposition);
        Assert.True(result.ProcessTreeDrained);
        Assert.True(result.RequiresDispositionProof);
        foreach (var name in new[] { "root.pid", "descendant.pid" })
        {
            var pid = int.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(invocation.TrxPath)!, name)));
            Assert.False(IsRunning(pid));
        }
    }

    [Fact]
    public async Task Protected_chain_requires_both_browsers_and_a_final_verified_disposition()
    {
        var prepared = Prepared();
        var state = BootstrapRunState.Open(prepared.Path, prepared.Sha, RunId);
        Assert.Equal(BootstrapRunStage.Prepared, state.Stage);
        Assert.Null(state.Accept(await Passed("pair-chromium"), Gate("pair-chromium")));
        Assert.Equal(BootstrapRunStage.Prepared, state.Stage);

        var prior = prepared.Sha;
        var edge = Gate("pair-edge");
        var pair = state.Accept(await Passed("pair-edge"), edge)!;
        var pairJson = AssertTransition(pair, BootstrapRunStage.PairPassed, prior);
        Assert.Equal(_chromiumSha, (string?)pairJson["pairChromiumSha256"]);
        Assert.Equal(edge.Sha256, (string?)pairJson["gateSha256"]);

        prior = pair.Sha256;
        foreach (var step in new[]
        {
            ("cutover", BootstrapRunStage.CutoverPassed),
            ("workload", BootstrapRunStage.WorkloadRecorded),
            ("recovery", BootstrapRunStage.RecoveryRecorded),
        })
        {
            var receipt = state.Accept(await Passed(step.Item1), Gate(step.Item1))!;
            var payload = AssertTransition(receipt, step.Item2, prior);
            if (step.Item1 == "workload") Assert.Equal("Measured", (string?)payload["workloadStatus"]);
            if (step.Item1 == "recovery") Assert.Equal(120L, (long?)payload["recoveryRtoMilliseconds"]);
            prior = receipt.Sha256;
        }
        Assert.Equal(BootstrapRunStage.RecoveryRecorded, state.Stage);
        var native = Native("disposition");
        await Task.Delay(35);
        var incomplete = ProtectedDispositionReceipt.Write(Path.Combine(_root, "incomplete.json"), RunId, Candidate,
            native.Path, native.Sha, prior, DateTimeOffset.UtcNow,
            processDrained: true, httpQueuesAbsent: false, sqlQuiescent: true, resourcesVerified: true);
        Assert.Throws<IOException>(() => state.Complete(incomplete));
        var complete = ProtectedDispositionReceipt.Write(Path.Combine(_root, "complete.json"), RunId, Candidate,
            native.Path, native.Sha, prior, DateTimeOffset.UtcNow,
            processDrained: true, httpQueuesAbsent: true, sqlQuiescent: true, resourcesVerified: true);
        var final = AssertTransition(state.Complete(complete), BootstrapRunStage.CandidateVerified, prior);
        Assert.Equal(prior, (string?)final["recoveryTransitionSha256"]);
        Assert.Equal(complete.ObservedAtUtc, (DateTimeOffset?)final["dispositionObservedAtUtc"]);
    }

    [Theory]
    [InlineData("pre-recovery observation")]
    [InlineData("equal recovery timestamp")]
    [InlineData("stale transition")]
    [InlineData("recovery native file")]
    [InlineData("earlier gate native file")]
    [InlineData("earlier gate envelope")]
    [InlineData("native bytes copied to another file")]
    [InlineData("native path reused with different bytes")]
    [InlineData("recovery transition as evidence")]
    [InlineData("workload transition as evidence")]
    public async Task Final_disposition_requires_fresh_independent_evidence_bound_to_recovery(string defect)
    {
        var earlier = DateTimeOffset.UtcNow;
        var prepared = Prepared();
        var state = BootstrapRunState.Open(prepared.Path, prepared.Sha, RunId);
        var chromium = Gate("pair-chromium");
        Assert.Null(state.Accept(await Passed("pair-chromium"), chromium));
        Assert.NotNull(state.Accept(await Passed("pair-edge"), Gate("pair-edge")));
        Assert.NotNull(state.Accept(await Passed("cutover"), Gate("cutover")));
        var workload = state.Accept(await Passed("workload"), Gate("workload"))!;
        var recoveryGate = Gate("recovery");
        var recovery = state.Accept(await Passed("recovery"), recoveryGate)!;
        await Task.Delay(35);

        var native = defect switch
        {
            "recovery native file" => (recoveryGate.NativePath, recoveryGate.NativeSha256),
            "earlier gate native file" => (chromium.NativePath, chromium.NativeSha256),
            "earlier gate envelope" => (chromium.PrivatePath, chromium.Sha256),
            "native bytes copied to another file" => CopyNative(recoveryGate.NativePath),
            "native path reused with different bytes" => ReplaceNative(recoveryGate.NativePath),
            "recovery transition as evidence" => (recovery.Path, recovery.Sha256),
            "workload transition as evidence" => (workload.Path, workload.Sha256),
            _ => Native("disposition"),
        };
        var observedAt = defect switch
        {
            "pre-recovery observation" => earlier,
            "equal recovery timestamp" => recovery.RecordedAtUtc,
            _ => DateTimeOffset.UtcNow,
        };
        var disposition = ProtectedDispositionReceipt.Write(Path.Combine(_root, "complete.json"), RunId, Candidate,
            native.Item1, native.Item2, defect == "stale transition" ? workload.Sha256 : recovery.Sha256,
            observedAt,
            processDrained: true, httpQueuesAbsent: true, sqlQuiescent: true, resourcesVerified: true);
        Assert.Throws<IOException>(() => state.Complete(disposition));
        Assert.Equal(BootstrapRunStage.RecoveryRecorded, state.Stage);
    }

    [Theory]
    [InlineData("BLOCKED_REVIEWED_FRONTEND_PIN")]
    [InlineData("BLOCKED_REVIEWED_ARCHIVE_PIN")]
    public void Blocked_prepared_receipt_cannot_open_a_run(string admission)
    {
        var prepared = Prepared(admission);
        Assert.Throws<IOException>(() => BootstrapRunState.Open(prepared.Path, prepared.Sha, RunId));
        Assert.False(File.Exists(Path.Combine(_root, "run-attempt.json")));
    }

    [Fact]
    public async Task Refuses_skipped_replayed_and_out_of_order_gates_and_a_second_open()
    {
        var prepared = Prepared();
        var state = BootstrapRunState.Open(prepared.Path, prepared.Sha, RunId);
        var edge = await Passed("pair-edge");
        var edgeGate = Gate("pair-edge");
        Assert.Throws<IOException>(() => state.Accept(edge, edgeGate));
        await Task.Delay(35);
        var chromium = await Passed("pair-chromium");
        var chromiumGate = Gate("pair-chromium");
        Assert.Null(state.Accept(chromium, chromiumGate));
        Assert.Throws<IOException>(() => state.Accept(chromium, chromiumGate));
        var workload = await Passed("workload");
        var workloadGate = Gate("workload");
        Assert.Throws<IOException>(() => state.Accept(workload, workloadGate));
        Assert.Throws<IOException>(() => state.Accept(edge, edgeGate));
        var serialEdge = await Passed("pair-edge");
        Assert.NotNull(state.Accept(serialEdge, edgeGate));
        Assert.Throws<IOException>(() => state.Accept(serialEdge, edgeGate));
        Assert.Throws<IOException>(() => BootstrapRunState.Open(prepared.Path, prepared.Sha, "different-run"));
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("backend")]
    [InlineData("frontend")]
    [InlineData("dist")]
    [InlineData("run")]
    [InlineData("protected native file")]
    public async Task Gate_envelope_must_match_prepared_candidate_and_native_receipt(string drift)
    {
        var prepared = Prepared();
        var state = BootstrapRunState.Open(prepared.Path, prepared.Sha, RunId);
        var result = await Passed("pair-chromium");
        var native = Native("pair-chromium");
        var gate = ProtectedGateReceipt.Write(Path.Combine(_root, "pair-chromium.json"), "pair-chromium",
            drift == "run" ? "different-run" : RunId, drift == "candidate" ? "different-candidate" : Candidate,
            drift == "backend" ? Frontend : Backend, drift == "frontend" ? Backend : Frontend,
            drift == "dist" ? Publish : Dist, native.Path, drift == "protected native file" ? Publish : native.Sha);
        Assert.Throws<IOException>(() => state.Accept(result, gate));
        Assert.Equal(BootstrapRunStage.Prepared, state.Stage);
    }

    [Fact]
    public async Task Failed_result_cannot_advance_and_records_a_terminal_retained_attempt()
    {
        var prepared = Prepared();
        var state = BootstrapRunState.Open(prepared.Path, prepared.Sha, RunId);
        var result = await Runner((_, _) => Task.FromResult(new GuiProcessResult(1, "", "")))
            .RunAsync(Invocation("pair-chromium"), CancellationToken.None);
        var gate = Gate("pair-chromium");
        Assert.Throws<IOException>(() => state.Accept(result, gate));
        var failure = state.Fail(result);
        var payload = AssertTransition(failure, BootstrapRunStage.FailedRetained, prepared.Sha);
        Assert.Equal("NonzeroExit", (string?)payload["disposition"]);
        Assert.True((bool?)payload["requiresDispositionProof"]);
        var retry = await Passed("pair-chromium");
        Assert.Throws<IOException>(() => state.Accept(retry, gate));
    }

    [Theory]
    [InlineData("pair-chromium", "chromium", true)]
    [InlineData("pair-edge", "edge", true)]
    [InlineData("pair-edge", "chromium", false)]
    [InlineData("cutover", "chromium", false)]
    public async Task Long_pair_budget_is_scoped_before_launch_and_forwarded_to_owned_process(string name, string project, bool admitted)
    {
        var env = new Dictionary<string, string> { ["MYDMSVN_WEB6_CANDIDATE_ENABLE"] = "1",
            ["MYDMSVN_WEB_PILOT_PROJECT"] = project, ["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"] = RunId };
        var invocation = Invocation(name, Web6CandidatePairBudgets.PairFilter, env, TimeSpan.FromMinutes(44));
        var called = false;
        var runner = Runner((request, _) =>
        {
            called = true;
            Assert.True(request.Web6CandidatePair);
            Assert.Equal(TimeSpan.FromMinutes(44), request.Timeout);
            return Task.FromResult(new GuiProcessResult(1, "", ""));
        });
        if (admitted) await runner.RunAsync(invocation, CancellationToken.None);
        else await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(invocation, CancellationToken.None));
        Assert.Equal(admitted, called);
    }

    [Theory]
    [InlineData(3600, true)]
    [InlineData(3601, false)]
    [InlineData(3660, false)]
    public async Task Cutover_work_gate_never_borrows_termination_minute(int seconds, bool admitted)
    {
        var project = Path.Combine(_root, "tests", Web6SelectorPlan.Owner, Web6SelectorPlan.Owner + ".csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        File.WriteAllText(project, "fixture project");
        var env = new Dictionary<string, string>
        {
            ["MYDMSVN_WEB6_CUTOVER_ENABLE"] = "1", ["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"] = RunId,
            [Web6SelectorPlan.PairPassedDigestVariable] = new string('a', 64),
        };
        var invocation = Invocation("cutover", "FullyQualifiedName=MyDmsVn.Server.Infrastructure.IntegrationTests.WebHostIisExpressTests.Web6_pinned_cutover_runs_protected_IIS_and_browser_lifecycle", env, TimeSpan.FromSeconds(seconds));
        var called = false;
        var runner = new SelectorProcessRunner(Executable, GitExecutable, _gitSha, _root, project, (request, _) =>
        {
            called = true;
            Assert.Equal(TimeSpan.FromSeconds(3600), request.Timeout);
            Assert.False(request.Web6CandidatePair);
            Assert.True(request.Web6Cutover);
            return Task.FromResult(new GuiProcessResult(1, "", ""));
        });
        if (admitted) await runner.RunAsync(invocation, CancellationToken.None);
        else await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(invocation, CancellationToken.None));
        Assert.Equal(admitted, called);
    }

    [Theory]
    [InlineData("TimedOut", true)]
    [InlineData("TimedOut", false)]
    [InlineData("Canceled", true)]
    [InlineData("Canceled", false)]
    [InlineData("CleanupFailed", false)]
    public async Task Exact_cutover_containment_failure_never_becomes_mandatory_success(string reason, bool drained)
    {
        var project = Path.Combine(_root, "tests", Web6SelectorPlan.Owner, Web6SelectorPlan.Owner + ".csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        File.WriteAllText(project, "fixture project");
        var env = new Dictionary<string, string>
        {
            ["MYDMSVN_WEB6_CUTOVER_ENABLE"] = "1", ["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"] = RunId,
            [Web6SelectorPlan.PairPassedDigestVariable] = new string('a', 64),
        };
        var invocation = Invocation("cutover", Web6CutoverSelectorBudgets.Filter, env, TimeSpan.FromSeconds(3600));
        var calls = 0;
        var runner = new SelectorProcessRunner(Executable, GitExecutable, _gitSha, _root, project, (request, _) =>
        {
            calls++;
            Assert.True(request.Web6Cutover);
            throw new GuiProcessException(reason, drained, "private-output", "private-error", -1, new TimeoutException());
        });
        var result = await runner.RunAsync(invocation, CancellationToken.None);
        Assert.Equal(1, calls);
        Assert.False(result.PassedMandatoryTests);
        Assert.Equal(reason == "TimedOut" ? SelectorDisposition.TimedOut : reason == "Canceled" ? SelectorDisposition.Canceled : SelectorDisposition.ProcessUncertain, result.Disposition);
        Assert.True(result.RequiresDispositionProof);
        Assert.Equal(drained, result.ProcessTreeDrained);
        Assert.Equal(0, result.Executed);
    }

    private SelectorProcessRunner Runner(Func<BootstrapProcessRequest, CancellationToken, Task<GuiProcessResult>> run) =>
        new(Executable, GitExecutable, _gitSha, _root, Project, run);

    private SelectorInvocation Invocation(string name, string filter = Filter, IReadOnlyDictionary<string, string>? environment = null,
        TimeSpan? timeout = null) => new(name, filter, environment ?? new Dictionary<string, string>
        { ["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"] = RunId },
            timeout ?? TimeSpan.FromSeconds(5), Path.Combine(_root, "trx-" + Guid.NewGuid().ToString("N"), "result.trx"), SelectorDll);

    private Task<SelectorResult> Passed(string name)
    {
        var invocation = Invocation(name);
        return Runner((_, _) =>
        {
            File.WriteAllText(invocation.TrxPath, Trx());
            return Task.FromResult(new GuiProcessResult(0, "", ""));
        }).RunAsync(invocation, CancellationToken.None);
    }

    private string Trx(string outcome = "Passed", int executed = 1, int passed = 1, int failed = 0, int skipped = 0) => $"""
        <TestRun xmlns="{TrxNamespace}">
          <Results><UnitTestResult testId="{TestId}" executionId="{ExecutionId}" outcome="{outcome}" /></Results>
          <TestDefinitions><UnitTest id="{TestId}" storage="{SelectorDll.ToLowerInvariant()}"><Execution id="{ExecutionId}"/><TestMethod className="Fixture.Namespace.Selectors" name="Required" codeBase="{SelectorDll}"/></UnitTest></TestDefinitions>
          <TestEntries><TestEntry testId="{TestId}" executionId="{ExecutionId}"/></TestEntries>
          <ResultSummary outcome="{(failed == 0 ? "Completed" : "Failed")}"><Counters total="1" executed="{executed}" passed="{passed}" failed="{failed}" notExecuted="{skipped}" error="0" aborted="0"/></ResultSummary>
        </TestRun>
        """;

    private (string Path, string Sha) Prepared(string admission = "PIN_MATCHED")
    {
        var path = Path.Combine(_root, "prepared.json");
        var sha = ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(new JObject
        {
            ["version"] = 1, ["state"] = "Prepared", ["cutoverAdmission"] = admission, ["candidateId"] = Candidate,
            ["taskRoot"] = _root, ["backendSha"] = Backend, ["frontendSha"] = Frontend,
            ["candidateDistSha256"] = Dist, ["currentPublishSha256"] = Publish,
            ["expiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"),
        }));
        return (path, sha);
    }

    private string? _chromiumSha;

    private ProtectedGateReceipt Gate(string name)
    {
        var native = Native(name);
        var receipt = ProtectedGateReceipt.Write(Path.Combine(_root, name + ".json"), name, RunId, Candidate,
            Backend, Frontend, Dist, native.Path, native.Sha, workloadStatus: name == "workload" ? "Measured" : null,
            recoveryRpo: name == "recovery" ? 0 : null, recoveryRtoMilliseconds: name == "recovery" ? 120 : null);
        if (name == "pair-chromium") _chromiumSha = receipt.Sha256;
        return receipt;
    }

    private (string Path, string Sha) Native(string name)
    {
        var path = Path.Combine(_root, name + "-native.json");
        return (path, ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(new JObject { ["version"] = 1, ["gate"] = name })));
    }

    private (string, string) CopyNative(string source)
    {
        var path = Path.Combine(_root, "copied-native.json");
        return (path, ProtectedJsonFile.WriteNew(path, File.ReadAllBytes(source)));
    }

    private static (string, string) ReplaceNative(string path)
    {
        File.Delete(path);
        return (path, ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(new JObject { ["version"] = 1, ["replaced"] = true })));
    }

    private static JObject AssertTransition(RunTransitionReceipt receipt, BootstrapRunStage stage, string previous)
    {
        Assert.Equal(stage, receipt.Stage);
        using var protectedFile = ProtectedJsonFile.Open(receipt.Path, receipt.Sha256);
        var payload = JObject.Parse(System.Text.Encoding.UTF8.GetString(protectedFile.Bytes));
        Assert.Equal(stage.ToString(), (string?)payload["state"]);
        Assert.Equal(previous, (string?)payload["previousSha256"]);
        Assert.Equal(RunId, (string?)payload["runId"]);
        Assert.Equal(Candidate, (string?)payload["candidateId"]);
        Assert.Equal(Backend, (string?)payload["backendSha"]);
        Assert.Equal(Frontend, (string?)payload["frontendSha"]);
        Assert.Equal(Dist, (string?)payload["frontendDistSha256"]);
        Assert.Equal(Publish, (string?)payload["backendPublishSha256"]);
        return payload;
    }

    private static bool IsRunning(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
