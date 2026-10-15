using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MyDmsVn.SqlTestRunner.Inventory;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Inventory;

public sealed class SqlTestInventoryTests
{
    [Fact]
    public void Discovery_parser_preserves_rows_without_exposing_parameter_values()
    {
        var text = "The following Tests are available:\n" +
            "    Example.Tests.Fact\n" +
            "    Example.Tests.Theory(value: secret-a)\n" +
            "    Example.Tests.Theory(value: secret-b)\n";

        var cases = VstestDiscoveryParser.Parse(text, "net8.0");

        Assert.Equal(3, cases.Count);
        Assert.Equal(3, cases.Select(item => item.StableCaseId).Distinct().Count());
        Assert.All(cases, item => Assert.DoesNotContain("secret", item.StableCaseId, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Example.*")]
    [InlineData("Example.Tests.Method(value: 1)")]
    public void Exemptions_reject_wildcards_and_row_values(string method)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            NonSqlExemptionManifest.ValidateMethodName(method));

        Assert.Contains("exact method", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stale_fingerprint_is_not_an_exemption()
    {
        var exemption = new NonSqlExemption("Example.Tests.Method", "old", "pure assertion", "reviewed");
        var result = NonSqlExemptionManifest.Match(
            exemption,
            new TestMethodDescriptor("Example.Tests.Method", IsTheory: false, "new"));

        Assert.False(result.IsMatch);
        Assert.Equal(NonSqlExemptionFailure.StaleFingerprint, result.Failure);
    }

    [Fact]
    public void Either_reviewed_tfm_fingerprint_matches_the_same_method_exemption()
    {
        var exemption = new NonSqlExemption(
            "Example.Tests.Method",
            "net48-hash|net8-hash",
            "pure assertion",
            "reviewed on both TFMs");

        var net48 = NonSqlExemptionManifest.Match(
            exemption,
            new TestMethodDescriptor("Example.Tests.Method", IsTheory: false, "net48-hash"));
        var net8 = NonSqlExemptionManifest.Match(
            exemption,
            new TestMethodDescriptor("Example.Tests.Method", IsTheory: false, "net8-hash"));

        Assert.True(net48.IsMatch);
        Assert.True(net8.IsMatch);
    }

    [Fact]
    public void Assembly_inventory_finds_xunit_methods_and_hashes_their_bodies()
    {
        var methods = TestMethodInventory.Read(typeof(SqlTestInventoryTests).Assembly.Location);

        var current = Assert.Single(methods, method =>
            method.FullyQualifiedName.EndsWith(
                ".Assembly_inventory_finds_xunit_methods_and_hashes_their_bodies",
                StringComparison.Ordinal));
        Assert.False(current.IsTheory);
        Assert.Equal(64, current.BodyFingerprint.Length);
    }

    [Fact]
    public void Reviewed_code_fingerprint_changes_for_async_state_machine_or_helper_source_changes()
    {
        var original = TestMethodInventory.FingerprintReviewedCode(
            "Example.Tests.AsyncMethod",
            new Dictionary<string, byte[]>
            {
                ["tests/Example.Tests/AsyncTest.cs"] = new byte[] { 1, 2, 3 },
                ["tests/MyDmsVn.SqlTestHarness/Helper.cs"] = new byte[] { 4, 5, 6 },
            });
        var asyncBodyChanged = TestMethodInventory.FingerprintReviewedCode(
            "Example.Tests.AsyncMethod",
            new Dictionary<string, byte[]>
            {
                ["tests/Example.Tests/AsyncTest.cs"] = new byte[] { 1, 2, 4 },
                ["tests/MyDmsVn.SqlTestHarness/Helper.cs"] = new byte[] { 4, 5, 6 },
            });
        var helperChanged = TestMethodInventory.FingerprintReviewedCode(
            "Example.Tests.AsyncMethod",
            new Dictionary<string, byte[]>
            {
                ["tests/Example.Tests/AsyncTest.cs"] = new byte[] { 1, 2, 3 },
                ["tests/MyDmsVn.SqlTestHarness/Helper.cs"] = new byte[] { 4, 5, 7 },
            });

        Assert.NotEqual(original, asyncBodyChanged);
        Assert.NotEqual(original, helperChanged);
    }

    [Fact]
    public void Integration_inventory_includes_every_derived_sql_server_fact()
    {
        var assemblyPath = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "MyDmsVn.Server.Infrastructure.IntegrationTests",
            "bin",
            "Release",
            "net8.0",
            "MyDmsVn.Server.Infrastructure.IntegrationTests.dll");
        var actual = TestMethodInventory.Read(assemblyPath)
            .Select(method => method.FullyQualifiedName)
            .ToHashSet(StringComparer.Ordinal);
        var assembly = Assembly.LoadFrom(assemblyPath);
        var expected = assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.CustomAttributes.Any(attribute =>
                attribute.AttributeType.Name == "SqlServerFactAttribute"))
            .Select(method => method.DeclaringType!.FullName + "." + method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(expected);
        Assert.DoesNotContain(expected, method => !actual.Contains(method));
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
