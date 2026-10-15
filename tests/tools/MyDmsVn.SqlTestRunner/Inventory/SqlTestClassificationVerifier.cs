using System;
using System.Collections.Generic;
using System.Linq;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class SqlTestCategories
{
    public const string FastSql = "FastSQL";
    public const string MigrationSchema = "MigrationSchema";
    public const string SecurityProvisioning = "SecurityProvisioning";
    public const string SqlConcurrency = "SqlConcurrency";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        FastSql,
        MigrationSchema,
        SecurityProvisioning,
        SqlConcurrency,
    };
}

public static class SqlTestClassificationVerifier
{
    public static SqlTestClassificationResult Verify(
        IReadOnlyList<TestMethodDescriptor> methods,
        IReadOnlyList<NonSqlExemption> exemptions)
    {
        var failures = new HashSet<SqlTestClassificationFailure>();
        var validCategories = new HashSet<string>(SqlTestCategories.All, StringComparer.Ordinal);
        var categoryCounts = SqlTestCategories.All.ToDictionary(
            category => category,
            _ => 0,
            StringComparer.Ordinal);

        foreach (var method in methods)
        {
            var exemption = exemptions.SingleOrDefault(candidate =>
                string.Equals(candidate.MethodName, method.FullyQualifiedName, StringComparison.Ordinal));
            var isExempt = exemption is not null && NonSqlExemptionManifest.Match(exemption, method).IsMatch;
            if (exemption is not null && !isExempt)
            {
                failures.Add(SqlTestClassificationFailure.InvalidOrStaleExemption);
            }

            var categories = method.Categories.Distinct(StringComparer.Ordinal).ToArray();
            if (isExempt)
            {
                if (categories.Length != 0)
                {
                    failures.Add(SqlTestClassificationFailure.ExemptMethodHasSqlCategory);
                }

                continue;
            }

            if (categories.Length == 0)
            {
                failures.Add(SqlTestClassificationFailure.MissingSqlCategory);
                continue;
            }

            foreach (var category in categories)
            {
                if (!validCategories.Contains(category))
                {
                    failures.Add(SqlTestClassificationFailure.InvalidSqlCategory);
                    continue;
                }

                categoryCounts[category]++;
            }
        }

        if (exemptions.GroupBy(exemption => exemption.MethodName, StringComparer.Ordinal).Any(group => group.Count() != 1) ||
            exemptions.Any(exemption => methods.All(method =>
                !string.Equals(method.FullyQualifiedName, exemption.MethodName, StringComparison.Ordinal))))
        {
            failures.Add(SqlTestClassificationFailure.UnknownOrDuplicateExemption);
        }

        return new SqlTestClassificationResult(failures.OrderBy(value => value).ToArray(), categoryCounts);
    }
}

public enum SqlTestClassificationFailure
{
    MissingSqlCategory,
    InvalidSqlCategory,
    InvalidOrStaleExemption,
    ExemptMethodHasSqlCategory,
    UnknownOrDuplicateExemption,
}

public sealed record SqlTestClassificationResult(
    IReadOnlyList<SqlTestClassificationFailure> Failures,
    IReadOnlyDictionary<string, int> CategoryCounts)
{
    public bool IsComplete => Failures.Count == 0;
}
