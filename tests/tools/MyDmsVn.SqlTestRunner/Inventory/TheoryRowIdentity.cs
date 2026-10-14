using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class TheoryRowIdentity
{
    private static readonly Regex Parameters = new Regex("\\(.*\\)$", RegexOptions.Compiled);

    public static string MethodName(string displayName) =>
        Parameters.Replace(displayName.Trim(), string.Empty);

    public static string Create(string methodName, string tfm, string displayName)
    {
        var normalizedDisplay = displayName.Trim();
        var value = methodName + "|" + tfm + "|display:" + normalizedDisplay;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return methodName + "#" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
