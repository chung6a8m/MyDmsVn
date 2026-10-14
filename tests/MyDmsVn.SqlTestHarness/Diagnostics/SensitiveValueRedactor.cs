using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyDmsVn.SqlTestHarness.Diagnostics;

public static class SensitiveValueRedactor
{
    public static string Redact(string value, params string?[] sensitiveValues)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var redacted = value;
        foreach (var sensitiveValue in sensitiveValues
                     .Where(candidate => !string.IsNullOrEmpty(candidate))
                     .OrderByDescending(candidate => candidate!.Length))
        {
            redacted = ReplaceOrdinalIgnoreCase(redacted, sensitiveValue!, "[REDACTED]");
        }

        return redacted;
    }

    public static string ShortenFingerprint(string fingerprint)
    {
        if (fingerprint is null)
        {
            throw new ArgumentNullException(nameof(fingerprint));
        }

        if (fingerprint.Length != 64 || fingerprint.Any(value => !IsLowerHex(value)))
        {
            throw new ArgumentException("A lowercase 64-character SHA-256 fingerprint is required.", nameof(fingerprint));
        }

        return fingerprint.Substring(0, 12);
    }

    private static string ReplaceOrdinalIgnoreCase(string value, string oldValue, string newValue)
    {
        var start = 0;
        var index = value.IndexOf(oldValue, start, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        while (index >= 0)
        {
            builder.Append(value, start, index - start);
            builder.Append(newValue);
            start = index + oldValue.Length;
            index = value.IndexOf(oldValue, start, StringComparison.OrdinalIgnoreCase);
        }

        builder.Append(value, start, value.Length - start);
        return builder.ToString();
    }

    private static bool IsLowerHex(char value) =>
        (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f');
}
