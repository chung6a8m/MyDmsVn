using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;

namespace MyDmsVn.SqlTestRunner.Gates;

/// <summary>
/// Performs source and expanded discovery reconciliation before any SQL test
/// execution or DDL. It is not an execution result and cannot replace TRX
/// and positively owned database cleanup evidence.
/// </summary>
public interface ISqlGateManifestPreflight
{
    Task<SqlGateManifestPreflightResult> VerifyAsync(
        TestGateRunOptions options,
        TestGateManifest manifest,
        IReadOnlyList<TestGateDefinition> gates,
        CancellationToken cancellationToken);
}

public sealed record SqlGateManifestPreflightIssue(string GateId, string Framework, string Reason);

public sealed record SqlGateManifestPreflightResult(
    IReadOnlyList<SqlGateManifestPreflightIssue> Issues)
{
    public bool IsComplete => Issues.Count == 0;
}

public sealed class SqlGateManifestPreflight : ISqlGateManifestPreflight
{
    private readonly IProcessRunner _processes;
    private readonly ITestGateSourceInventory _inventory;

    public SqlGateManifestPreflight(IProcessRunner processes, ITestGateSourceInventory inventory)
    {
        _processes = processes;
        _inventory = inventory;
    }

    public async Task<SqlGateManifestPreflightResult> VerifyAsync(
        TestGateRunOptions options,
        TestGateManifest manifest,
        IReadOnlyList<TestGateDefinition> gates,
        CancellationToken cancellationToken)
    {
        var issues = new List<SqlGateManifestPreflightIssue>();
        var seenProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (gates.Count == 0 ||
            gates.Any(gate => gate.StrictMode != TestGateStrictMode.SqlPrAcceptance))
        {
            issues.Add(new SqlGateManifestPreflightIssue(
                "SQL", "all", "No reviewed SQL acceptance gate selection."));
            return new SqlGateManifestPreflightResult(issues);
        }

        foreach (var gate in gates)
        {
            foreach (var tfm in gate.Frameworks)
            {
                // Multiple Final gate entries may reference the same physical project.
                // Unfiltered SQL discovery and source validation are project-wide.
                if (!seenProjects.Add(gate.Project + "|" + tfm))
                {
                    continue;
                }

                var project = Path.GetFullPath(Path.Combine(options.RepositoryRoot, gate.Project));
                var build = await ExecuteAsync(
                    "build \"" + project + "\" -c Release -f " + tfm + " --no-restore",
                    options, gate.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
                if (build.ExitCode != 0 || build.TimedOut)
                {
                    issues.Add(new SqlGateManifestPreflightIssue(
                        gate.GateId, tfm, "Build failed before SQL manifest verification."));
                    continue;
                }

                try
                {
                    var definitions = manifest.LogicalCases.Where(item =>
                        string.Equals(item.OwnerProject, gate.Project, StringComparison.OrdinalIgnoreCase) &&
                        item.Frameworks.Contains(tfm, StringComparer.Ordinal)).ToArray();
                    if (definitions.Length == 0)
                    {
                        issues.Add(new SqlGateManifestPreflightIssue(
                            gate.GateId, tfm, "No reviewed source cases for this SQL project/TFM."));
                        continue;
                    }

                    var assemblyPath = Path.Combine(
                        Path.GetDirectoryName(project)!, "bin", "Release", tfm,
                        Path.GetFileNameWithoutExtension(project) + ".dll");
                    var source = _inventory.Read(assemblyPath);
                    var sourceBinding = LogicalCaseBinder.BindSource(
                        source, manifest.LogicalCases, gate.Project, tfm, manifest.Exemptions);

                    if (!sourceBinding.IsComplete)
                    {
                        issues.Add(new SqlGateManifestPreflightIssue(
                            gate.GateId, tfm,
                            "Source: " + string.Join(",", sourceBinding.Failures)));
                        AddSourceFingerprintDiagnostics(issues, gate.GateId, tfm, definitions, source);
                        continue;
                    }

                    var discovery = await ExecuteAsync(
                        "test \"" + project + "\" -c Release -f " + tfm +
                        " --no-build --no-restore --list-tests",
                        options, gate.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
                    if (discovery.ExitCode != 0 || discovery.TimedOut)
                    {
                        issues.Add(new SqlGateManifestPreflightIssue(
                            gate.GateId, tfm, "Discovery failed before SQL execution."));
                        continue;
                    }

                    var discovered = VstestDiscoveryParser.Parse(discovery.StandardOutput, tfm);
                    var discoveryBinding = LogicalCaseBinder.BindDiscovery(
                        discovered, manifest.LogicalCases, gate.Project, tfm);
                    if (!discoveryBinding.IsComplete ||
                        !sourceBinding.Cases.Select(item => item.LogicalKey)
                            .OrderBy(key => key, StringComparer.Ordinal)
                            .SequenceEqual(discoveryBinding.Cases.Select(item => item.LogicalKey)
                                .OrderBy(key => key, StringComparer.Ordinal), StringComparer.Ordinal))
                    {
                        issues.Add(new SqlGateManifestPreflightIssue(
                            gate.GateId, tfm, "Discovery: " +
                            string.Join(",", discoveryBinding.Failures) +
                            "; source/discovered logical-case sets differ."));
                    }
                }
                catch (Exception exception) when (
                    exception is IOException or InvalidDataException or InvalidOperationException
                        or ArgumentException or System.Reflection.ReflectionTypeLoadException)
                {
                    issues.Add(new SqlGateManifestPreflightIssue(
                        gate.GateId, tfm, "Compiled inventory or source metadata cannot be inspected."));
                }
            }
        }

        return new SqlGateManifestPreflightResult(issues);
    }

    private static void AddSourceFingerprintDiagnostics(
        ICollection<SqlGateManifestPreflightIssue> issues,
        string gateId,
        string tfm,
        IReadOnlyList<LogicalCaseDefinition> definitions,
        IReadOnlyList<TestMethodDescriptor> source)
    {
        var sourceMethods = source.GroupBy(method => method.NormalizedSignature, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        // Emit normalized test method signatures only; never raw Theory arguments,
        // SQL endpoints, approval identifiers, journal contents or credentials.
        foreach (var definition in definitions)
        {
            if (!sourceMethods.TryGetValue(definition.NormalizedMethodSignature, out var methods))
            {
                issues.Add(new SqlGateManifestPreflightIssue(
                    gateId, tfm, "Missing: " + definition.NormalizedMethodSignature));
            }
            else if (methods.Length == 1 &&
                !string.Equals(methods[0].BodyFingerprint, definition.ReviewedCodeFingerprint,
                    StringComparison.Ordinal))
            {
                issues.Add(new SqlGateManifestPreflightIssue(
                    gateId, tfm, "FingerprintMismatch: " + definition.NormalizedMethodSignature));
            }
        }
    }

    private Task<CommandResult> ExecuteAsync(
        string command, TestGateRunOptions options, int timeoutSeconds, CancellationToken token) =>
        _processes.RunAsync(new CommandSpec(
            "dotnet", command, options.RepositoryRoot, TimeSpan.FromSeconds(timeoutSeconds)), token);
}
