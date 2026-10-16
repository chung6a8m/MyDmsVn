using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Focused;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reconciliation;
using MyDmsVn.SqlTestRunner.Reporting;

namespace MyDmsVn.SqlTestRunner;

internal static class Program
{
    private static readonly Regex RevisionPattern = new Regex(
        "^[0-9a-fA-F]{40}$",
        RegexOptions.CultureInvariant);

    public static Task<int> Main(string[] args) =>
        RunAsync(args, RunBaselineAsync, RunPrAcceptanceAsync, RunTestGateAsync, CancellationToken.None);

    internal static async Task<int> RunAsync(
        string[] args,
        Func<BenchmarkRunOptions, CancellationToken, Task<BenchmarkRunResult>> baselineRunner,
        CancellationToken cancellationToken) =>
        await RunAsync(args, baselineRunner, RunPrAcceptanceAsync, RunTestGateAsync, cancellationToken)
            .ConfigureAwait(false);

    internal static async Task<int> RunAsync(
        string[] args,
        Func<BenchmarkRunOptions, CancellationToken, Task<BenchmarkRunResult>> baselineRunner,
        Func<BenchmarkRunOptions, CancellationToken, Task<BenchmarkRunResult>> acceptanceRunner,
        CancellationToken cancellationToken) =>
        await RunAsync(args, baselineRunner, acceptanceRunner, RunTestGateAsync, cancellationToken)
            .ConfigureAwait(false);

    internal static async Task<int> RunAsync(
        string[] args,
        Func<BenchmarkRunOptions, CancellationToken, Task<BenchmarkRunResult>> baselineRunner,
        Func<BenchmarkRunOptions, CancellationToken, Task<BenchmarkRunResult>> acceptanceRunner,
        Func<TestGateRunOptions, CancellationToken, Task<TestGateRunResult>> testGateRunner,
        CancellationToken cancellationToken,
        Func<TestGateRunOptions, bool>? finalProvenanceVerifier = null)
    {
        if (args.Length == 0)
        {
            return 64;
        }

        if (string.Equals(args[0], "preflight", StringComparison.OrdinalIgnoreCase))
        {
            return await new EnvironmentPreflightGate()
                .ValidateAsync(cancellationToken)
                .ConfigureAwait(false)
                ? 0
                : 2;
        }

        if (string.Equals(args[0], "focused-run", StringComparison.OrdinalIgnoreCase))
        {
            return await RunFocusedAsync(ParseOptions(args), cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(args[0], "gui-acceptance", StringComparison.OrdinalIgnoreCase))
        {
            return await RunGuiAcceptanceAsync(ParseOptions(args), cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(args[0], "test-gate", StringComparison.Ordinal))
        {
            if (!TryParseTestGateOptions(args, out var testGateOptions))
            {
                return 64;
            }

            var verifyProvenance = finalProvenanceVerifier ?? TestGateProvenance.Verify;
            if (testGateOptions.Mode == TestGateMode.Final && !verifyProvenance(testGateOptions))
            {
                return 3;
            }

            var gateResult = await testGateRunner(testGateOptions, cancellationToken).ConfigureAwait(false);
            if (gateResult.Status == TestGateRunStatus.Succeeded &&
                testGateOptions.Mode == TestGateMode.Final &&
                !verifyProvenance(testGateOptions))
            {
                return 3;
            }

            return gateResult.Status switch
            {
                TestGateRunStatus.Succeeded => 0,
                TestGateRunStatus.ResourceDenied => 2,
                _ => 3,
            };
        }

        var isBaseline = string.Equals(args[0], "baseline", StringComparison.OrdinalIgnoreCase);
        var isAcceptance = string.Equals(
            args[0],
            "pr-acceptance",
            StringComparison.OrdinalIgnoreCase);
        if (!isBaseline && !isAcceptance)
        {
            return 64;
        }

        var values = ParseOptions(args);
        if (!values.TryGetValue("repo", out var repository) ||
            !values.TryGetValue("project", out var project) ||
            !values.TryGetValue("artifacts", out var artifacts) ||
            !values.TryGetValue("approval", out var approval) ||
            !values.TryGetValue("frameworks", out var frameworkValue) ||
            !TryParseFrameworks(frameworkValue, out var frameworks) ||
            (isAcceptance &&
             (!values.TryGetValue("revision", out var revision) ||
              !RevisionPattern.IsMatch(revision))))
        {
            return 64;
        }

        Environment.SetEnvironmentVariable(
            SqlTestHarnessEnvironment.ApprovalFileVariable,
            approval);
        Directory.CreateDirectory(artifacts);
        var options = new BenchmarkRunOptions(
                    Path.GetFullPath(repository),
                    Path.GetFullPath(project),
                    Path.GetFullPath(artifacts),
                    Path.GetFullPath(approval),
                    frameworks,
                    TimeSpan.FromMinutes(30),
                    WarmupPasses: isAcceptance ? 0 : 1,
                    MeasuredPasses: isAcceptance ? 1 : 3,
                    OverallRunTimeout: TimeSpan.FromHours(4))
        {
            Mode = isAcceptance ? "pr-acceptance" : "baseline",
            Revision = isAcceptance ? values["revision"] : string.Empty,
        };
        var result = await (isAcceptance ? acceptanceRunner : baselineRunner)(
                options,
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsComplete ? 0 : 3;
    }

    private static async Task<int> RunFocusedAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var required = new[]
        {
            "repo", "project", "artifacts", "approval", "category", "frameworks",
        };
        if (required.Any(key => !values.ContainsKey(key)))
        {
            return 64;
        }

        if (!TryParseFrameworks(values["frameworks"], out var frameworks))
        {
            return 64;
        }

        Environment.SetEnvironmentVariable(SqlTestHarnessEnvironment.ApprovalFileVariable, values["approval"]);
        Directory.CreateDirectory(values["artifacts"]);
        using var runner = new OwnedProcessRunner();
        var options = new FocusedRunOptions(
            Path.GetFullPath(values["repo"]),
            Path.GetFullPath(values["project"]),
            Path.GetFullPath(values["artifacts"]),
            Path.GetFullPath(values["approval"]),
            values["category"],
            frameworks,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1));
        var guarded = await new SqlExecutionLeaseGuard().RunAsync(
            options.ApprovalFile,
            "legacy/focused-run/" + options.Category,
            options.ArtifactRoot,
            options.CommandTimeout,
            token => new FocusedRunCoordinator(new EnvironmentPreflightGate(), runner).RunAsync(options, token),
            result => !result.RecoveryRequired,
            cancellationToken).ConfigureAwait(false);
        return guarded.Acquired && guarded.Result.IsComplete ? 0 : 3;
    }

    private static async Task<int> RunGuiAcceptanceAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var required = new[] { "repo", "project", "artifacts", "framework", "revision", "timeout-seconds" };
        if (required.Any(key => !values.ContainsKey(key)) ||
            !string.Equals(values["framework"], "net48", StringComparison.Ordinal) ||
            !RevisionPattern.IsMatch(values["revision"]) ||
            !int.TryParse(values["timeout-seconds"], out var timeoutSeconds) || timeoutSeconds <= 0)
        {
            return 64;
        }

        var repositoryRoot = Path.GetFullPath(values["repo"]);
        if (!TestGateProvenance.VerifyCleanRevision(repositoryRoot, values["revision"])) return 3;
        var manifestPath = Path.Combine(repositoryRoot, "tests", "test-gates.v1.json");
        TestFrameworkProfile? profile;
        try { profile = TestGateManifestLoader.Load(manifestPath).FrameworkProfile; }
        catch (InvalidDataException) { return 64; }
        if (profile is null) return 64;

        var projectPath = Path.IsPathRooted(values["project"])
            ? Path.GetFullPath(values["project"])
            : Path.GetFullPath(Path.Combine(repositoryRoot, values["project"]));
        var options = new GuiTestAcceptanceOptions(
            repositoryRoot, projectPath, values["framework"],
            Path.GetFullPath(values["artifacts"]), TimeSpan.FromSeconds(timeoutSeconds), values["revision"])
        {
            ApprovedProjectPath = Path.GetFullPath(Path.Combine(repositoryRoot,
                profile.GuiProject.Replace('/', Path.DirectorySeparatorChar))),
            ApprovedFrameworks = profile.ActiveGuiFrameworks,
        };
        using var runner = new GuiOwnedProcessRunner();
        var result = await new GuiTestAcceptanceCoordinator(
            runner, new GuiTestSourceInventory(), new GuiProcessSafetyProbe(), new GlobalGuiExecutionLease())
            .RunAsync(options, cancellationToken).ConfigureAwait(false);
        if (!result.IsComplete) return 3;

        await GuiTestAcceptanceEvidenceWriter.WriteAsync(options, result, cancellationToken).ConfigureAwait(false);
        var report = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            revision = options.Revision,
            framework = options.Framework,
            project = result.Project,
            sourceMethods = result.SourceMethods,
            discoveredCases = result.DiscoveredCases,
            executedCases = result.ExecutedCases,
            durationMilliseconds = result.Duration.TotalMilliseconds,
            watchdogClean = true,
            guiLeaseClean = true,
        });
        await File.WriteAllTextAsync(Path.Combine(options.ArtifactRoot, "desktop-d2.sanitized.json"), report, cancellationToken)
            .ConfigureAwait(false);
        return 0;
    }

    private static async Task<BenchmarkRunResult> RunBaselineAsync(
        BenchmarkRunOptions options,
        CancellationToken cancellationToken)
    {
        using var runner = new OwnedProcessRunner();
        var guarded = await new SqlExecutionLeaseGuard().RunAsync(
            options.ApprovalFile,
            "legacy/baseline",
            options.ArtifactRoot,
            options.CommandTimeout,
            token => new BenchmarkRunCoordinator(new EnvironmentPreflightGate(), runner)
                .RunAsync(options, token),
            result => !result.RecoveryRequired,
            cancellationToken).ConfigureAwait(false);
        return guarded.Acquired
            ? guarded.Result
            : new BenchmarkRunResult(false, Array.Empty<BenchmarkFrameworkRun>());
    }

    private static async Task<BenchmarkRunResult> RunPrAcceptanceAsync(
        BenchmarkRunOptions options,
        CancellationToken cancellationToken)
    {
        using var runner = new OwnedProcessRunner();
        var guarded = await new SqlExecutionLeaseGuard().RunAsync(
            options.ApprovalFile,
            "legacy/pr-acceptance",
            options.ArtifactRoot,
            options.CommandTimeout,
            token => new BenchmarkRunCoordinator(
                    new EnvironmentPreflightGate(),
                    runner,
                    reportExporter: new SqlAcceptanceReportExporter())
                .RunAsync(options, token),
            result => !result.RecoveryRequired,
            cancellationToken).ConfigureAwait(false);
        return guarded.Acquired
            ? guarded.Result
            : new BenchmarkRunResult(false, Array.Empty<BenchmarkFrameworkRun>());
    }

    private static async Task<TestGateRunResult> RunTestGateAsync(
        TestGateRunOptions options,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.ArtifactRoot);
        using var runner = new OwnedProcessRunner();
        return await new TestGateCoordinator(
                runner,
                new TestGateSourceInventory(),
                new TestGateReportExporter(),
                new TestGateApprovalReader(),
                new SqlExecutionLeaseFactory(),
                new TestGateSqlAcceptanceRunner(runner))
            .RunAsync(options, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryParseTestGateOptions(string[] args, out TestGateRunOptions options)
    {
        options = null!;
        var allowed = new HashSet<string>(
            new[] { "repo", "manifest", "mode", "plan", "task", "artifacts", "approval", "revision" },
            StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (args.Length < 3 || (args.Length - 1) % 2 != 0)
        {
            return false;
        }

        for (var index = 1; index < args.Length; index += 2)
        {
            var key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                return false;
            }

            key = key.Substring(2);
            var value = args[index + 1];
            if (!allowed.Contains(key) || values.ContainsKey(key) || string.IsNullOrWhiteSpace(value) ||
                value.StartsWith("--", StringComparison.Ordinal))
            {
                return false;
            }

            values.Add(key, value);
        }

        var required = new[] { "repo", "manifest", "mode", "plan", "task", "artifacts" };
        if (required.Any(key => !values.ContainsKey(key)) ||
            !Enum.TryParse<TestGateMode>(values["mode"], ignoreCase: false, out var mode) ||
            (values.TryGetValue("revision", out var suppliedRevision) &&
             !RevisionPattern.IsMatch(suppliedRevision)) ||
            (mode == TestGateMode.Final &&
             (!values.ContainsKey("approval") || !values.ContainsKey("revision"))))
        {
            return false;
        }

        var repository = Path.GetFullPath(values["repo"]);
        options = new TestGateRunOptions(
            repository,
            Path.GetFullPath(values["manifest"], repository),
            mode,
            values["plan"],
            values["task"],
            Path.GetFullPath(values["artifacts"], repository),
            values.TryGetValue("approval", out var approval)
                ? Path.GetFullPath(approval, repository)
                : null,
            values.TryGetValue("revision", out var revision) ? revision : null);
        return true;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index + 1 < args.Length; index += 2)
        {
            if (args[index].StartsWith("--", StringComparison.Ordinal))
            {
                values[args[index].Substring(2)] = args[index + 1];
            }
        }

        return values;
    }

    private static bool TryParseFrameworks(string value, out IReadOnlyList<string> frameworks)
    {
        frameworks = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parsed = value.Split(',', StringSplitOptions.None);
        var supported = new[] { "net48", "net8.0" };
        if (parsed.Any(string.IsNullOrWhiteSpace) ||
            parsed.Any(item => !supported.Contains(item, StringComparer.Ordinal)) ||
            parsed.Distinct(StringComparer.Ordinal).Count() != parsed.Length ||
            !parsed.SequenceEqual(supported.Where(item => parsed.Contains(item, StringComparer.Ordinal)), StringComparer.Ordinal))
        {
            return false;
        }

        frameworks = parsed;
        return true;
    }
}
