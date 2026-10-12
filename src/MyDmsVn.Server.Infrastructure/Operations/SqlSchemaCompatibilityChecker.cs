using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.Operations;

public sealed class SqlSchemaCompatibilityChecker : ISchemaCompatibilityChecker
{
    private const string ArtifactSql =
        "SELECT ArtifactKey FROM (" +
        "SELECT N'Table:Users' AS ArtifactKey, OBJECT_ID(N'dbo.Users', N'U') AS ObjectId UNION ALL " +
        "SELECT N'Table:Roles', OBJECT_ID(N'dbo.Roles', N'U') UNION ALL " +
        "SELECT N'Table:UserRoles', OBJECT_ID(N'dbo.UserRoles', N'U') UNION ALL " +
        "SELECT N'Table:RolePermissions', OBJECT_ID(N'dbo.RolePermissions', N'U') UNION ALL " +
        "SELECT N'Table:UserPermissions', OBJECT_ID(N'dbo.UserPermissions', N'U') UNION ALL " +
        "SELECT N'Invariant:IdentityChecks', CASE WHEN (SELECT COUNT(*) FROM sys.check_constraints " +
        "WHERE SCHEMA_NAME(schema_id) = N'dbo' AND is_disabled = 0 AND is_not_trusted = 0 AND name IN (" +
        "N'CK_Users_Username_NotBlank', N'CK_Users_NormalizedUsername_NotBlank', " +
        "N'CK_Users_DisplayName_NotBlank', N'CK_Users_Source_NotBlank', " +
        "N'CK_Users_PasswordAlgorithm_NotBlank', N'CK_Roles_RoleName_NotBlank', " +
        "N'CK_Roles_NormalizedRoleName_NotBlank', N'CK_RolePermissions_PermissionKey_NotBlank', " +
        "N'CK_UserPermissions_PermissionKey_NotBlank')) = 9 THEN 1 END UNION ALL " +
        "SELECT N'Table:Products', OBJECT_ID(N'dbo.Products', N'U') UNION ALL " +
        "SELECT N'Table:Warehouses', OBJECT_ID(N'dbo.Warehouses', N'U') UNION ALL " +
        "SELECT N'Table:Employees', OBJECT_ID(N'dbo.Employees', N'U') UNION ALL " +
        "SELECT N'Table:Customers', OBJECT_ID(N'dbo.Customers', N'U') UNION ALL " +
        "SELECT N'Invariant:CatalogChecks', CASE WHEN (SELECT COUNT(*) FROM sys.check_constraints " +
        "WHERE SCHEMA_NAME(schema_id) = N'dbo' AND is_disabled = 0 AND is_not_trusted = 0 AND name IN (" +
        "N'CK_Products_Code_NotBlank', N'CK_Products_Name_NotBlank', N'CK_Products_Unit_NotBlank', " +
        "N'CK_Warehouses_Code_NotBlank', N'CK_Warehouses_Name_NotBlank', " +
        "N'CK_Employees_Code_NotBlank', N'CK_Employees_Name_NotBlank', " +
        "N'CK_Customers_Code_NotBlank', N'CK_Customers_Name_NotBlank')) = 9 THEN 1 END UNION ALL " +
        "SELECT N'Function:CatalogTextContainsNul', OBJECT_ID(N'dbo.CatalogTextContainsNul', N'FN') UNION ALL " +
        "SELECT N'Invariant:CatalogNulChecks', CASE WHEN (SELECT COUNT(*) FROM sys.check_constraints " +
        "WHERE SCHEMA_NAME(schema_id) = N'dbo' AND is_disabled = 0 AND is_not_trusted = 0 AND name IN (" +
        "N'CK_Products_Code_NoNul', N'CK_Products_Name_NoNul', N'CK_Products_Unit_NoNul', " +
        "N'CK_Warehouses_Code_NoNul', N'CK_Warehouses_Name_NoNul', " +
        "N'CK_Employees_Code_NoNul', N'CK_Employees_Name_NoNul', " +
        "N'CK_Customers_Code_NoNul', N'CK_Customers_Name_NoNul')) = 9 THEN 1 END UNION ALL " +
        "SELECT N'Table:GoodsReceipts', OBJECT_ID(N'dbo.GoodsReceipts', N'U') UNION ALL " +
        "SELECT N'Table:GoodsReceiptLines', OBJECT_ID(N'dbo.GoodsReceiptLines', N'U') UNION ALL " +
        "SELECT N'Invariant:GoodsReceiptChecks', CASE WHEN (SELECT COUNT(*) FROM sys.check_constraints " +
        "WHERE SCHEMA_NAME(schema_id) = N'dbo' AND is_disabled = 0 AND is_not_trusted = 0 AND name IN (" +
        "N'CK_GoodsReceipts_ReceiptNo_NotBlank', N'CK_GoodsReceipts_ReceiptNo_NoNul', " +
        "N'CK_GoodsReceipts_Status', N'CK_GoodsReceipts_PostingAudit', " +
        "N'CK_GoodsReceiptLines_LineNo', N'CK_GoodsReceiptLines_Quantity', " +
        "N'CK_GoodsReceiptLines_UnitCost')) = 7 THEN 1 END UNION ALL " +
        "SELECT N'Sequence:GoodsReceiptNumberSequence', OBJECT_ID(N'dbo.GoodsReceiptNumberSequence', N'SO') UNION ALL " +
        "SELECT N'Table:StockLedger', OBJECT_ID(N'dbo.StockLedger', N'U') UNION ALL " +
        "SELECT N'Table:StockBalances', OBJECT_ID(N'dbo.StockBalances', N'U') UNION ALL " +
        "SELECT N'Invariant:InventoryChecks', CASE WHEN (SELECT COUNT(*) FROM sys.check_constraints " +
        "WHERE SCHEMA_NAME(schema_id) = N'dbo' AND is_disabled = 0 AND is_not_trusted = 0 AND name IN (" +
        "N'CK_StockLedger_DocumentType_NotBlank', N'CK_StockLedger_DocumentType_NoNul', " +
        "N'CK_StockLedger_QuantityChange_NonZero')) = 3 THEN 1 END UNION ALL " +
        "SELECT N'Trigger:TR_StockLedger_AppendOnly', (SELECT object_id FROM sys.triggers " +
        "WHERE object_id = OBJECT_ID(N'dbo.TR_StockLedger_AppendOnly', N'TR') " +
        "AND parent_id = OBJECT_ID(N'dbo.StockLedger', N'U') AND is_disabled = 0)" +
        ") AS Artifacts WHERE ObjectId IS NOT NULL " +
        "UNION ALL SELECT N'Unknown:UserObject' WHERE EXISTS (" +
        "SELECT 1 FROM sys.objects AS candidate " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = candidate.schema_id " +
        "WHERE candidate.is_ms_shipped = 0 " +
        "AND candidate.type IN (N'U', N'V', N'P', N'FN', N'IF', N'TF', N'SO', N'TR') " +
        "AND (ownerSchema.name <> N'dbo' OR candidate.name NOT IN (" +
        "N'SchemaVersions', N'Users', N'Roles', N'UserRoles', N'RolePermissions', N'UserPermissions', " +
        "N'Products', N'Warehouses', N'Employees', N'Customers', N'CatalogTextContainsNul', " +
        "N'GoodsReceipts', N'GoodsReceiptLines', N'GoodsReceiptNumberSequence', " +
        "N'StockLedger', N'StockBalances', N'TR_StockLedger_AppendOnly')));";

    private readonly string _connectionString;

    public SqlSchemaCompatibilityChecker(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "A SQL Server connection string is required.",
                nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<SchemaCompatibilityResult> CheckAsync(
        CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await CheckConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SchemaCompatibilityResult> CheckConnectionAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            throw new ArgumentException("The SQL connection must be open.", nameof(connection));
        }

        var journalExists = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL THEN 0 ELSE 1 END;",
                cancellationToken: cancellationToken)).ConfigureAwait(false) == 1;

        IReadOnlyList<string> scripts;
        if (journalExists)
        {
            var applied = await connection.QueryAsync<string>(
                new CommandDefinition(
                    "SELECT ScriptName FROM dbo.SchemaVersions ORDER BY Id;",
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            scripts = applied.ToList();
        }
        else
        {
            scripts = Array.Empty<string>();
        }

        var artifactRows = await connection.QueryAsync<string>(
            new CommandDefinition(
                ArtifactSql,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        var artifacts = new HashSet<string>(artifactRows, StringComparer.Ordinal);

        return SchemaCompatibilityPolicy.Evaluate(
            journalExists,
            scripts,
            artifacts);
    }
}
