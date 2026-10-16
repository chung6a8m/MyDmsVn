using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Reconciliation;
using MyDmsVn.SqlTestRunner.Trx;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Gates;

public sealed class TestGateRepositoryContractTests
{
    private static readonly Lazy<RepositoryEvidence> CurrentEvidence = new(LoadEvidence);
    private static string? _lastAuditFailure;

    [Fact]
    public void Current_manifest_matches_source_and_expanded_discovery_for_every_declared_tfm()
    {
        var manifest = LoadManifest();

        Assert.True(Audit(manifest, CurrentEvidence.Value), _lastAuditFailure);
        Assert.Equal(
            new[] { "net48", "net8.0" },
            manifest.LogicalCases.SelectMany(item => item.Frameworks)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal));
        var plan = "20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8";
        foreach (var task in Enumerable.Range(1, 5).Select(index => $"NET8-PAUSE-T0{index}"))
        {
            Assert.NotEmpty(TestGateManifestValidator.Select(
                manifest, RepositoryRoot(), TestGateMode.Task, plan, task).Gates);
        }

        var final = TestGateManifestValidator.Select(
            manifest, RepositoryRoot(), TestGateMode.Final, plan, "NET8-PAUSE-T05");
        var sql = Assert.Single(final.Gates, gate => gate.StrictMode == TestGateStrictMode.SqlPrAcceptance);
        Assert.Equal(new[] { "net48" }, sql.Frameworks);
        Assert.Equal(manifest.FrameworkProfile!.SqlProject, sql.Project);
    }

    [Fact]
    public void Net8_pause_final_executes_every_required_non_gui_test_project()
    {
        var final = TestGateManifestValidator.Select(
            LoadManifest(), RepositoryRoot(), TestGateMode.Final,
            "20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8", "NET8-PAUSE-T05");

        Assert.Equal(new[]
        {
            "tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj",
            "tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj",
            "tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj",
            "tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj",
        }, final.Gates.Where(gate => gate.StrictMode == TestGateStrictMode.DotnetTest)
            .Select(gate => gate.Project).OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void Repository_contract_rejects_inventory_and_gate_mutations()
    {
        var manifest = LoadManifest();
        var theory = manifest.LogicalCases.First(item => item.Rows.Any(row => row.LocatorSha256 is not null));
        var otherOwner = manifest.LogicalCases.First(item => !string.Equals(
            item.OwnerProject,
            theory.OwnerProject,
            StringComparison.OrdinalIgnoreCase)).OwnerProject;

        Assert.False(Audit(manifest with
        {
            LogicalCases = manifest.LogicalCases.Skip(1).ToArray(),
        }, CurrentEvidence.Value));
        Assert.False(Audit(manifest with
        {
            LogicalCases = manifest.LogicalCases.Concat(new[]
            {
                theory with
                {
                    LogicalCaseId = theory.LogicalCaseId + "-duplicate",
                    OwnerProject = otherOwner,
                },
            }).ToArray(),
        }, CurrentEvidence.Value));
        Assert.False(Audit(Replace(manifest, theory, theory with
        {
            Classification = new[] { "CategoryTypo" },
        }), CurrentEvidence.Value));
        Assert.False(Audit(Replace(manifest, theory, theory with
        {
            ReviewedCodeFingerprint = new string('f', 64),
        }), CurrentEvidence.Value));
        Assert.False(Audit(Replace(manifest, theory, theory with
        {
            Rows = theory.Rows.Select((row, index) => index == 0
                    ? row with { LocatorSha256 = new string('e', 64) }
                    : row)
                .ToArray(),
        }), CurrentEvidence.Value));

        var missingProject = manifest with
        {
            Gates = manifest.Gates.Select((gate, index) => index == 0
                    ? gate with { Project = "tests/Missing.Tests/Missing.Tests.csproj" }
                    : gate)
                .ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => SelectTask(missingProject));

        var missingTfm = manifest with
        {
            Gates = manifest.Gates.Select((gate, index) => index == 0
                    ? gate with { Frameworks = new[] { "net48" } }
                    : gate)
                .ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => SelectTask(missingTfm));
    }

    private static TestGateManifest Replace(
        TestGateManifest manifest,
        LogicalCaseDefinition current,
        LogicalCaseDefinition replacement) => manifest with
        {
            LogicalCases = manifest.LogicalCases.Select(item =>
                    ReferenceEquals(item, current) ? replacement : item)
                .ToArray(),
        };

    private static TestGateSelection SelectTask(TestGateManifest manifest) =>
        TestGateManifestValidator.Select(
            manifest,
            RepositoryRoot(),
            TestGateMode.Task,
            "20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8",
            "NET8-PAUSE-T01");

    private static bool Audit(TestGateManifest manifest, RepositoryEvidence evidence)
    {
        var sourceCases = new List<BoundLogicalCase>();
        var discoveryCases = new List<BoundLogicalCase>();
        foreach (var owner in manifest.LogicalCases.Select(item => item.OwnerProject)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!evidence.Source.TryGetValue(owner, out var source))
            {
                return false;
            }

            var historicalFrameworks = manifest.LogicalCases.Where(item => string.Equals(
                    item.OwnerProject,
                    owner,
                    StringComparison.OrdinalIgnoreCase))
                .SelectMany(item => item.Frameworks)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var frameworks = manifest.FrameworkProfile is { } profile &&
                string.Equals(owner, profile.SqlProject, StringComparison.OrdinalIgnoreCase)
                    ? historicalFrameworks.Intersect(profile.ActiveSqlFrameworks, StringComparer.OrdinalIgnoreCase).ToArray()
                    : historicalFrameworks;
            foreach (var tfm in frameworks)
            {
                if (!evidence.Discovery.TryGetValue((owner, tfm), out var discovered))
                {
                    return false;
                }

                var sourceBinding = LogicalCaseBinder.BindSource(
                    source,
                    manifest.LogicalCases,
                    owner,
                    tfm,
                    manifest.Exemptions);
                var discoveryBinding = LogicalCaseBinder.BindDiscovery(
                    discovered,
                    manifest.LogicalCases,
                    owner,
                    tfm);
                if (!sourceBinding.IsComplete || !discoveryBinding.IsComplete)
                {
                    var expectedLocators = manifest.LogicalCases
                        .Where(item => string.Equals(item.OwnerProject, owner, StringComparison.OrdinalIgnoreCase))
                        .SelectMany(item => item.Rows)
                        .Where(row => row.LocatorSha256 is not null)
                        .Select(row => row.LocatorSha256!)
                        .ToHashSet(StringComparer.Ordinal);
                    var actualLocators = discovered.Select(item => item.PhysicalRowLocator)
                        .ToHashSet(StringComparer.Ordinal);
                    _lastAuditFailure = owner + "|" + tfm + "|" + string.Join(
                        ",",
                        sourceBinding.Failures.Concat(discoveryBinding.Failures)) +
                        $"|discovered={discovered.Count}|missingLocators={expectedLocators.Except(actualLocators).Count()}" +
                        $"|extraLocators={actualLocators.Except(expectedLocators).Count()}";
                    return false;
                }

                sourceCases.AddRange(sourceBinding.Cases);
                discoveryCases.AddRange(discoveryBinding.Cases);
            }
        }

        var expected = sourceCases.Select(item => new ExpectedLogicalCase(
                item.LogicalKey,
                item.LogicalCaseId,
                item.OwnerProject,
                item.Tfm,
                item.NormalizedMethodSignature,
                item.RowKey,
                item.ReviewedCodeFingerprint,
                item.Classification))
            .ToArray();
        var executed = discoveryCases.Select(item => new ExecutedLogicalCase(
                item.LogicalKey,
                item.LogicalCaseId,
                item.OwnerProject,
                item.Tfm,
                item.NormalizedMethodSignature,
                item.RowKey,
                item.ReviewedCodeFingerprint,
                item.Classification,
                TestOutcome.Passed,
                TimeSpan.Zero))
            .ToArray();
        var reconciliation = LogicalCaseInventoryReconciler.Reconcile(
            expected,
            sourceCases,
            discoveryCases,
            executed,
            manifest.IdentityMappings,
            manifest.Exemptions);
        _lastAuditFailure = string.Join(",", reconciliation.Reasons);
        return reconciliation.IsComplete;
    }

    private static RepositoryEvidence LoadEvidence()
    {
        var root = RepositoryRoot();
        var manifest = LoadManifest();
        var source = new Dictionary<string, IReadOnlyList<TestMethodDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var discovery = new Dictionary<(string Owner, string Tfm), IReadOnlyList<DiscoveredTestCase>>();
        foreach (var owner in manifest.LogicalCases.Select(item => item.OwnerProject)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var historical = manifest.LogicalCases.Where(item => string.Equals(item.OwnerProject, owner, StringComparison.OrdinalIgnoreCase))
                .SelectMany(item => item.Frameworks).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var frameworks = manifest.FrameworkProfile is { } profile &&
                string.Equals(owner, profile.SqlProject, StringComparison.OrdinalIgnoreCase)
                    ? profile.ActiveSqlFrameworks
                    : historical;
            var assembly = Path.Combine(root, Path.GetDirectoryName(owner)!, "bin", "Release", frameworks[0],
                Path.GetFileNameWithoutExtension(owner) + ".dll");
            source[owner] = TestMethodInventory.Read(assembly);
            foreach (var framework in frameworks)
            {
                discovery[(owner, framework)] = Discover(root, owner, framework);
            }
        }

        return new RepositoryEvidence(source, discovery);
    }

    private static IReadOnlyList<DiscoveredTestCase> Discover(
        string root,
        string project,
        string tfm)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(
                "dotnet",
                $"test \"{project}\" -c Release -f {tfm} --no-build --list-tests")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
            },
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Bounded test discovery exceeded two minutes.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Bounded test discovery failed: " + error);
        }

        return VstestDiscoveryParser.Parse(output, tfm);
    }

    private static TestGateManifest LoadManifest() => TestGateManifestLoader.Load(
        Path.Combine(RepositoryRoot(), "tests", "test-gates.v1.json"));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MyDmsVn.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record RepositoryEvidence(
        IReadOnlyDictionary<string, IReadOnlyList<TestMethodDescriptor>> Source,
        IReadOnlyDictionary<(string Owner, string Tfm), IReadOnlyList<DiscoveredTestCase>> Discovery);
}
