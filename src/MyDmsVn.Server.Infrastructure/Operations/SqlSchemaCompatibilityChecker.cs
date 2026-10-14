using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.Infrastructure.Operations;

public sealed class SqlSchemaCompatibilityChecker : ISchemaCompatibilityChecker
{
    private const string CheckDefinitionHashSql =
        "CONVERT(varchar(64), HASHBYTES('SHA2_256', candidate.definition), 2)";

    private const string PermissionProvisioningArtifactSql =
        "WITH ManagedColumnSignatures AS (SELECT ownerTable.name AS TableName, candidate.column_id, " +
        "CONCAT(ownerTable.name, N'|', candidate.column_id, N'|', candidate.name, N'|', " +
        "TYPE_NAME(candidate.user_type_id), N'|', candidate.max_length, N'|', candidate.precision, N'|', " +
        "candidate.scale, N'|', candidate.is_nullable, N'|', candidate.is_ansi_padded, N'|', " +
        "candidate.is_rowguidcol, N'|', candidate.is_identity, N'|', candidate.is_computed, N'|', " +
        "candidate.is_filestream, N'|', candidate.is_replicated, N'|', candidate.is_sparse, N'|', " +
        "candidate.is_column_set, N'|', candidate.generated_always_type, N'|', " +
        "ISNULL(CONVERT(nvarchar(10), candidate.encryption_type), N''), N'|', " +
        "ISNULL(candidate.collation_name, N''), N'|', ISNULL(defaultConstraint.name, N''), N'|', " +
        "ISNULL(defaultConstraint.definition, N''), N'|', " +
        "ISNULL(CONVERT(nvarchar(100), identityColumn.seed_value), N''), N'|', " +
        "ISNULL(CONVERT(nvarchar(100), identityColumn.increment_value), N''), N'|', " +
        "ISNULL(CONVERT(nvarchar(1), identityColumn.is_not_for_replication), N''), N'|', " +
        "ISNULL(computedColumn.definition, N''), N'|', " +
        "ISNULL(CONVERT(nvarchar(1), computedColumn.is_persisted), N'')) AS Signature " +
        "FROM sys.tables AS ownerTable " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.columns AS candidate ON candidate.object_id = ownerTable.object_id " +
        "LEFT JOIN sys.default_constraints AS defaultConstraint " +
        "ON defaultConstraint.parent_object_id = candidate.object_id " +
        "AND defaultConstraint.parent_column_id = candidate.column_id " +
        "LEFT JOIN sys.identity_columns AS identityColumn ON identityColumn.object_id = candidate.object_id " +
        "AND identityColumn.column_id = candidate.column_id " +
        "LEFT JOIN sys.computed_columns AS computedColumn ON computedColumn.object_id = candidate.object_id " +
        "AND computedColumn.column_id = candidate.column_id " +
        "WHERE ownerSchema.name = N'dbo' AND ownerTable.name IN (" +
        "N'Permissions', N'PermissionLegacyAssignmentDrift', N'PermissionSynchronizationState', " +
        "N'SystemManagedRoles', N'PermissionGrantApprovals', N'ManagedRolePermissionGrants', " +
        "N'SecurityBootstrapState', N'PermissionSecurityAudit')), " +
        "ManagedColumnFingerprint AS (SELECT CONVERT(varchar(64), " +
        "HASHBYTES('SHA2_256', STRING_AGG(CAST(Signature AS nvarchar(max)), N';') " +
        "WITHIN GROUP (ORDER BY TableName, column_id)), 2) AS DefinitionHash " +
        "FROM ManagedColumnSignatures) " +
        "SELECT ArtifactKey FROM (" +
        "SELECT N'Table:Permissions' AS ArtifactKey, OBJECT_ID(N'dbo.Permissions', N'U') AS ObjectId UNION ALL " +
        "SELECT N'Table:PermissionLegacyAssignmentDrift', OBJECT_ID(N'dbo.PermissionLegacyAssignmentDrift', N'U') UNION ALL " +
        "SELECT N'Table:PermissionSynchronizationState', OBJECT_ID(N'dbo.PermissionSynchronizationState', N'U') UNION ALL " +
        "SELECT N'Table:SystemManagedRoles', OBJECT_ID(N'dbo.SystemManagedRoles', N'U') UNION ALL " +
        "SELECT N'Table:PermissionGrantApprovals', OBJECT_ID(N'dbo.PermissionGrantApprovals', N'U') UNION ALL " +
        "SELECT N'Table:ManagedRolePermissionGrants', OBJECT_ID(N'dbo.ManagedRolePermissionGrants', N'U') UNION ALL " +
        "SELECT N'Table:SecurityBootstrapState', OBJECT_ID(N'dbo.SecurityBootstrapState', N'U') UNION ALL " +
        "SELECT N'Table:PermissionSecurityAudit', OBJECT_ID(N'dbo.PermissionSecurityAudit', N'U') UNION ALL " +
        "SELECT N'Invariant:PermissionProvisioningColumns', (SELECT 1 FROM ManagedColumnFingerprint " +
        "WHERE DefinitionHash = N'B1627B64E0A6EAC6FA05F7F79F40EE28FCDFFCDCF948A2EFE6D9AA1B8DA8B04E') UNION ALL " +
        "SELECT N'Invariant:PermissionProvisioningChecks', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'ManagedRolePermissionGrants', N'CK_ManagedRolePermissionGrants_Audit_NotBlank', N'C3297E89E511D67A096CE34BDFB9A5345522D3C3619CA20E4FB23B7F936D8DE4'), " +
        "(N'ManagedRolePermissionGrants', N'CK_ManagedRolePermissionGrants_GrantState', N'586CD7C33300F30D2166034267083B683DB0D480A00AF50B6A3230ADBFA410E6'), " +
        "(N'PermissionGrantApprovals', N'CK_PermissionGrantApprovals_AdministratorDecision', N'84C44D039C4868A389D750ADBBA7F085F2DCBDA1A622367AFF866EC26B01A959'), " +
        "(N'PermissionGrantApprovals', N'CK_PermissionGrantApprovals_Audit_NotBlank', N'681AF9E7181174FBBB94F3F4C84B60029F0E7FE6D211018FA834B82BF24EFA6D'), " +
        "(N'PermissionGrantApprovals', N'CK_PermissionGrantApprovals_CatalogDecision', N'8EA8F921330EDFAB1D2ED90AD3F6FCD6192935B43C75B4873FA8D768835F5A14'), " +
        "(N'PermissionLegacyAssignmentDrift', N'CK_PermissionLegacyAssignmentDrift_Counts', N'B11CEE4D9D7A647D392FF36F4979ED86C56AE9CCCFB5AC657AB37FD3C169D125'), " +
        "(N'PermissionLegacyAssignmentDrift', N'CK_PermissionLegacyAssignmentDrift_ReviewStatus', N'7A560DE801CAAFAC46CF0F6CCD1D16B37E60CCCF520B134E26BBDEEBAACE94C9'), " +
        "(N'Permissions', N'CK_Permissions_ActivationAudit', N'575E5BF8A7AA9AF02C7A82E769A39F10EFA128DFB31B45FA3AF203BD18BDED17'), " +
        "(N'Permissions', N'CK_Permissions_AdministratorGrantPolicy', N'471DFEE324672A374D1D6135C191EBBC6782BFE50959C7FF75806780FE848671'), " +
        "(N'Permissions', N'CK_Permissions_CatalogStatus', N'465A54942A6B0C4D1935D39BF2D21916AB974E31585FE71555DBFBF8D03DB4B0'), " +
        "(N'Permissions', N'CK_Permissions_Description_NotBlank', N'596DC6573A7BDD97650686D077B7E26994811E9AA362EDE8F6E570296229D5DE'), " +
        "(N'Permissions', N'CK_Permissions_Module_NotBlank', N'C9ACA015EC38845BD83BF64601647A480496E0D18F10FEA6A6586CA10E3FA02A'), " +
        "(N'Permissions', N'CK_Permissions_PermissionKey_NotBlank', N'B7C138CDE71723BD91C9DA71142F8528A64A6D64236DDA207FAC28F8E8461772'), " +
        "(N'PermissionSecurityAudit', N'CK_PermissionSecurityAudit_Actor_NotBlank', N'34D5E818E3EE4068D990CC60CBAAF8CB203C3C14816A5DA512EB6CB62883F96F'), " +
        "(N'PermissionSecurityAudit', N'CK_PermissionSecurityAudit_Operation_NotBlank', N'EE65835E7E8C3799F01B7CD69357E421B6B1232B264BC25273DF7E6E58A857E1'), " +
        "(N'PermissionSecurityAudit', N'CK_PermissionSecurityAudit_Outcome', N'777BD9AC2065FCBC9EF56B0857EF961667F0BC376A6D975225CB98EB958AAA55'), " +
        "(N'PermissionSecurityAudit', N'CK_PermissionSecurityAudit_ReasonCode_NotBlank', N'903563E4A34FA80BBF665955736A2D567D839CF090E205DAC3B8572689F11DB2'), " +
        "(N'PermissionSynchronizationState', N'CK_PermissionSynchronizationState_Singleton', N'F09ADD08176F8A85F097FE80C11F2EF91BE5F045F158C19DC990C1221D0F9E56'), " +
        "(N'SecurityBootstrapState', N'CK_SecurityBootstrapState_Audit_NotBlank', N'4AC2004383A587818FD186231620FF806571FB3A9929CC571CFDB8D10B11E61D'), " +
        "(N'SecurityBootstrapState', N'CK_SecurityBootstrapState_Key_NotBlank', N'E10064B039E948855BF7D7802C0697143F69CF671527B9895269D96998BF9638'), " +
        "(N'SystemManagedRoles', N'CK_SystemManagedRoles_Name_NotBlank', N'2064FF992AD24DF22A7EA2023B1D6472D6BED23CA517F1EE73760DEFBAEF3CC9'), " +
        "(N'SystemManagedRoles', N'CK_SystemManagedRoles_SystemRoleKey_NotBlank', N'6A88FC673B78E5862597BE9B93D1AB5AC8C253BD1F582C1D8BCDF0B7CC46F23E')) " +
        "AS expected(TableName, ConstraintName, DefinitionHash) INNER JOIN sys.check_constraints AS candidate " +
        "ON candidate.name = expected.ConstraintName " +
        "AND candidate.parent_object_id = OBJECT_ID(N'dbo.' + expected.TableName, N'U') " +
        "WHERE SCHEMA_NAME(candidate.schema_id) = N'dbo' AND candidate.is_disabled = 0 " +
        "AND candidate.is_not_trusted = 0 AND " + CheckDefinitionHashSql + " = expected.DefinitionHash) = 22 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:PermissionProvisioningPrimaryKeys', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'Permissions', N'PK_Permissions', N'PermissionKey:ASC'), " +
        "(N'PermissionLegacyAssignmentDrift', N'PK_PermissionLegacyAssignmentDrift', N'PermissionKey:ASC'), " +
        "(N'PermissionSynchronizationState', N'PK_PermissionSynchronizationState', N'StateId:ASC'), " +
        "(N'SystemManagedRoles', N'PK_SystemManagedRoles', N'ManagedRoleId:ASC'), " +
        "(N'PermissionGrantApprovals', N'PK_PermissionGrantApprovals', N'PermissionKey:ASC'), " +
        "(N'ManagedRolePermissionGrants', N'PK_ManagedRolePermissionGrants', N'ManagedRolePermissionGrantId:ASC'), " +
        "(N'SecurityBootstrapState', N'PK_SecurityBootstrapState', N'BootstrapKey:ASC'), " +
        "(N'PermissionSecurityAudit', N'PK_PermissionSecurityAudit', N'PermissionSecurityAuditId:ASC')) " +
        "AS expected(TableName, ConstraintName, KeyColumns) " +
        "INNER JOIN sys.tables AS ownerTable ON ownerTable.name = expected.TableName " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.key_constraints AS candidate ON candidate.parent_object_id = ownerTable.object_id " +
        "AND candidate.name = expected.ConstraintName AND candidate.type = N'PK' " +
        "INNER JOIN sys.indexes AS backingIndex ON backingIndex.object_id = ownerTable.object_id " +
        "AND backingIndex.index_id = candidate.unique_index_id " +
        "CROSS APPLY (SELECT STRING_AGG(keyColumn.name + CASE WHEN indexColumn.is_descending_key = 1 " +
        "THEN N':DESC' ELSE N':ASC' END, N',') WITHIN GROUP (ORDER BY indexColumn.key_ordinal) AS KeyColumns " +
        "FROM sys.index_columns AS indexColumn INNER JOIN sys.columns AS keyColumn " +
        "ON keyColumn.object_id = indexColumn.object_id AND keyColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = backingIndex.object_id AND indexColumn.index_id = backingIndex.index_id " +
        "AND indexColumn.key_ordinal > 0) AS actual WHERE ownerSchema.name = N'dbo' " +
        "AND backingIndex.is_unique = 1 AND backingIndex.is_disabled = 0 " +
        "AND backingIndex.ignore_dup_key = 0 " +
        "AND actual.KeyColumns = expected.KeyColumns) = 8 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:PermissionProvisioningUniqueIndexes', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'PermissionSynchronizationState', N'UX_PermissionSynchronizationState_DatabaseIdentity', N'DatabaseIdentity:ASC', N'', N''), " +
        "(N'SystemManagedRoles', N'UX_SystemManagedRoles_SystemRoleKey', N'SystemRoleKey:ASC', N'', N''), " +
        "(N'SystemManagedRoles', N'UX_SystemManagedRoles_RoleId', N'RoleId:ASC', N'', N''), " +
        "(N'ManagedRolePermissionGrants', N'UX_ManagedRolePermissionGrants_Role_Key', N'ManagedRoleId:ASC,PermissionKey:ASC', N'', N''), " +
        "(N'PermissionSecurityAudit', N'UX_PermissionSecurityAudit_AttemptId', N'AttemptId:ASC', N'', N'')) " +
        "AS expected(TableName, IndexName, KeyColumns, IncludedColumns, FilterDefinition) " +
        "INNER JOIN sys.tables AS ownerTable ON ownerTable.name = expected.TableName " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.indexes AS candidate ON candidate.object_id = ownerTable.object_id AND candidate.name = expected.IndexName " +
        "CROSS APPLY (SELECT STRING_AGG(keyColumn.name + CASE WHEN indexColumn.is_descending_key = 1 " +
        "THEN N':DESC' ELSE N':ASC' END, N',') WITHIN GROUP (ORDER BY indexColumn.key_ordinal) AS KeyColumns " +
        "FROM sys.index_columns AS indexColumn INNER JOIN sys.columns AS keyColumn " +
        "ON keyColumn.object_id = indexColumn.object_id AND keyColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = candidate.object_id AND indexColumn.index_id = candidate.index_id " +
        "AND indexColumn.key_ordinal > 0) AS actual " +
        "CROSS APPLY (SELECT STRING_AGG(includedColumn.name, N',') " +
        "WITHIN GROUP (ORDER BY indexColumn.index_column_id) AS IncludedColumns " +
        "FROM sys.index_columns AS indexColumn INNER JOIN sys.columns AS includedColumn " +
        "ON includedColumn.object_id = indexColumn.object_id AND includedColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = candidate.object_id AND indexColumn.index_id = candidate.index_id " +
        "AND indexColumn.is_included_column = 1) AS included " +
        "WHERE ownerSchema.name = N'dbo' AND candidate.is_unique = 1 AND candidate.is_disabled = 0 " +
        "AND candidate.ignore_dup_key = 0 " +
        "AND actual.KeyColumns = expected.KeyColumns " +
        "AND ISNULL(included.IncludedColumns, N'') = expected.IncludedColumns " +
        "AND expected.FilterDefinition = N'' AND candidate.has_filter = 0) = 5 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:PermissionProvisioningForeignKeys', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'PermissionLegacyAssignmentDrift', N'Permissions', N'FK_PermissionLegacyAssignmentDrift_Permissions', N'PermissionKey=PermissionKey'), " +
        "(N'SystemManagedRoles', N'Roles', N'FK_SystemManagedRoles_Roles', N'RoleId=RoleId'), " +
        "(N'PermissionGrantApprovals', N'Permissions', N'FK_PermissionGrantApprovals_Permissions', N'PermissionKey=PermissionKey'), " +
        "(N'ManagedRolePermissionGrants', N'SystemManagedRoles', N'FK_ManagedRolePermissionGrants_SystemManagedRoles', N'ManagedRoleId=ManagedRoleId'), " +
        "(N'ManagedRolePermissionGrants', N'Permissions', N'FK_ManagedRolePermissionGrants_Permissions', N'PermissionKey=PermissionKey')) " +
        "AS expected(ParentTableName, ReferencedTableName, ForeignKeyName, ColumnMappings) " +
        "INNER JOIN sys.tables AS parentTable ON parentTable.name = expected.ParentTableName " +
        "INNER JOIN sys.schemas AS parentSchema ON parentSchema.schema_id = parentTable.schema_id " +
        "INNER JOIN sys.tables AS referencedTable ON referencedTable.name = expected.ReferencedTableName " +
        "INNER JOIN sys.schemas AS referencedSchema ON referencedSchema.schema_id = referencedTable.schema_id " +
        "INNER JOIN sys.foreign_keys AS candidate ON candidate.parent_object_id = parentTable.object_id " +
        "AND candidate.referenced_object_id = referencedTable.object_id AND candidate.name = expected.ForeignKeyName " +
        "CROSS APPLY (SELECT STRING_AGG(parentColumn.name + N'=' + referencedColumn.name, N',') " +
        "WITHIN GROUP (ORDER BY mapping.constraint_column_id) AS ColumnMappings " +
        "FROM sys.foreign_key_columns AS mapping INNER JOIN sys.columns AS parentColumn " +
        "ON parentColumn.object_id = mapping.parent_object_id AND parentColumn.column_id = mapping.parent_column_id " +
        "INNER JOIN sys.columns AS referencedColumn ON referencedColumn.object_id = mapping.referenced_object_id " +
        "AND referencedColumn.column_id = mapping.referenced_column_id " +
        "WHERE mapping.constraint_object_id = candidate.object_id) AS actual " +
        "WHERE parentSchema.name = N'dbo' AND referencedSchema.name = N'dbo' " +
        "AND candidate.is_disabled = 0 AND candidate.is_not_trusted = 0 " +
        "AND candidate.delete_referential_action = 0 AND candidate.update_referential_action = 0 " +
        "AND candidate.is_not_for_replication = 0 AND actual.ColumnMappings = expected.ColumnMappings) = 5 THEN 1 END" +
        ") AS Artifacts WHERE ObjectId IS NOT NULL;";

    private const string ColumnArtifactSql =
        "WITH ManagedColumnSignatures AS (SELECT CASE " +
        "WHEN ownerTable.name = N'SchemaVersions' THEN N'Persistence' " +
        "WHEN ownerTable.name IN (N'Users', N'Roles', N'UserRoles', N'RolePermissions', N'UserPermissions') THEN N'Identity' " +
        "WHEN ownerTable.name IN (N'Products', N'Warehouses', N'Employees', N'Customers') THEN N'Catalog' " +
        "WHEN ownerTable.name IN (N'GoodsReceipts', N'GoodsReceiptLines') THEN N'GoodsReceipt' " +
        "ELSE N'Inventory' END AS GroupName, ownerTable.name AS TableName, candidate.column_id, " +
        "CONCAT(ownerTable.name, N'|', candidate.column_id, N'|', candidate.name, N'|', " +
        "TYPE_NAME(candidate.user_type_id), N'|', candidate.max_length, N'|', candidate.precision, N'|', " +
        "candidate.scale, N'|', candidate.is_nullable, N'|', candidate.is_ansi_padded, N'|', " +
        "candidate.is_rowguidcol, N'|', candidate.is_identity, N'|', candidate.is_computed, N'|', " +
        "candidate.is_filestream, N'|', candidate.is_replicated, N'|', candidate.is_sparse, N'|', " +
        "candidate.is_column_set, N'|', candidate.generated_always_type, N'|', " +
        "ISNULL(CONVERT(nvarchar(10), candidate.encryption_type), N''), N'|', " +
        "ISNULL(candidate.collation_name, N''), N'|', ISNULL(defaultConstraint.name, N''), N'|', " +
        "ISNULL(defaultConstraint.definition, N''), N'|', " +
        "ISNULL(CONVERT(nvarchar(100), identityColumn.seed_value), N''), N'|', " +
        "ISNULL(CONVERT(nvarchar(100), identityColumn.increment_value), N''), N'|', " +
        "ISNULL(CONVERT(nvarchar(1), identityColumn.is_not_for_replication), N''), N'|', " +
        "ISNULL(computedColumn.definition, N''), N'|', " +
        "ISNULL(CONVERT(nvarchar(1), computedColumn.is_persisted), N'')) AS Signature " +
        "FROM sys.tables AS ownerTable " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.columns AS candidate ON candidate.object_id = ownerTable.object_id " +
        "LEFT JOIN sys.default_constraints AS defaultConstraint " +
        "ON defaultConstraint.parent_object_id = candidate.object_id " +
        "AND defaultConstraint.parent_column_id = candidate.column_id " +
        "LEFT JOIN sys.identity_columns AS identityColumn ON identityColumn.object_id = candidate.object_id " +
        "AND identityColumn.column_id = candidate.column_id " +
        "LEFT JOIN sys.computed_columns AS computedColumn ON computedColumn.object_id = candidate.object_id " +
        "AND computedColumn.column_id = candidate.column_id " +
        "WHERE ownerSchema.name = N'dbo' AND ownerTable.name IN (" +
        "N'SchemaVersions', N'Users', N'Roles', N'UserRoles', N'RolePermissions', N'UserPermissions', " +
        "N'Products', N'Warehouses', N'Employees', N'Customers', N'GoodsReceipts', N'GoodsReceiptLines', " +
        "N'StockLedger', N'StockBalances')), " +
        "ManagedColumnFingerprints AS (SELECT GroupName, CONVERT(varchar(64), " +
        "HASHBYTES('SHA2_256', STRING_AGG(CAST(Signature AS nvarchar(max)), N';') " +
        "WITHIN GROUP (ORDER BY TableName, column_id)), 2) AS DefinitionHash " +
        "FROM ManagedColumnSignatures GROUP BY GroupName) " +
        "SELECT ArtifactKey FROM (" +
        "SELECT N'Invariant:PersistenceColumns' AS ArtifactKey, (SELECT 1 FROM ManagedColumnFingerprints " +
        "WHERE GroupName = N'Persistence' AND DefinitionHash = N'FED09938A268A9B35F8F717E56DAB46AF2B2A0A12CB03C53F38B267FE8950E44') AS ObjectId UNION ALL " +
        "SELECT N'Invariant:IdentityColumns', (SELECT 1 FROM ManagedColumnFingerprints " +
        "WHERE GroupName = N'Identity' AND DefinitionHash = N'8AB4CE51C0DC70E21C658EE8CFBCB259FABEA28E93CA95F124E14247065C05F9') UNION ALL " +
        "SELECT N'Invariant:CatalogColumns', (SELECT 1 FROM ManagedColumnFingerprints " +
        "WHERE GroupName = N'Catalog' AND DefinitionHash = N'15F743045CEEC388F0F4E9C330B9D0A663AC0175D67EE485EAFA270AF5938980') UNION ALL " +
        "SELECT N'Invariant:GoodsReceiptColumns', (SELECT 1 FROM ManagedColumnFingerprints " +
        "WHERE GroupName = N'GoodsReceipt' AND DefinitionHash = N'282B57D0FE026037BA622DC9A2C378ED83FF2F24C3D9186538FBD8098FEA00D2') UNION ALL " +
        "SELECT N'Invariant:InventoryColumns', (SELECT 1 FROM ManagedColumnFingerprints " +
        "WHERE GroupName = N'Inventory' AND DefinitionHash = N'9B9B546EB802B5B3ABB62EE35086356AF482682D24E2F83673F6E7E3068A417B')) " +
        "AS Artifacts WHERE ObjectId IS NOT NULL;";

    private const string ArtifactSql =
        "SELECT ArtifactKey FROM (" +
        "SELECT N'Table:Users' AS ArtifactKey, OBJECT_ID(N'dbo.Users', N'U') AS ObjectId UNION ALL " +
        "SELECT N'Table:Roles', OBJECT_ID(N'dbo.Roles', N'U') UNION ALL " +
        "SELECT N'Table:UserRoles', OBJECT_ID(N'dbo.UserRoles', N'U') UNION ALL " +
        "SELECT N'Table:RolePermissions', OBJECT_ID(N'dbo.RolePermissions', N'U') UNION ALL " +
        "SELECT N'Table:UserPermissions', OBJECT_ID(N'dbo.UserPermissions', N'U') UNION ALL " +
        "SELECT N'Invariant:IdentityChecks', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'Users', N'CK_Users_Username_NotBlank', N'2872BEB326EEDB8889356C7EC712C93948FF90A980039E0F32DD5FCC6D41B640'), " +
        "(N'Users', N'CK_Users_NormalizedUsername_NotBlank', N'10551A196B3EA3311377167CF5C2362983606E445277E018346581CB1E988082'), " +
        "(N'Users', N'CK_Users_DisplayName_NotBlank', N'D7D96A4A6A2C42E718ACF2253BE05394C5473C630EE75DD9952AC33013C6DB49'), " +
        "(N'Users', N'CK_Users_Source_NotBlank', N'B17D8B94E17F6C3F2B3C26F2ED9322AB3E7804AF5F03409AF207BAAFB2ABEC67'), " +
        "(N'Users', N'CK_Users_PasswordAlgorithm_NotBlank', N'B8DF013ECED4587A9A4C8CDACE69996D990EBDBFCC7294E4CDE00F6AED2EAF9A'), " +
        "(N'Roles', N'CK_Roles_RoleName_NotBlank', N'F9C99D823FB4CA24C6C6D1AD6A47AEF7F394194D79D9942379D8F7337CE61C87'), " +
        "(N'Roles', N'CK_Roles_NormalizedRoleName_NotBlank', N'FDB72AF36AAE73861FD8CC66A2AC2C8FAAC4B04A7F54E22E2411099AECE1BF08'), " +
        "(N'RolePermissions', N'CK_RolePermissions_PermissionKey_NotBlank', N'B7C138CDE71723BD91C9DA71142F8528A64A6D64236DDA207FAC28F8E8461772'), " +
        "(N'UserPermissions', N'CK_UserPermissions_PermissionKey_NotBlank', N'B7C138CDE71723BD91C9DA71142F8528A64A6D64236DDA207FAC28F8E8461772')) " +
        "AS expected(TableName, ConstraintName, DefinitionHash) INNER JOIN sys.check_constraints AS candidate " +
        "ON candidate.name = expected.ConstraintName " +
        "AND candidate.parent_object_id = OBJECT_ID(N'dbo.' + expected.TableName, N'U') " +
        "WHERE SCHEMA_NAME(candidate.schema_id) = N'dbo' " +
        "AND candidate.is_disabled = 0 AND candidate.is_not_trusted = 0 AND " +
        CheckDefinitionHashSql + " = expected.DefinitionHash) = 9 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:IdentityPrimaryKeys', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'Users', N'PK_Users', N'UserId'), (N'Roles', N'PK_Roles', N'RoleId'), " +
        "(N'UserRoles', N'PK_UserRoles', N'UserRoleId'), " +
        "(N'RolePermissions', N'PK_RolePermissions', N'RolePermissionId'), " +
        "(N'UserPermissions', N'PK_UserPermissions', N'UserPermissionId')) " +
        "AS expected(TableName, ConstraintName, KeyColumns) " +
        "INNER JOIN sys.tables AS ownerTable ON ownerTable.name = expected.TableName " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.key_constraints AS candidate ON candidate.parent_object_id = ownerTable.object_id " +
        "AND candidate.name = expected.ConstraintName AND candidate.type = N'PK' " +
        "INNER JOIN sys.indexes AS backingIndex ON backingIndex.object_id = ownerTable.object_id " +
        "AND backingIndex.index_id = candidate.unique_index_id " +
        "CROSS APPLY (SELECT STRING_AGG(keyColumn.name, N',') WITHIN GROUP (ORDER BY indexColumn.key_ordinal) " +
        "AS KeyColumns FROM sys.index_columns AS indexColumn " +
        "INNER JOIN sys.columns AS keyColumn ON keyColumn.object_id = indexColumn.object_id " +
        "AND keyColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = backingIndex.object_id AND indexColumn.index_id = backingIndex.index_id " +
        "AND indexColumn.key_ordinal > 0) AS actual " +
        "WHERE ownerSchema.name = N'dbo' AND backingIndex.is_unique = 1 " +
        "AND backingIndex.is_disabled = 0 AND actual.KeyColumns = expected.KeyColumns) = 5 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:IdentityUniqueIndexes', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'Users', N'UX_Users_NormalizedUsername', N'NormalizedUsername:ASC', N''), " +
        "(N'Roles', N'UX_Roles_NormalizedRoleName', N'NormalizedRoleName:ASC', N''), " +
        "(N'UserRoles', N'UX_UserRoles_UserId_RoleId', N'UserId:ASC,RoleId:ASC', N''), " +
        "(N'RolePermissions', N'UX_RolePermissions_RoleId_PermissionKey', N'RoleId:ASC,PermissionKey:ASC', N''), " +
        "(N'UserPermissions', N'UX_UserPermissions_UserId_PermissionKey', N'UserId:ASC,PermissionKey:ASC', N'')) " +
        "AS expected(TableName, IndexName, KeyColumns, FilterDefinition) " +
        "INNER JOIN sys.tables AS ownerTable ON ownerTable.name = expected.TableName " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.indexes AS candidate ON candidate.object_id = ownerTable.object_id " +
        "AND candidate.name = expected.IndexName " +
        "CROSS APPLY (SELECT STRING_AGG(keyColumn.name + CASE WHEN indexColumn.is_descending_key = 1 " +
        "THEN N':DESC' ELSE N':ASC' END, N',') WITHIN GROUP (ORDER BY indexColumn.key_ordinal) AS KeyColumns " +
        "FROM sys.index_columns AS indexColumn INNER JOIN sys.columns AS keyColumn " +
        "ON keyColumn.object_id = indexColumn.object_id AND keyColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = candidate.object_id AND indexColumn.index_id = candidate.index_id " +
        "AND indexColumn.key_ordinal > 0) AS actual " +
        "WHERE ownerSchema.name = N'dbo' AND candidate.is_unique = 1 AND candidate.is_disabled = 0 " +
        "AND actual.KeyColumns = expected.KeyColumns " +
        "AND ((expected.FilterDefinition = N'' AND candidate.has_filter = 0) OR " +
        "(expected.FilterDefinition <> N'' AND candidate.has_filter = 1 " +
        "AND candidate.filter_definition IS NOT NULL " +
        "AND UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(candidate.filter_definition, " +
        "N'[', N''), N']', N''), N' ', N''), N'(', N''), N')', N'')) = expected.FilterDefinition))) " +
        "= 5 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:IdentityForeignKeys', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'UserRoles', N'Users', N'FK_UserRoles_Users', N'UserId=UserId'), " +
        "(N'UserRoles', N'Roles', N'FK_UserRoles_Roles', N'RoleId=RoleId'), " +
        "(N'RolePermissions', N'Roles', N'FK_RolePermissions_Roles', N'RoleId=RoleId'), " +
        "(N'UserPermissions', N'Users', N'FK_UserPermissions_Users', N'UserId=UserId')) " +
        "AS expected(ParentTableName, ReferencedTableName, ForeignKeyName, ColumnMappings) " +
        "INNER JOIN sys.tables AS parentTable ON parentTable.name = expected.ParentTableName " +
        "INNER JOIN sys.schemas AS parentSchema ON parentSchema.schema_id = parentTable.schema_id " +
        "INNER JOIN sys.tables AS referencedTable ON referencedTable.name = expected.ReferencedTableName " +
        "INNER JOIN sys.schemas AS referencedSchema ON referencedSchema.schema_id = referencedTable.schema_id " +
        "INNER JOIN sys.foreign_keys AS candidate ON candidate.parent_object_id = parentTable.object_id " +
        "AND candidate.referenced_object_id = referencedTable.object_id " +
        "AND candidate.name = expected.ForeignKeyName " +
        "CROSS APPLY (SELECT STRING_AGG(parentColumn.name + N'=' + referencedColumn.name, N',') " +
        "WITHIN GROUP (ORDER BY mapping.constraint_column_id) AS ColumnMappings " +
        "FROM sys.foreign_key_columns AS mapping " +
        "INNER JOIN sys.columns AS parentColumn ON parentColumn.object_id = mapping.parent_object_id " +
        "AND parentColumn.column_id = mapping.parent_column_id " +
        "INNER JOIN sys.columns AS referencedColumn ON referencedColumn.object_id = mapping.referenced_object_id " +
        "AND referencedColumn.column_id = mapping.referenced_column_id " +
        "WHERE mapping.constraint_object_id = candidate.object_id) AS actual " +
        "WHERE parentSchema.name = N'dbo' AND referencedSchema.name = N'dbo' " +
        "AND candidate.is_disabled = 0 AND candidate.is_not_trusted = 0 " +
        "AND candidate.delete_referential_action = 0 AND candidate.update_referential_action = 0 " +
        "AND candidate.is_not_for_replication = 0 " +
        "AND actual.ColumnMappings = expected.ColumnMappings) = 4 THEN 1 END UNION ALL " +
        "SELECT N'Table:Products', OBJECT_ID(N'dbo.Products', N'U') UNION ALL " +
        "SELECT N'Table:Warehouses', OBJECT_ID(N'dbo.Warehouses', N'U') UNION ALL " +
        "SELECT N'Table:Employees', OBJECT_ID(N'dbo.Employees', N'U') UNION ALL " +
        "SELECT N'Table:Customers', OBJECT_ID(N'dbo.Customers', N'U') UNION ALL " +
        "SELECT N'Invariant:CatalogChecks', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'Products', N'CK_Products_Code_NotBlank', N'23FCEB161C71A42FEC25AC9AF6841A808FF0D0F3E38C3E78D9DFEDF7A4EDA94A'), " +
        "(N'Products', N'CK_Products_Name_NotBlank', N'EEE8C692DF3DB15031900C2CE3E79E9E96A0BDC4F164BEC3448D0B2240898AFE'), " +
        "(N'Products', N'CK_Products_Unit_NotBlank', N'85465FFFF967FFD759157FAC4ED802B6BF5ECB62F9A5D7C3ABCA6DD13C1C1ADE'), " +
        "(N'Warehouses', N'CK_Warehouses_Code_NotBlank', N'23FCEB161C71A42FEC25AC9AF6841A808FF0D0F3E38C3E78D9DFEDF7A4EDA94A'), " +
        "(N'Warehouses', N'CK_Warehouses_Name_NotBlank', N'EEE8C692DF3DB15031900C2CE3E79E9E96A0BDC4F164BEC3448D0B2240898AFE'), " +
        "(N'Employees', N'CK_Employees_Code_NotBlank', N'23FCEB161C71A42FEC25AC9AF6841A808FF0D0F3E38C3E78D9DFEDF7A4EDA94A'), " +
        "(N'Employees', N'CK_Employees_Name_NotBlank', N'EEE8C692DF3DB15031900C2CE3E79E9E96A0BDC4F164BEC3448D0B2240898AFE'), " +
        "(N'Customers', N'CK_Customers_Code_NotBlank', N'23FCEB161C71A42FEC25AC9AF6841A808FF0D0F3E38C3E78D9DFEDF7A4EDA94A'), " +
        "(N'Customers', N'CK_Customers_Name_NotBlank', N'EEE8C692DF3DB15031900C2CE3E79E9E96A0BDC4F164BEC3448D0B2240898AFE')) " +
        "AS expected(TableName, ConstraintName, DefinitionHash) " +
        "INNER JOIN sys.check_constraints AS candidate ON candidate.name = expected.ConstraintName " +
        "AND candidate.parent_object_id = OBJECT_ID(N'dbo.' + expected.TableName, N'U') " +
        "WHERE SCHEMA_NAME(candidate.schema_id) = N'dbo' AND candidate.is_disabled = 0 " +
        "AND candidate.is_not_trusted = 0 AND " + CheckDefinitionHashSql + " = expected.DefinitionHash) " +
        "= 9 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:CatalogPrimaryKeys', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'Products', N'PK_Products', N'ProductId'), " +
        "(N'Warehouses', N'PK_Warehouses', N'WarehouseId'), " +
        "(N'Employees', N'PK_Employees', N'EmployeeId'), " +
        "(N'Customers', N'PK_Customers', N'CustomerId')) " +
        "AS expected(TableName, ConstraintName, KeyColumns) " +
        "INNER JOIN sys.tables AS ownerTable ON ownerTable.name = expected.TableName " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.key_constraints AS candidate ON candidate.parent_object_id = ownerTable.object_id " +
        "AND candidate.name = expected.ConstraintName AND candidate.type = N'PK' " +
        "INNER JOIN sys.indexes AS backingIndex ON backingIndex.object_id = ownerTable.object_id " +
        "AND backingIndex.index_id = candidate.unique_index_id " +
        "CROSS APPLY (SELECT STRING_AGG(keyColumn.name, N',') WITHIN GROUP (ORDER BY indexColumn.key_ordinal) " +
        "AS KeyColumns FROM sys.index_columns AS indexColumn " +
        "INNER JOIN sys.columns AS keyColumn ON keyColumn.object_id = indexColumn.object_id " +
        "AND keyColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = backingIndex.object_id AND indexColumn.index_id = backingIndex.index_id " +
        "AND indexColumn.key_ordinal > 0) AS actual " +
        "WHERE ownerSchema.name = N'dbo' AND backingIndex.is_unique = 1 " +
        "AND backingIndex.is_disabled = 0 AND actual.KeyColumns = expected.KeyColumns) = 4 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:CatalogUniqueIndexes', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'Products', N'UX_Products_Code', N'Code:ASC', N''), " +
        "(N'Warehouses', N'UX_Warehouses_Code', N'Code:ASC', N''), " +
        "(N'Employees', N'UX_Employees_Code', N'Code:ASC', N''), " +
        "(N'Employees', N'UX_Employees_UserId', N'UserId:ASC', N'USERIDISNOTNULL'), " +
        "(N'Customers', N'UX_Customers_Code', N'Code:ASC', N'')) " +
        "AS expected(TableName, IndexName, KeyColumns, FilterDefinition) " +
        "INNER JOIN sys.tables AS ownerTable ON ownerTable.name = expected.TableName " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.indexes AS candidate ON candidate.object_id = ownerTable.object_id " +
        "AND candidate.name = expected.IndexName " +
        "CROSS APPLY (SELECT STRING_AGG(keyColumn.name + CASE WHEN indexColumn.is_descending_key = 1 " +
        "THEN N':DESC' ELSE N':ASC' END, N',') WITHIN GROUP (ORDER BY indexColumn.key_ordinal) AS KeyColumns " +
        "FROM sys.index_columns AS indexColumn INNER JOIN sys.columns AS keyColumn " +
        "ON keyColumn.object_id = indexColumn.object_id AND keyColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = candidate.object_id AND indexColumn.index_id = candidate.index_id " +
        "AND indexColumn.key_ordinal > 0) AS actual " +
        "WHERE ownerSchema.name = N'dbo' AND candidate.is_unique = 1 AND candidate.is_disabled = 0 " +
        "AND actual.KeyColumns = expected.KeyColumns " +
        "AND ((expected.FilterDefinition = N'' AND candidate.has_filter = 0) OR " +
        "(expected.FilterDefinition <> N'' AND candidate.has_filter = 1 " +
        "AND candidate.filter_definition IS NOT NULL " +
        "AND UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(candidate.filter_definition, " +
        "N'[', N''), N']', N''), N' ', N''), N'(', N''), N')', N'')) = expected.FilterDefinition))) " +
        "= 5 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:CatalogForeignKeys', CASE WHEN EXISTS (SELECT 1 " +
        "FROM sys.foreign_keys AS candidate " +
        "CROSS APPLY (SELECT STRING_AGG(parentColumn.name + N'=' + referencedColumn.name, N',') " +
        "WITHIN GROUP (ORDER BY mapping.constraint_column_id) AS ColumnMappings " +
        "FROM sys.foreign_key_columns AS mapping " +
        "INNER JOIN sys.columns AS parentColumn ON parentColumn.object_id = mapping.parent_object_id " +
        "AND parentColumn.column_id = mapping.parent_column_id " +
        "INNER JOIN sys.columns AS referencedColumn ON referencedColumn.object_id = mapping.referenced_object_id " +
        "AND referencedColumn.column_id = mapping.referenced_column_id " +
        "WHERE mapping.constraint_object_id = candidate.object_id) AS actual " +
        "WHERE candidate.parent_object_id = OBJECT_ID(N'dbo.Employees', N'U') " +
        "AND candidate.referenced_object_id = OBJECT_ID(N'dbo.Users', N'U') " +
        "AND candidate.name = N'FK_Employees_Users' AND actual.ColumnMappings = N'UserId=UserId' " +
        "AND candidate.is_disabled = 0 AND candidate.is_not_trusted = 0 " +
        "AND candidate.delete_referential_action = 0 AND candidate.update_referential_action = 0 " +
        "AND candidate.is_not_for_replication = 0) THEN 1 END UNION ALL " +
        "SELECT N'Function:CatalogTextContainsNul', (SELECT candidate.object_id FROM sys.objects AS candidate " +
        "INNER JOIN sys.sql_modules AS module ON module.object_id = candidate.object_id " +
        "WHERE candidate.object_id = OBJECT_ID(N'dbo.CatalogTextContainsNul', N'FN') " +
        "AND module.is_schema_bound = 1 AND CONVERT(varchar(64), HASHBYTES('SHA2_256', module.definition), 2) " +
        "= N'01E1398093EDEF37C189633F5326D1A341AB0F1F92818A1D14966977096711EA') UNION ALL " +
        "SELECT N'Invariant:CatalogNulChecks', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'Products', N'CK_Products_Code_NoNul', N'E9DB7942EED2E287580FF319821B2D43EABD3382376C44F960475E94AE0708B1'), " +
        "(N'Products', N'CK_Products_Name_NoNul', N'A7833AA0BE5F9F44B010989AD1732737836CE8665BD0F97A533A2A7429ED134B'), " +
        "(N'Products', N'CK_Products_Unit_NoNul', N'13F4C1476ED0B7FA5CF0AB8DD7ED2D7F93BE7EF92DF485F88C340F66D929F64B'), " +
        "(N'Warehouses', N'CK_Warehouses_Code_NoNul', N'E9DB7942EED2E287580FF319821B2D43EABD3382376C44F960475E94AE0708B1'), " +
        "(N'Warehouses', N'CK_Warehouses_Name_NoNul', N'A7833AA0BE5F9F44B010989AD1732737836CE8665BD0F97A533A2A7429ED134B'), " +
        "(N'Employees', N'CK_Employees_Code_NoNul', N'E9DB7942EED2E287580FF319821B2D43EABD3382376C44F960475E94AE0708B1'), " +
        "(N'Employees', N'CK_Employees_Name_NoNul', N'A7833AA0BE5F9F44B010989AD1732737836CE8665BD0F97A533A2A7429ED134B'), " +
        "(N'Customers', N'CK_Customers_Code_NoNul', N'E9DB7942EED2E287580FF319821B2D43EABD3382376C44F960475E94AE0708B1'), " +
        "(N'Customers', N'CK_Customers_Name_NoNul', N'A7833AA0BE5F9F44B010989AD1732737836CE8665BD0F97A533A2A7429ED134B')) " +
        "AS expected(TableName, ConstraintName, DefinitionHash) " +
        "INNER JOIN sys.check_constraints AS candidate ON candidate.name = expected.ConstraintName " +
        "AND candidate.parent_object_id = OBJECT_ID(N'dbo.' + expected.TableName, N'U') " +
        "WHERE SCHEMA_NAME(candidate.schema_id) = N'dbo' AND candidate.is_disabled = 0 " +
        "AND candidate.is_not_trusted = 0 AND " + CheckDefinitionHashSql +
        " = expected.DefinitionHash) = 9 THEN 1 END UNION ALL " +
        "SELECT N'Table:GoodsReceipts', OBJECT_ID(N'dbo.GoodsReceipts', N'U') UNION ALL " +
        "SELECT N'Table:GoodsReceiptLines', OBJECT_ID(N'dbo.GoodsReceiptLines', N'U') UNION ALL " +
        "SELECT N'Invariant:GoodsReceiptChecks', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'GoodsReceipts', N'CK_GoodsReceipts_ReceiptNo_NotBlank', N'3D0358848A37394FF49216C835A00A9F05E599C613C359E70A3B7D30D82DD1D7'), " +
        "(N'GoodsReceipts', N'CK_GoodsReceipts_ReceiptNo_NoNul', N'D4C15759ABE188C90E3AD4068427FEF438E2D09867441D1A8B9B29D5E5212ED6'), " +
        "(N'GoodsReceipts', N'CK_GoodsReceipts_Status', N'8344730B300FB3ED5D0A6FF393665103C9DE7EA2E15E760B3D49E0500749265F'), " +
        "(N'GoodsReceipts', N'CK_GoodsReceipts_PostingAudit', N'327AA8B6BFA0CBB490B3B2BD5316A8D0A1847A681026C9EBC407812688C3AB5B'), " +
        "(N'GoodsReceiptLines', N'CK_GoodsReceiptLines_LineNo', N'965B6391581EA73F0E5722208E50B64D29EB79476DD28A2CDF64E3232E51ED03'), " +
        "(N'GoodsReceiptLines', N'CK_GoodsReceiptLines_Quantity', N'C2C67FAFEFA3E52357BF08DE91DE08AD93DCC89AB9A112ACCBF265D14EE835DE'), " +
        "(N'GoodsReceiptLines', N'CK_GoodsReceiptLines_UnitCost', N'D623BB8D3536CD0C49E7ABB973C5AF40A556F173F4971E50E7ADBC8A1F9D02E1')) " +
        "AS expected(TableName, ConstraintName, DefinitionHash) " +
        "INNER JOIN sys.check_constraints AS candidate ON candidate.name = expected.ConstraintName " +
        "AND candidate.parent_object_id = OBJECT_ID(N'dbo.' + expected.TableName, N'U') " +
        "WHERE SCHEMA_NAME(candidate.schema_id) = N'dbo' AND candidate.is_disabled = 0 " +
        "AND candidate.is_not_trusted = 0 AND " + CheckDefinitionHashSql +
        " = expected.DefinitionHash) = 7 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:GoodsReceiptPrimaryKeys', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'GoodsReceipts', N'PK_GoodsReceipts', N'ReceiptId'), " +
        "(N'GoodsReceiptLines', N'PK_GoodsReceiptLines', N'ReceiptLineId')) " +
        "AS expected(TableName, ConstraintName, KeyColumns) " +
        "INNER JOIN sys.tables AS ownerTable ON ownerTable.name = expected.TableName " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.key_constraints AS candidate ON candidate.parent_object_id = ownerTable.object_id " +
        "AND candidate.name = expected.ConstraintName AND candidate.type = N'PK' " +
        "INNER JOIN sys.indexes AS backingIndex ON backingIndex.object_id = ownerTable.object_id " +
        "AND backingIndex.index_id = candidate.unique_index_id " +
        "CROSS APPLY (SELECT STRING_AGG(keyColumn.name, N',') WITHIN GROUP (ORDER BY indexColumn.key_ordinal) " +
        "AS KeyColumns FROM sys.index_columns AS indexColumn " +
        "INNER JOIN sys.columns AS keyColumn ON keyColumn.object_id = indexColumn.object_id " +
        "AND keyColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = backingIndex.object_id AND indexColumn.index_id = backingIndex.index_id " +
        "AND indexColumn.key_ordinal > 0) AS actual " +
        "WHERE ownerSchema.name = N'dbo' AND backingIndex.is_unique = 1 " +
        "AND backingIndex.is_disabled = 0 AND actual.KeyColumns = expected.KeyColumns) = 2 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:GoodsReceiptUniqueIndexes', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'GoodsReceipts', N'UX_GoodsReceipts_ReceiptNo', N'ReceiptNo:ASC', N''), " +
        "(N'GoodsReceiptLines', N'UX_GoodsReceiptLines_ReceiptId_LineNo', N'ReceiptId:ASC,LineNo:ASC', N''), " +
        "(N'GoodsReceiptLines', N'UX_GoodsReceiptLines_ReceiptId_ProductId', " +
        "N'ReceiptId:ASC,ProductId:ASC', N'')) " +
        "AS expected(TableName, IndexName, KeyColumns, FilterDefinition) " +
        "INNER JOIN sys.tables AS ownerTable ON ownerTable.name = expected.TableName " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.indexes AS candidate ON candidate.object_id = ownerTable.object_id " +
        "AND candidate.name = expected.IndexName " +
        "CROSS APPLY (SELECT STRING_AGG(keyColumn.name + CASE WHEN indexColumn.is_descending_key = 1 " +
        "THEN N':DESC' ELSE N':ASC' END, N',') WITHIN GROUP (ORDER BY indexColumn.key_ordinal) AS KeyColumns " +
        "FROM sys.index_columns AS indexColumn INNER JOIN sys.columns AS keyColumn " +
        "ON keyColumn.object_id = indexColumn.object_id AND keyColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = candidate.object_id AND indexColumn.index_id = candidate.index_id " +
        "AND indexColumn.key_ordinal > 0) AS actual " +
        "WHERE ownerSchema.name = N'dbo' AND candidate.is_unique = 1 AND candidate.is_disabled = 0 " +
        "AND actual.KeyColumns = expected.KeyColumns " +
        "AND ((expected.FilterDefinition = N'' AND candidate.has_filter = 0) OR " +
        "(expected.FilterDefinition <> N'' AND candidate.has_filter = 1 " +
        "AND candidate.filter_definition IS NOT NULL " +
        "AND UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(candidate.filter_definition, " +
        "N'[', N''), N']', N''), N' ', N''), N'(', N''), N')', N'')) = expected.FilterDefinition))) " +
        "= 3 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:GoodsReceiptForeignKeys', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'GoodsReceipts', N'Warehouses', N'FK_GoodsReceipts_Warehouses', N'WarehouseId=WarehouseId'), " +
        "(N'GoodsReceipts', N'Employees', N'FK_GoodsReceipts_Employees', N'EmployeeId=EmployeeId'), " +
        "(N'GoodsReceipts', N'Users', N'FK_GoodsReceipts_CreatedByUsers', N'CreatedByUserId=UserId'), " +
        "(N'GoodsReceipts', N'Users', N'FK_GoodsReceipts_UpdatedByUsers', N'UpdatedByUserId=UserId'), " +
        "(N'GoodsReceipts', N'Users', N'FK_GoodsReceipts_PostedByUsers', N'PostedByUserId=UserId'), " +
        "(N'GoodsReceiptLines', N'GoodsReceipts', N'FK_GoodsReceiptLines_GoodsReceipts', N'ReceiptId=ReceiptId'), " +
        "(N'GoodsReceiptLines', N'Products', N'FK_GoodsReceiptLines_Products', N'ProductId=ProductId'), " +
        "(N'GoodsReceiptLines', N'Users', N'FK_GoodsReceiptLines_CreatedByUsers', N'CreatedByUserId=UserId'), " +
        "(N'GoodsReceiptLines', N'Users', N'FK_GoodsReceiptLines_UpdatedByUsers', N'UpdatedByUserId=UserId')) " +
        "AS expected(ParentTableName, ReferencedTableName, ForeignKeyName, ColumnMappings) " +
        "INNER JOIN sys.tables AS parentTable ON parentTable.name = expected.ParentTableName " +
        "INNER JOIN sys.schemas AS parentSchema ON parentSchema.schema_id = parentTable.schema_id " +
        "INNER JOIN sys.tables AS referencedTable ON referencedTable.name = expected.ReferencedTableName " +
        "INNER JOIN sys.schemas AS referencedSchema ON referencedSchema.schema_id = referencedTable.schema_id " +
        "INNER JOIN sys.foreign_keys AS candidate ON candidate.parent_object_id = parentTable.object_id " +
        "AND candidate.referenced_object_id = referencedTable.object_id " +
        "AND candidate.name = expected.ForeignKeyName " +
        "CROSS APPLY (SELECT STRING_AGG(parentColumn.name + N'=' + referencedColumn.name, N',') " +
        "WITHIN GROUP (ORDER BY mapping.constraint_column_id) AS ColumnMappings " +
        "FROM sys.foreign_key_columns AS mapping " +
        "INNER JOIN sys.columns AS parentColumn ON parentColumn.object_id = mapping.parent_object_id " +
        "AND parentColumn.column_id = mapping.parent_column_id " +
        "INNER JOIN sys.columns AS referencedColumn ON referencedColumn.object_id = mapping.referenced_object_id " +
        "AND referencedColumn.column_id = mapping.referenced_column_id " +
        "WHERE mapping.constraint_object_id = candidate.object_id) AS actual " +
        "WHERE parentSchema.name = N'dbo' AND referencedSchema.name = N'dbo' " +
        "AND candidate.is_disabled = 0 AND candidate.is_not_trusted = 0 " +
        "AND candidate.delete_referential_action = 0 AND candidate.update_referential_action = 0 " +
        "AND candidate.is_not_for_replication = 0 " +
        "AND actual.ColumnMappings = expected.ColumnMappings) = 9 THEN 1 END UNION ALL " +
        "SELECT N'Sequence:GoodsReceiptNumberSequence', (SELECT candidate.object_id " +
        "FROM sys.sequences AS candidate " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = candidate.schema_id " +
        "WHERE ownerSchema.name = N'dbo' AND candidate.name = N'GoodsReceiptNumberSequence' " +
        "AND candidate.system_type_id = TYPE_ID(N'bigint') " +
        "AND candidate.user_type_id = TYPE_ID(N'bigint') " +
        "AND CONVERT(bigint, candidate.start_value) = 1 " +
        "AND CONVERT(bigint, candidate.increment) = 1 " +
        "AND CONVERT(bigint, candidate.minimum_value) = 1 " +
        "AND CONVERT(bigint, candidate.maximum_value) = 9999999999 " +
        "AND candidate.is_cycling = 0) UNION ALL " +
        "SELECT N'Table:StockLedger', OBJECT_ID(N'dbo.StockLedger', N'U') UNION ALL " +
        "SELECT N'Table:StockBalances', OBJECT_ID(N'dbo.StockBalances', N'U') UNION ALL " +
        "SELECT N'Invariant:InventoryChecks', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'StockLedger', N'CK_StockLedger_DocumentType_NotBlank', N'0C092258E5E7154925A31288A8984D884C038C259693CF02698C44DB02B12951'), " +
        "(N'StockLedger', N'CK_StockLedger_DocumentType_NoNul', N'C90D111F206E7DEB209C1594FC875A6743EE219763EF96B3703402C57534894F'), " +
        "(N'StockLedger', N'CK_StockLedger_QuantityChange_NonZero', N'EB2C31FB555286D2AE222F5273CEE87F5A26834BB640B7331D4FD430F32F9FEA')) " +
        "AS expected(TableName, ConstraintName, DefinitionHash) INNER JOIN sys.check_constraints AS candidate " +
        "ON candidate.name = expected.ConstraintName " +
        "AND candidate.parent_object_id = OBJECT_ID(N'dbo.' + expected.TableName, N'U') " +
        "WHERE SCHEMA_NAME(candidate.schema_id) = N'dbo' " +
        "AND candidate.is_disabled = 0 AND candidate.is_not_trusted = 0 AND " +
        CheckDefinitionHashSql + " = expected.DefinitionHash) = 3 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:InventoryPrimaryKeys', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'StockLedger', N'PK_StockLedger', N'LedgerId'), " +
        "(N'StockBalances', N'PK_StockBalances', N'WarehouseId,ProductId')) " +
        "AS expected(TableName, ConstraintName, KeyColumns) " +
        "INNER JOIN sys.tables AS ownerTable ON ownerTable.name = expected.TableName " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = ownerTable.schema_id " +
        "INNER JOIN sys.key_constraints AS candidate ON candidate.parent_object_id = ownerTable.object_id " +
        "AND candidate.name = expected.ConstraintName AND candidate.type = N'PK' " +
        "INNER JOIN sys.indexes AS backingIndex ON backingIndex.object_id = ownerTable.object_id " +
        "AND backingIndex.index_id = candidate.unique_index_id " +
        "CROSS APPLY (SELECT STRING_AGG(keyColumn.name, N',') WITHIN GROUP (ORDER BY indexColumn.key_ordinal) " +
        "AS KeyColumns FROM sys.index_columns AS indexColumn " +
        "INNER JOIN sys.columns AS keyColumn ON keyColumn.object_id = indexColumn.object_id " +
        "AND keyColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = backingIndex.object_id AND indexColumn.index_id = backingIndex.index_id " +
        "AND indexColumn.key_ordinal > 0) AS actual " +
        "WHERE ownerSchema.name = N'dbo' AND backingIndex.is_unique = 1 " +
        "AND backingIndex.is_disabled = 0 AND actual.KeyColumns = expected.KeyColumns) = 2 THEN 1 END UNION ALL " +
        "SELECT N'Invariant:InventoryUniqueIndexes', CASE WHEN EXISTS (SELECT 1 " +
        "FROM sys.indexes AS candidate " +
        "CROSS APPLY (SELECT STRING_AGG(keyColumn.name + CASE WHEN indexColumn.is_descending_key = 1 " +
        "THEN N':DESC' ELSE N':ASC' END, N',') WITHIN GROUP (ORDER BY indexColumn.key_ordinal) AS KeyColumns " +
        "FROM sys.index_columns AS indexColumn INNER JOIN sys.columns AS keyColumn " +
        "ON keyColumn.object_id = indexColumn.object_id AND keyColumn.column_id = indexColumn.column_id " +
        "WHERE indexColumn.object_id = candidate.object_id AND indexColumn.index_id = candidate.index_id " +
        "AND indexColumn.key_ordinal > 0) AS actual " +
        "WHERE candidate.object_id = OBJECT_ID(N'dbo.StockLedger', N'U') " +
        "AND candidate.name = N'UX_StockLedger_DocumentType_DocumentLineId' " +
        "AND candidate.is_unique = 1 AND candidate.is_disabled = 0 " +
        "AND actual.KeyColumns = N'DocumentType:ASC,DocumentLineId:ASC' " +
        "AND candidate.has_filter = 0) THEN 1 END UNION ALL " +
        "SELECT N'Invariant:InventoryForeignKeys', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'StockLedger', N'Warehouses', N'FK_StockLedger_Warehouses', N'WarehouseId=WarehouseId'), " +
        "(N'StockLedger', N'Products', N'FK_StockLedger_Products', N'ProductId=ProductId'), " +
        "(N'StockLedger', N'Users', N'FK_StockLedger_PostedByUsers', N'PostedByUserId=UserId'), " +
        "(N'StockBalances', N'Warehouses', N'FK_StockBalances_Warehouses', N'WarehouseId=WarehouseId'), " +
        "(N'StockBalances', N'Products', N'FK_StockBalances_Products', N'ProductId=ProductId')) " +
        "AS expected(ParentTableName, ReferencedTableName, ForeignKeyName, ColumnMappings) " +
        "INNER JOIN sys.tables AS parentTable ON parentTable.name = expected.ParentTableName " +
        "INNER JOIN sys.schemas AS parentSchema ON parentSchema.schema_id = parentTable.schema_id " +
        "INNER JOIN sys.tables AS referencedTable ON referencedTable.name = expected.ReferencedTableName " +
        "INNER JOIN sys.schemas AS referencedSchema ON referencedSchema.schema_id = referencedTable.schema_id " +
        "INNER JOIN sys.foreign_keys AS candidate ON candidate.parent_object_id = parentTable.object_id " +
        "AND candidate.referenced_object_id = referencedTable.object_id " +
        "AND candidate.name = expected.ForeignKeyName " +
        "CROSS APPLY (SELECT STRING_AGG(parentColumn.name + N'=' + referencedColumn.name, N',') " +
        "WITHIN GROUP (ORDER BY mapping.constraint_column_id) AS ColumnMappings " +
        "FROM sys.foreign_key_columns AS mapping " +
        "INNER JOIN sys.columns AS parentColumn ON parentColumn.object_id = mapping.parent_object_id " +
        "AND parentColumn.column_id = mapping.parent_column_id " +
        "INNER JOIN sys.columns AS referencedColumn ON referencedColumn.object_id = mapping.referenced_object_id " +
        "AND referencedColumn.column_id = mapping.referenced_column_id " +
        "WHERE mapping.constraint_object_id = candidate.object_id) AS actual " +
        "WHERE parentSchema.name = N'dbo' AND referencedSchema.name = N'dbo' " +
        "AND candidate.is_disabled = 0 AND candidate.is_not_trusted = 0 " +
        "AND candidate.delete_referential_action = 0 AND candidate.update_referential_action = 0 " +
        "AND candidate.is_not_for_replication = 0 " +
        "AND actual.ColumnMappings = expected.ColumnMappings) = 5 THEN 1 END UNION ALL " +
        "SELECT N'Trigger:TR_StockLedger_AppendOnly', (SELECT candidate.object_id FROM sys.triggers AS candidate " +
        "INNER JOIN sys.sql_modules AS module ON module.object_id = candidate.object_id " +
        "WHERE candidate.object_id = OBJECT_ID(N'dbo.TR_StockLedger_AppendOnly', N'TR') " +
        "AND candidate.parent_id = OBJECT_ID(N'dbo.StockLedger', N'U') AND candidate.is_disabled = 0 " +
        "AND candidate.is_instead_of_trigger = 1 " +
        "AND (SELECT COUNT(*) FROM sys.trigger_events AS event WHERE event.object_id = candidate.object_id " +
        "AND event.type_desc IN (N'UPDATE', N'DELETE')) = 2 " +
        "AND (SELECT COUNT(*) FROM sys.trigger_events AS event WHERE event.object_id = candidate.object_id) = 2 " +
        "AND CONVERT(varchar(64), HASHBYTES('SHA2_256', module.definition), 2) = " +
        "N'1E44C8680130FF964973EF5E580CC0FE09C459DA3B5A1DE5433C20F7BBDD513B')" +
        ") AS Artifacts WHERE ObjectId IS NOT NULL " +
        "UNION ALL SELECT N'Unknown:UserObject' WHERE EXISTS (" +
        "SELECT 1 FROM sys.objects AS candidate " +
        "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = candidate.schema_id " +
        "WHERE candidate.is_ms_shipped = 0 " +
        "AND (candidate.parent_object_id = 0 OR candidate.type IN (N'TR', N'TA')) " +
        "AND NOT (ownerSchema.name = N'dbo' AND (" +
        "(candidate.type = N'U' AND candidate.name IN (" +
        "N'SchemaVersions', N'Users', N'Roles', N'UserRoles', N'RolePermissions', N'UserPermissions', " +
        "N'Products', N'Warehouses', N'Employees', N'Customers', " +
        "N'GoodsReceipts', N'GoodsReceiptLines', N'StockLedger', N'StockBalances', " +
        "N'Permissions', N'PermissionLegacyAssignmentDrift', N'PermissionSynchronizationState', " +
        "N'SystemManagedRoles', N'PermissionGrantApprovals', N'ManagedRolePermissionGrants', " +
        "N'SecurityBootstrapState', N'PermissionSecurityAudit')) OR " +
        "(candidate.type = N'FN' AND candidate.name = N'CatalogTextContainsNul') OR " +
        "(candidate.type = N'SO' AND candidate.name = N'GoodsReceiptNumberSequence') OR " +
        "(candidate.type = N'TR' AND candidate.name = N'TR_StockLedger_AppendOnly' " +
        "AND candidate.parent_object_id = OBJECT_ID(N'dbo.StockLedger', N'U'))))) " +
        "OR EXISTS (SELECT 1 FROM sys.triggers AS candidate " +
        "WHERE candidate.parent_class = 0 AND candidate.is_ms_shipped = 0) " +
        "OR EXISTS (SELECT 1 FROM sys.types AS candidate WHERE candidate.is_user_defined = 1);";

    private readonly string _connectionString;
    private readonly bool _requireCurrentPermissionRelease;
    private readonly IPermissionRegistry _permissionRegistry;

    public SqlSchemaCompatibilityChecker(string connectionString)
        : this(connectionString, requireCurrentPermissionRelease: true)
    {
    }

    public SqlSchemaCompatibilityChecker(
        string connectionString,
        bool requireCurrentPermissionRelease)
        : this(
            connectionString,
            requireCurrentPermissionRelease,
            PermissionRegistryComposition.CreateDefault())
    {
    }

    public SqlSchemaCompatibilityChecker(
        string connectionString,
        bool requireCurrentPermissionRelease,
        IPermissionRegistry permissionRegistry)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "A SQL Server connection string is required.",
                nameof(connectionString));
        }

        _connectionString = connectionString;
        _requireCurrentPermissionRelease = requireCurrentPermissionRelease;
        _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
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

        if (journalExists)
        {
            var columnArtifactRows = await connection.QueryAsync<string>(
                new CommandDefinition(
                    ColumnArtifactSql,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            artifacts.UnionWith(columnArtifactRows);

            var permissionArtifactRows = await connection.QueryAsync<string>(
                new CommandDefinition(
                    PermissionProvisioningArtifactSql,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            artifacts.UnionWith(permissionArtifactRows);
        }

        var compatibility = SchemaCompatibilityPolicy.Evaluate(
            journalExists,
            scripts,
            artifacts);
        if (!compatibility.IsCompatible || !_requireCurrentPermissionRelease)
        {
            return compatibility;
        }

        if (!PermissionReleaseManifest.MatchesCurrent(_permissionRegistry))
        {
            return PermissionSynchronizationRequired();
        }

        var lastAppliedReleaseId = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT LastAppliedReleaseId FROM dbo.PermissionSynchronizationState;",
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (string.Equals(
                lastAppliedReleaseId,
                PermissionReleaseManifest.CurrentReleaseId,
                StringComparison.Ordinal))
        {
            return compatibility;
        }

        return PermissionSynchronizationRequired();
    }

    private static SchemaCompatibilityResult PermissionSynchronizationRequired()
    {
        return new SchemaCompatibilityResult(
            SchemaCompatibilityStatus.PermissionSynchronizationRequired,
            "Security.PermissionSynchronizationRequired",
            "Danh mục quyền chưa được đồng bộ cho bản phát hành hiện tại. Liên hệ quản trị viên để chạy permission sync đã được phê duyệt.");
    }
}
