using System;
using System.Linq;
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
}
