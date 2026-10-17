using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyDmsVn.SqlTestRunner.Gates;

/// <summary>
/// Plans the longest runner-owned paths before a SQL gate can acquire its lease.
/// The conservative limit keeps the runner compatible with Windows tools that do
/// not opt into extended-length paths.
/// </summary>
public static class TestGateArtifactPathPlanner
{
    public const int MaximumPathLength = 240;
    public const string LogicalRunIdFileName = "execution-id.txt";

    public static string NewPhysicalId() => "r-" + Guid.NewGuid().ToString("N").Substring(0, 16);

    public static IReadOnlyList<string> FindOverBudgetPaths(
        string artifactRoot,
        IReadOnlyList<TestGateDefinition> gates,
        string? repositoryRoot = null)
    {
        var root = Path.GetFullPath(artifactRoot);
        repositoryRoot ??= root;
        var overBudget = FindOverBudgetPaths(root).ToList();
        var shadowRoot = Path.Combine(
            Path.GetTempPath(), "MyDmsVn", "TestGate", "runner-12345678901234567890");
        var hasSqlPreflight = gates.Any(gate => gate.StrictMode == TestGateStrictMode.SqlPrAcceptance);
        var preflightRoot = Path.Combine(
            Path.GetFullPath(repositoryRoot), "artifacts",
            "testgate-preflight-0123456789abcdef0123456789abcdef");
        var inventoryRoot = Path.Combine(
            Path.GetFullPath(repositoryRoot), "artifacts",
            "testgate-inventory-0123456789abcdef0123456789abcdef");
        var candidates = new List<(string Kind, string Path)>
        {
            ("runner shadow assembly", Path.Combine(shadowRoot, "MyDmsVn.SqlTestRunner.dll")),
            ("runner shadow dependency", Path.Combine(
                shadowRoot, "Microsoft.Extensions.DependencyInjection.Abstractions.dll")),
            ("runner shadow runtime configuration", Path.Combine(
                shadowRoot, "MyDmsVn.SqlTestRunner.runtimeconfig.json")),
        };

        if (hasSqlPreflight)
        {
            candidates.Add(("preflight build properties", Path.Combine(
                Path.GetFullPath(repositoryRoot), "tests", "tools", "TestGate.IsolatedBuild.props")));
            candidates.Add(("preflight intermediate assets", Path.Combine(
                preflightRoot, "obj", "MyDmsVn.Server.Infrastructure.IntegrationTests",
                "project.assets.json")));
            candidates.Add(("preflight intermediate NuGet props", Path.Combine(
                preflightRoot, "obj", "MyDmsVn.Server.Infrastructure.IntegrationTests",
                "MyDmsVn.Server.Infrastructure.IntegrationTests.csproj.nuget.g.props")));
            candidates.Add(("preflight intermediate reference assembly", Path.Combine(
                preflightRoot, "obj", "MyDmsVn.Server.Infrastructure.IntegrationTests",
                "Release", "net48", "ref", "MyDmsVn.Server.Infrastructure.IntegrationTests.dll")));
            candidates.Add(("inventory intermediate assets", Path.Combine(
                inventoryRoot, "obj", "MyDmsVn.Desktop.Tests", "project.assets.json")));
            candidates.Add(("inventory intermediate NuGet lock", Path.Combine(
                inventoryRoot, "obj", "MyDmsVn.Desktop.Tests", "packages.lock.json")));
            foreach (var projectName in gates
                         .Where(gate => gate.StrictMode == TestGateStrictMode.SqlPrAcceptance)
                         .Select(gate => Path.GetFileNameWithoutExtension(gate.Project)))
            {
                candidates.Add(("preflight build output", Path.Combine(
                    preflightRoot, projectName, "Release", "net48", projectName + ".dll")));
            }

            candidates.Add(("inventory build output", Path.Combine(
                inventoryRoot, "MyDmsVn.Desktop.Tests", "Release", "net48",
                "MyDmsVn.Desktop.Tests.dll")));
        }

        foreach (var gate in gates)
        {
            var gateRoot = Path.Combine(root, gate.GateId);
            candidates.Add(("TRX", Path.Combine(gateRoot, "t-r-0123456789abcdef.trx")));
            candidates.Add(("timing", Path.Combine(gateRoot, "m-r-0123456789abcdef.jsonl")));
            candidates.Add(("ownership journal temporary file", Path.Combine(
                gateRoot, "ownership", "r-0123456789abcdef",
                "MyDmsVn_Test_0123456789abcdef0123456789abcdef.json." +
                "0123456789abcdef0123456789abcdef.tmp")));
            candidates.Add(("logical execution ID temporary file", Path.Combine(
                gateRoot, "ownership", "r-0123456789abcdef",
                LogicalRunIdFileName + ".tmp-0123456789abcdef0123456789abcdef")));
            candidates.Add(("ownership recovery", Path.Combine(
                gateRoot, "recovery-r-0123456789abcdef.json")));
            candidates.Add(("ownership recovery temporary file", Path.Combine(
                gateRoot, "recovery-r-0123456789abcdef.json.tmp-" +
                "0123456789abcdef0123456789abcdef")));
            candidates.Add(("gate report", Path.Combine(root, gate.GateId + ".sanitized.json")));
            candidates.Add(("gate report temporary file", Path.Combine(
                root, gate.GateId + ".sanitized.json.tmp")));
            foreach (var tfm in gate.Frameworks)
            {
                candidates.Add(("acceptance report", Path.Combine(
                    gateRoot, "sql-pr-acceptance-" + tfm + ".json")));
                candidates.Add(("acceptance report temporary file", Path.Combine(
                    gateRoot, "sql-pr-acceptance-" + tfm + ".json.tmp-" +
                    "0123456789abcdef0123456789abcdef")));
            }
        }

        overBudget.AddRange(candidates
            .Where(candidate => candidate.Path.Length > MaximumPathLength)
            .Select(candidate => candidate.Kind));
        return overBudget.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> FindOverBudgetPaths(string artifactRoot)
    {
        var root = Path.GetFullPath(artifactRoot);
        var shadowRoot = Path.Combine(
            Path.GetTempPath(), "MyDmsVn", "TestGate", "runner-12345678901234567890");
        var candidates = new List<(string Kind, string Path)>
        {
            ("runner shadow assembly", Path.Combine(shadowRoot, "MyDmsVn.SqlTestRunner.dll")),
            ("runner shadow dependency", Path.Combine(
                shadowRoot, "Microsoft.Extensions.DependencyInjection.Abstractions.dll")),
            ("runner shadow runtime configuration", Path.Combine(
                shadowRoot, "MyDmsVn.SqlTestRunner.runtimeconfig.json")),
            ("task result", Path.Combine(root, "r-0123456789abcdef", "result.trx")),
            ("focused result", Path.Combine(root, "r-0123456789abcdef", "result.trx")),
            ("timing", Path.Combine(root, "m-0123456789abcdef.jsonl")),
            ("ownership journal temporary file", Path.Combine(
                root, "ownership", "r-0123456789abcdef",
                "MyDmsVn_Test_0123456789abcdef0123456789abcdef.json." +
                "0123456789abcdef0123456789abcdef.tmp")),
            ("logical execution ID temporary file", Path.Combine(
                root, "ownership", "r-0123456789abcdef",
                LogicalRunIdFileName + ".tmp-0123456789abcdef0123456789abcdef")),
            ("ownership recovery", Path.Combine(root, "recovery-r-0123456789abcdef.json")),
            ("ownership recovery temporary file", Path.Combine(
                root, "recovery-r-0123456789abcdef.json.tmp-0123456789abcdef0123456789abcdef")),
            ("benchmark TRX", Path.Combine(root, "t-r-0123456789abcdef.trx")),
            ("benchmark report", Path.Combine(root, "sql-pr-acceptance-net48.json")),
            ("benchmark report temporary file", Path.Combine(
                root, "sql-pr-acceptance-net48.json.tmp-0123456789abcdef0123456789abcdef")),
        };

        return candidates
            .Where(candidate => candidate.Path.Length > MaximumPathLength)
            .Select(candidate => candidate.Kind)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static void WriteLogicalRunId(string artifactDirectory, string logicalRunId)
    {
        var mappingPath = Path.Combine(artifactDirectory, LogicalRunIdFileName);
        var temporaryPath = mappingPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporaryPath, logicalRunId);
            File.Move(temporaryPath, mappingPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static string ReadLogicalRunId(string journalDirectory)
    {
        var mapping = Path.Combine(journalDirectory, LogicalRunIdFileName);
        return File.Exists(mapping) ? File.ReadAllText(mapping) : Path.GetFileName(journalDirectory);
    }

    public static bool IsEmptyOrMetadataOnlyJournal(string journalDirectory, string logicalRunId)
    {
        try
        {
            return IsValidJournalLayout(journalDirectory, logicalRunId) &&
                !Directory.EnumerateFiles(journalDirectory, "*.json", SearchOption.TopDirectoryOnly).Any();
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool IsValidJournalLayout(string journalDirectory, string logicalRunId)
    {
        try
        {
            if (!Directory.Exists(journalDirectory))
            {
                return false;
            }

            var hasMapping = false;
            foreach (var entry in Directory.GetFileSystemEntries(journalDirectory))
            {
                if (!File.Exists(entry))
                {
                    return false;
                }

                var name = Path.GetFileName(entry);
                if (string.Equals(name, LogicalRunIdFileName, StringComparison.Ordinal))
                {
                    if (hasMapping)
                    {
                        return false;
                    }

                    hasMapping = true;
                    var mappedRunId = File.ReadAllText(entry);
                    if (!IsSafeLogicalRunId(mappedRunId) ||
                        !string.Equals(mappedRunId, logicalRunId, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
                else if (!IsOwnershipRecordFileName(name))
                {
                    return false;
                }
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsSafeLogicalRunId(string logicalRunId) =>
        logicalRunId.Length is > 0 and <= 200 &&
        logicalRunId.All(character =>
            char.IsLetterOrDigit(character) || character == '-' || character == '_' || character == '.');

    private static bool IsOwnershipRecordFileName(string name)
    {
        const string prefix = "MyDmsVn_Test_";
        const string suffix = ".json";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
            !name.EndsWith(suffix, StringComparison.Ordinal) ||
            name.Length != prefix.Length + 32 + suffix.Length)
        {
            return false;
        }

        return name.Substring(prefix.Length, 32).All(character =>
            (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
    }
}
