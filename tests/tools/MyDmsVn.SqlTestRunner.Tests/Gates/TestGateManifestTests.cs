using System;
using System.Collections.Generic;
using System.IO;
using MyDmsVn.SqlTestRunner.Gates;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Gates;

public sealed class TestGateManifestTests
{
    [Fact]
    public void Load_rejects_unknown_or_missing_schema_fields()
    {
        using var files = new ManifestFiles();

        Assert.Throws<InvalidDataException>(() =>
            TestGateManifestLoader.Load(files.Write(ValidJson.Replace(
                "\"schemaVersion\": 1,",
                string.Empty,
                StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() =>
            TestGateManifestLoader.Load(files.Write(ValidJson.Replace(
                "\"schemaVersion\": 1,",
                "\"schemaVersion\": 2,",
                StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() =>
            TestGateManifestLoader.Load(files.Write(ValidJson.Replace(
                "\"schemaVersion\": 1,",
                "\"schemaVersion\": 1, \"unexpected\": true,",
                StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() =>
            TestGateManifestLoader.Load(files.Write(ValidJson.Replace(
                "\"mode\": \"Task\"",
                "\"mode\": \"Unknown\"",
                StringComparison.Ordinal))));
    }

    [Fact]
    public void Load_rejects_duplicate_gate_and_identity_ids()
    {
        using var files = new ManifestFiles();
        var duplicateGate = ValidJson.Replace(
            "    }\r\n  ],\r\n  \"logicalCases\"",
            "    },\r\n" + GateJson + "\r\n  ],\r\n  \"logicalCases\"",
            StringComparison.Ordinal);
        var duplicateIdentity = ValidJson.Replace(
            "    }\r\n  ],\r\n  \"identityMappings\"",
            "    },\r\n" + LogicalCaseJson + "\r\n  ],\r\n  \"identityMappings\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            TestGateManifestLoader.Load(files.Write(duplicateGate)));
        Assert.Throws<InvalidDataException>(() =>
            TestGateManifestLoader.Load(files.Write(duplicateIdentity)));
    }

    [Fact]
    public void Load_accepts_the_exact_v1_shape()
    {
        using var files = new ManifestFiles();

        var manifest = TestGateManifestLoader.Load(files.Write(ValidJson));

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Single(manifest.Gates);
        Assert.Single(manifest.LogicalCases);
        Assert.Empty(manifest.IdentityMappings);
        Assert.Empty(manifest.Exemptions);
        Assert.Equal(TestGateMode.Task, manifest.Gates[0].Mode);
        Assert.Equal(TestGateSelectionKind.UnfilteredProject, manifest.Gates[0].Selection.Kind);
    }

    [Fact]
    public void Net8_pause_final_uses_reviewed_active_sql_profile_and_rejects_tool_only_runner_coverage()
    {
        using var repository = new TestRepository();
        var profile = new TestFrameworkProfile(
            "net8-pause-stage-b", "NET8-PLAN", "sql-net48.csproj", new[] { "net48" },
            new[] { "net48", "net8.0" }, "desktop-net48.csproj", new[] { "net48" },
            new[] { "net48", "net8.0-windows" },
            new[] { new TestFrameworkProject("tests.csproj", new[] { "net8.0" }) }, 1);
        var sql = Gate("final-sql", taskId: "NET8-PAUSE-T05", mode: TestGateMode.Final,
            project: "sql-net48.csproj", frameworks: new[] { "net48" }) with
        {
            PlanId = "NET8-PLAN",
            StrictMode = TestGateStrictMode.SqlPrAcceptance,
            Resources = new TestGateResources(TestGateResourceRequirement.RequiredExclusive, TestGateResourceRequirement.None),
            Evidence = new[] { TestGateEvidenceKind.Source, TestGateEvidenceKind.Discovery, TestGateEvidenceKind.Trx,
                TestGateEvidenceKind.SanitizedReport, TestGateEvidenceKind.Ownership },
        };
        var manifest = Manifest(sql) with
        {
            FrameworkProfile = profile,
            LogicalCases = new[] { new LogicalCaseDefinition("case", "sql-net48.csproj",
                new[] { "net48", "net8.0" }, "Example.Sql.Case()", new string('a', 64),
                new[] { "SqlIntegration" }, new[] { new TheoryRowDefinition("fact", null) }) },
        };

        var selected = TestGateManifestValidator.Select(manifest, repository.Root, TestGateMode.Final,
            "NET8-PLAN", "NET8-PAUSE-T05");
        Assert.Single(selected.Gates);

        var disguisedRunner = sql with { Project = "tests.csproj", Frameworks = new[] { "net8.0" } };
        Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
            manifest with { Gates = new[] { disguisedRunner } }, repository.Root, TestGateMode.Final,
            "NET8-PLAN", "NET8-PAUSE-T05"));
        Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
            manifest with { Gates = new[] { sql with { Frameworks = new[] { "net8.0" } } } },
            repository.Root, TestGateMode.Final, "NET8-PLAN", "NET8-PAUSE-T05"));
    }

    [Fact]
    public void Select_rejects_unknown_or_omitted_plan_task_and_empty_gate_set()
    {
        using var repository = new TestRepository();
        var manifest = Manifest(Gate("runner-task"));

        Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
            manifest, repository.Root, TestGateMode.Task, "unknown", "TESTGATE-T03"));
        Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
            manifest, repository.Root, TestGateMode.Task, "20261016-001", string.Empty));
        Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
            Manifest(), repository.Root, TestGateMode.Task, "20261016-001", "TESTGATE-T03"));
    }

    [Fact]
    public void Select_rejects_paths_outside_repository_and_missing_projects()
    {
        using var repository = new TestRepository();
        var outside = Path.Combine(Path.GetDirectoryName(repository.Root)!, "outside.csproj");
        File.WriteAllText(outside, TestRepository.Net8Project);
        try
        {
            Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
                Manifest(Gate("escape", project: "../outside.csproj")),
                repository.Root,
                TestGateMode.Task,
                "20261016-001",
                "TESTGATE-T03"));
            Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
                Manifest(Gate("missing", project: "missing.csproj")),
                repository.Root,
                TestGateMode.Task,
                "20261016-001",
                "TESTGATE-T03"));

            var link = Path.Combine(repository.Root, "linked.csproj");
            try
            {
                File.CreateSymbolicLink(link, outside);
                Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
                    Manifest(Gate("link", project: "linked.csproj")),
                    repository.Root,
                    TestGateMode.Task,
                    "20261016-001",
                    "TESTGATE-T03"));
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException ||
                exception is PlatformNotSupportedException ||
                exception is IOException)
            {
                // Symbolic-link creation is optional on Windows hosts without developer privileges.
            }
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void Select_rejects_missing_declared_tfm_and_empty_filter()
    {
        using var repository = new TestRepository();

        Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
            Manifest(Gate("wrong-tfm", frameworks: new[] { "net48" })),
            repository.Root,
            TestGateMode.Task,
            "20261016-001",
            "TESTGATE-T03"));
        Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
            Manifest(Gate(
                "empty-filter",
                selection: new TestGateSelectionDefinition(
                    TestGateSelectionKind.FullyQualifiedName,
                    Array.Empty<string>()))),
            repository.Root,
            TestGateMode.Task,
            "20261016-001",
            "TESTGATE-T03"));
    }

    [Fact]
    public void Select_returns_only_exact_task_or_final_gates()
    {
        using var repository = new TestRepository();
        var manifest = Manifest(
            Gate("task-first"),
            Gate("other-task", taskId: "OTHER"),
            Gate("final-first", mode: TestGateMode.Final),
            Gate("task-second"));

        var selection = TestGateManifestValidator.Select(
            manifest,
            repository.Root,
            TestGateMode.Task,
            "20261016-001",
            "TESTGATE-T03");

        Assert.Equal(
            new[] { "task-first", "task-second" },
            System.Linq.Enumerable.Select(selection.Gates, gate => gate.GateId));
        Assert.All(selection.Gates, gate => Assert.Equal("TESTGATE-T03", gate.TaskId));
    }

    [Fact]
    public void Final_selection_requires_one_unfiltered_exclusive_dual_tfm_SQL_acceptance_gate()
    {
        using var repository = new TestRepository();
        var unitOnly = Gate("final-unit", mode: TestGateMode.Final);
        var mandatorySql = Gate(
            "final-sql",
            mode: TestGateMode.Final,
            project: "sql-tests.csproj",
            frameworks: new[] { "net48", "net8.0" }) with
        {
            StrictMode = TestGateStrictMode.SqlPrAcceptance,
            Resources = new TestGateResources(
                TestGateResourceRequirement.RequiredExclusive,
                TestGateResourceRequirement.None),
            Evidence = new[]
            {
                TestGateEvidenceKind.Source,
                TestGateEvidenceKind.Discovery,
                TestGateEvidenceKind.Trx,
                TestGateEvidenceKind.SanitizedReport,
                TestGateEvidenceKind.Ownership,
            },
        };

        Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
            Manifest(unitOnly), repository.Root, TestGateMode.Final, "20261016-001", "TESTGATE-T03"));

        var selection = TestGateManifestValidator.Select(
            Manifest(unitOnly, mandatorySql),
            repository.Root,
            TestGateMode.Final,
            "20261016-001",
            "TESTGATE-T03");

        Assert.Equal(2, selection.Gates.Count);
    }

    [Theory]
    [InlineData(TestGateResourceRequirement.RequiredExclusive, TestGateResourceRequirement.None)]
    [InlineData(TestGateResourceRequirement.None, TestGateResourceRequirement.RequiredExclusive)]
    public void Final_rejects_DotnetTest_gates_with_unguarded_resources(
        TestGateResourceRequirement sql,
        TestGateResourceRequirement gui)
    {
        using var repository = new TestRepository();
        var resourceGate = Gate("resource-bearing-final", mode: TestGateMode.Final) with
        {
            Resources = new TestGateResources(sql, gui),
        };
        var mandatorySql = Gate(
            "final-sql",
            mode: TestGateMode.Final,
            project: "sql-tests.csproj",
            frameworks: new[] { "net48", "net8.0" }) with
        {
            StrictMode = TestGateStrictMode.SqlPrAcceptance,
            Resources = new TestGateResources(
                TestGateResourceRequirement.RequiredExclusive,
                TestGateResourceRequirement.None),
            Evidence = new[]
            {
                TestGateEvidenceKind.Source,
                TestGateEvidenceKind.Discovery,
                TestGateEvidenceKind.Trx,
                TestGateEvidenceKind.SanitizedReport,
                TestGateEvidenceKind.Ownership,
            },
        };

        Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(
            Manifest(resourceGate, mandatorySql),
            repository.Root,
            TestGateMode.Final,
            "20261016-001",
            "TESTGATE-T03"));
    }

    private static TestGateManifest Manifest(params TestGateDefinition[] gates) => new TestGateManifest(
        1,
        gates,
        Array.Empty<LogicalCaseDefinition>(),
        Array.Empty<IdentityMappingDefinition>(),
        Array.Empty<TestGateExemptionDefinition>());

    private static TestGateDefinition Gate(
        string gateId,
        string taskId = "TESTGATE-T03",
        TestGateMode mode = TestGateMode.Task,
        string project = "tests.csproj",
        IReadOnlyList<string>? frameworks = null,
        TestGateSelectionDefinition? selection = null) => new TestGateDefinition(
            gateId,
            "20261016-001",
            taskId,
            mode,
            project,
            frameworks ?? new[] { "net8.0" },
            "TestInfrastructure",
            new[] { "NonSql" },
            selection ?? new TestGateSelectionDefinition(
                TestGateSelectionKind.UnfilteredProject,
                Array.Empty<string>()),
            Array.Empty<TestGateBuildPrerequisite>(),
            new TestGateResources(
                TestGateResourceRequirement.None,
                TestGateResourceRequirement.None),
            TestGateStrictMode.DotnetTest,
            30,
            60,
            new[]
            {
                TestGateEvidenceKind.Source,
                TestGateEvidenceKind.Discovery,
                TestGateEvidenceKind.Trx,
            });

    private const string GateJson = """
    {
      "gateId": "runner-task",
      "planId": "20261016-001",
      "taskId": "TESTGATE-T03",
      "mode": "Task",
      "project": "tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj",
      "frameworks": [ "net8.0" ],
      "module": "TestInfrastructure",
      "classification": [ "NonSql" ],
      "selection": { "kind": "UnfilteredProject", "patterns": [] },
      "buildPrerequisites": [],
      "resources": { "sql": "None", "gui": "None" },
      "strictMode": "DotnetTest",
      "commandTimeoutSeconds": 1800,
      "overallTimeoutSeconds": 3600,
      "evidence": [ "Source", "Discovery", "Trx" ]
    }
""";

    private const string LogicalCaseJson = """
    {
      "logicalCaseId": "runner-case",
      "ownerProject": "tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj",
      "frameworks": [ "net8.0" ],
      "normalizedMethodSignature": "Example.Tests.Case()",
      "reviewedCodeFingerprint": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "classification": [ "NonSql" ],
      "rows": [ { "rowKey": "fact", "locatorSha256": null } ]
    }
""";

    private static readonly string ValidJson = """
{
  "schemaVersion": 1,
  "gates": [
""" + GateJson + """

  ],
  "logicalCases": [
""" + LogicalCaseJson + """

  ],
  "identityMappings": [],
  "exemptions": []
}
""";

    private sealed class ManifestFiles : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "MyDmsVn.TestGateManifestTests",
            Guid.NewGuid().ToString("N"));

        public string Write(string json)
        {
            Directory.CreateDirectory(_root);
            var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, json);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
    }

    private sealed class TestRepository : IDisposable
    {
        public const string Net8Project = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
""";

        public TestRepository()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "MyDmsVn.TestGateManifestTests.Repository",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "tests.csproj"), Net8Project);
            File.WriteAllText(
                Path.Combine(Root, "sql-tests.csproj"),
                Net8Project.Replace("<TargetFramework>net8.0</TargetFramework>", "<TargetFrameworks>net48;net8.0</TargetFrameworks>", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "sql-net48.csproj"),
                Net8Project.Replace("net8.0", "net48", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "desktop-net48.csproj"),
                Net8Project.Replace("net8.0", "net48", StringComparison.Ordinal));
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
