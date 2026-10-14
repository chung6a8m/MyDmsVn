using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Processes;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Benchmark;

public sealed class BenchmarkRunCoordinatorTests
{
    [Fact]
    public async Task Blocked_preflight_never_launches_a_test_process()
    {
        var process = new RecordingProcessRunner();
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(false),
            process,
            new StubResultGate());

        var result = await coordinator.RunAsync(Options(), CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Empty(process.Commands);
    }

    [Fact]
    public async Task Valid_run_uses_sequential_tfms_unique_trx_and_required_child_environment()
    {
        var process = new RecordingProcessRunner();
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            process,
            new StubResultGate());

        var result = await coordinator.RunAsync(Options(), CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.Equal(2, process.Commands.Count);
        Assert.NotEqual(process.Commands[0].Arguments, process.Commands[1].Arguments);
        Assert.All(process.Commands, command =>
        {
            Assert.Contains("--no-build", command.Arguments);
            Assert.Contains("--logger", command.Arguments);
            Assert.True(command.Environment.ContainsKey("MYDMSVN_SQL_TEST_APPROVAL_FILE"));
            Assert.True(command.Environment.ContainsKey("MYDMSVN_SQL_TEST_RUN_ID"));
        });
    }

    private static BenchmarkRunOptions Options() => new BenchmarkRunOptions(
        "repo",
        "tests.csproj",
        "artifacts",
        "approval.json",
        new[] { "net48", "net8.0" },
        TimeSpan.FromMinutes(1));

    private sealed class StubPreflightGate : IBenchmarkPreflightGate
    {
        private readonly bool _allowed;

        public StubPreflightGate(bool allowed) => _allowed = allowed;

        public Task<bool> ValidateAsync(CancellationToken cancellationToken) => Task.FromResult(_allowed);
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<CommandSpec> Commands { get; } = new List<CommandSpec>();

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(new CommandResult(0, false, 1, string.Empty, string.Empty, Array.Empty<int>()));
        }
    }

    private sealed class StubResultGate : IBenchmarkResultGate
    {
        public bool IsValid(string trxPath, string tfm) => true;
    }
}
