using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
    public void Unresolved_dependency_cannot_be_approved_by_a_matching_fingerprint()
    {
        var exemption = new NonSqlExemption("Example.Tests.Method", "same", "pure", "reviewed");
        var method = new TestMethodDescriptor(
            "Example.Tests.Method",
            IsTheory: false,
            "same",
            Array.Empty<string>(),
            IsBodyFingerprintComplete: false);

        var result = NonSqlExemptionManifest.Match(exemption, method);

        Assert.False(result.IsMatch);
        Assert.Equal(NonSqlExemptionFailure.UnresolvedDependency, result.Failure);
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
    public void Reviewed_code_fingerprint_changes_only_for_the_method_or_reached_helpers()
    {
        var method = typeof(FingerprintRootFixture).GetMethod(
            nameof(FingerprintRootFixture.AsyncRoot),
            BindingFlags.Public | BindingFlags.Static)!;
        var original = TestMethodInventory.FingerprintReviewedCode(
            method,
            WithProjectFiles(new Dictionary<string, byte[]>
            {
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                    "internal static class FingerprintRootFixture { /* root-v1 */ }"),
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                    "internal static class FingerprintHelperFixture { /* helper-v1 */ }"),
                ["src/Unrelated/UnrelatedFixture.cs"] = Encoding.UTF8.GetBytes(
                    "namespace Totally.Unrelated; internal static class UnrelatedFixture { /* unrelated-v1 */ }"),
            }));
        var asyncBodyChanged = TestMethodInventory.FingerprintReviewedCode(
            method,
            WithProjectFiles(new Dictionary<string, byte[]>
            {
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                    "internal static class FingerprintRootFixture { /* root-v2 */ }"),
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                    "internal static class FingerprintHelperFixture { /* helper-v1 */ }"),
                ["src/Unrelated/UnrelatedFixture.cs"] = Encoding.UTF8.GetBytes(
                    "namespace Totally.Unrelated; internal static class UnrelatedFixture { /* unrelated-v1 */ }"),
            }));
        var helperChanged = TestMethodInventory.FingerprintReviewedCode(
            method,
            WithProjectFiles(new Dictionary<string, byte[]>
            {
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                    "internal static class FingerprintRootFixture { /* root-v1 */ }"),
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                    "internal static class FingerprintHelperFixture { /* helper-v2 */ }"),
                ["src/Unrelated/UnrelatedFixture.cs"] = Encoding.UTF8.GetBytes(
                    "namespace Totally.Unrelated; internal static class UnrelatedFixture { /* unrelated-v1 */ }"),
            }));
        var unrelatedChanged = TestMethodInventory.FingerprintReviewedCode(
            method,
            WithProjectFiles(new Dictionary<string, byte[]>
            {
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                    "internal static class FingerprintRootFixture { /* root-v1 */ }"),
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                    "internal static class FingerprintHelperFixture { /* helper-v1 */ }"),
                ["src/Unrelated/UnrelatedFixture.cs"] = Encoding.UTF8.GetBytes(
                    "namespace Totally.Unrelated; internal static class UnrelatedFixture { /* unrelated-v2 */ }"),
            }));
        var unrelatedSameNameChanged = TestMethodInventory.FingerprintReviewedCode(
            method,
            WithProjectFiles(new Dictionary<string, byte[]>
            {
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                    "internal static class FingerprintRootFixture { /* root-v1 */ }"),
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                    "internal static class FingerprintHelperFixture { /* helper-v1 */ }"),
                ["src/Unrelated/Scratch.cs"] = Encoding.UTF8.GetBytes(
                    "namespace Totally.Unrelated; internal static class FingerprintHelperFixture { /* unrelated-v2 */ }"),
            }));
        var commentAndLiteralChanged = TestMethodInventory.FingerprintReviewedCode(
            method,
            WithProjectFiles(new Dictionary<string, byte[]>
            {
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                    "internal static class FingerprintRootFixture { /* root-v1 */ }"),
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                    "internal static class FingerprintHelperFixture { /* helper-v1 */ }"),
                ["src/Duplicate/Duplicate.csproj"] = Encoding.UTF8.GetBytes(
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
                    "<AssemblyName>MyDmsVn.SqlTestRunner.Tests</AssemblyName>" +
                    "</PropertyGroup></Project>"),
                ["src/Duplicate/Scratch.cs"] = Source(
                    "internal static class Other { const string Text = \"class FingerprintHelperFixture\"; } " +
                    "// class FingerprintHelperFixture"),
            }));

        Assert.NotEqual(original, asyncBodyChanged);
        Assert.NotEqual(original, helperChanged);
        Assert.Equal(original, unrelatedChanged);
        Assert.Equal(original, unrelatedSameNameChanged);
        Assert.Equal(original, commentAndLiteralChanged);
    }

    [Fact]
    public void Reviewed_code_fingerprint_maps_a_reached_type_declared_in_a_differently_named_file()
    {
        var method = typeof(FingerprintRootFixture).GetMethod(
            nameof(FingerprintRootFixture.AsyncRoot),
            BindingFlags.Public | BindingFlags.Static)!;
        var original = TestMethodInventory.FingerprintReviewedCode(
            method,
            WithProjectFiles(new Dictionary<string, byte[]>
            {
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                    "internal static class FingerprintRootFixture { }"),
                ["tests/MyDmsVn.SqlTestRunner.Tests/UnexpectedName.cs"] = Source(
                    "internal static class FingerprintHelperFixture { /* v1 */ }"),
            }));
        var helperChanged = TestMethodInventory.FingerprintReviewedCode(
            method,
            WithProjectFiles(new Dictionary<string, byte[]>
            {
                ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                    "internal static class FingerprintRootFixture { }"),
                ["tests/MyDmsVn.SqlTestRunner.Tests/UnexpectedName.cs"] = Source(
                    "internal static class FingerprintHelperFixture { /* v2 */ }"),
            }));

        Assert.NotEqual(original, helperChanged);
    }

    [Fact]
    public void Reviewed_code_fingerprint_includes_reached_fields_and_inlined_constants()
    {
        var method = typeof(FingerprintRootFixture).GetMethod(
            nameof(FingerprintRootFixture.AsyncRoot),
            BindingFlags.Public | BindingFlags.Static)!;
        var original = TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintDependencyFiles("field-v1", 17));
        var fieldChanged = TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintDependencyFiles("field-v2", 17));
        var constantChanged = TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintDependencyFiles("field-v1", 18));
        var unrelatedSameNameConstantChanged = TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintDependencyFiles("field-v1", 17, unrelatedConstantValue: 99));

        Assert.True(TestMethodInventory.IsReviewedCodeFingerprintComplete(
            method,
            FingerprintDependencyFiles("field-v1", 17)));
        Assert.NotEqual(original, fieldChanged);
        Assert.NotEqual(original, constantChanged);
        Assert.Equal(original, unrelatedSameNameConstantChanged);
    }

    [Fact]
    public void Reviewed_code_fingerprint_resolves_inherited_constant_binding()
    {
        var method = typeof(FingerprintRootFixture).GetMethod(
            nameof(FingerprintRootFixture.AsyncRoot),
            BindingFlags.Public | BindingFlags.Static)!;
        var original = TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintInheritedConstantFiles(17));
        var constantChanged = TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintInheritedConstantFiles(18));

        Assert.True(TestMethodInventory.IsReviewedCodeFingerprintComplete(
            method,
            FingerprintInheritedConstantFiles(17)));
        Assert.NotEqual(original, constantChanged);
    }

    [Fact]
    public void Reviewed_code_fingerprint_tracks_enum_members()
    {
        var method = typeof(FingerprintRootFixture).GetMethod(
            nameof(FingerprintRootFixture.AsyncRoot),
            BindingFlags.Public | BindingFlags.Static)!;

        Assert.NotEqual(
            TestMethodInventory.FingerprintReviewedCode(method, FingerprintEnumFiles(1)),
            TestMethodInventory.FingerprintReviewedCode(method, FingerprintEnumFiles(2)));
    }

    [Fact]
    public void Reviewed_code_fingerprint_resolves_qualified_inheritance_without_namespace_collision()
    {
        var method = typeof(FingerprintRootFixture).GetMethod(
            nameof(FingerprintRootFixture.AsyncRoot),
            BindingFlags.Public | BindingFlags.Static)!;
        var original = TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintQualifiedInheritanceFiles(17, 99));

        Assert.NotEqual(original, TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintQualifiedInheritanceFiles(18, 99)));
        Assert.Equal(original, TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintQualifiedInheritanceFiles(17, 100)));
    }

    [Fact]
    public void Reviewed_code_fingerprint_resolves_imported_inheritance_with_namespace_collision()
    {
        var method = typeof(FingerprintRootFixture).GetMethod(
            nameof(FingerprintRootFixture.AsyncRoot),
            BindingFlags.Public | BindingFlags.Static)!;
        var original = TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintImportedInheritanceFiles(17, 99));

        Assert.NotEqual(original, TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintImportedInheritanceFiles(18, 99)));
        Assert.Equal(original, TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintImportedInheritanceFiles(17, 100)));
    }

    [Fact]
    public void Reviewed_code_fingerprint_tracks_nested_constants_and_attributed_enum_members()
    {
        var method = typeof(FingerprintRootFixture).GetMethod(
            nameof(FingerprintRootFixture.AsyncRoot),
            BindingFlags.Public | BindingFlags.Static)!;
        var original = TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintNestedConstantFiles(17, 1));

        Assert.NotEqual(original, TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintNestedConstantFiles(18, 1)));
        Assert.NotEqual(original, TestMethodInventory.FingerprintReviewedCode(
            method,
            FingerprintNestedConstantFiles(17, 2)));
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

    private static byte[] Source(string declaration) => Encoding.UTF8.GetBytes(
        "namespace MyDmsVn.SqlTestRunner.Tests.Inventory; " + declaration);

    private static IReadOnlyDictionary<string, byte[]> WithProjectFiles(
        Dictionary<string, byte[]> files)
    {
        files["tests/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj"] =
            Encoding.UTF8.GetBytes("<Project Sdk=\"Microsoft.NET.Sdk\" />");
        files["src/Unrelated/Unrelated.csproj"] = Encoding.UTF8.GetBytes(
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<AssemblyName>Totally.Unrelated</AssemblyName>" +
            "</PropertyGroup></Project>");
        return files;
    }

    private static IReadOnlyDictionary<string, byte[]> FingerprintDependencyFiles(
        string fieldMarker,
        int constantValue,
        int? unrelatedConstantValue = null)
    {
        var files = new Dictionary<string, byte[]>
        {
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                "internal static class FingerprintRootFixture { void Root() { " +
                "FingerprintHelperFixture.Helper(FingerprintDependencyFixture.Value, " +
                "FingerprintConstantFixture.Constant); } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                "internal static class FingerprintHelperFixture { void Helper(int first, int second) { } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintDependencyFixture.cs"] = Source(
                "internal static class FingerprintDependencyFixture { " +
                "internal static readonly int Value = 1; /* " + fieldMarker + " */ }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintConstants.cs"] = Source(
                "internal static class OtherConstants { internal const int Ignored = 0; } " +
                "internal static class FingerprintConstantFixture { " +
                "internal const int Unused = 0, Constant = " + constantValue + "; }"),
        };
        if (unrelatedConstantValue.HasValue)
        {
            files["src/Unrelated/Scratch.cs"] = Encoding.UTF8.GetBytes(
                "namespace Totally.Unrelated; internal static class FingerprintConstantFixture { " +
                "internal const int Constant = " + unrelatedConstantValue.Value + "; }");
        }

        return WithProjectFiles(files);
    }

    private static IReadOnlyDictionary<string, byte[]> FingerprintInheritedConstantFiles(int constantValue) =>
        WithProjectFiles(new Dictionary<string, byte[]>
        {
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                "internal static class FingerprintRootFixture { void Root() { " +
                "FingerprintHelperFixture.Helper(FingerprintDependencyFixture.Value, " +
                "DerivedConstants.Constant); } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                "internal static class FingerprintHelperFixture { void Helper(int first, int second) { } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintDependencyFixture.cs"] = Source(
                "internal static class FingerprintDependencyFixture { internal static readonly int Value = 1; }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintConstants.cs"] = Source(
                "internal class BaseConstants { internal const int Constant = " + constantValue + "; } " +
                "internal class DerivedConstants : BaseConstants { }"),
        });

    private static IReadOnlyDictionary<string, byte[]> FingerprintEnumFiles(int activeValue) =>
        WithProjectFiles(new Dictionary<string, byte[]>
        {
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                "internal static class FingerprintRootFixture { void Root() { " +
                "FingerprintHelperFixture.Helper(FingerprintDependencyFixture.Value, (int)FingerprintStatus.Active); } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                "internal static class FingerprintHelperFixture { void Helper(int first, int second) { } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintDependencyFixture.cs"] = Source(
                "internal static class FingerprintDependencyFixture { internal static readonly int Value = 1; }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintStatus.cs"] = Source(
                "internal enum FingerprintStatus { None = 0, Active = " + activeValue + " }"),
        });

    private static IReadOnlyDictionary<string, byte[]> FingerprintQualifiedInheritanceFiles(
        int reachedValue,
        int unrelatedValue) => WithProjectFiles(new Dictionary<string, byte[]>
        {
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                "internal static class FingerprintRootFixture { void Root() { " +
                "FingerprintHelperFixture.Helper(FingerprintDependencyFixture.Value, N1.DerivedConstants.Constant); } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                "internal static class FingerprintHelperFixture { void Helper(int first, int second) { } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintDependencyFixture.cs"] = Source(
                "internal static class FingerprintDependencyFixture { internal static readonly int Value = 1; }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/N1Constants.cs"] = Encoding.UTF8.GetBytes(
                "namespace N1; internal class BaseConstants { internal const int Constant = " + reachedValue + "; } " +
                "internal class DerivedConstants : BaseConstants { }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/N2Constants.cs"] = Encoding.UTF8.GetBytes(
                "namespace N2; internal class DerivedConstants { internal const int Constant = " + unrelatedValue + "; }"),
        });

    private static IReadOnlyDictionary<string, byte[]> FingerprintImportedInheritanceFiles(
        int reachedValue,
        int unrelatedValue) => WithProjectFiles(new Dictionary<string, byte[]>
        {
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Encoding.UTF8.GetBytes(
                "using N1; namespace MyDmsVn.SqlTestRunner.Tests.Inventory; " +
                "internal static class FingerprintRootFixture { void Root() { " +
                "FingerprintHelperFixture.Helper(FingerprintDependencyFixture.Value, DerivedConstants.Constant); } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                "internal static class FingerprintHelperFixture { void Helper(int first, int second) { } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintDependencyFixture.cs"] = Source(
                "internal static class FingerprintDependencyFixture { internal static readonly int Value = 1; }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/N1Constants.cs"] = Encoding.UTF8.GetBytes(
                "namespace N1; internal class BaseConstants { internal const int Constant = " + reachedValue + "; } " +
                "internal class DerivedConstants : BaseConstants { }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/N2Constants.cs"] = Encoding.UTF8.GetBytes(
                "namespace N2; internal class DerivedConstants { internal const int Constant = " + unrelatedValue + "; }"),
        });

    private static IReadOnlyDictionary<string, byte[]> FingerprintNestedConstantFiles(
        int constantValue,
        int enumValue) => WithProjectFiles(new Dictionary<string, byte[]>
        {
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintRootFixture.cs"] = Source(
                "internal static class FingerprintRootFixture { void Root() { " +
                "FingerprintHelperFixture.Helper(Outer.Values.Constant, (int)Outer.Status.Active); } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/FingerprintHelperFixture.cs"] = Source(
                "internal static class FingerprintHelperFixture { void Helper(int first, int second) { } }"),
            ["tests/MyDmsVn.SqlTestRunner.Tests/NestedConstants.cs"] = Source(
                "internal static class Outer { internal static class Values { " +
                "internal const int Constant = " + constantValue + "; } " +
                "internal enum Status { None = 0, [System.Obsolete] Active = " + enumValue + " } }"),
        });

}

internal static class FingerprintRootFixture
{
    public static async Task AsyncRoot()
    {
        await Task.Yield();
        FingerprintHelperFixture.Helper(
            FingerprintDependencyFixture.Value,
            FingerprintConstantFixture.Constant);
        FingerprintHelperFixture.Helper(
            FingerprintDependencyFixture.Value,
            (int)FingerprintStatus.Active);
    }
}

internal static class FingerprintHelperFixture
{
    public static void Helper(int first, int second)
    {
    }
}

internal static class FingerprintDependencyFixture
{
    internal static readonly int Value = 1;
}

internal static class FingerprintConstantFixture
{
    internal const int Unused = 0, Constant = 17;
}

internal enum FingerprintStatus
{
    None = 0,
    Active = 1,
}
