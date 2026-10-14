using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace MyDmsVn.SqlTestRunner.Reconciliation;

public static class MigrationInvocationManifestAuditor
{
    public static bool IsValid(string repositoryRoot, string manifestPath, string tfm)
    {
        try
        {
            var entries = MigrationInvocationManifest.Read(manifestPath);
            if (entries.Count == 0 ||
                entries.Any(entry =>
                    string.IsNullOrWhiteSpace(entry.InvocationId) ||
                    string.IsNullOrWhiteSpace(entry.Class) ||
                    string.IsNullOrWhiteSpace(entry.Method) ||
                    string.IsNullOrWhiteSpace(entry.SourceFile) ||
                    !entry.Tfms.Contains(tfm, StringComparer.Ordinal) ||
                    (!string.Equals(entry.ExpectedInvocationRule, "once-per-test-execution", StringComparison.Ordinal) &&
                     !string.Equals(entry.ExpectedInvocationRule, "once-per-caller-execution", StringComparison.Ordinal))) ||
                entries.GroupBy(entry => entry.InvocationId, StringComparer.Ordinal).Any(group => group.Count() != 1))
            {
                return false;
            }

            if (entries.Any(entry =>
            {
                var expectations = MigrationInvocationManifest.ResolveExpectations(entry, repositoryRoot);
                return expectations.Count == 0 || expectations.Any(expectation =>
                    string.IsNullOrWhiteSpace(expectation.TestMethod) || expectation.Count <= 0);
            }))
            {
                return false;
            }

            var root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var group in entries.GroupBy(entry => entry.SourceFile, StringComparer.Ordinal))
            {
                var sourcePath = Path.GetFullPath(Path.Combine(root, group.Key.Replace('/', Path.DirectorySeparatorChar)));
                if (!sourcePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(sourcePath))
                {
                    return false;
                }

                var source = File.ReadAllText(sourcePath);
                var className = group.First().Class;
                var sourceInvocationIds = Regex.Matches(
                        source,
                        "\\\"(?<id>" + Regex.Escape(className) +
                        "\\.[A-Za-z0-9_]+\\.(?:migration-[0-9]+|direct-[A-Za-z0-9_-]+))\\\"")
                    .Cast<Match>()
                    .Select(match => match.Groups["id"].Value)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var manifestInvocationIds = group.Select(entry => entry.InvocationId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                if (!sourceInvocationIds.SequenceEqual(manifestInvocationIds, StringComparer.Ordinal))
                {
                    return false;
                }

                var directCalls = Regex.Matches(source, "\\.PerformUpgrade\\s*\\(").Count;
                var directEntries = group.Count(entry =>
                    entry.InvocationId.IndexOf(".direct-", StringComparison.Ordinal) >= 0);
                var migrationCalls = Regex.Matches(source, "\\.\\s*Migrate\\s*\\(").Count;
                var measuredMigrationCalls = Regex.Matches(
                    source,
                    "\\bMeasuredDatabaseMigrationRunner\\s*\\.\\s*Migrate\\s*\\(").Count;
                if (directCalls != directEntries || migrationCalls != measuredMigrationCalls)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is JsonException ||
            exception is ArgumentException)
        {
            return false;
        }
    }
}
