using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MyDmsVn.SqlTestRunner.Gates;

public static class TestGateManifestValidator
{
    public static TestGateSelection Select(
        TestGateManifest manifest,
        string repositoryRoot,
        TestGateMode mode,
        string planId,
        string taskId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(repositoryRoot) ||
            string.IsNullOrWhiteSpace(planId) ||
            string.IsNullOrWhiteSpace(taskId))
        {
            throw new InvalidDataException("A supported manifest and non-empty gate identity are required.");
        }

        var root = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(root))
        {
            throw new InvalidDataException("The test-gate repository root does not exist.");
        }

        var gates = manifest.Gates
            .Where(gate =>
                gate.Mode == mode &&
                string.Equals(gate.PlanId, planId, StringComparison.Ordinal) &&
                string.Equals(gate.TaskId, taskId, StringComparison.Ordinal))
            .ToArray();
        if (gates.Length == 0)
        {
            throw new InvalidDataException("The requested test-gate identity has no reviewed gates.");
        }

        foreach (var gate in gates)
        {
            ValidateGate(root, gate);
            if (mode == TestGateMode.Final &&
                gate.StrictMode == TestGateStrictMode.DotnetTest &&
                (gate.Resources.Sql != TestGateResourceRequirement.None ||
                 gate.Resources.Gui != TestGateResourceRequirement.None))
            {
                throw new InvalidDataException(
                    $"DotnetTest gate '{gate.GateId}' cannot declare unguarded SQL or GUI resources.");
            }
        }

        if (mode == TestGateMode.Final)
        {
            ValidateFinalSqlContract(gates);
        }

        return new TestGateSelection(mode, planId, taskId, gates);
    }

    private static void ValidateFinalSqlContract(IReadOnlyCollection<TestGateDefinition> gates)
    {
        var sqlGates = gates.Where(gate => gate.StrictMode == TestGateStrictMode.SqlPrAcceptance)
            .ToArray();
        var requiredEvidence = new[]
        {
            TestGateEvidenceKind.Source,
            TestGateEvidenceKind.Discovery,
            TestGateEvidenceKind.Trx,
            TestGateEvidenceKind.SanitizedReport,
            TestGateEvidenceKind.Ownership,
        };
        if (sqlGates.Length != 1 ||
            !sqlGates[0].Frameworks.SequenceEqual(new[] { "net48", "net8.0" }, StringComparer.Ordinal) ||
            sqlGates[0].Selection.Kind != TestGateSelectionKind.UnfilteredProject ||
            sqlGates[0].Selection.Patterns.Count != 0 ||
            sqlGates[0].Resources.Sql != TestGateResourceRequirement.RequiredExclusive ||
            sqlGates[0].Resources.Gui != TestGateResourceRequirement.None ||
            requiredEvidence.Any(kind => !sqlGates[0].Evidence.Contains(kind)))
        {
            throw new InvalidDataException(
                "Final requires one reviewed unfiltered exclusive SQL acceptance gate for net48 and net8.0.");
        }
    }

    private static void ValidateGate(string repositoryRoot, TestGateDefinition gate)
    {
        if (gate.Frameworks.Count == 0 ||
            gate.Frameworks.Any(string.IsNullOrWhiteSpace) ||
            gate.Frameworks.Distinct(StringComparer.Ordinal).Count() != gate.Frameworks.Count)
        {
            throw new InvalidDataException($"Gate '{gate.GateId}' has an invalid framework list.");
        }

        if (gate.Classification.Count == 0 ||
            gate.Classification.Any(string.IsNullOrWhiteSpace) ||
            gate.CommandTimeoutSeconds <= 0 ||
            gate.OverallTimeoutSeconds < gate.CommandTimeoutSeconds)
        {
            throw new InvalidDataException($"Gate '{gate.GateId}' has incomplete reviewed metadata.");
        }

        if (gate.Selection.Kind == TestGateSelectionKind.UnfilteredProject)
        {
            if (gate.Selection.Patterns.Count != 0)
            {
                throw new InvalidDataException(
                    $"Unfiltered gate '{gate.GateId}' cannot declare filter patterns.");
            }
        }
        else if (gate.Selection.Patterns.Count == 0 ||
                 gate.Selection.Patterns.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException(
                $"Filtered gate '{gate.GateId}' requires reviewed non-empty patterns.");
        }

        ValidateProject(repositoryRoot, gate.Project, gate.Frameworks);
        foreach (var prerequisite in gate.BuildPrerequisites)
        {
            ValidateProject(repositoryRoot, prerequisite.Project, prerequisite.Frameworks);
        }
    }

    private static void ValidateProject(
        string repositoryRoot,
        string relativeProject,
        IReadOnlyCollection<string> requiredFrameworks)
    {
        if (string.IsNullOrWhiteSpace(relativeProject) || Path.IsPathRooted(relativeProject))
        {
            throw new InvalidDataException("Test-gate project paths must be repository-relative.");
        }

        var project = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            relativeProject.Replace('/', Path.DirectorySeparatorChar)));
        RequireContained(repositoryRoot, project);
        if (!File.Exists(project) || HasReparsePoint(repositoryRoot, project))
        {
            throw new InvalidDataException(
                $"Test-gate project '{relativeProject}' is missing or resolves through a link.");
        }

        IReadOnlyCollection<string> declaredFrameworks;
        try
        {
            declaredFrameworks = XDocument.Load(project)
                .Descendants()
                .Where(element =>
                    string.Equals(element.Name.LocalName, "TargetFramework", StringComparison.Ordinal) ||
                    string.Equals(element.Name.LocalName, "TargetFrameworks", StringComparison.Ordinal))
                .SelectMany(element => element.Value.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException || exception is System.Xml.XmlException)
        {
            throw new InvalidDataException(
                $"Test-gate project '{relativeProject}' cannot be read.",
                exception);
        }

        if (declaredFrameworks.Count == 0 ||
            requiredFrameworks.Count == 0 ||
            requiredFrameworks.Any(tfm => !declaredFrameworks.Contains(tfm)))
        {
            throw new InvalidDataException(
                $"Test-gate project '{relativeProject}' does not declare every required TFM.");
        }
    }

    private static void RequireContained(string repositoryRoot, string path)
    {
        var rootPrefix = repositoryRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A test-gate path escapes the repository root.");
        }
    }

    private static bool HasReparsePoint(string repositoryRoot, string path)
    {
        var relative = Path.GetRelativePath(repositoryRoot, path);
        var current = repositoryRoot;
        foreach (var segment in relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }
}
