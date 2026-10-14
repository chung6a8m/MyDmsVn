using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace MyDmsVn.SqlTestRunner.Reconciliation;

public static class MigrationInvocationManifest
{
    private static readonly Regex MethodDeclaration = new Regex(
        "(?m)^[ \\t]*(?<access>public|private|internal|protected)[ \\t]+" +
        "(?:(?:static|async|virtual|override|sealed|new)[ \\t\\r\\n]+)*" +
        "(?:Task(?:<[^>{}\\r\\n]+>)?|ValueTask(?:<[^>{}\\r\\n]+>)?|void)" +
        "[ \\t\\r\\n]+(?<name>[A-Za-z_][A-Za-z0-9_]*)[ \\t\\r\\n]*\\(",
        RegexOptions.Compiled);

    public static IReadOnlyList<MigrationInvocationManifestEntry> Read(string path) =>
        (IReadOnlyList<MigrationInvocationManifestEntry>?)
            JsonConvert.DeserializeObject<List<MigrationInvocationManifestEntry>>(
                File.ReadAllText(path)) ?? Array.Empty<MigrationInvocationManifestEntry>();

    public static IReadOnlyList<MigrationInvocationExpectation> ResolveExpectations(
        MigrationInvocationManifestEntry entry,
        string repositoryRoot)
    {
        if (entry.ExpectedInvocations.Length > 0)
        {
            return entry.ExpectedInvocations;
        }

        if (string.Equals(
                entry.ExpectedInvocationRule,
                "once-per-test-execution",
                StringComparison.Ordinal))
        {
            return new[] { new MigrationInvocationExpectation { TestMethod = entry.Method, Count = 1 } };
        }

        if (!string.Equals(
                entry.ExpectedInvocationRule,
                "once-per-caller-execution",
                StringComparison.Ordinal))
        {
            return Array.Empty<MigrationInvocationExpectation>();
        }

        var sourcePath = Path.Combine(
            repositoryRoot,
            entry.SourceFile.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(sourcePath);
        var declarations = MethodDeclaration.Matches(source).Cast<Match>().ToArray();
        var callPattern = "\\b" + Regex.Escape(entry.Method) + "\\s*\\(";
        var callsByMethod = new Dictionary<string, int>(StringComparer.Ordinal);
        var recognizedCalls = 0;
        for (var index = 0; index < declarations.Length; index++)
        {
            var declaration = declarations[index];
            if (!string.Equals(
                    declaration.Groups["access"].Value,
                    "public",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var bodyStart = declaration.Index + declaration.Length;
            var bodyEnd = index + 1 < declarations.Length
                ? declarations[index + 1].Index
                : source.Length;
            var callCount = Regex.Matches(
                source.Substring(bodyStart, bodyEnd - bodyStart),
                callPattern).Count;
            if (callCount == 0)
            {
                continue;
            }

            var methodName = declaration.Groups["name"].Value;
            callsByMethod[methodName] = callCount;
            recognizedCalls += callCount;
        }

        var declarationCount = declarations.Count(declaration => string.Equals(
            declaration.Groups["name"].Value,
            entry.Method,
            StringComparison.Ordinal));
        var sourceCallCount = Regex.Matches(source, callPattern).Count - declarationCount;
        if (recognizedCalls != sourceCallCount)
        {
            return Array.Empty<MigrationInvocationExpectation>();
        }

        return callsByMethod
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new MigrationInvocationExpectation
            {
                TestMethod = pair.Key,
                Count = pair.Value,
            })
            .ToArray();
    }
}

public sealed class MigrationInvocationManifestEntry
{
    public string InvocationId { get; set; } = string.Empty;

    public string Class { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string SourceFile { get; set; } = string.Empty;

    public string[] Tfms { get; set; } = Array.Empty<string>();

    public string ExpectedInvocationRule { get; set; } = string.Empty;

    public MigrationInvocationExpectation[] ExpectedInvocations { get; set; } =
        Array.Empty<MigrationInvocationExpectation>();

}

public sealed class MigrationInvocationExpectation
{
    public string TestMethod { get; set; } = string.Empty;

    public int Count { get; set; }
}
