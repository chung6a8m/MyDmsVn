using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyDmsVn.SqlTestRunner.Gates;

public static class TestGateManifestLoader
{
    private const int SupportedSchemaVersion = 1;

    public static TestGateManifest Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidDataException("The test-gate manifest file does not exist.");
        }

        ManifestDocument document;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            };
            options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
            document = JsonSerializer.Deserialize<ManifestDocument>(File.ReadAllText(path), options)
                ?? throw new InvalidDataException("The test-gate manifest must contain a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The test-gate manifest must match the exact supported schema.",
                exception);
        }

        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported test-gate manifest schemaVersion '{document.SchemaVersion}'.");
        }

        var gates = Required(document.Gates, "gates").Select(ToGate).ToArray();
        var logicalCases = Required(document.LogicalCases, "logicalCases")
            .Select(ToLogicalCase)
            .ToArray();
        var mappings = Required(document.IdentityMappings, "identityMappings")
            .Select(ToMapping)
            .ToArray();
        var exemptions = Required(document.Exemptions, "exemptions")
            .Select(ToExemption)
            .ToArray();

        RequireUnique(gates.Select(item => item.GateId), "gateId");
        RequireUnique(logicalCases.Select(item => item.LogicalCaseId), "logicalCaseId");
        RequireUnique(mappings.Select(item => item.MappingId), "mappingId");
        RequireUnique(exemptions.Select(item => item.ExemptionId), "exemptionId");

        return new TestGateManifest(
            document.SchemaVersion,
            gates,
            logicalCases,
            mappings,
            exemptions);
    }

    private static TestGateDefinition ToGate(GateDocument value)
    {
        var selection = Required(value.Selection, "selection");
        var resources = Required(value.Resources, "resources");
        return new TestGateDefinition(
            Required(value.GateId, "gateId"),
            Required(value.PlanId, "planId"),
            Required(value.TaskId, "taskId"),
            value.Mode,
            Required(value.Project, "project"),
            RequiredStrings(value.Frameworks, "frameworks"),
            Required(value.Module, "module"),
            RequiredStrings(value.Classification, "classification"),
            new TestGateSelectionDefinition(
                selection.Kind,
                Required(selection.Patterns, "selection.patterns")),
            Required(value.BuildPrerequisites, "buildPrerequisites")
                .Select(item => new TestGateBuildPrerequisite(
                    Required(item.Project, "buildPrerequisites.project"),
                    RequiredStrings(item.Frameworks, "buildPrerequisites.frameworks")))
                .ToArray(),
            new TestGateResources(resources.Sql, resources.Gui),
            value.StrictMode,
            Positive(value.CommandTimeoutSeconds, "commandTimeoutSeconds"),
            Positive(value.OverallTimeoutSeconds, "overallTimeoutSeconds"),
            Required(value.Evidence, "evidence"));
    }

    private static LogicalCaseDefinition ToLogicalCase(LogicalCaseDocument value) =>
        new LogicalCaseDefinition(
            Required(value.LogicalCaseId, "logicalCaseId"),
            Required(value.OwnerProject, "ownerProject"),
            RequiredStrings(value.Frameworks, "logicalCases.frameworks"),
            Required(value.NormalizedMethodSignature, "normalizedMethodSignature"),
            RequiredSha256(value.ReviewedCodeFingerprint, "reviewedCodeFingerprint"),
            RequiredStrings(value.Classification, "logicalCases.classification"),
            Required(value.Rows, "rows")
                .Select(row => new TheoryRowDefinition(
                    Required(row.RowKey, "rowKey"),
                    OptionalSha256(row.LocatorSha256, "locatorSha256")))
                .ToArray());

    private static IdentityMappingDefinition ToMapping(IdentityMappingDocument value) =>
        new IdentityMappingDefinition(
            Required(value.MappingId, "mappingId"),
            Required(value.OldLogicalCaseId, "oldLogicalCaseId"),
            Required(value.NewLogicalCaseId, "newLogicalCaseId"),
            Required(value.Rationale, "rationale"));

    private static TestGateExemptionDefinition ToExemption(ExemptionDocument value) =>
        new TestGateExemptionDefinition(
            Required(value.ExemptionId, "exemptionId"),
            Required(value.NormalizedMethodSignature, "normalizedMethodSignature"),
            RequiredStrings(value.Classification, "exemptions.classification"),
            Required(value.Rationale, "rationale"),
            RequiredSha256(value.ReviewedCodeFingerprint, "reviewedCodeFingerprint"));

    private static T Required<T>(T? value, string name) where T : class =>
        value ?? throw new InvalidDataException($"The test-gate manifest field '{name}' is required.");

    private static string Required(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The test-gate manifest field '{name}' is required.")
            : value;

    private static IReadOnlyList<string> RequiredStrings(IReadOnlyList<string>? values, string name)
    {
        var required = Required(values, name);
        if (required.Count == 0 || required.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException(
                $"The test-gate manifest field '{name}' requires non-empty values.");
        }

        return required;
    }

    private static int Positive(int value, string name) => value <= 0
        ? throw new InvalidDataException($"The test-gate manifest field '{name}' must be positive.")
        : value;

    private static string RequiredSha256(string? value, string name)
    {
        var required = Required(value, name);
        return IsSha256(required)
            ? required
            : throw new InvalidDataException(
                $"The test-gate manifest field '{name}' must be a lowercase SHA-256 value.");
    }

    private static string? OptionalSha256(string? value, string name) => value is null
        ? null
        : IsSha256(value)
            ? value
            : throw new InvalidDataException(
                $"The test-gate manifest field '{name}' must be null or a lowercase SHA-256 value.");

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireUnique(IEnumerable<string> values, string name)
    {
        if (values.GroupBy(value => value, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            throw new InvalidDataException(
                $"The test-gate manifest contains a duplicate '{name}'.");
        }
    }

    private sealed class ManifestDocument
    {
        [JsonRequired]
        public int SchemaVersion { get; set; }

        [JsonRequired]
        public GateDocument[]? Gates { get; set; }

        [JsonRequired]
        public LogicalCaseDocument[]? LogicalCases { get; set; }

        [JsonRequired]
        public IdentityMappingDocument[]? IdentityMappings { get; set; }

        [JsonRequired]
        public ExemptionDocument[]? Exemptions { get; set; }
    }

    private sealed class GateDocument
    {
        [JsonRequired]
        public string? GateId { get; set; }

        [JsonRequired]
        public string? PlanId { get; set; }

        [JsonRequired]
        public string? TaskId { get; set; }

        [JsonRequired]
        public TestGateMode Mode { get; set; }

        [JsonRequired]
        public string? Project { get; set; }

        [JsonRequired]
        public string[]? Frameworks { get; set; }

        [JsonRequired]
        public string? Module { get; set; }

        [JsonRequired]
        public string[]? Classification { get; set; }

        [JsonRequired]
        public SelectionDocument? Selection { get; set; }

        [JsonRequired]
        public BuildPrerequisiteDocument[]? BuildPrerequisites { get; set; }

        [JsonRequired]
        public ResourceDocument? Resources { get; set; }

        [JsonRequired]
        public TestGateStrictMode StrictMode { get; set; }

        [JsonRequired]
        public int CommandTimeoutSeconds { get; set; }

        [JsonRequired]
        public int OverallTimeoutSeconds { get; set; }

        [JsonRequired]
        public TestGateEvidenceKind[]? Evidence { get; set; }
    }

    private sealed class SelectionDocument
    {
        [JsonRequired]
        public TestGateSelectionKind Kind { get; set; }

        [JsonRequired]
        public string[]? Patterns { get; set; }
    }

    private sealed class BuildPrerequisiteDocument
    {
        [JsonRequired]
        public string? Project { get; set; }

        [JsonRequired]
        public string[]? Frameworks { get; set; }
    }

    private sealed class ResourceDocument
    {
        [JsonRequired]
        public TestGateResourceRequirement Sql { get; set; }

        [JsonRequired]
        public TestGateResourceRequirement Gui { get; set; }
    }

    private sealed class LogicalCaseDocument
    {
        [JsonRequired]
        public string? LogicalCaseId { get; set; }

        [JsonRequired]
        public string? OwnerProject { get; set; }

        [JsonRequired]
        public string[]? Frameworks { get; set; }

        [JsonRequired]
        public string? NormalizedMethodSignature { get; set; }

        [JsonRequired]
        public string? ReviewedCodeFingerprint { get; set; }

        [JsonRequired]
        public string[]? Classification { get; set; }

        [JsonRequired]
        public TheoryRowDocument[]? Rows { get; set; }
    }

    private sealed class TheoryRowDocument
    {
        [JsonRequired]
        public string? RowKey { get; set; }

        [JsonRequired]
        public string? LocatorSha256 { get; set; }
    }

    private sealed class IdentityMappingDocument
    {
        [JsonRequired]
        public string? MappingId { get; set; }

        [JsonRequired]
        public string? OldLogicalCaseId { get; set; }

        [JsonRequired]
        public string? NewLogicalCaseId { get; set; }

        [JsonRequired]
        public string? Rationale { get; set; }
    }

    private sealed class ExemptionDocument
    {
        [JsonRequired]
        public string? ExemptionId { get; set; }

        [JsonRequired]
        public string? NormalizedMethodSignature { get; set; }

        [JsonRequired]
        public string[]? Classification { get; set; }

        [JsonRequired]
        public string? Rationale { get; set; }

        [JsonRequired]
        public string? ReviewedCodeFingerprint { get; set; }
    }
}
