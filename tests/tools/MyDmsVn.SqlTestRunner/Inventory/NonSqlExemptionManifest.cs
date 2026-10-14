using System;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class NonSqlExemptionManifest
{
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

        return string.Equals(exemption.MethodBodyFingerprint, method.BodyFingerprint, StringComparison.Ordinal)
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
}

public sealed record NonSqlExemptionMatch(bool IsMatch, NonSqlExemptionFailure Failure);
