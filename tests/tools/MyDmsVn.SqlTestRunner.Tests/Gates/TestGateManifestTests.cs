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
