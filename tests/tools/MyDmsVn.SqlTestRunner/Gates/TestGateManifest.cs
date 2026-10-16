using System;
using System.Collections.Generic;

namespace MyDmsVn.SqlTestRunner.Gates;

public enum TestGateMode
{
    Task,
    Final,
}

public enum TestGateSelectionKind
{
    UnfilteredProject,
    FullyQualifiedName,
    Category,
}

public enum TestGateResourceRequirement
{
    None,
    RequiredExclusive,
}

public enum TestGateStrictMode
{
    DotnetTest,
    SqlPrAcceptance,
}

public enum TestGateEvidenceKind
{
    Source,
    Discovery,
    Trx,
    SanitizedReport,
    Ownership,
}

public sealed record TestGateManifest(
    int SchemaVersion,
    IReadOnlyList<TestGateDefinition> Gates,
    IReadOnlyList<LogicalCaseDefinition> LogicalCases,
    IReadOnlyList<IdentityMappingDefinition> IdentityMappings,
    IReadOnlyList<TestGateExemptionDefinition> Exemptions);

public sealed record TestGateDefinition(
    string GateId,
    string PlanId,
    string TaskId,
    TestGateMode Mode,
    string Project,
    IReadOnlyList<string> Frameworks,
    string Module,
    IReadOnlyList<string> Classification,
    TestGateSelectionDefinition Selection,
    IReadOnlyList<TestGateBuildPrerequisite> BuildPrerequisites,
    TestGateResources Resources,
    TestGateStrictMode StrictMode,
    int CommandTimeoutSeconds,
    int OverallTimeoutSeconds,
    IReadOnlyList<TestGateEvidenceKind> Evidence);

public sealed record TestGateSelectionDefinition(
    TestGateSelectionKind Kind,
    IReadOnlyList<string> Patterns);

public sealed record TestGateBuildPrerequisite(
    string Project,
    IReadOnlyList<string> Frameworks);

public sealed record TestGateResources(
    TestGateResourceRequirement Sql,
    TestGateResourceRequirement Gui);

public sealed record LogicalCaseDefinition(
    string LogicalCaseId,
    string OwnerProject,
    IReadOnlyList<string> Frameworks,
    string NormalizedMethodSignature,
    string ReviewedCodeFingerprint,
    IReadOnlyList<string> Classification,
    IReadOnlyList<TheoryRowDefinition> Rows);

public sealed record TheoryRowDefinition(
    string RowKey,
    string? LocatorSha256);

public sealed record IdentityMappingDefinition(
    string MappingId,
    string OldLogicalCaseId,
    string NewLogicalCaseId,
    string Rationale);

public sealed record TestGateExemptionDefinition(
    string ExemptionId,
    string NormalizedMethodSignature,
    IReadOnlyList<string> Classification,
    string Rationale,
    string ReviewedCodeFingerprint);

public sealed record TestGateSelection(
    TestGateMode Mode,
    string PlanId,
    string TaskId,
    IReadOnlyList<TestGateDefinition> Gates);
