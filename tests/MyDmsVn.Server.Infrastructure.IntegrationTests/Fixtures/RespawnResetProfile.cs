using System;
using System.Data;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Operations;
using Respawn;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// One immutable application-schema checkpoint for one positively owned allocation.
// Test-only: never pass an arbitrary application connection string to this profile.
internal sealed class RespawnResetProfile
{
    private const string ExpectedTrigger = @"CREATE TRIGGER dbo.TR_StockLedger_AppendOnly
ON dbo.StockLedger
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, 'StockLedger is append-only.', 1;
END;";

    private static readonly string[] AppendOnlyTables = { "StockLedger", "StockValuationEntries" };

    private static readonly string[] MutableTables =
    {
        "Users", "Roles", "UserRoles", "RolePermissions", "UserPermissions", "SystemSecuritySettings",
        "Products", "Warehouses", "Employees", "Customers", "GoodsReceipts", "GoodsReceiptLines",
        "StockLedger", "StockBalances", "SecurityAuditEvents",
        "SalesReceipts", "SalesReceiptLines", "StockValuationEntries",
    };

    private static readonly string[] AllowedConstraints =
    {
        "CK_Customers_Code_NoNul",
        "CK_Customers_Code_NotBlank",
        "CK_Customers_Name_NoNul",
        "CK_Customers_Name_NotBlank",
        "CK_Employees_Code_NoNul",
        "CK_Employees_Code_NotBlank",
        "CK_Employees_Name_NoNul",
        "CK_Employees_Name_NotBlank",
        "CK_GoodsReceiptLines_LineNo",
        "CK_GoodsReceiptLines_Quantity",
        "CK_GoodsReceiptLines_UnitCost",
        "CK_GoodsReceipts_PostingAudit",
        "CK_GoodsReceipts_ReceiptNo_NoNul",
        "CK_GoodsReceipts_ReceiptNo_NotBlank",
        "CK_GoodsReceipts_Status",
        "CK_Products_Code_NoNul",
        "CK_Products_Code_NotBlank",
        "CK_Products_Name_NoNul",
        "CK_Products_Name_NotBlank",
        "CK_Products_Unit_NoNul",
        "CK_Products_Unit_NotBlank",
        "CK_RolePermissions_PermissionKey_NotBlank",
        "CK_Roles_NormalizedRoleName_NotBlank",
        "CK_Roles_RoleName_NotBlank",
        "CK_SalesReceiptLines_Cost",
        "CK_SalesReceiptLines_LineAmount",
        "CK_SalesReceiptLines_LineNo",
        "CK_SalesReceiptLines_Quantity",
        "CK_SalesReceiptLines_Unit",
        "CK_SalesReceiptLines_UnitPrice",
        "CK_SalesReceipts_PostingAudit",
        "CK_SalesReceipts_SaleNo_NoNul",
        "CK_SalesReceipts_SaleNo_NotBlank",
        "CK_SalesReceipts_Status",
        "CK_SecurityAuditEvents_Operation_NotBlank",
        "CK_SecurityAuditEvents_TargetId",
        "CK_SecurityAuditEvents_TargetType",
        "CK_StockBalances_EmptyValue",
        "CK_StockBalances_InventoryValue",
        "CK_StockBalances_Quantity",
        "CK_StockLedger_DocumentType_NoNul",
        "CK_StockLedger_DocumentType_NotBlank",
        "CK_StockLedger_QuantityChange_NonZero",
        "CK_SystemSecuritySettings_Singleton",
        "CK_SystemSecuritySettings_Username_NotBlank",
        "CK_UserPermissions_PermissionKey_NotBlank",
        "CK_Users_DisplayName_NotBlank",
        "CK_Users_NormalizedUsername_NotBlank",
        "CK_Users_PasswordAlgorithm_NotBlank",
        "CK_Users_Source_NotBlank",
        "CK_Users_Username_NotBlank",
        "CK_Warehouses_Code_NoNul",
        "CK_Warehouses_Code_NotBlank",
        "CK_Warehouses_Name_NoNul",
        "CK_Warehouses_Name_NotBlank",
        "DF_Customers_CreatedAtUtc",
        "DF_Customers_IsActive",
        "DF_Employees_CreatedAtUtc",
        "DF_Employees_IsActive",
        "DF_GoodsReceiptLines_CreatedAtUtc",
        "DF_GoodsReceipts_CreatedAtUtc",
        "DF_GoodsReceipts_Status",
        "DF_Products_CreatedAtUtc",
        "DF_Products_IsActive",
        "DF_RolePermissions_CreatedAtUtc",
        "DF_Roles_AssignmentRevision",
        "DF_Roles_CreatedAtUtc",
        "DF_Roles_IsActive",
        "DF_SalesReceiptLines_CreatedAtUtc",
        "DF_SalesReceipts_CreatedAtUtc",
        "DF_SalesReceipts_Status",
        "DF_SecurityAuditEvents_OccurredAtUtc",
        "DF_StockBalances_Quantity",
        "DF_SystemSecuritySettings_CreatedAtUtc",
        "DF_UserPermissions_CreatedAtUtc",
        "DF_UserRoles_CreatedAtUtc",
        "DF_Users_AssignmentRevision",
        "DF_Users_CreatedAtUtc",
        "DF_Users_IsActive",
        "DF_Warehouses_CreatedAtUtc",
        "DF_Warehouses_IsActive",
        "FK_Employees_Users",
        "FK_GoodsReceiptLines_CreatedByUsers",
        "FK_GoodsReceiptLines_GoodsReceipts",
        "FK_GoodsReceiptLines_Products",
        "FK_GoodsReceiptLines_UpdatedByUsers",
        "FK_GoodsReceipts_CreatedByUsers",
        "FK_GoodsReceipts_Employees",
        "FK_GoodsReceipts_PostedByUsers",
        "FK_GoodsReceipts_UpdatedByUsers",
        "FK_GoodsReceipts_Warehouses",
        "FK_RolePermissions_Roles",
        "FK_SalesReceiptLines_CreatedByUsers",
        "FK_SalesReceiptLines_Products",
        "FK_SalesReceiptLines_SalesReceipts",
        "FK_SalesReceiptLines_UpdatedByUsers",
        "FK_SalesReceipts_CreatedByUsers",
        "FK_SalesReceipts_Customers",
        "FK_SalesReceipts_Employees",
        "FK_SalesReceipts_PostedByUsers",
        "FK_SalesReceipts_UpdatedByUsers",
        "FK_SalesReceipts_Warehouses",
        "FK_SecurityAuditEvents_Actor",
        "FK_StockBalances_Products",
        "FK_StockBalances_Warehouses",
        "FK_StockLedger_PostedByUsers",
        "FK_StockLedger_Products",
        "FK_StockLedger_Warehouses",
        "FK_StockValuationEntries_StockLedger",
        "FK_SystemSecuritySettings_Superuser",
        "FK_UserPermissions_Users",
        "FK_UserRoles_Roles",
        "FK_UserRoles_Users",
        "PK_Customers",
        "PK_Employees",
        "PK_GoodsReceiptLines",
        "PK_GoodsReceipts",
        "PK_Products",
        "PK_RolePermissions",
        "PK_Roles",
        "PK_SalesReceiptLines",
        "PK_SalesReceipts",
        "PK_SecurityAuditEvents",
        "PK_StockBalances",
        "PK_StockLedger",
        "PK_StockValuationEntries",
        "PK_SystemSecuritySettings",
        "PK_UserPermissions",
        "PK_UserRoles",
        "PK_Users",
        "PK_Warehouses",
    };

    private static readonly string[] AllowedIndexes =
    {
        "IX_GoodsReceiptLines_ProductId",
        "IX_GoodsReceipts_EmployeeId",
        "IX_GoodsReceipts_ReceiptDate_ReceiptId",
        "IX_GoodsReceipts_WarehouseId_ReceiptDate",
        "IX_SalesReceiptLines_ProductId_SaleId",
        "IX_SalesReceipts_EmployeeId",
        "IX_SalesReceipts_SaleDate_SaleId",
        "IX_SalesReceipts_WarehouseId_SaleDate",
        "IX_SecurityAuditEvents_Target",
        "IX_StockBalances_ProductId",
        "IX_StockLedger_ProductId",
        "IX_StockLedger_WarehouseId_ProductId_PostedAtUtc_LedgerId",
        "IX_UserRoles_RoleId",
        "UX_Customers_Code",
        "UX_Employees_Code",
        "UX_Employees_UserId",
        "UX_GoodsReceiptLines_ReceiptId_LineNo",
        "UX_GoodsReceiptLines_ReceiptId_ProductId",
        "UX_GoodsReceipts_ReceiptNo",
        "UX_Products_Code",
        "UX_RolePermissions_RoleId_PermissionKey",
        "UX_Roles_NormalizedRoleName",
        "UX_SalesReceiptLines_SaleId_LineNo",
        "UX_SalesReceipts_SaleNo",
        "UX_StockLedger_DocumentType_DocumentLineId",
        "UX_SystemSecuritySettings_SuperuserUserId",
        "UX_UserPermissions_UserId_PermissionKey",
        "UX_UserRoles_UserId_RoleId",
        "UX_Users_NormalizedUsername",
        "UX_Warehouses_Code",
    };

    // Snapshot metadata, not mutable data/counters. Include permissions/principals and every
    // user object/column/index/FK/check/module so cached Respawn SQL cannot outlive its schema.
    private const string BoundarySql = @"
SELECT CONVERT(nvarchar(max),(SELECT s.schema_id,s.name,s.principal_id
 FROM sys.schemas s ORDER BY s.schema_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT o.object_id,o.schema_id,o.parent_object_id,o.name,o.type,
 m.definition,m.uses_ansi_nulls,m.uses_quoted_identifier,m.is_schema_bound,m.execute_as_principal_id,
 t.is_disabled,t.is_instead_of_trigger,t.is_not_for_replication
 FROM sys.objects o LEFT JOIN sys.sql_modules m ON m.object_id=o.object_id
 LEFT JOIN sys.triggers t ON t.object_id=o.object_id WHERE o.is_ms_shipped=0
 ORDER BY o.object_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT c.object_id,c.column_id,c.name,c.system_type_id,c.user_type_id,
 c.max_length,c.precision,c.scale,c.collation_name,c.is_nullable,c.is_identity,c.is_computed,
 c.default_object_id,c.rule_object_id,ic.seed_value,ic.increment_value,cc.definition,cc.is_persisted
 FROM sys.columns c JOIN sys.objects o ON o.object_id=c.object_id
 LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id
 LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id
 WHERE o.is_ms_shipped=0 ORDER BY c.object_id,c.column_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT i.object_id,i.index_id,i.name,i.type,i.is_unique,i.is_primary_key,
 i.is_unique_constraint,i.is_disabled,i.has_filter,i.filter_definition,
 c.index_column_id,c.column_id,c.key_ordinal,c.is_descending_key,c.is_included_column
 FROM sys.indexes i JOIN sys.objects o ON o.object_id=i.object_id
 LEFT JOIN sys.index_columns c ON c.object_id=i.object_id AND c.index_id=i.index_id
 WHERE o.is_ms_shipped=0 ORDER BY i.object_id,i.index_id,c.index_column_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT f.object_id,f.parent_object_id,f.referenced_object_id,f.is_disabled,
 f.is_not_trusted,f.is_not_for_replication,f.delete_referential_action,f.update_referential_action,
 c.constraint_column_id,c.parent_column_id,c.referenced_column_id
 FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
 ORDER BY f.object_id,c.constraint_column_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT object_id,parent_object_id,definition,is_disabled,is_not_trusted,
 is_not_for_replication FROM sys.check_constraints ORDER BY object_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT object_id,parent_object_id,definition FROM sys.default_constraints ORDER BY object_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT object_id,system_type_id,user_type_id,start_value,increment,
 minimum_value,maximum_value,is_cycling,is_cached,cache_size FROM sys.sequences ORDER BY object_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT principal_id,name,type,sid,default_schema_name,owning_principal_id
 FROM sys.database_principals ORDER BY principal_id FOR XML RAW, BINARY BASE64)),
 CONVERT(nvarchar(max),(SELECT role_principal_id,member_principal_id FROM sys.database_role_members
 ORDER BY role_principal_id,member_principal_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT class,major_id,minor_id,grantee_principal_id,grantor_principal_id,type,state
 FROM sys.database_permissions ORDER BY class,major_id,minor_id,grantee_principal_id,grantor_principal_id,type FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT t.object_id,t.name,t.parent_class,t.parent_id,t.is_disabled,
 t.is_instead_of_trigger,t.is_not_for_replication,m.definition,e.type,e.type_desc
 FROM sys.triggers t LEFT JOIN sys.sql_modules m ON m.object_id=t.object_id
 LEFT JOIN sys.trigger_events e ON e.object_id=t.object_id
 WHERE t.is_ms_shipped=0 ORDER BY t.object_id,e.type FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT schema_id,name,user_type_id,system_type_id,max_length,precision,scale,
 is_nullable,is_table_type FROM sys.types WHERE is_user_defined=1 ORDER BY user_type_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT assembly_id,name,permission_set FROM sys.assemblies
 WHERE is_user_defined=1 ORDER BY assembly_id FOR XML RAW)),
 CONVERT(nvarchar(max),(SELECT Id,ScriptName,Applied FROM dbo.SchemaVersions ORDER BY Id FOR XML RAW));";

    private readonly SqlTestDatabase _database;
    private readonly Checkpoint _checkpoint;
    private readonly Func<SqlConnection, CancellationToken, Task>? _afterTriggerDisabled;
    private string[] _boundary = Array.Empty<string>();
    private int _active;
    private int _poisoned;

    private RespawnResetProfile(SqlTestDatabase database, int timeout,
        Func<SqlConnection, CancellationToken, Task>? afterTriggerDisabled)
    {
        _database = database;
        _afterTriggerDisabled = afterTriggerDisabled;
        _checkpoint = new Checkpoint
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = new[] { "dbo" },
            TablesToIgnore = new[] { "SchemaVersions" },
            WithReseed = true,
            CommandTimeout = timeout,
        };
    }

    internal bool BelongsTo(SqlTestDatabase database) => ReferenceEquals(_database, database);

    public bool IsPoisoned => Volatile.Read(ref _poisoned) != 0;

    public static async Task<RespawnResetProfile> CreateAsync(SqlTestDatabase database,
        int commandTimeoutSeconds = 60,
        Func<SqlConnection, CancellationToken, Task>? afterTriggerDisabled = null,
        CancellationToken cancellationToken = default)
    {
        if (database is null) { throw new ArgumentNullException(nameof(database)); }
        if (commandTimeoutSeconds < 1 || commandTimeoutSeconds > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds));
        }

        var profile = new RespawnResetProfile(database, commandTimeoutSeconds, afterTriggerDisabled);
        using (var connection = new SqlConnection(database.ConnectionString))
        using (var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            budget.CancelAfter(TimeSpan.FromMinutes(2));
            await connection.OpenAsync(budget.Token).ConfigureAwait(false);
            using (budget.Token.Register(connection.Close))
            {
                await database.VerifyOwnershipAsync(connection, budget.Token).ConfigureAwait(false);
                var compatibility = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                    .CheckConnectionAsync(connection, budget.Token).ConfigureAwait(false);
                if (compatibility.Status != SchemaCompatibilityStatus.Compatible)
                {
                    throw new InvalidOperationException("The application schema is not compatible with the reset profile.");
                }
                using (var safety = new SqlCommand(@"
SELECT (SELECT COUNT(*) FROM sys.schemas s WHERE s.principal_id<>s.schema_id OR
 (s.schema_id NOT BETWEEN 1 AND 4 AND NOT EXISTS
  (SELECT 1 FROM sys.database_principals p WHERE p.principal_id=s.schema_id AND p.name=s.name AND p.is_fixed_role=1)))
 + (SELECT COUNT(*) FROM sys.database_principals WHERE principal_id>4 AND is_fixed_role=0)
 + (SELECT COUNT(*) FROM sys.database_role_members WHERE NOT(role_principal_id=16384 AND member_principal_id=1))
 + (SELECT COUNT(*) FROM sys.database_permissions WHERE NOT
  ((class=0 AND major_id=0 AND grantee_principal_id=0 AND grantor_principal_id=1 AND state='G'
   AND permission_name IN ('VIEW ANY COLUMN ENCRYPTION KEY DEFINITION','VIEW ANY COLUMN MASTER KEY DEFINITION'))
 OR (class=0 AND major_id=0 AND grantee_principal_id=1 AND grantor_principal_id=1 AND state='G' AND permission_name='CONNECT')
 OR (class=1 AND major_id<0 AND grantee_principal_id=0 AND grantor_principal_id=1 AND state='G' AND permission_name='SELECT')));", connection) { CommandTimeout = 30 })
                {
                    if (Convert.ToInt32(await safety.ExecuteScalarAsync(budget.Token).ConfigureAwait(false)) != 0)
                    {
                        throw new InvalidOperationException("Unexpected initial schema, principal or permission boundary.");
                    }
                }
                await ValidateTriggerAsync(connection, budget.Token).ConfigureAwait(false);
                await ValidateTablesAndConstraintsAsync(connection, budget.Token).ConfigureAwait(false);
                await ValidateInitialObjectsAsync(connection, budget.Token).ConfigureAwait(false);
                profile._boundary = await ReadBoundaryAsync(connection, budget.Token).ConfigureAwait(false);
            }
        }

        // Build Respawn's cached metadata and prove the first empty reset before publication.
        await profile.ResetAsync(cancellationToken).ConfigureAwait(false);
        return profile;
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
        {
            throw new InvalidOperationException("A reset is already active for this allocation.");
        }
        try
        {
            if (IsPoisoned) { throw new InvalidOperationException("The reset profile is poisoned."); }
            await ResetCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Volatile.Write(ref _poisoned, 1);
            throw;
        }
        finally { Volatile.Write(ref _active, 0); }
    }

    // Seed data may change rows, but must not change the cached schema/journal/ownership boundary.
    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
        {
            throw new InvalidOperationException("A reset or verification is already active.");
        }
        try
        {
            if (IsPoisoned) { throw new InvalidOperationException("The reset profile is poisoned."); }
            using var connection = new SqlConnection(_database.ConnectionString);
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(TimeSpan.FromMinutes(2));
            await connection.OpenAsync(budget.Token).ConfigureAwait(false);
            using (budget.Token.Register(connection.Close))
            {
                await ValidateBoundaryAsync(connection, budget.Token).ConfigureAwait(false);
            }
        }
        catch { Volatile.Write(ref _poisoned, 1); throw; }
        finally { Volatile.Write(ref _active, 0); }
    }

    private async Task ResetCoreAsync(CancellationToken cancellationToken)
    {
        Exception? failure = null;
        var triggerMayBeDisabled = false;
        using var connection = new SqlConnection(_database.ConnectionString);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await connection.OpenAsync(budget.Token).ConfigureAwait(false);
            // Respawn v4 has no token overload. Closing this owned nonpooled connection cancels
            // the actual operation; we still await that task before restoration or admission.
            using (budget.Token.Register(connection.Close))
            {
                await ValidateBoundaryAsync(connection, budget.Token).ConfigureAwait(false);
                budget.Token.ThrowIfCancellationRequested();
                triggerMayBeDisabled = true; // DDL may succeed even if its response is cancelled.
                foreach (var table in AppendOnlyTables)
                {
                    await ExecuteAsync(connection, "DISABLE TRIGGER dbo.TR_" + table + "_AppendOnly ON dbo." + table + ";", budget.Token).ConfigureAwait(false);
                    if (_afterTriggerDisabled != null)
                    {
                        await _afterTriggerDisabled(connection, budget.Token).ConfigureAwait(false);
                    }
                    budget.Token.ThrowIfCancellationRequested();
                }
                await _checkpoint.Reset(connection).ConfigureAwait(false);
                budget.Token.ThrowIfCancellationRequested();
                await ExecuteAsync(connection,
                    "ALTER SEQUENCE dbo.GoodsReceiptNumberSequence RESTART WITH 1; ALTER SEQUENCE dbo.SalesReceiptNumberSequence RESTART WITH 1;", budget.Token).ConfigureAwait(false);
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            // Never restore over an unknown allocation or guessed trigger. Restoration gets
            // a separate finite budget even when the reset connection was closed/cancelled.
            connection.Close();
            if (triggerMayBeDisabled)
            {
                // Each protection has its own bounded connection and failure boundary. A missing
                // or damaged trigger must never strand the other protection disabled.
                foreach (var table in AppendOnlyTables)
                {
                    try
                    {
                        using var restoreBudget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                        using var restore = new SqlConnection(_database.ConnectionString);
                        await restore.OpenAsync(restoreBudget.Token).ConfigureAwait(false);
                        using (restoreBudget.Token.Register(restore.Close))
                        {
                            await _database.VerifyOwnershipAsync(restore, restoreBudget.Token).ConfigureAwait(false);
                            await ValidateTriggerDefinitionAsync(restore, table, restoreBudget.Token).ConfigureAwait(false);
                            await ExecuteAsync(restore, "ENABLE TRIGGER dbo.TR_" + table + "_AppendOnly ON dbo." + table + ";", restoreBudget.Token).ConfigureAwait(false);
                            await ValidateTriggerAsync(restore, table, restoreBudget.Token).ConfigureAwait(false);
                        }
                    }
                    catch (Exception cleanup)
                    {
                        failure = failure is null ? cleanup : new AggregateException("Reset and trigger restoration failed.", failure, cleanup);
                    }
                }
            }
        }
        if (failure != null) { ExceptionDispatchInfo.Capture(failure).Throw(); }

        using var verification = new SqlConnection(_database.ConnectionString);
        await verification.OpenAsync(budget.Token).ConfigureAwait(false);
        using (budget.Token.Register(verification.Close))
        {
            await ValidateBoundaryAsync(verification, budget.Token).ConfigureAwait(false);
            foreach (var table in MutableTables)
            {
                using var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.[" + table + "];", verification) { CommandTimeout = 30 };
                if (Convert.ToInt64(await command.ExecuteScalarAsync(budget.Token).ConfigureAwait(false)) != 0)
                {
                    throw new InvalidOperationException("Reset left mutable application rows behind.");
                }
            }
        }
    }

    private async Task ValidateBoundaryAsync(SqlConnection connection, CancellationToken token)
    {
        await _database.VerifyOwnershipAsync(connection, token).ConfigureAwait(false);
        await ValidateTriggerAsync(connection, token).ConfigureAwait(false);
        await ValidateTablesAndConstraintsAsync(connection, token).ConfigureAwait(false);
        if (!_boundary.SequenceEqual(await ReadBoundaryAsync(connection, token).ConfigureAwait(false), StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The schema, journal or permission boundary changed after checkpoint creation.");
        }
    }

    private static async Task ValidateTriggerAsync(SqlConnection connection, CancellationToken token)
    {
        foreach (var table in AppendOnlyTables)
        {
            await ValidateTriggerAsync(connection, table, token).ConfigureAwait(false);
        }
    }

    private static async Task ValidateTriggerAsync(SqlConnection connection, string table, CancellationToken token)
    {
        await ValidateTriggerDefinitionAsync(connection, table, token).ConfigureAwait(false);
        using var command = new SqlCommand(@"SELECT COUNT(*) FROM sys.triggers
WHERE object_id=OBJECT_ID(@trigger) AND is_disabled=0;", connection) { CommandTimeout = 30 };
        command.Parameters.AddWithValue("@trigger", "dbo.TR_" + table + "_AppendOnly");
        if (Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) != 1)
        {
            throw new InvalidOperationException("An append-only trigger is disabled.");
        }
    }

    private static async Task ValidateTriggerDefinitionAsync(SqlConnection connection, string table, CancellationToken token)
    {
        using var command = new SqlCommand(@"SELECT m.definition FROM sys.triggers t
JOIN sys.sql_modules m ON m.object_id=t.object_id
WHERE t.object_id=OBJECT_ID(@trigger)
AND t.parent_id=OBJECT_ID(@table) AND t.is_instead_of_trigger=1 AND t.is_not_for_replication=0;", connection) { CommandTimeout = 30 };
        command.Parameters.AddWithValue("@trigger", "dbo.TR_" + table + "_AppendOnly");
        command.Parameters.AddWithValue("@table", "dbo." + table);
        var definition = await command.ExecuteScalarAsync(token).ConfigureAwait(false) as string;
        var expected = ExpectedTrigger.Replace("StockLedger", table);
        if (definition is null || !string.Equals(definition.Replace("\r\n", "\n").Trim(), expected.Replace("\r\n", "\n").Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The exact append-only trigger definition could not be verified.");
        }
    }

    private static async Task ValidateTablesAndConstraintsAsync(SqlConnection connection, CancellationToken token)
    {
        using var command = new SqlCommand(@"SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0;
SELECT SCHEMA_NAME(schema_id),name FROM sys.tables WHERE is_ms_shipped=0 ORDER BY name;
SELECT (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 + (SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1);", connection) { CommandTimeout = 30 };
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await reader.ReadAsync(token).ConfigureAwait(false);
        if (reader.GetInt32(0) != 19) { throw new InvalidOperationException("Unexpected application table boundary."); }
        await reader.NextResultAsync(token).ConfigureAwait(false);
        var expected = MutableTables.Concat(new[] { "SchemaVersions" }).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var actual = new System.Collections.Generic.List<string>();
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            if (!string.Equals(reader.GetString(0), "dbo", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unexpected application table schema.");
            }
            actual.Add(reader.GetString(1));
        }
        if (!expected.SequenceEqual(actual.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Unexpected application table boundary.");
        }
        await reader.NextResultAsync(token).ConfigureAwait(false);
        await reader.ReadAsync(token).ConfigureAwait(false);
        if (reader.GetInt32(0) != 0) { throw new InvalidOperationException("Disabled or untrusted constraints reject reuse."); }
    }

    private static async Task ValidateInitialObjectsAsync(SqlConnection connection, CancellationToken token)
    {
        using var command = new SqlCommand(@"SELECT SCHEMA_NAME(schema_id),name,type,OBJECT_NAME(parent_object_id)
FROM sys.objects WHERE is_ms_shipped=0;
SELECT name FROM sys.indexes WHERE object_id IN(SELECT object_id FROM sys.tables WHERE is_ms_shipped=0)
AND is_primary_key=0 AND index_id>0;
SELECT (SELECT COUNT(*) FROM sys.triggers WHERE is_ms_shipped=0)
 + (SELECT COUNT(*) FROM sys.types WHERE is_user_defined=1)
 + (SELECT COUNT(*) FROM sys.assemblies WHERE is_user_defined=1);", connection) { CommandTimeout = 30 };
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var constraints = new System.Collections.Generic.List<string>();
        var modules = new System.Collections.Generic.List<string>();
        var expectedModules = new[] { "FN:CatalogTextContainsNul", "P:ApplySecurityAdministration",
            "P:ApplySecurityPermissionImport", "P:ReadSecurityPermissionImportTarget",
            "SO:GoodsReceiptNumberSequence", "SO:SalesReceiptNumberSequence",
            "TR:TR_StockLedger_AppendOnly", "TR:TR_StockValuationEntries_AppendOnly" };
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            var name = reader.GetString(1);
            var type = reader.GetString(2).Trim();
            var parent = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            if (reader.GetString(0) != "dbo") { throw new InvalidOperationException("Unexpected object schema."); }
            if (type == "U") { continue; } // Exact table set checked separately.
            if (type == "PK" && parent == "SchemaVersions") { continue; } // DbUp's generated constraint name.
            if (AllowedConstraints.Contains(name, StringComparer.Ordinal) &&
                (type == "PK" || type == "F" || type == "C" || type == "D"))
            {
                constraints.Add(name);
                continue;
            }
            if (expectedModules.Contains(type + ":" + name, StringComparer.Ordinal))
            {
                modules.Add(type + ":" + name);
                continue;
            }
            throw new InvalidOperationException("Unexpected initial schema object.");
        }
        if (!AllowedConstraints.SequenceEqual(constraints.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal) ||
            !expectedModules.SequenceEqual(modules.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The initial schema object inventory is incomplete.");
        }
        await reader.NextResultAsync(token).ConfigureAwait(false);
        var indexes = new System.Collections.Generic.List<string>();
        while (await reader.ReadAsync(token).ConfigureAwait(false)) { indexes.Add(reader.GetString(0)); }
        if (!AllowedIndexes.SequenceEqual(indexes.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Unexpected initial index inventory.");
        }
        await reader.NextResultAsync(token).ConfigureAwait(false);
        await reader.ReadAsync(token).ConfigureAwait(false);
        if (reader.GetInt32(0) != 2)
        {
            throw new InvalidOperationException("Unexpected initial trigger, type or assembly inventory.");
        }
    }

    private static async Task<string[]> ReadBoundaryAsync(SqlConnection connection, CancellationToken token)
    {
        using var command = new SqlCommand(BoundarySql, connection) { CommandTimeout = 30 };
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await reader.ReadAsync(token).ConfigureAwait(false);
        var values = new string[reader.FieldCount];
        for (var index = 0; index < values.Length; index++) { values[index] = reader.IsDBNull(index) ? string.Empty : reader.GetString(index); }
        return values;
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken token)
    {
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}
