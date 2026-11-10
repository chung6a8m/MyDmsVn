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
    private string Executable => Path.Combine(_root, "fixture.exe");
    private string Project => Path.Combine(_root, "fixture.csproj");

    public Web6CandidateBootstrapRunnerTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Executable, "fake tool");
        File.WriteAllText(Project, "fake project");
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
            Assert.Contains(Filter, command.Arguments);
            Assert.Contains("trx;LogFileName=result.trx", command.Arguments);
            Assert.Contains(Path.GetDirectoryName(invocation.TrxPath), command.Arguments);
            Assert.Equal("1", command.Environment["MYDMSVN_WEB6_CANDIDATE_ENABLE"]);
            Assert.DoesNotContain("MYDMSVN_WEB_JWT_KEYS", command.Environment.Keys);
            Assert.DoesNotContain("DOTNET_STARTUP_HOOKS", command.Environment.Keys);
            File.WriteAllText(invocation.TrxPath, Trx());
            return Task.FromResult(new GuiProcessResult(0, "success", ""));
        });
        var result = await runner.RunAsync(invocation, CancellationToken.None);
        Assert.Equal(SelectorDisposition.Passed, result.Disposition);
        Assert.Equal((1, 1, 1, 0, 0), (result.Discovered, result.Executed, result.Passed, result.Failed, result.Skipped));
        Assert.Equal(64, result.TrxSha256?.Length);
        await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(invocation, CancellationToken.None));
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
        var result = await new SelectorProcessRunner(Executable, _root, Project).RunAsync(invocation, CancellationToken.None);
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
        var incomplete = ProtectedDispositionReceipt.Write(Path.Combine(_root, "incomplete.json"), RunId,
            Candidate, native.Path, native.Sha, processDrained: true, httpQueuesAbsent: false, sqlQuiescent: true, resourcesVerified: true);
        Assert.Throws<IOException>(() => state.Complete(incomplete));
        var complete = ProtectedDispositionReceipt.Write(Path.Combine(_root, "complete.json"), RunId,
            Candidate, native.Path, native.Sha, processDrained: true, httpQueuesAbsent: true, sqlQuiescent: true, resourcesVerified: true);
        AssertTransition(state.Complete(complete), BootstrapRunStage.CandidateVerified, prior);
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

    private SelectorProcessRunner Runner(Func<BootstrapProcessRequest, CancellationToken, Task<GuiProcessResult>> run) =>
        new(Executable, _root, Project, run);

    private SelectorInvocation Invocation(string name, string filter = Filter, IReadOnlyDictionary<string, string>? environment = null,
        TimeSpan? timeout = null) => new(name, filter, environment ?? new Dictionary<string, string>
        { ["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"] = RunId },
            timeout ?? TimeSpan.FromSeconds(5), Path.Combine(_root, "trx-" + Guid.NewGuid().ToString("N"), "result.trx"));

    private Task<SelectorResult> Passed(string name)
    {
        var invocation = Invocation(name);
        return Runner((_, _) =>
        {
            File.WriteAllText(invocation.TrxPath, Trx());
            return Task.FromResult(new GuiProcessResult(0, "", ""));
        }).RunAsync(invocation, CancellationToken.None);
    }

    private static string Trx(string outcome = "Passed", int executed = 1, int passed = 1, int failed = 0, int skipped = 0) => $"""
        <TestRun xmlns="{TrxNamespace}">
          <Results><UnitTestResult testId="{TestId}" executionId="{ExecutionId}" outcome="{outcome}" /></Results>
          <TestDefinitions><UnitTest id="{TestId}"><Execution id="{ExecutionId}"/><TestMethod className="Fixture.Namespace.Selectors" name="Required"/></UnitTest></TestDefinitions>
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
        return (path, ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(new JObject { ["version"] = 1 })));
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
