using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Trx;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class LogicalCaseBinder
{
    private static readonly Regex MethodGenericArity = new Regex(
        "`[0-9]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static LogicalCaseBindingResult BindSource(
        IReadOnlyCollection<TestMethodDescriptor> source,
        IReadOnlyCollection<LogicalCaseDefinition> expected,
        string ownerProject,
        string tfm,
        IReadOnlyCollection<TestGateExemptionDefinition>? exemptions = null,
        IReadOnlyCollection<string>? fingerprintRequiredSignatures = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var context = Prepare(expected, ownerProject, tfm);
        var failures = new HashSet<LogicalCaseBindingFailure>(context.Failures);
        var cases = new List<BoundLogicalCase>();
        foreach (var definition in context.Definitions)
        {
            var matches = source.Where(item => string.Equals(
                    item.NormalizedSignature,
                    definition.NormalizedMethodSignature,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                failures.Add(LogicalCaseBindingFailure.MissingMethod);
                continue;
            }

            if (matches.Length != 1)
            {
                failures.Add(LogicalCaseBindingFailure.AmbiguousMethod);
                continue;
            }

            var match = matches[0];
            // A focused Task gate still checks all project method identities,
            // classifications and row shapes. Fingerprint drift outside its
            // reviewed selection is deferred to the complete Final gate.
            var requireFingerprint = fingerprintRequiredSignatures is null ||
                fingerprintRequiredSignatures.Contains(
                    definition.NormalizedMethodSignature, StringComparer.Ordinal);
            if (requireFingerprint && !match.IsBodyFingerprintComplete && !HasReviewedIncompleteFingerprintExemption(
                    match,
                    definition,
                    exemptions ?? Array.Empty<TestGateExemptionDefinition>()))
            {
                failures.Add(LogicalCaseBindingFailure.IncompleteFingerprint);
            }

            if (requireFingerprint && !string.Equals(
                    match.BodyFingerprint,
                    definition.ReviewedCodeFingerprint,
                    StringComparison.Ordinal))
            {
                failures.Add(LogicalCaseBindingFailure.FingerprintMismatch);
            }

            var sourceClassification = match.Categories.Count == 0
                ? new[] { "NonSql" }
                : match.Categories.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!sourceClassification.SequenceEqual(
                    definition.Classification.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
            {
                failures.Add(LogicalCaseBindingFailure.ClassificationMismatch);
            }

            var isFact = IsFact(definition);
            if (match.IsTheory == isFact)
            {
                failures.Add(LogicalCaseBindingFailure.RowShapeMismatch);
                continue;
            }

            cases.AddRange(Expand(definition, tfm));
        }

        if (source.Any(method => !context.Definitions.Any(definition => string.Equals(
                definition.NormalizedMethodSignature,
                method.NormalizedSignature,
                StringComparison.Ordinal))))
        {
            failures.Add(LogicalCaseBindingFailure.UnexpectedMethod);
        }

        return Result(cases, failures);
    }

    private static bool HasReviewedIncompleteFingerprintExemption(
        TestMethodDescriptor method,
        LogicalCaseDefinition definition,
        IReadOnlyCollection<TestGateExemptionDefinition> exemptions) => exemptions.Any(exemption =>
            !string.IsNullOrWhiteSpace(exemption.Rationale) &&
            string.Equals(exemption.NormalizedMethodSignature, definition.NormalizedMethodSignature, StringComparison.Ordinal) &&
            string.Equals(exemption.ReviewedCodeFingerprint, method.BodyFingerprint, StringComparison.Ordinal) &&
            new HashSet<string>(exemption.Classification, StringComparer.Ordinal).SetEquals(definition.Classification));

    public static LogicalCaseBindingResult BindDiscovery(
        IReadOnlyCollection<DiscoveredTestCase> discovered,
        IReadOnlyCollection<LogicalCaseDefinition> expected,
        string ownerProject,
        string tfm)
    {
        ArgumentNullException.ThrowIfNull(discovered);
        var physical = discovered.Select(item => new PhysicalCase(
                item.MethodName,
                item.Tfm,
                item.PhysicalRowLocator,
                null,
                TimeSpan.Zero))
            .ToArray();
        var result = BindPhysical(physical, expected, ownerProject, tfm);
        return Result(result.Cases, result.Failures);
    }

    public static LogicalExecutionBindingResult BindExecution(
        IReadOnlyCollection<TrxTestResult> executed,
        IReadOnlyCollection<LogicalCaseDefinition> expected,
        string ownerProject,
        string tfm)
    {
        ArgumentNullException.ThrowIfNull(executed);
        var physical = executed.Select(item => new PhysicalCase(
                item.MethodName,
                item.Tfm,
                item.PhysicalRowLocator,
                item.Outcome,
                item.Duration))
            .ToArray();
        var result = BindPhysical(physical, expected, ownerProject, tfm);
        var byKey = result.Cases.ToDictionary(item => item.LogicalKey, StringComparer.Ordinal);
        var cases = new List<ExecutedLogicalCase>();
        foreach (var item in physical)
        {
            var match = FindExpected(item, result.Context, result.Failures);
            if (match is null)
            {
                continue;
            }

            var expectedCase = CreateExpected(match.Definition, match.Row, tfm);
            if (!byKey.ContainsKey(expectedCase.LogicalKey) || item.Outcome is null)
            {
                continue;
            }

            cases.Add(new ExecutedLogicalCase(
                expectedCase.LogicalKey,
                expectedCase.LogicalCaseId,
                expectedCase.OwnerProject,
                expectedCase.Tfm,
                expectedCase.NormalizedMethodSignature,
                expectedCase.RowKey,
                expectedCase.ReviewedCodeFingerprint,
                expectedCase.Classification,
                item.Outcome.Value,
                item.Duration));
        }

        return new LogicalExecutionBindingResult(
            cases,
            result.Failures.OrderBy(value => value).ToArray());
    }

    private static PhysicalBindingResult BindPhysical(
        IReadOnlyCollection<PhysicalCase> physical,
        IReadOnlyCollection<LogicalCaseDefinition> expected,
        string ownerProject,
        string tfm)
    {
        var context = Prepare(expected, ownerProject, tfm);
        var failures = new HashSet<LogicalCaseBindingFailure>(context.Failures);
        var cases = new List<BoundLogicalCase>();
        foreach (var item in physical)
        {
            if (!string.Equals(item.Tfm, tfm, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(LogicalCaseBindingFailure.TfmMismatch);
                continue;
            }

            var match = FindExpected(item, context, failures);
            if (match is null)
            {
                continue;
            }

            cases.Add(CreateBound(match.Definition, match.Row, tfm));
        }

        if (cases.GroupBy(item => item.LogicalKey, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            failures.Add(LogicalCaseBindingFailure.DuplicateCase);
        }

        var expectedKeys = context.Definitions
            .SelectMany(definition => Expand(definition, tfm))
            .Select(item => item.LogicalKey)
            .ToHashSet(StringComparer.Ordinal);
        var boundKeys = cases.Select(item => item.LogicalKey).ToHashSet(StringComparer.Ordinal);
        if (expectedKeys.Except(boundKeys).Any())
        {
            failures.Add(LogicalCaseBindingFailure.MissingRow);
        }

        return new PhysicalBindingResult(cases, failures, context);
    }

    private static ExpectedMatch? FindExpected(
        PhysicalCase item,
        BindingContext context,
        ISet<LogicalCaseBindingFailure> failures)
    {
        var candidates = context.Definitions.Where(definition => string.Equals(
                MethodName(definition.NormalizedMethodSignature),
                item.MethodName,
                StringComparison.Ordinal))
            .ToArray();
        if (candidates.Length == 0)
        {
            failures.Add(LogicalCaseBindingFailure.UnexpectedMethod);
            return null;
        }

        if (candidates.Length != 1)
        {
            failures.Add(LogicalCaseBindingFailure.AmbiguousMethod);
            return null;
        }

        var definition = candidates[0];
        TheoryRowDefinition? row;
        if (IsFact(definition))
        {
            row = definition.Rows[0];
        }
        else
        {
            row = definition.Rows.SingleOrDefault(candidate => string.Equals(
                candidate.LocatorSha256,
                item.PhysicalRowLocator,
                StringComparison.Ordinal));
        }

        if (row is null)
        {
            failures.Add(LogicalCaseBindingFailure.UnexpectedRow);
            return null;
        }

        return new ExpectedMatch(definition, row);
    }

    private static BindingContext Prepare(
        IReadOnlyCollection<LogicalCaseDefinition> expected,
        string ownerProject,
        string tfm)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (string.IsNullOrWhiteSpace(ownerProject) || string.IsNullOrWhiteSpace(tfm))
        {
            throw new ArgumentException("A physical owner and TFM are required.");
        }

        var definitions = expected.Where(definition =>
                string.Equals(definition.OwnerProject, ownerProject, StringComparison.OrdinalIgnoreCase) &&
                definition.Frameworks.Contains(tfm, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var failures = new HashSet<LogicalCaseBindingFailure>();
        if (definitions.Length == 0)
        {
            failures.Add(LogicalCaseBindingFailure.NoExpectedCases);
        }

        foreach (var definition in definitions)
        {
            if (definition.Rows.Count == 0)
            {
                failures.Add(LogicalCaseBindingFailure.MissingRow);
            }

            if (definition.Rows.GroupBy(row => row.RowKey, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
            {
                failures.Add(LogicalCaseBindingFailure.DuplicateRowId);
            }

            if (definition.Rows.Where(row => row.LocatorSha256 is not null)
                .GroupBy(row => row.LocatorSha256, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
            {
                failures.Add(LogicalCaseBindingFailure.DuplicateRowLocator);
            }

            var isFact = IsFact(definition);
            if ((isFact && definition.Rows[0].LocatorSha256 is not null) ||
                (!isFact && definition.Rows.Any(row => row.LocatorSha256 is null)))
            {
                failures.Add(LogicalCaseBindingFailure.RowShapeMismatch);
            }
        }

        return new BindingContext(definitions, failures);
    }

    private static bool IsFact(LogicalCaseDefinition definition) =>
        definition.Rows.Count == 1 &&
        string.Equals(definition.Rows[0].RowKey, "fact", StringComparison.Ordinal);

    private static IEnumerable<BoundLogicalCase> Expand(
        LogicalCaseDefinition definition,
        string tfm) => definition.Rows.Select(row => CreateBound(definition, row, tfm));

    private static BoundLogicalCase CreateBound(
        LogicalCaseDefinition definition,
        TheoryRowDefinition row,
        string tfm)
    {
        var expected = CreateExpected(definition, row, tfm);
        return new BoundLogicalCase(
            expected.LogicalKey,
            expected.LogicalCaseId,
            expected.OwnerProject,
            expected.Tfm,
            expected.NormalizedMethodSignature,
            expected.RowKey,
            expected.ReviewedCodeFingerprint,
            expected.Classification);
    }

    private static ExpectedLogicalCase CreateExpected(
        LogicalCaseDefinition definition,
        TheoryRowDefinition row,
        string tfm) => new ExpectedLogicalCase(
            LogicalCaseIdentity.Create(
                1,
                tfm,
                definition.NormalizedMethodSignature,
                row.RowKey,
                definition.Classification),
            definition.LogicalCaseId,
            definition.OwnerProject,
            tfm,
            definition.NormalizedMethodSignature,
            row.RowKey,
            definition.ReviewedCodeFingerprint,
            definition.Classification);

    private static string MethodName(string normalizedSignature)
    {
        var parenthesis = normalizedSignature.IndexOf('(');
        var method = parenthesis < 0
            ? normalizedSignature
            : normalizedSignature.Substring(0, parenthesis);
        return MethodGenericArity.Replace(method, string.Empty);
    }

    private static LogicalCaseBindingResult Result(
        IReadOnlyCollection<BoundLogicalCase> cases,
        IEnumerable<LogicalCaseBindingFailure> failures) => new LogicalCaseBindingResult(
            cases.ToArray(),
            failures.Distinct().OrderBy(value => value).ToArray());

    private sealed record PhysicalCase(
        string MethodName,
        string Tfm,
        string PhysicalRowLocator,
        TestOutcome? Outcome,
        TimeSpan Duration);

    private sealed record ExpectedMatch(
        LogicalCaseDefinition Definition,
        TheoryRowDefinition Row);

    private sealed record BindingContext(
        IReadOnlyList<LogicalCaseDefinition> Definitions,
        IReadOnlyCollection<LogicalCaseBindingFailure> Failures);

    private sealed record PhysicalBindingResult(
        IReadOnlyList<BoundLogicalCase> Cases,
        HashSet<LogicalCaseBindingFailure> Failures,
        BindingContext Context);
}

public enum LogicalCaseBindingFailure
{
    NoExpectedCases,
    MissingMethod,
    UnexpectedMethod,
    AmbiguousMethod,
    DuplicateRowId,
    DuplicateRowLocator,
    MissingRow,
    UnexpectedRow,
    DuplicateCase,
    TfmMismatch,
    FingerprintMismatch,
    IncompleteFingerprint,
    ClassificationMismatch,
    RowShapeMismatch,
}

public sealed record ExpectedLogicalCase(
    string LogicalKey,
    string LogicalCaseId,
    string OwnerProject,
    string Tfm,
    string NormalizedMethodSignature,
    string RowKey,
    string ReviewedCodeFingerprint,
    IReadOnlyList<string> Classification);

public sealed record BoundLogicalCase(
    string LogicalKey,
    string LogicalCaseId,
    string OwnerProject,
    string Tfm,
    string NormalizedMethodSignature,
    string RowKey,
    string ReviewedCodeFingerprint,
    IReadOnlyList<string> Classification);

public sealed record ExecutedLogicalCase(
    string LogicalKey,
    string LogicalCaseId,
    string OwnerProject,
    string Tfm,
    string NormalizedMethodSignature,
    string RowKey,
    string ReviewedCodeFingerprint,
    IReadOnlyList<string> Classification,
    TestOutcome Outcome,
    TimeSpan Duration);

public sealed record LogicalCaseBindingResult(
    IReadOnlyList<BoundLogicalCase> Cases,
    IReadOnlyList<LogicalCaseBindingFailure> Failures)
{
    public bool IsComplete => Failures.Count == 0;
}

public sealed record LogicalExecutionBindingResult(
    IReadOnlyList<ExecutedLogicalCase> Cases,
    IReadOnlyList<LogicalCaseBindingFailure> Failures)
{
    public bool IsComplete => Failures.Count == 0;
}
