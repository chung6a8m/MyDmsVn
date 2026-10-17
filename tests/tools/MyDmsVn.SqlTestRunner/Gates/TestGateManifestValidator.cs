using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MyDmsVn.SqlTestRunner.Gates;

public static class TestGateManifestValidator
{
    private static readonly HashSet<string> SqlLogicalCaseCategories = new HashSet<string>(StringComparer.Ordinal)
    {
        "FastSQL",
        "MigrationSchema",
        "SecurityProvisioning",
        "SqlConcurrency",
    };

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

        if (manifest.FrameworkProfile is { } selectedProfile &&
            string.Equals(selectedProfile.PlanId, planId, StringComparison.Ordinal))
        {
            ValidateFrameworkProfile(manifest, selectedProfile, planId);
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
            ValidateFinalSqlContract(manifest, gates, planId, taskId);
        }

        return new TestGateSelection(mode, planId, taskId, gates);
    }

    private static void ValidateFinalSqlContract(
        TestGateManifest manifest,
        IReadOnlyCollection<TestGateDefinition> gates,
        string planId,
        string taskId)
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
        var modularTask = sqlGates.Length > 1 ||
            taskId is "TESTGATE-T05-B" or "TESTGATE-T05-C" or "TESTGATE-T05-D";
        var activeProfile = manifest.FrameworkProfile is { } profile &&
            (!modularTask || string.Equals(profile.PlanId, planId, StringComparison.Ordinal))
                ? profile
                : null;
        var expectedFrameworks = (IReadOnlyList<string>)new[] { "net48", "net8.0" };
        if (activeProfile is not null)
        {
            expectedFrameworks = activeProfile.ActiveSqlFrameworks;
        }
        else if (sqlGates.Length != 0)
        {
            expectedFrameworks = sqlGates[0].Frameworks;
        }

        if (sqlGates.Length == 0 ||
            (activeProfile is not null && sqlGates.Length != 1) ||
            sqlGates.Any(gate =>
                !gate.Frameworks.SequenceEqual(expectedFrameworks, StringComparer.Ordinal) ||
                gate.Selection.Kind != TestGateSelectionKind.UnfilteredProject ||
                gate.Selection.Patterns.Count != 0 ||
                gate.Resources.Sql != TestGateResourceRequirement.RequiredExclusive ||
                gate.Resources.Gui != TestGateResourceRequirement.None ||
                requiredEvidence.Any(kind => !gate.Evidence.Contains(kind))) ||
            sqlGates.Select(gate => gate.Project)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != sqlGates.Length ||
            (activeProfile is not null &&
             !string.Equals(sqlGates[0].Project, activeProfile.SqlProject, StringComparison.Ordinal)) ||
            (activeProfile is null &&
             !HasCompleteModularSqlOwnership(manifest.LogicalCases, sqlGates, expectedFrameworks)))
        {
            throw new InvalidDataException(
                "Final requires reviewed unfiltered exclusive SQL acceptance gates with unique project and complete case ownership for the active framework profile.");
        }
    }

    private static bool HasCompleteModularSqlOwnership(
        IReadOnlyList<LogicalCaseDefinition> logicalCases,
        IReadOnlyList<TestGateDefinition> sqlGates,
        IReadOnlyList<string> expectedFrameworks)
    {
        var gateProjects = sqlGates.Select(gate => gate.Project)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sqlCategories = sqlGates.SelectMany(gate => gate.Classification)
            .Where(category => !string.Equals(category, "NonSql", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var sqlCases = logicalCases.Where(item =>
            (gateProjects.Contains(item.OwnerProject) || item.Classification.Any(sqlCategories.Contains)) &&
            item.Frameworks.Any(expectedFrameworks.Contains)).ToArray();
        if (sqlCases.Length == 0)
        {
            return false;
        }

        foreach (var gate in sqlGates)
        {
            var owned = sqlCases.Where(item =>
                string.Equals(item.OwnerProject, gate.Project, StringComparison.OrdinalIgnoreCase) &&
                gate.Frameworks.All(tfm => item.Frameworks.Contains(tfm, StringComparer.Ordinal))).ToArray();
            if (owned.Length == 0 || gate.Classification.Any(category =>
                    !owned.Any(item => item.Classification.Contains(category, StringComparer.Ordinal))))
            {
                return false;
            }
        }

        return sqlCases.All(item => gateProjects.Contains(item.OwnerProject) &&
            sqlGates.Count(gate =>
                string.Equals(gate.Project, item.OwnerProject, StringComparison.OrdinalIgnoreCase) &&
                gate.Frameworks.All(tfm => item.Frameworks.Contains(tfm, StringComparer.Ordinal))) == 1);
    }

    private static void ValidateFrameworkProfile(
        TestGateManifest manifest,
        TestFrameworkProfile profile,
        string planId)
    {
        if (string.IsNullOrWhiteSpace(profile.ProfileId) ||
            !string.Equals(profile.PlanId, planId, StringComparison.Ordinal) ||
            profile.ActiveSqlFrameworks.Count == 0 ||
            profile.ActiveGuiFrameworks.Count == 0 ||
            profile.MinimumHistoricalSqlLogicalCases <= 0 ||
            profile.ActiveSqlFrameworks.Any(tfm => !profile.HistoricalSqlFrameworks.Contains(tfm, StringComparer.Ordinal)) ||
            profile.ActiveGuiFrameworks.Any(tfm => !profile.HistoricalGuiFrameworks.Contains(tfm, StringComparer.Ordinal)) ||
            profile.ToolOnlyProjects.Any(project =>
                string.Equals(project.Project, profile.SqlProject, StringComparison.Ordinal) ||
                string.Equals(project.Project, profile.GuiProject, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("The reviewed active/historical framework profile is invalid.");
        }

        var historicalSqlOwners = manifest.Gates
            .Where(gate => gate.StrictMode == TestGateStrictMode.SqlPrAcceptance)
            .Select(gate => gate.Project)
            .Append(profile.SqlProject)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var historicalSqlCases = manifest.LogicalCases.Count(item =>
            (historicalSqlOwners.Contains(item.OwnerProject) ||
             item.Classification.Any(SqlLogicalCaseCategories.Contains)) &&
            profile.HistoricalSqlFrameworks.All(tfm => item.Frameworks.Contains(tfm, StringComparer.Ordinal)));
        if (historicalSqlCases < profile.MinimumHistoricalSqlLogicalCases)
        {
            throw new InvalidDataException("The historical SQL logical-case inventory shrank below its reviewed floor.");
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
