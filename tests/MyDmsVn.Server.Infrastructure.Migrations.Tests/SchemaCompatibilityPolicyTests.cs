using System.Collections.Generic;
using MyDmsVn.Server.Infrastructure.Operations;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SchemaCompatibilityPolicyTests
{
    private static readonly string[] CurrentScripts =
    {
        "001_PersistenceFoundation.sql",
        "002_Identity.sql",
        "003_Catalog.sql",
        "004_Catalog_Nul_Constraints.sql",
        "005_GoodsReceipt.sql",
        "006_GoodsReceipt_Number_Sequence.sql",
        "007_Inventory_Stock.sql",
        "008_Security_Administration.sql",
    };

    private static readonly string[] CurrentArtifacts =
    {
        "Invariant:PersistenceColumns",
        "Table:Users",
        "Table:Roles",
        "Table:UserRoles",
        "Table:RolePermissions",
        "Table:UserPermissions",
        "Table:SystemSecuritySettings",
        "Invariant:SystemSecuritySettings",
        "Table:SecurityAuditEvents",
        "Invariant:SecurityAdministration",
        "Procedure:ApplySecurityAdministration",
        "Invariant:IdentityColumns",
        "Invariant:IdentityChecks",
        "Invariant:IdentityPrimaryKeys",
        "Invariant:IdentityUniqueIndexes",
        "Invariant:IdentityForeignKeys",
        "Table:Products",
        "Table:Warehouses",
        "Table:Employees",
        "Table:Customers",
        "Invariant:CatalogColumns",
        "Invariant:CatalogChecks",
        "Invariant:CatalogPrimaryKeys",
        "Invariant:CatalogUniqueIndexes",
        "Invariant:CatalogForeignKeys",
        "Function:CatalogTextContainsNul",
        "Invariant:CatalogNulChecks",
        "Table:GoodsReceipts",
        "Table:GoodsReceiptLines",
        "Invariant:GoodsReceiptColumns",
        "Invariant:GoodsReceiptChecks",
        "Invariant:GoodsReceiptPrimaryKeys",
        "Invariant:GoodsReceiptUniqueIndexes",
        "Invariant:GoodsReceiptForeignKeys",
        "Sequence:GoodsReceiptNumberSequence",
        "Table:StockLedger",
        "Table:StockBalances",
        "Invariant:InventoryColumns",
        "Invariant:InventoryChecks",
        "Invariant:InventoryPrimaryKeys",
        "Invariant:InventoryUniqueIndexes",
        "Invariant:InventoryForeignKeys",
        "Trigger:TR_StockLedger_AppendOnly",
    };

    [Fact]
    public void Exact_history_and_artifacts_are_compatible()
    {
        var result = Evaluate(CurrentScripts, CurrentArtifacts);

        Assert.True(result.IsCompatible);
        Assert.Equal(SchemaCompatibilityStatus.Compatible, result.Status);
        Assert.Equal("Schema.Compatible", result.Code);
    }

    [Fact]
    public void Missing_journal_is_empty_and_fails_closed()
    {
        var result = SchemaCompatibilityPolicy.Evaluate(
            journalExists: false,
            new string[0],
            new HashSet<string>());

        Assert.False(result.IsCompatible);
        Assert.Equal(SchemaCompatibilityStatus.Empty, result.Status);
        Assert.Equal("Schema.Empty", result.Code);
    }

    [Fact]
    public void Valid_prefix_without_future_artifacts_is_too_old()
    {
        var result = Evaluate(
            new[]
            {
                CurrentScripts[0],
                CurrentScripts[1],
                CurrentScripts[2],
                CurrentScripts[3],
                CurrentScripts[4],
                CurrentScripts[5],
            },
            new[]
            {
                "Invariant:PersistenceColumns",
                "Table:Users",
                "Table:Roles",
                "Table:UserRoles",
                "Table:RolePermissions",
                "Table:UserPermissions",
                "Table:SystemSecuritySettings",
                "Invariant:SystemSecuritySettings",
                "Invariant:IdentityColumns",
                "Table:Products",
                "Table:Warehouses",
                "Table:Employees",
                "Table:Customers",
                "Invariant:CatalogColumns",
                "Function:CatalogTextContainsNul",
                "Invariant:IdentityChecks",
                "Invariant:IdentityPrimaryKeys",
                "Invariant:IdentityUniqueIndexes",
                "Invariant:IdentityForeignKeys",
                "Invariant:CatalogChecks",
                "Invariant:CatalogPrimaryKeys",
                "Invariant:CatalogUniqueIndexes",
                "Invariant:CatalogForeignKeys",
                "Invariant:CatalogNulChecks",
                "Table:GoodsReceipts",
                "Table:GoodsReceiptLines",
                "Invariant:GoodsReceiptColumns",
                "Invariant:GoodsReceiptChecks",
                "Invariant:GoodsReceiptPrimaryKeys",
                "Invariant:GoodsReceiptUniqueIndexes",
                "Invariant:GoodsReceiptForeignKeys",
                "Sequence:GoodsReceiptNumberSequence",
            });

        Assert.Equal(SchemaCompatibilityStatus.TooOld, result.Status);
        Assert.Equal("Schema.TooOld", result.Code);
    }

    [Fact]
    public void Known_future_artifact_without_its_journal_entry_is_partial()
    {
        var scripts = new List<string>(CurrentScripts);
        scripts.RemoveAt(scripts.Count - 1);

        var result = Evaluate(scripts, CurrentArtifacts);

        Assert.Equal(SchemaCompatibilityStatus.Partial, result.Status);
        Assert.Equal("Schema.PartialMigration", result.Code);
    }

    [Fact]
    public void Current_history_missing_required_artifact_is_partial()
    {
        var artifacts = new List<string>(CurrentArtifacts);
        artifacts.Remove("Table:StockLedger");

        var result = Evaluate(CurrentScripts, artifacts);

        Assert.Equal(SchemaCompatibilityStatus.Partial, result.Status);
        Assert.Equal("Schema.PartialMigration", result.Code);
    }

    [Fact]
    public void Reordered_or_missing_middle_history_is_unknown()
    {
        var scripts = new List<string>(CurrentScripts);
        scripts.RemoveAt(3);

        var result = Evaluate(scripts, CurrentArtifacts);

        Assert.Equal(SchemaCompatibilityStatus.UnknownHistory, result.Status);
        Assert.Equal("Schema.UnknownHistory", result.Code);
    }

    [Fact]
    public void Appended_unknown_script_is_too_new()
    {
        var scripts = new List<string>(CurrentScripts)
        {
            "008_Future.sql",
        };

        var result = Evaluate(scripts, CurrentArtifacts);

        Assert.Equal(SchemaCompatibilityStatus.TooNew, result.Status);
        Assert.Equal("Schema.TooNew", result.Code);
    }

    [Fact]
    public void Appended_script_with_future_artifact_is_too_new()
    {
        var scripts = new List<string>(CurrentScripts)
        {
            "008_Future.sql",
        };
        var artifacts = new List<string>(CurrentArtifacts)
        {
            "Unknown:UserObject",
        };

        var result = Evaluate(scripts, artifacts);

        Assert.Equal(SchemaCompatibilityStatus.TooNew, result.Status);
        Assert.Equal("Schema.TooNew", result.Code);
    }

    private static SchemaCompatibilityResult Evaluate(
        IEnumerable<string> scripts,
        IEnumerable<string> artifacts)
    {
        return SchemaCompatibilityPolicy.Evaluate(
            journalExists: true,
            new List<string>(scripts),
            new HashSet<string>(artifacts));
    }
}
