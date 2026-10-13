using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MyDmsVn.Server.Infrastructure.Diagnostics
{
    public static class SensitiveDataRedactor
    {
        private static readonly Regex ConnectionStringPattern = new Regex(
            @"(?i)\b(?:server|data\s+source)\s*=.*?(?=$|[\r\n])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex AuthorizationHeaderPattern = new Regex(
            @"(?im)\bauthorization\s*[:=]\s*[^\r\n]*",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex NamedSecretPattern = new Regex(
            "(?i)\\b(passwordhash|passwordsalt|password|pwd|user\\s+id|uid|auth(?:entication)?token|access\\s+token)\\s*[:=]\\s*(?:\"[^\"\\r\\n]*\"|'[^'\\r\\n]*'|[^;,\\r\\n]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex BearerPattern = new Regex(
            @"(?i)\bbearer\s+[A-Za-z0-9._~+\-/]+=*",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string Redact(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var safe = AuthorizationHeaderPattern.Replace(value, "Authorization=[REDACTED]");
            safe = BearerPattern.Replace(safe, "Bearer [REDACTED]");
            safe = ConnectionStringPattern.Replace(safe, "[REDACTED-CONNECTION-STRING]");
            safe = NamedSecretPattern.Replace(safe, "$1=[REDACTED]");
            return safe.Length <= 4096 ? safe : safe.Substring(0, 4096) + " [TRUNCATED]";
        }

        public static string ToSafeDiagnostic(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            var parts = new List<string>();
            Exception? current = exception;
            for (var depth = 0; current != null && depth < 8; depth++)
            {
                parts.Add(current.GetType().FullName + ": " + Redact(current.Message));
                current = current.InnerException;
            }

            return string.Join(" --> ", parts);
        }
    }
}
