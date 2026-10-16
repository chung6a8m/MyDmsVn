using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Gates;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests;

public sealed class ProgramTests
{
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task Test_gate_task_maps_status_without_approval()
    {
        TestGateRunOptions? received = null;
        var exit = await Program.RunAsync(
            TestGateArguments("Task"),
            (_, _) => throw new InvalidOperationException(),
            (_, _) => throw new InvalidOperationException(),
            (options, _) =>
            {
                received = options;
                return Task.FromResult(new TestGateRunResult(TestGateRunStatus.Succeeded, Array.Empty<TestGateFrameworkResult>()));
            },
            CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.NotNull(received);
        Assert.Equal(TestGateMode.Task, received.Mode);
        Assert.Null(received.ApprovalFile);
    }

    [Theory]
    [InlineData(TestGateRunStatus.ResourceDenied, 2)]
    [InlineData(TestGateRunStatus.Incomplete, 3)]
    public async Task Test_gate_maps_non_success_status(TestGateRunStatus status, int expectedExit)
    {
        var exit = await Program.RunAsync(
            TestGateArguments("Task"),
            (_, _) => throw new InvalidOperationException(),
            (_, _) => throw new InvalidOperationException(),
            (_, _) => Task.FromResult(new TestGateRunResult(status, Array.Empty<TestGateFrameworkResult>())),
            CancellationToken.None);

        Assert.Equal(expectedExit, exit);
    }

    [Fact]
    public async Task Test_gate_final_requires_approval_and_exact_revision()
    {
        var called = false;
        var invalid = TestGateArguments("Final");
        var valid = invalid.Concat(new[] { "--approval", "approval.json", "--revision", Revision }).ToArray();

        var invalidExit = await TestGateProgram(invalid, _ => called = true);
        var validExit = await TestGateProgram(valid, _ => called = true);

        Assert.Equal(64, invalidExit);
        Assert.Equal(0, validExit);
        Assert.True(called);
    }

    [Fact]
    public async Task Test_gate_final_rejects_unverified_or_changed_repository_provenance()
    {
        var args = TestGateArguments("Final")
            .Concat(new[] { "--approval", "approval.json", "--revision", Revision })
            .ToArray();
        var calls = 0;
        var runnerCalled = false;

        var rejectedBeforeRun = await Program.RunAsync(
            args,
            (_, _) => throw new InvalidOperationException(),
            (_, _) => throw new InvalidOperationException(),
            (_, _) =>
            {
                runnerCalled = true;
                return Task.FromResult(new TestGateRunResult(TestGateRunStatus.Succeeded, Array.Empty<TestGateFrameworkResult>()));
            },
            CancellationToken.None,
            _ => false);
        var rejectedAfterRun = await Program.RunAsync(
            args,
            (_, _) => throw new InvalidOperationException(),
            (_, _) => throw new InvalidOperationException(),
            (_, _) => Task.FromResult(new TestGateRunResult(TestGateRunStatus.Succeeded, Array.Empty<TestGateFrameworkResult>())),
            CancellationToken.None,
            _ => ++calls == 1);

        Assert.Equal(3, rejectedBeforeRun);
        Assert.False(runnerCalled);
        Assert.Equal(3, rejectedAfterRun);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Final_provenance_requires_exact_tracked_manifest_HEAD_and_clean_tree()
    {
        using var repository = new ProvenanceRepository();
        var options = repository.Options();

        Assert.True(TestGateProvenance.Verify(options));
        Assert.False(TestGateProvenance.Verify(options with
        {
            ManifestPath = Path.Combine(repository.Root, "external.json"),
        }));
        Assert.False(TestGateProvenance.Verify(options with { Revision = Revision }));

        File.AppendAllText(repository.Manifest, Environment.NewLine);
        Assert.False(TestGateProvenance.Verify(options));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("missing-value")]
    [InlineData("missing-plan")]
    public async Task Test_gate_rejects_malformed_options_before_runner_creation(string kind)
    {
        var args = TestGateArguments("Task").ToList();
        switch (kind)
        {
            case "duplicate":
                args.AddRange(new[] { "--plan", "again" });
                break;
            case "unknown":
                args.AddRange(new[] { "--project", "forbidden.csproj" });
                break;
            case "missing-value":
                args.Add("--revision");
                break;
            case "missing-plan":
                var index = args.IndexOf("--plan");
                args.RemoveRange(index, 2);
                break;
        }

        var called = false;
        var exit = await TestGateProgram(args.ToArray(), _ => called = true);

        Assert.Equal(64, exit);
        Assert.False(called);
    }

    private static string[] TestGateArguments(string mode) => new[]
    {
        "test-gate", "--repo", ".", "--manifest", "tests/test-gates.v1.json",
        "--mode", mode, "--plan", "20261016-001", "--task", "TESTGATE-T03",
        "--artifacts", ".artifacts",
    };

    private static Task<int> TestGateProgram(string[] args, Action<TestGateRunOptions> called) => Program.RunAsync(
        args,
        (_, _) => throw new InvalidOperationException(),
        (_, _) => throw new InvalidOperationException(),
        (options, _) =>
        {
            called(options);
            return Task.FromResult(new TestGateRunResult(TestGateRunStatus.Succeeded, Array.Empty<TestGateFrameworkResult>()));
        },
        CancellationToken.None,
        _ => true);

    private sealed class ProvenanceRepository : IDisposable
    {
        public ProvenanceRepository()
        {
            Root = Path.Combine(Path.GetTempPath(), "MyDmsVn.Provenance", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "tests"));
            Manifest = Path.Combine(Root, "tests", "test-gates.v1.json");
            File.WriteAllText(Manifest, "{}");
            File.WriteAllText(Path.Combine(Root, ".gitignore"), ".artifacts/" + Environment.NewLine);
            Git("init");
            Git("config", "user.email", "test@example.invalid");
            Git("config", "user.name", "Test");
            Git("add", ".");
            Git("commit", "-m", "fixture");
            RevisionValue = Git("rev-parse", "HEAD").Trim();
        }

        public string Root { get; }
        public string Manifest { get; }
        public string RevisionValue { get; }

        public TestGateRunOptions Options() => new(
            Root,
            Manifest,
            TestGateMode.Final,
            "20261016-001",
            "TESTGATE-T03",
            Path.Combine(Root, ".artifacts"),
            Path.Combine(Root, "approval.json"),
            RevisionValue);

        public void Dispose()
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }

        private string Git(params string[] arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    WorkingDirectory = Root,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
    }

    [Fact]
    public async Task Baseline_required_report_failure_maps_to_exit_code_three_without_SQL()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ProgramTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var called = false;
            var result = await Program.RunAsync(
                new[]
                {
                    "baseline",
                    "--repo", root,
                    "--project", Path.Combine(root, "tests.csproj"),
                    "--artifacts", Path.Combine(root, "artifacts"),
                    "--approval", Path.Combine(root, "approval.json"),
                    "--frameworks", "net48",
                },
                (options, cancellationToken) =>
                {
                    called = true;
                    return Task.FromResult(new BenchmarkRunResult(
                        false,
                        Array.Empty<BenchmarkFrameworkRun>()));
                },
                CancellationToken.None);

            Assert.True(called);
            Assert.Equal(3, result);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Pr_acceptance_maps_to_a_single_pass_profile_for_selected_tfms()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ProgramTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            BenchmarkRunOptions? received = null;
            var result = await Program.RunAsync(
                new[]
                {
                    "pr-acceptance",
                    "--repo", root,
                    "--project", Path.Combine(root, "tests.csproj"),
                    "--artifacts", Path.Combine(root, "artifacts"),
                    "--approval", Path.Combine(root, "approval.json"),
                    "--revision", Revision,
                    "--frameworks", "net48",
                },
                (options, cancellationToken) => throw new InvalidOperationException("Baseline must not run."),
                (options, cancellationToken) =>
                {
                    received = options;
                    return Task.FromResult(new BenchmarkRunResult(
                        true,
                        Array.Empty<BenchmarkFrameworkRun>()));
                },
                CancellationToken.None);

            Assert.Equal(0, result);
            Assert.NotNull(received);
            Assert.Equal(0, received.WarmupPasses);
            Assert.Equal(1, received.MeasuredPasses);
            Assert.Equal("pr-acceptance", received.Mode);
            Assert.Equal(Revision, received.Revision);
            Assert.Equal(new[] { "net48" }, received.TargetFrameworks);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("net9.0")]
    [InlineData("net48,net48")]
    [InlineData("net8.0,net48")]
    public async Task Strict_modes_reject_invalid_framework_arrays_before_runner_creation(string frameworks)
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ProgramTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var called = false;
            var result = await Program.RunAsync(
                new[]
                {
                    "pr-acceptance", "--repo", root, "--project", Path.Combine(root, "tests.csproj"),
                    "--artifacts", Path.Combine(root, "artifacts"), "--approval", Path.Combine(root, "approval.json"),
                    "--revision", Revision, "--frameworks", frameworks,
                },
                (_, _) => throw new InvalidOperationException(),
                (_, _) =>
                {
                    called = true;
                    return Task.FromResult(new BenchmarkRunResult(true, Array.Empty<BenchmarkFrameworkRun>()));
                },
                CancellationToken.None);

            Assert.Equal(64, result);
            Assert.False(called);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("net48")]
    [InlineData("net8.0")]
    [InlineData("net48,net8.0")]
    public async Task Strict_modes_accept_supported_active_or_reactivation_framework_order(string frameworks)
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ProgramTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            BenchmarkRunOptions? received = null;
            var exit = await Program.RunAsync(new[]
            {
                "pr-acceptance", "--repo", root, "--project", Path.Combine(root, "tests.csproj"),
                "--artifacts", Path.Combine(root, "artifacts"), "--approval", Path.Combine(root, "approval.json"),
                "--revision", Revision, "--frameworks", frameworks,
            }, (_, _) => throw new InvalidOperationException(), (options, _) =>
            {
                received = options;
                return Task.FromResult(new BenchmarkRunResult(true, Array.Empty<BenchmarkFrameworkRun>()));
            }, CancellationToken.None);

            Assert.Equal(0, exit);
            Assert.Equal(frameworks.Split(','), received!.TargetFrameworks);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-commit")]
    public async Task Pr_acceptance_rejects_missing_or_invalid_revision(string revision)
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ProgramTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var called = false;
            var result = await Program.RunAsync(
                new[]
                {
                    "pr-acceptance",
                    "--repo", root,
                    "--project", Path.Combine(root, "tests.csproj"),
                    "--artifacts", Path.Combine(root, "artifacts"),
                    "--approval", Path.Combine(root, "approval.json"),
                    "--revision", revision,
                },
                (options, cancellationToken) => throw new InvalidOperationException("Baseline must not run."),
                (options, cancellationToken) =>
                {
                    called = true;
                    return Task.FromResult(new BenchmarkRunResult(
                        true,
                        Array.Empty<BenchmarkFrameworkRun>()));
                },
                CancellationToken.None);

            Assert.Equal(64, result);
            Assert.False(called);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
