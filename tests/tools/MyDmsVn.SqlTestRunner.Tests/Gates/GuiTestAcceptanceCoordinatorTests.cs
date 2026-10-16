using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
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

        Assert.Contains("status --porcelain --untracked-files=all", script, StringComparison.Ordinal);
        Assert.Contains("--framework net48", script, StringComparison.Ordinal);
        Assert.Contains("--revision $Revision", script, StringComparison.Ordinal);
        Assert.Contains("--timeout-seconds $TimeoutSeconds", script, StringComparison.Ordinal);
        Assert.Contains("gui-acceptance", script, StringComparison.Ordinal);
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

    private sealed class GuiFixture : IDisposable
    {
        public GuiFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "MyDmsVn.GuiAcceptance", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Project = Path.Combine(Root, "Desktop.Tests.csproj");
            File.WriteAllText(Project, "<Project />");
            Options = new GuiTestAcceptanceOptions(Root, Project, "net48", Path.Combine(Root, "artifacts"), TimeSpan.FromSeconds(5));
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
        public GuiProcessRunner(GuiFixture fixture, string failure) { _fixture = fixture; _failure = failure; }
        public int Calls { get; private set; }

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            Calls++;
            if (command.Arguments.Contains("--list-tests", StringComparison.Ordinal))
            {
                return Task.FromResult(Result("The following Tests are available:\r\n  Example.Gui.Case\r\n", false));
            }

            if (_failure != "missing-trx")
            {
                Directory.CreateDirectory(_fixture.Options.ArtifactRoot);
                File.WriteAllText(_fixture.Options.TrxPath,
                    "<TestRun><Results><UnitTestResult testName=\"Example.Gui.Case\" outcome=\"Passed\" duration=\"00:00:00.001\" /></Results></TestRun>");
            }

            return Task.FromResult(Result(string.Empty, _failure == "timeout"));
        }

        private static CommandResult Result(string output, bool timedOut) => new(
            timedOut ? -1 : 0, timedOut, 42, output, string.Empty,
            timedOut ? new[] { 42 } : Array.Empty<int>(), TimeSpan.FromMilliseconds(1));
    }

    private sealed class StubGuiInventory : IGuiTestSourceInventory
    {
        public IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath) => new[]
        {
            new TestMethodDescriptor("Example.Gui.Case", false, new string('a', 64), new[] { "NonSql" }, true)
            { NormalizedSignature = "Example.Gui.Case()" },
        };
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
