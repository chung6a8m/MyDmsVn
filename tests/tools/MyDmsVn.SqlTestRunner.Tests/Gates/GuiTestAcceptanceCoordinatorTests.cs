using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Trx;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Gates;

public sealed class GuiTestAcceptanceCoordinatorTests
{
    [Fact]
    public void Source_inventory_reads_net48_WinForms_tests_without_loading_framework_assemblies()
    {
        var root = FindRepositoryRoot();
        var assembly = Path.Combine(root, "tests", "MyDmsVn.Desktop.Tests", "bin", "Release", "net48",
            "MyDmsVn.Desktop.Tests.dll");

        var methods = new GuiTestSourceInventory().Read(assembly);

        Assert.Contains(methods, method => method.FullyQualifiedName ==
            "MyDmsVn.Desktop.Tests.MasterDetailPrototypeControlTests.SetRows_projects_master_rows_into_the_source_grid");
        Assert.All(methods, method => Assert.True(method.IsBodyFingerprintComplete));
    }

    [Fact]
    public void Reviewed_entry_point_binds_clean_HEAD_net48_and_bounded_runner()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "tests", "tools", "Invoke-GuiTestAcceptance.ps1"));
        var program = File.ReadAllText(Path.Combine(root, "tests", "tools", "MyDmsVn.SqlTestRunner", "Program.cs"));

        Assert.Contains("status --porcelain --untracked-files=all", script, StringComparison.Ordinal);
        Assert.Contains("--framework net48", script, StringComparison.Ordinal);
        Assert.Contains("--revision $Revision", script, StringComparison.Ordinal);
        Assert.Contains("--timeout-seconds $TimeoutSeconds", script, StringComparison.Ordinal);
        Assert.Contains("gui-acceptance", script, StringComparison.Ordinal);
        Assert.Contains("profile.GuiProject", program, StringComparison.Ordinal);
        Assert.Contains("ApprovedFrameworks = profile.ActiveGuiFrameworks", program, StringComparison.Ordinal);
        Assert.Contains("project = result.Project", program, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("missing-trx")]
    [InlineData("unexpected-dialog")]
    [InlineData("orphan")]
    public async Task Failure_path_is_incomplete_and_fail_closed(string failure)
    {
        using var fixture = new GuiFixture();
        var process = new GuiProcessRunner(fixture, failure);
        var safety = new StubSafetyProbe(failure == "unexpected-dialog", failure == "orphan");
        var coordinator = new GuiTestAcceptanceCoordinator(process, new StubGuiInventory(), safety, new StubGuiLease(true));

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
    }

    [Fact]
    public async Task Locked_global_gui_resource_blocks_before_process_launch()
    {
        using var fixture = new GuiFixture();
        var process = new GuiProcessRunner(fixture, string.Empty);
        var coordinator = new GuiTestAcceptanceCoordinator(
            process, new StubGuiInventory(), new StubSafetyProbe(false, false), new StubGuiLease(false));

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Equal(0, process.Calls);
    }

    [Fact]
    public async Task Unreviewed_project_is_rejected_before_build_or_test_launch()
    {
        using var fixture = new GuiFixture();
        var otherProject = Path.Combine(fixture.Root, "Architecture.Tests.csproj");
        File.WriteAllText(otherProject, "<Project />");
        var options = fixture.Options with { ProjectPath = otherProject };
        var process = new GuiProcessRunner(fixture, string.Empty);
        var coordinator = new GuiTestAcceptanceCoordinator(
            process, new StubGuiInventory(), new StubSafetyProbe(false, false), new StubGuiLease(true));

        var result = await coordinator.RunAsync(options, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Equal(0, process.Calls);
    }

    [Fact]
    public async Task Unreviewed_framework_is_rejected_before_build_or_test_launch()
    {
        using var fixture = new GuiFixture();
        var options = fixture.Options with { ApprovedFrameworks = new[] { "net8.0-windows" } };
        var process = new GuiProcessRunner(fixture, string.Empty);
        var coordinator = new GuiTestAcceptanceCoordinator(
            process, new StubGuiInventory(), new StubSafetyProbe(false, false), new StubGuiLease(true));

        var result = await coordinator.RunAsync(options, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Equal(0, process.Calls);
    }

    [Fact]
    public async Task Gui_binary_is_built_before_source_inventory_is_read()
    {
        using var fixture = new GuiFixture();
        var process = new GuiProcessRunner(fixture, string.Empty);
        var inventory = new StubGuiInventory(() => process.Commands.Any(command =>
            command.StartsWith("build ", StringComparison.Ordinal)));
        var coordinator = new GuiTestAcceptanceCoordinator(
            process, inventory, new StubSafetyProbe(false, false), new StubGuiLease(true));

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.True(inventory.ReadAfterBuild);
        Assert.Equal("Desktop.Tests.csproj", result.Project);
        Assert.Contains(process.Commands, command => command.StartsWith("build ", StringComparison.Ordinal) &&
            command.Contains("--disable-build-servers", StringComparison.Ordinal) && command.Contains("-m:1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Successful_run_retains_case_level_source_discovery_and_execution_evidence()
    {
        using var fixture = new GuiFixture();
        var process = new GuiProcessRunner(fixture, string.Empty);
        var source = new StubGuiInventory();
        var coordinator = new GuiTestAcceptanceCoordinator(
            process, source, new StubSafetyProbe(false, false), new StubGuiLease(true));

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.Single(result.SourceMethodEvidence);
        Assert.Single(result.DiscoveredCaseEvidence);
        Assert.Single(result.ExecutedCaseEvidence);
        Assert.Equal("Example.Gui.Case", result.SourceMethodEvidence[0].FullyQualifiedName);
        Assert.Equal("Example.Gui.Case", result.DiscoveredCaseEvidence[0].MethodName);
        Assert.Equal("Example.Gui.Case", result.ExecutedCaseEvidence[0].MethodName);
        Assert.Equal(TestOutcome.Passed, result.ExecutedCaseEvidence[0].Outcome);

        await GuiTestAcceptanceEvidenceWriter.WriteAsync(fixture.Options, result, CancellationToken.None);

        var evidencePath = Path.Combine(fixture.Options.ArtifactRoot, "desktop-d2.inventory.json");
        Assert.True(File.Exists(evidencePath));
        using var evidence = JsonDocument.Parse(File.ReadAllText(evidencePath));
        Assert.Equal(new string('a', 40), evidence.RootElement.GetProperty("revision").GetString());
        Assert.Equal(1, evidence.RootElement.GetProperty("sourceMethods").GetArrayLength());
        Assert.Equal(1, evidence.RootElement.GetProperty("discoveredCases").GetArrayLength());
        Assert.Equal(1, evidence.RootElement.GetProperty("executedCases").GetArrayLength());
    }

    [Fact]
    public async Task Duplicate_theory_locators_must_match_as_a_multiset()
    {
        using var fixture = new GuiFixture();
        var duplicateDisplay = "Example.Gui.Theory(value: 1)";
        var otherDisplay = "Example.Gui.Theory(value: 2)";
        var process = new GuiProcessRunner(
            fixture,
            string.Empty,
            "The following Tests are available:\r\n  " + duplicateDisplay + "\r\n  " + duplicateDisplay +
                "\r\n  " + otherDisplay + "\r\n",
            new[] { duplicateDisplay, otherDisplay, otherDisplay });
        var inventory = new StubGuiInventory(() => true, "Example.Gui.Theory", isTheory: true);
        var coordinator = new GuiTestAcceptanceCoordinator(
            process, inventory, new StubSafetyProbe(false, false), new StubGuiLease(true));

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
    }

    [Fact]
    public async Task Global_gui_lease_can_be_released_after_an_async_thread_switch()
    {
        var lease = new GlobalGuiExecutionLease().TryAcquire(TimeSpan.FromSeconds(1));
        Assert.NotNull(lease);

        var exception = await Record.ExceptionAsync(() => Task.Run(() => lease!.Dispose()));

        Assert.Null(exception);
    }

    private sealed class GuiFixture : IDisposable
    {
        public GuiFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "MyDmsVn.GuiAcceptance", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Project = Path.Combine(Root, "Desktop.Tests.csproj");
            File.WriteAllText(Project, "<Project />");
            Options = new GuiTestAcceptanceOptions(Root, Project, "net48", Path.Combine(Root, "artifacts"),
                TimeSpan.FromSeconds(5), new string('a', 40))
            {
                ApprovedProjectPath = Project,
                ApprovedFrameworks = new[] { "net48" },
            };
        }

        public string Root { get; }
        public string Project { get; }
        public GuiTestAcceptanceOptions Options { get; }
        public void Dispose() => Directory.Delete(Root, true);
    }

    private sealed class GuiProcessRunner : IProcessRunner
    {
        private readonly GuiFixture _fixture;
        private readonly string _failure;
        private readonly string _discoveryOutput;
        private readonly IReadOnlyList<string>? _trxRows;
        public GuiProcessRunner(
            GuiFixture fixture,
            string failure,
            string? discoveryOutput = null,
            IReadOnlyList<string>? trxRows = null)
        {
            _fixture = fixture;
            _failure = failure;
            _discoveryOutput = discoveryOutput ?? "The following Tests are available:\r\n  Example.Gui.Case\r\n";
            _trxRows = trxRows;
        }
        public int Calls { get; private set; }
        public List<string> Commands { get; } = new();

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            Calls++;
            Commands.Add(command.Arguments);
            if (command.Arguments.StartsWith("build ", StringComparison.Ordinal))
            {
                return Task.FromResult(Result(string.Empty, false));
            }

            if (command.Arguments.Contains("--list-tests", StringComparison.Ordinal))
            {
                return Task.FromResult(Result(_discoveryOutput, false));
            }

            if (_failure != "missing-trx")
            {
                Directory.CreateDirectory(_fixture.Options.ArtifactRoot);
                var rows = _trxRows ?? new[] { "Example.Gui.Case" };
                var results = string.Join(string.Empty, rows.Select(row =>
                    "<UnitTestResult testName=\"" + row + "\" outcome=\"Passed\" duration=\"00:00:00.001\" />"));
                File.WriteAllText(_fixture.Options.TrxPath, "<TestRun><Results>" + results + "</Results></TestRun>");
            }

            return Task.FromResult(Result(string.Empty, _failure == "timeout"));
        }

        private static CommandResult Result(string output, bool timedOut) => new(
            timedOut ? -1 : 0, timedOut, 42, output, string.Empty,
            timedOut ? new[] { 42 } : Array.Empty<int>(), TimeSpan.FromMilliseconds(1));
    }

    private sealed class StubGuiInventory : IGuiTestSourceInventory
    {
        private readonly Func<bool> _buildObserved;
        private readonly string _method;
        private readonly bool _isTheory;
        public StubGuiInventory(Func<bool>? buildObserved = null, string method = "Example.Gui.Case", bool isTheory = false)
        {
            _buildObserved = buildObserved ?? (() => true);
            _method = method;
            _isTheory = isTheory;
        }
        public bool ReadAfterBuild { get; private set; }
        public IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath)
        {
            ReadAfterBuild = _buildObserved();
            return new[]
            {
                new TestMethodDescriptor(_method, _isTheory, new string('a', 64), new[] { "NonSql" }, true)
                { NormalizedSignature = _method + "()" },
            };
        }
    }

    private sealed class StubSafetyProbe : IGuiProcessSafetyProbe
    {
        private readonly bool _dialog;
        private readonly bool _orphan;
        public StubSafetyProbe(bool dialog, bool orphan) { _dialog = dialog; _orphan = orphan; }
        public GuiProcessSafetyResult Inspect(CommandResult result) =>
            new(!_dialog, _orphan ? new[] { 99 } : Array.Empty<int>());
    }

    private sealed class StubGuiLease : IGuiExecutionLease
    {
        private readonly bool _acquired;
        public StubGuiLease(bool acquired) => _acquired = acquired;
        public IDisposable? TryAcquire(TimeSpan timeout) => _acquired ? new Releaser() : null;
        private sealed class Releaser : IDisposable { public void Dispose() { } }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MyDmsVn.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
