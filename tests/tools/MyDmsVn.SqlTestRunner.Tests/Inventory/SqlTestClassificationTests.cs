using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyDmsVn.SqlTestRunner.Inventory;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Inventory;

public sealed class SqlTestClassificationTests
{
    [Fact]
    public void Sql_method_without_a_valid_category_is_incomplete()
    {
        var methods = new[]
        {
            Method("Example.Tests.Missing", Array.Empty<string>()),
        };

        var result = SqlTestClassificationVerifier.Verify(methods, Array.Empty<NonSqlExemption>());

        Assert.False(result.IsComplete);
        Assert.Contains(SqlTestClassificationFailure.MissingSqlCategory, result.Failures);
    }

    [Fact]
    public void Non_sql_exemption_cannot_hide_a_categorized_sql_test()
    {
        var method = Method("Example.Tests.Mixed", new[] { SqlTestCategories.FastSql });
        var exemptions = new[]
        {
            new NonSqlExemption(method.FullyQualifiedName, method.BodyFingerprint, "pure", "reviewed call graph"),
        };

        var result = SqlTestClassificationVerifier.Verify(new[] { method }, exemptions);

        Assert.False(result.IsComplete);
        Assert.Contains(SqlTestClassificationFailure.ExemptMethodHasSqlCategory, result.Failures);
    }

    [Fact]
    public void Valid_categories_are_reported_once_per_method()
    {
        var methods = new[]
        {
            Method("Example.Tests.Fast", new[] { SqlTestCategories.FastSql }),
            Method("Example.Tests.Concurrent", new[]
            {
                SqlTestCategories.SqlConcurrency,
                SqlTestCategories.SecurityProvisioning,
                SqlTestCategories.SqlConcurrency,
            }),
        };

        var result = SqlTestClassificationVerifier.Verify(methods, Array.Empty<NonSqlExemption>());

        Assert.True(result.IsComplete);
        Assert.Equal(1, result.CategoryCounts[SqlTestCategories.FastSql]);
        Assert.Equal(1, result.CategoryCounts[SqlTestCategories.SqlConcurrency]);
        Assert.Equal(1, result.CategoryCounts[SqlTestCategories.SecurityProvisioning]);
        Assert.Equal(0, result.CategoryCounts[SqlTestCategories.MigrationSchema]);
    }

    [Theory]
    [InlineData("net48")]
    public void Integration_inventory_is_fully_classified_from_the_reviewed_manifest(string tfm)
    {
        var root = FindRepositoryRoot();
        var projects = new[]
        {
            "tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj",
            "tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj",
            "tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj",
            "tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj",
            "tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj",
            "tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj",
        };
        var assemblyPaths = projects.Select(project => FixtureAssemblyPath(root, project, tfm)).ToArray();
        var manifestPath = Path.Combine(root, "tests", "tools", "sql-test-nonsql-exemptions.json");

        var methodsByProject = assemblyPaths.Select(path => TestMethodInventory.Read(path)).ToArray();
        var methods = methodsByProject.SelectMany(projectMethods => projectMethods).ToArray();
        var exemptions = NonSqlExemptionManifest.Read(manifestPath);
        foreach (var projectMethods in methodsByProject)
        {
            var projectNames = projectMethods.Select(method => method.FullyQualifiedName)
                .ToHashSet(StringComparer.Ordinal);
            var projectExemptions = exemptions.Where(exemption => projectNames.Contains(exemption.MethodName)).ToArray();
            var projectResult = SqlTestClassificationVerifier.Verify(projectMethods, projectExemptions);
            Assert.True(projectResult.IsComplete, string.Join(", ", projectResult.Failures));
        }

        var result = SqlTestClassificationVerifier.Verify(methods, exemptions);
        var invalidExemptions = exemptions
            .Select(exemption => new
            {
                exemption.MethodName,
                Match = methods
                    .Where(method => method.FullyQualifiedName == exemption.MethodName)
                    .Select(method => NonSqlExemptionManifest.Match(exemption, method))
                    .SingleOrDefault(),
            })
            .Where(item => item.Match is not null && !item.Match.IsMatch)
            .Select(item => item.MethodName + ":" + item.Match!.Failure);
        var unclassifiedMethods = methods
            .Where(method => method.Categories.Count == 0 && exemptions.All(exemption =>
                !string.Equals(exemption.MethodName, method.FullyQualifiedName, StringComparison.Ordinal)))
            .Select(method => method.FullyQualifiedName);

        Assert.True(
            result.IsComplete,
            string.Join(", ", result.Failures) + Environment.NewLine +
            string.Join(Environment.NewLine, invalidExemptions) + Environment.NewLine +
            string.Join(Environment.NewLine, unclassifiedMethods));
        Assert.All(SqlTestCategories.All, category => Assert.True(result.CategoryCounts[category] > 0));
    }

    private static TestMethodDescriptor Method(string name, IReadOnlyList<string> categories) =>
        new TestMethodDescriptor(name, IsTheory: false, "fingerprint", categories);

    private static string FixtureAssemblyPath(string root, string project, string tfm)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(project);
        var isolatedOutputRoot = Environment.GetEnvironmentVariable("MYDMSVN_TESTGATE_INVENTORY_ROOT");
        var isolatedPath = string.IsNullOrWhiteSpace(isolatedOutputRoot)
            ? string.Empty
            : Path.Combine(isolatedOutputRoot, assemblyName, "Release", tfm, assemblyName + ".dll");
        if (!string.IsNullOrWhiteSpace(isolatedPath) && File.Exists(isolatedPath))
        {
            return isolatedPath;
        }

        return Path.Combine(
            root,
            Path.GetDirectoryName(project)!,
            "bin",
            "Release",
            tfm,
            assemblyName + ".dll");
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
