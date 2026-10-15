using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class NonSqlExemptionManifest
{
    public static IReadOnlyList<NonSqlExemption> Read(string path)
    {
        using var stream = File.OpenRead(path);
        var exemptions = JsonSerializer.Deserialize<List<NonSqlExemption>>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException("The non-SQL exemption manifest is empty.");
        foreach (var exemption in exemptions)
        {
            ValidateMethodName(exemption.MethodName);
            if (string.IsNullOrWhiteSpace(exemption.MethodBodyFingerprint) ||
                string.IsNullOrWhiteSpace(exemption.Rationale) ||
                string.IsNullOrWhiteSpace(exemption.Evidence))
            {
                throw new InvalidDataException("Every non-SQL exemption needs a fingerprint, rationale and evidence.");
            }
        }

        return exemptions;
    }

    public static void ValidateMethodName(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName) ||
            methodName.IndexOf('*') >= 0 ||
            methodName.IndexOf('?') >= 0 ||
            methodName.IndexOf('(') >= 0)
        {
            throw new InvalidOperationException("A non-SQL exemption must name one exact method.");
        }
    }

    public static NonSqlExemptionMatch Match(
        NonSqlExemption exemption,
        TestMethodDescriptor method)
    {
        ValidateMethodName(exemption.MethodName);
        if (!string.Equals(exemption.MethodName, method.FullyQualifiedName, StringComparison.Ordinal))
        {
            return new NonSqlExemptionMatch(false, NonSqlExemptionFailure.MethodMismatch);
        }

        if (!method.IsBodyFingerprintComplete)
        {
            return new NonSqlExemptionMatch(false, NonSqlExemptionFailure.UnresolvedDependency);
        }

        return exemption.MethodBodyFingerprint
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Contains(method.BodyFingerprint, StringComparer.Ordinal)
            ? new NonSqlExemptionMatch(true, NonSqlExemptionFailure.None)
            : new NonSqlExemptionMatch(false, NonSqlExemptionFailure.StaleFingerprint);
    }
}

public sealed record NonSqlExemption(
    string MethodName,
    string MethodBodyFingerprint,
    string Rationale,
    string Evidence);

public enum NonSqlExemptionFailure
{
    None,
    MethodMismatch,
    StaleFingerprint,
    UnresolvedDependency,
}

public sealed record NonSqlExemptionMatch(bool IsMatch, NonSqlExemptionFailure Failure);
