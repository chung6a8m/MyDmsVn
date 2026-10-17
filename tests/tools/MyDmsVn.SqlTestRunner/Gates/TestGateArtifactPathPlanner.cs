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
        IReadOnlyList<TestGateDefinition> gates)
    {
        var root = Path.GetFullPath(artifactRoot);
        var overBudget = FindOverBudgetPaths(root).ToList();
        var shadowRoot = Path.Combine(
            Path.GetTempPath(), "MyDmsVn", "TestGate", "runner-12345678901234567890");
        var candidates = new List<(string Kind, string Path)>
        {
            ("runner shadow assembly", Path.Combine(shadowRoot, "MyDmsVn.SqlTestRunner.dll")),
            ("runner shadow dependency", Path.Combine(
                shadowRoot, "Microsoft.Extensions.DependencyInjection.Abstractions.dll")),
            ("runner shadow runtime configuration", Path.Combine(
                shadowRoot, "MyDmsVn.SqlTestRunner.runtimeconfig.json")),
            ("preflight build output", Path.Combine(
                Path.GetTempPath(), "MyDmsVn", "TestGate",
                "preflight-0123456789abcdef0123456789abcdef", "Release", "net48",
                "MyDmsVn.Server.Infrastructure.Foundation.Tests.dll")),
        };

        foreach (var gate in gates)
        {
            var gateRoot = Path.Combine(root, gate.GateId);
            candidates.Add(("TRX", Path.Combine(gateRoot, "t-r-0123456789abcdef.trx")));
            candidates.Add(("timing", Path.Combine(gateRoot, "m-r-0123456789abcdef.jsonl")));
            candidates.Add(("ownership journal temporary file", Path.Combine(
                gateRoot, "ownership", "r-0123456789abcdef",
                "MyDmsVn_Test_0123456789abcdef0123456789abcdef.json." +
                "0123456789abcdef0123456789abcdef.tmp")));
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
            ("preflight build output", Path.Combine(
                Path.GetTempPath(), "MyDmsVn", "TestGate",
                "preflight-0123456789abcdef0123456789abcdef", "Release", "net48",
                "MyDmsVn.Server.Infrastructure.Foundation.Tests.dll")),
            ("task result", Path.Combine(root, "r-0123456789abcdef", "result.trx")),
            ("focused result", Path.Combine(root, "r-0123456789abcdef", "result.trx")),
            ("timing", Path.Combine(root, "m-0123456789abcdef.jsonl")),
            ("ownership journal temporary file", Path.Combine(
                root, "ownership", "r-0123456789abcdef",
                "MyDmsVn_Test_0123456789abcdef0123456789abcdef.json." +
                "0123456789abcdef0123456789abcdef.tmp")),
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
        File.WriteAllText(Path.Combine(artifactDirectory, LogicalRunIdFileName), logicalRunId);
    }

    public static string ReadLogicalRunId(string journalDirectory)
    {
        var mapping = Path.Combine(journalDirectory, LogicalRunIdFileName);
        return File.Exists(mapping) ? File.ReadAllText(mapping) : Path.GetFileName(journalDirectory);
    }
}
