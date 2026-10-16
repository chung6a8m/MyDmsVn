using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Trx;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Inventory;

public sealed class LogicalCaseIdentityTests
{
    [Fact]
    public void Durable_key_ignores_physical_owner_and_classification_order()
    {
        const string signature = "Example.Tests.Case(System.Int32)";

        var keyBeforeMove = LogicalCaseIdentity.Create(
            1, "net8.0", signature, "fact", new[] { "FastSQL", "SqlConcurrency" });
        var keyAfterMove = LogicalCaseIdentity.Create(
            1, "net8.0", signature, "fact", new[] { "SqlConcurrency", "FastSQL" });

        Assert.Equal(keyBeforeMove, keyAfterMove);
        Assert.StartsWith("v1:", keyBeforeMove, StringComparison.Ordinal);
        Assert.DoesNotContain("Example.Tests", keyBeforeMove, StringComparison.Ordinal);
    }

    [Fact]
    public void Durable_key_changes_for_tfm_signature_or_opaque_row()
    {
        var net8Key = LogicalCaseIdentity.Create(
            1, "net8.0", "Example.Tests.Case(System.Int32)", "case-a", new[] { "NonSql" });

        Assert.NotEqual(net8Key, LogicalCaseIdentity.Create(
            1, "net48", "Example.Tests.Case(System.Int32)", "case-a", new[] { "NonSql" }));
        Assert.NotEqual(net8Key, LogicalCaseIdentity.Create(
            1, "net8.0", "Example.Tests.Case(System.String)", "case-a", new[] { "NonSql" }));
        Assert.NotEqual(net8Key, LogicalCaseIdentity.Create(
            1, "net8.0", "Example.Tests.Case(System.Int32)", "case-b", new[] { "NonSql" }));
    }

    [Fact]
    public void Normalized_signature_distinguishes_overloads_and_generic_arity()
    {
        var methods = typeof(SignatureFixture).GetMethods()
            .Where(method => method.DeclaringType == typeof(SignatureFixture))
            .ToArray();
        var integer = methods.Single(method =>
            method.Name == nameof(SignatureFixture.Case) &&
            method.GetParameters()[0].ParameterType == typeof(int));
        var text = methods.Single(method =>
            method.Name == nameof(SignatureFixture.Case) &&
            method.GetParameters()[0].ParameterType == typeof(string));
        var generic = methods.Single(method => method.Name == nameof(SignatureFixture.Generic));

        Assert.Equal(
            "MyDmsVn.SqlTestRunner.Tests.Inventory.SignatureFixture.Case(System.Int32)",
            LogicalCaseIdentity.NormalizeSignature(integer));
        Assert.Equal(
            "MyDmsVn.SqlTestRunner.Tests.Inventory.SignatureFixture.Case(System.String)",
            LogicalCaseIdentity.NormalizeSignature(text));
        Assert.Equal(
            "MyDmsVn.SqlTestRunner.Tests.Inventory.SignatureFixture.Generic`1(" +
            "System.Collections.Generic.IReadOnlyList<!!0>)",
            LogicalCaseIdentity.NormalizeSignature(generic));
    }

    [Fact]
    public void Physical_locator_is_deterministic_and_does_not_expose_arguments()
    {
        const string display = "Example.Tests.Theory(value: secret-argument)";

        var first = LogicalCaseIdentity.CreatePhysicalRowLocator("Example.Tests.Theory", display);
        var second = LogicalCaseIdentity.CreatePhysicalRowLocator("Example.Tests.Theory", display);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain("secret-argument", first, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bind_rejects_overloaded_discovery_name_as_ambiguous()
    {
        var locator = LogicalCaseIdentity.CreatePhysicalRowLocator(
            "Example.Tests.Case",
            "Example.Tests.Case(value: 1)");
        var expected = new[]
        {
            Definition("int", "Example.Tests.Case(System.Int32)", Row("case-int", locator)),
            Definition("text", "Example.Tests.Case(System.String)", Row("case-text", locator)),
        };

        var result = LogicalCaseBinder.BindDiscovery(
            new[] { Discovery("Example.Tests.Case", locator) },
            expected,
            Project,
            "net8.0");

        Assert.Contains(LogicalCaseBindingFailure.AmbiguousMethod, result.Failures);
        Assert.Empty(result.Cases);
    }

    [Fact]
    public void Bind_maps_reordered_theory_rows_by_opaque_locator()
    {
        var locatorA = LogicalCaseIdentity.CreatePhysicalRowLocator(
            "Example.Tests.Theory",
            "Example.Tests.Theory(value: alpha)");
        var locatorB = LogicalCaseIdentity.CreatePhysicalRowLocator(
            "Example.Tests.Theory",
            "Example.Tests.Theory(value: beta)");
        var expected = new[]
        {
            Definition(
                "theory",
                "Example.Tests.Theory(System.String)",
                Row("case-a", locatorA),
                Row("case-b", locatorB)),
        };

        var result = LogicalCaseBinder.BindDiscovery(
            new[]
            {
                Discovery("Example.Tests.Theory", locatorB, 0),
                Discovery("Example.Tests.Theory", locatorA, 1),
            },
            expected,
            Project,
            "net8.0");

        Assert.Empty(result.Failures);
        Assert.Equal(
            new[] { "case-a", "case-b" },
            result.Cases.Select(item => item.RowKey).OrderBy(value => value));
    }

    [Fact]
    public void Bind_rejects_duplicate_locator_or_row_id()
    {
        var locator = LogicalCaseIdentity.CreatePhysicalRowLocator(
            "Example.Tests.Theory",
            "Example.Tests.Theory(value: duplicate)");
        var duplicateLocator = Definition(
            "locator",
            "Example.Tests.Theory(System.String)",
            Row("case-a", locator),
            Row("case-b", locator));
        var duplicateRow = Definition(
            "row",
            "Example.Tests.OtherTheory(System.String)",
            Row("case-a", locator),
            Row("case-a", LogicalCaseIdentity.CreatePhysicalRowLocator(
                "Example.Tests.OtherTheory",
                "Example.Tests.OtherTheory(value: other)")));

        var locatorResult = LogicalCaseBinder.BindDiscovery(
            Array.Empty<DiscoveredTestCase>(),
            new[] { duplicateLocator },
            Project,
            "net8.0");
        var rowResult = LogicalCaseBinder.BindDiscovery(
            Array.Empty<DiscoveredTestCase>(),
            new[] { duplicateRow },
            Project,
            "net8.0");

        Assert.Contains(LogicalCaseBindingFailure.DuplicateRowLocator, locatorResult.Failures);
        Assert.Contains(LogicalCaseBindingFailure.DuplicateRowId, rowResult.Failures);
    }

    [Fact]
    public void Bind_rejects_missing_and_unexpected_rows()
    {
        var locatorA = LogicalCaseIdentity.CreatePhysicalRowLocator(
            "Example.Tests.Theory",
            "Example.Tests.Theory(value: alpha)");
        var locatorB = LogicalCaseIdentity.CreatePhysicalRowLocator(
            "Example.Tests.Theory",
            "Example.Tests.Theory(value: beta)");
        var unexpected = LogicalCaseIdentity.CreatePhysicalRowLocator(
            "Example.Tests.Theory",
            "Example.Tests.Theory(value: unexpected)");
        var expected = new[]
        {
            Definition(
                "theory",
                "Example.Tests.Theory(System.String)",
                Row("case-a", locatorA),
                Row("case-b", locatorB)),
        };

        var result = LogicalCaseBinder.BindDiscovery(
            new[]
            {
                Discovery("Example.Tests.Theory", locatorA, 0),
                Discovery("Example.Tests.Theory", unexpected, 1),
            },
            expected,
            Project,
            "net8.0");

        Assert.Contains(LogicalCaseBindingFailure.MissingRow, result.Failures);
        Assert.Contains(LogicalCaseBindingFailure.UnexpectedRow, result.Failures);
    }

    [Fact]
    public void Binder_does_not_return_raw_display_or_arguments()
    {
        const string display = "Example.Tests.Theory(value: secret-argument)";
        var locator = LogicalCaseIdentity.CreatePhysicalRowLocator("Example.Tests.Theory", display);

        var result = LogicalCaseBinder.BindDiscovery(
            new[] { Discovery("Example.Tests.Theory", locator) },
            new[]
            {
                Definition(
                    "theory",
                    "Example.Tests.Theory(System.String)",
                    Row("opaque-a", locator)),
            },
            Project,
            "net8.0");

        Assert.Empty(result.Failures);
        Assert.DoesNotContain(
            "secret-argument",
            JsonSerializer.Serialize(result),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Bind_source_requires_matching_fingerprint_and_classification()
    {
        var definition = new LogicalCaseDefinition(
            "fact",
            Project,
            new[] { "net8.0" },
            "Example.Tests.Fact()",
            Fingerprint,
            new[] { "NonSql" },
            new[] { new TheoryRowDefinition("fact", null) });
        var valid = new TestMethodDescriptor(
            "Example.Tests.Fact",
            IsTheory: false,
            Fingerprint)
        {
            NormalizedSignature = "Example.Tests.Fact()",
        };

        var complete = LogicalCaseBinder.BindSource(
            new[] { valid }, new[] { definition }, Project, "net8.0");
        var stale = LogicalCaseBinder.BindSource(
            new[] { valid with { BodyFingerprint = new string('f', 64) } },
            new[] { definition },
            Project,
            "net8.0");
        var matchingPartialGraph = LogicalCaseBinder.BindSource(
            new[] { valid with { IsBodyFingerprintComplete = false } },
            new[] { definition },
            Project,
            "net8.0");
        var reviewedPartialGraph = LogicalCaseBinder.BindSource(
            new[] { valid with { IsBodyFingerprintComplete = false } },
            new[] { definition },
            Project,
            "net8.0",
            new[]
            {
                new TestGateExemptionDefinition(
                    "partial-graph-example",
                    definition.NormalizedMethodSignature,
                    definition.Classification,
                    "Reviewed source is closed over the declaring-file boundary.",
                    Fingerprint),
            });

        Assert.True(complete.IsComplete);
        Assert.Single(complete.Cases);
        Assert.False(matchingPartialGraph.IsComplete);
        Assert.Contains(LogicalCaseBindingFailure.IncompleteFingerprint, matchingPartialGraph.Failures);
        Assert.True(reviewedPartialGraph.IsComplete);
        Assert.Contains(LogicalCaseBindingFailure.FingerprintMismatch, stale.Failures);
    }

    [Fact]
    public void Bind_execution_preserves_outcome_and_duration()
    {
        var locator = LogicalCaseIdentity.CreatePhysicalRowLocator(
            "Example.Tests.Theory",
            "Example.Tests.Theory(value: alpha)");
        var definition = Definition(
            "theory",
            "Example.Tests.Theory(System.String)",
            Row("case-a", locator));
        var executed = new TrxTestResult(
            "legacy-id",
            "Example.Tests.Theory",
            "net8.0",
            TestOutcome.Failed,
            TimeSpan.FromSeconds(2))
        {
            PhysicalRowLocator = locator,
        };

        var result = LogicalCaseBinder.BindExecution(
            new[] { executed }, new[] { definition }, Project, "net8.0");

        var bound = Assert.Single(result.Cases);
        Assert.Equal(TestOutcome.Failed, bound.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(2), bound.Duration);
    }

    private const string Project = "tests/Example.Tests/Example.Tests.csproj";

    private const string Fingerprint =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static LogicalCaseDefinition Definition(
        string id,
        string signature,
        params TheoryRowDefinition[] rows) => new LogicalCaseDefinition(
            id,
            Project,
            new[] { "net8.0" },
            signature,
            Fingerprint,
            new[] { "NonSql" },
            rows);

    private static TheoryRowDefinition Row(string key, string locator) => new TheoryRowDefinition(
        key,
        locator);

    private static DiscoveredTestCase Discovery(string method, string locator, int ordinal = 0) =>
        VstestDiscoveryParser.CreateCase(method, "net8.0", ordinal) with
        {
            PhysicalRowLocator = locator,
        };

}

internal sealed class SignatureFixture
{
    public void Case(int value)
    {
    }

    public void Case(string value)
    {
    }

    public void Generic<T>(IReadOnlyList<T> values)
    {
    }
}
