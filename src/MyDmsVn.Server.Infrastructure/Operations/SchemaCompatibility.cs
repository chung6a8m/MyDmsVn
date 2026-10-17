using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.Operations;

public interface ISchemaCompatibilityChecker
{
    Task<SchemaCompatibilityResult> CheckAsync(CancellationToken cancellationToken);
}

public enum SchemaCompatibilityStatus
{
    Compatible,
    Empty,
    TooOld,
    TooNew,
    UnknownHistory,
    Partial,
}

public sealed class SchemaCompatibilityResult
{
    public SchemaCompatibilityResult(
        SchemaCompatibilityStatus status,
        string code,
        string safeMessage)
    {
        Status = status;
        Code = code;
        SafeMessage = safeMessage;
    }

    public SchemaCompatibilityStatus Status { get; }

    public string Code { get; }

    public string SafeMessage { get; }

    public bool IsCompatible => Status == SchemaCompatibilityStatus.Compatible;
}

public static class SchemaCompatibilityPolicy
{
    public static readonly IReadOnlyList<string> CurrentScripts = new[]
    {
        "001_PersistenceFoundation.sql",
        "002_Identity.sql",
        "003_Catalog.sql",
        "004_Catalog_Nul_Constraints.sql",
        "005_GoodsReceipt.sql",
        "006_GoodsReceipt_Number_Sequence.sql",
        "007_Inventory_Stock.sql",
        "008_Security_Administration.sql",
        "009_Security_Permission_Import.sql",
    };

    private static readonly SchemaArtifact[] KnownArtifacts =
    {
        new SchemaArtifact("Invariant:PersistenceColumns", 1),
        new SchemaArtifact("Table:Users", 2),
        new SchemaArtifact("Table:Roles", 2),
        new SchemaArtifact("Table:UserRoles", 2),
        new SchemaArtifact("Table:RolePermissions", 2),
        new SchemaArtifact("Table:UserPermissions", 2),
        new SchemaArtifact("Table:SystemSecuritySettings", 2),
        new SchemaArtifact("Invariant:SystemSecuritySettings", 2),
        new SchemaArtifact("Table:SecurityAuditEvents", 8),
        new SchemaArtifact("Invariant:SecurityAdministration", 8),
        new SchemaArtifact("Procedure:ApplySecurityAdministration", 8),
        new SchemaArtifact("Procedure:ReadSecurityPermissionImportTarget", 9),
        new SchemaArtifact("Procedure:ApplySecurityPermissionImport", 9),
        new SchemaArtifact("Invariant:IdentityColumns", 2),
        new SchemaArtifact("Invariant:IdentityChecks", 2),
        new SchemaArtifact("Invariant:IdentityPrimaryKeys", 2),
        new SchemaArtifact("Invariant:IdentityUniqueIndexes", 2),
        new SchemaArtifact("Invariant:IdentityForeignKeys", 2),
        new SchemaArtifact("Table:Products", 3),
        new SchemaArtifact("Table:Warehouses", 3),
        new SchemaArtifact("Table:Employees", 3),
        new SchemaArtifact("Table:Customers", 3),
        new SchemaArtifact("Invariant:CatalogColumns", 3),
        new SchemaArtifact("Invariant:CatalogChecks", 3),
        new SchemaArtifact("Invariant:CatalogPrimaryKeys", 3),
        new SchemaArtifact("Invariant:CatalogUniqueIndexes", 3),
        new SchemaArtifact("Invariant:CatalogForeignKeys", 3),
        new SchemaArtifact("Function:CatalogTextContainsNul", 4),
        new SchemaArtifact("Invariant:CatalogNulChecks", 4),
        new SchemaArtifact("Table:GoodsReceipts", 5),
        new SchemaArtifact("Table:GoodsReceiptLines", 5),
        new SchemaArtifact("Invariant:GoodsReceiptColumns", 5),
        new SchemaArtifact("Invariant:GoodsReceiptChecks", 5),
        new SchemaArtifact("Invariant:GoodsReceiptPrimaryKeys", 5),
        new SchemaArtifact("Invariant:GoodsReceiptUniqueIndexes", 5),
        new SchemaArtifact("Invariant:GoodsReceiptForeignKeys", 5),
        new SchemaArtifact("Sequence:GoodsReceiptNumberSequence", 6),
        new SchemaArtifact("Table:StockLedger", 7),
        new SchemaArtifact("Table:StockBalances", 7),
        new SchemaArtifact("Invariant:InventoryColumns", 7),
        new SchemaArtifact("Invariant:InventoryChecks", 7),
        new SchemaArtifact("Invariant:InventoryPrimaryKeys", 7),
        new SchemaArtifact("Invariant:InventoryUniqueIndexes", 7),
        new SchemaArtifact("Invariant:InventoryForeignKeys", 7),
        new SchemaArtifact("Trigger:TR_StockLedger_AppendOnly", 7),
    };

    public static SchemaCompatibilityResult Evaluate(
        bool journalExists,
        IReadOnlyList<string> appliedScripts,
        ISet<string> existingArtifacts)
    {
        if (appliedScripts == null)
        {
            throw new ArgumentNullException(nameof(appliedScripts));
        }

        if (existingArtifacts == null)
        {
            throw new ArgumentNullException(nameof(existingArtifacts));
        }

        if (!journalExists)
        {
            if (ContainsUnknownArtifact(existingArtifacts))
            {
                return UnknownHistory();
            }

            return ContainsAnyKnownArtifact(existingArtifacts)
                ? Partial()
                : Empty();
        }

        var normalizedScripts = new List<string>(appliedScripts.Count);
        var uniqueScripts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var appliedScript in appliedScripts)
        {
            var normalized = NormalizeScriptName(appliedScript);
            if (!uniqueScripts.Add(normalized))
            {
                return UnknownHistory();
            }

            normalizedScripts.Add(normalized);
        }

        var comparableCount = Math.Min(normalizedScripts.Count, CurrentScripts.Count);
        for (var index = 0; index < comparableCount; index++)
        {
            if (!string.Equals(
                    normalizedScripts[index],
                    CurrentScripts[index],
                    StringComparison.Ordinal))
            {
                return UnknownHistory();
            }
        }

        if (normalizedScripts.Count > CurrentScripts.Count)
        {
            return TooNew();
        }

        if (ContainsUnknownArtifact(existingArtifacts))
        {
            return UnknownHistory();
        }

        if (!ArtifactsMatchAppliedPrefix(normalizedScripts.Count, existingArtifacts))
        {
            return Partial();
        }

        if (normalizedScripts.Count < CurrentScripts.Count)
        {
            return normalizedScripts.Count == 0 ? Empty() : TooOld();
        }

        return new SchemaCompatibilityResult(
            SchemaCompatibilityStatus.Compatible,
            "Schema.Compatible",
            "Phiên bản cơ sở dữ liệu tương thích với ứng dụng.");
    }

    private static bool ArtifactsMatchAppliedPrefix(
        int appliedScriptCount,
        ISet<string> existingArtifacts)
    {
        foreach (var artifact in KnownArtifacts)
        {
            var shouldExist = artifact.IntroducedByScript <= appliedScriptCount;
            if (existingArtifacts.Contains(artifact.Key) != shouldExist)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsAnyKnownArtifact(ISet<string> existingArtifacts)
    {
        foreach (var artifact in KnownArtifacts)
        {
            if (existingArtifacts.Contains(artifact.Key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsUnknownArtifact(ISet<string> existingArtifacts)
    {
        foreach (var artifact in existingArtifacts)
        {
            if (artifact.StartsWith("Unknown:", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeScriptName(string scriptName)
    {
        if (string.IsNullOrWhiteSpace(scriptName))
        {
            return string.Empty;
        }

        const string marker = ".Scripts.";
        var markerIndex = scriptName.LastIndexOf(marker, StringComparison.Ordinal);
        return markerIndex < 0
            ? scriptName.Trim()
            : scriptName.Substring(markerIndex + marker.Length).Trim();
    }

    private static SchemaCompatibilityResult Empty()
    {
        return new SchemaCompatibilityResult(
            SchemaCompatibilityStatus.Empty,
            "Schema.Empty",
            "Cơ sở dữ liệu chưa được cài đặt. Hãy chạy gói migration bằng tài khoản quản trị.");
    }

    private static SchemaCompatibilityResult TooOld()
    {
        return new SchemaCompatibilityResult(
            SchemaCompatibilityStatus.TooOld,
            "Schema.TooOld",
            "Cơ sở dữ liệu cũ hơn phiên bản ứng dụng. Hãy hoàn tất nâng cấp trước khi mở ứng dụng.");
    }

    private static SchemaCompatibilityResult TooNew()
    {
        return new SchemaCompatibilityResult(
            SchemaCompatibilityStatus.TooNew,
            "Schema.TooNew",
            "Cơ sở dữ liệu mới hơn phiên bản ứng dụng. Hãy cài đúng phiên bản ứng dụng được hỗ trợ.");
    }

    private static SchemaCompatibilityResult UnknownHistory()
    {
        return new SchemaCompatibilityResult(
            SchemaCompatibilityStatus.UnknownHistory,
            "Schema.UnknownHistory",
            "Lịch sử migration không được nhận dạng. Ứng dụng đã chặn truy cập để bảo vệ dữ liệu.");
    }

    private static SchemaCompatibilityResult Partial()
    {
        return new SchemaCompatibilityResult(
            SchemaCompatibilityStatus.Partial,
            "Schema.PartialMigration",
            "Phát hiện migration chưa hoàn tất hoặc schema không khớp lịch sử. Cần quản trị viên xử lý phục hồi.");
    }

    private sealed class SchemaArtifact
    {
        public SchemaArtifact(string key, int introducedByScript)
        {
            Key = key;
            IntroducedByScript = introducedByScript;
        }

        public string Key { get; }

        public int IntroducedByScript { get; }
    }
}
