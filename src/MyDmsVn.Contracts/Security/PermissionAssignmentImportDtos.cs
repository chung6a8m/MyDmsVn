using System.Collections.Generic;

namespace MyDmsVn.Contracts.Security;

public static class PermissionAssignmentImportLimits
{
    public const int MaximumUtf8Bytes = 1024 * 1024;
    public const int MaximumEntries = 5000;
}

public enum PermissionImportTargetTypeDto
{
    User = 0,
    Role = 1,
}

public sealed class PermissionAssignmentImportEntryDto
{
    public PermissionAssignmentImportEntryDto(string permissionKey, bool granted)
    {
        PermissionKey = permissionKey;
        Granted = granted;
    }

    public string PermissionKey { get; }
    public bool Granted { get; }
}

public sealed class ParsedPermissionAssignmentImportDto
{
    public ParsedPermissionAssignmentImportDto(
        int schemaVersion,
        PermissionImportTargetTypeDto targetType,
        int targetId,
        IReadOnlyList<PermissionAssignmentImportEntryDto> entries)
    {
        SchemaVersion = schemaVersion;
        TargetType = targetType;
        TargetId = targetId;
        Entries = entries;
    }

    public int SchemaVersion { get; }
    public PermissionImportTargetTypeDto TargetType { get; }
    public int TargetId { get; }
    public IReadOnlyList<PermissionAssignmentImportEntryDto> Entries { get; }
}

public sealed class PermissionAssignmentImportPreviewDto
{
    public PermissionAssignmentImportPreviewDto(
        ParsedPermissionAssignmentImportDto import,
        string targetName,
        long targetRevision,
        string catalogDigest,
        IReadOnlyList<PermissionAssignmentImportEntryDto> additions)
    {
        Import = import;
        TargetName = targetName;
        TargetRevision = targetRevision;
        CatalogDigest = catalogDigest;
        Additions = additions;
    }

    public ParsedPermissionAssignmentImportDto Import { get; }
    public string TargetName { get; }
    public long TargetRevision { get; }
    public string CatalogDigest { get; }
    public IReadOnlyList<PermissionAssignmentImportEntryDto> Additions { get; }
}

public sealed class PermissionAssignmentImportRequest
{
    public PermissionAssignmentImportRequest(
        string json,
        long expectedTargetRevision,
        string expectedCatalogDigest)
    {
        Json = json;
        ExpectedTargetRevision = expectedTargetRevision;
        ExpectedCatalogDigest = expectedCatalogDigest;
    }

    public string Json { get; }
    public long ExpectedTargetRevision { get; }
    public string ExpectedCatalogDigest { get; }
}

public sealed class PermissionAssignmentImportResultDto
{
    public PermissionAssignmentImportResultDto(long targetRevision, int addedCount, bool changed)
    {
        TargetRevision = targetRevision;
        AddedCount = addedCount;
        Changed = changed;
    }

    public long TargetRevision { get; }
    public int AddedCount { get; }
    public bool Changed { get; }
}
