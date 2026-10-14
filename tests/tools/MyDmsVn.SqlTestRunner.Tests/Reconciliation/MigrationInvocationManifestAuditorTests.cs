using System;
using System.IO;
using System.Linq;
using MyDmsVn.SqlTestRunner.Reconciliation;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Reconciliation;

public sealed class MigrationInvocationManifestAuditorTests
{
    [Fact]
    public void Reviewed_repository_manifest_passes_for_both_tfms()
    {
        var root = FindRepositoryRoot();
        var manifest = Path.Combine(root, "tests", "tools", "sqlperf-t01-migration-invocations.json");

        Assert.True(MigrationInvocationManifestAuditor.IsValid(root, manifest, "net48"));
        Assert.True(MigrationInvocationManifestAuditor.IsValid(root, manifest, "net8.0"));
    }

    [Fact]
    public void Audit_rejects_an_uninstrumented_direct_dbup_call()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ManifestAudit", Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(root, "tests", "ExampleTests.cs");
        var manifestPath = Path.Combine(root, "manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        try
        {
            File.WriteAllText(
                manifestPath,
                "[{\"invocationId\":\"ExampleTests.Case.direct-first\",\"class\":\"ExampleTests\"," +
                "\"method\":\"Case\",\"sourceFile\":\"tests/ExampleTests.cs\"," +
                "\"tfms\":[\"net8.0\"],\"expectedInvocationRule\":\"once-per-test-execution\"}]");
            File.WriteAllText(
                sourcePath,
                "class ExampleTests { void Case() { Use(\"ExampleTests.Case.direct-first\"); builder.PerformUpgrade(); } }");

            Assert.True(MigrationInvocationManifestAuditor.IsValid(root, manifestPath, "net8.0"));

            File.AppendAllText(sourcePath, "// deliberately uninstrumented\nbuilder.PerformUpgrade();");
            Assert.False(MigrationInvocationManifestAuditor.IsValid(root, manifestPath, "net8.0"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Caller_rule_counts_each_helper_call_per_test_method()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn.ManifestCallers", Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(root, "tests", "ExampleTests.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        try
        {
            File.WriteAllText(
                sourcePath,
                "public class ExampleTests\n{\n" +
                "    public async Task First() { await CreateDatabaseAsync(); }\n" +
                "    public async Task Second() { await CreateDatabaseAsync(); await CreateDatabaseAsync(); }\n" +
                "    private Task CreateDatabaseAsync() => Task.CompletedTask;\n}");
            var entry = new MigrationInvocationManifestEntry
            {
                Method = "CreateDatabaseAsync",
                SourceFile = "tests/ExampleTests.cs",
                ExpectedInvocationRule = "once-per-caller-execution",
            };

            var expectations = MigrationInvocationManifest.ResolveExpectations(entry, root);

            Assert.Equal(1, expectations.Single(item => item.TestMethod == "First").Count);
            Assert.Equal(2, expectations.Single(item => item.TestMethod == "Second").Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
