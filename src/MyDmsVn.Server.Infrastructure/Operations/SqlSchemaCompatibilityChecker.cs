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
    private const string CheckDefinitionHashSql =
        "CONVERT(varchar(64), HASHBYTES('SHA2_256', " +
        "UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(" +
        "candidate.definition, N'N'' ''', N'SPACE_LITERAL'), N'[', N''), N']', N''), " +
        "N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N''))), 2)";

    private const string ArtifactSql =
        "SELECT ArtifactKey FROM (" +
        "SELECT N'Table:Users' AS ArtifactKey, OBJECT_ID(N'dbo.Users', N'U') AS ObjectId UNION ALL " +
        "SELECT N'Table:Roles', OBJECT_ID(N'dbo.Roles', N'U') UNION ALL " +
        "SELECT N'Table:UserRoles', OBJECT_ID(N'dbo.UserRoles', N'U') UNION ALL " +
        "SELECT N'Table:RolePermissions', OBJECT_ID(N'dbo.RolePermissions', N'U') UNION ALL " +
        "SELECT N'Table:UserPermissions', OBJECT_ID(N'dbo.UserPermissions', N'U') UNION ALL " +
        "SELECT N'Invariant:IdentityChecks', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'CK_Users_Username_NotBlank', N'3B15153EA71E991806E379519D8D37C2786CFCC36E34E19EFEA691E479FD92A0'), " +
        "(N'CK_Users_NormalizedUsername_NotBlank', N'85ACE4F632F361662D674275F0B8EABAE5C08094A02D2478350917FBC98305BE'), " +
        "(N'CK_Users_DisplayName_NotBlank', N'80397D4D00619B4546C77C8B17ADD4E80196816542BD593FAC1380D176078B8D'), " +
        "(N'CK_Users_Source_NotBlank', N'78F1441993475AC4BDFE2061E8782223ACF302F23F7D5BAB6592A44B34FC96EC'), " +
        "(N'CK_Users_PasswordAlgorithm_NotBlank', N'77B87B6BB359C25CA6F908E81521AED421E9DF0429B723ABE8FFB0CB7E7F728B'), " +
        "(N'CK_Roles_RoleName_NotBlank', N'C4C80660CD7BB61436F6E3DE301011FC904FCC226235734DD293CAAF654DBA7D'), " +
        "(N'CK_Roles_NormalizedRoleName_NotBlank', N'CFAB0343432A8BA8E66A4E93245FCEAF0293C71C9BC9797C1826C4BFD5AE3939'), " +
        "(N'CK_RolePermissions_PermissionKey_NotBlank', N'CDCA87E34C8A2199DA85385882397E6B56D3EC877ABA875086B1853824922ABC'), " +
        "(N'CK_UserPermissions_PermissionKey_NotBlank', N'CDCA87E34C8A2199DA85385882397E6B56D3EC877ABA875086B1853824922ABC')) " +
        "AS expected(ConstraintName, DefinitionHash) INNER JOIN sys.check_constraints AS candidate " +
        "ON candidate.name = expected.ConstraintName WHERE SCHEMA_NAME(candidate.schema_id) = N'dbo' " +
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
        "(N'CK_Products_Code_NotBlank', N'08DF8F041A746F6F91477CA9907AFE9EEE6A7CB992F10ED47274CB2D1E2FD46E'), " +
        "(N'CK_Products_Name_NotBlank', N'86756261A05666C0F33B5F34F44AD3891DDA77644376073F0D76EDC8BA425176'), " +
        "(N'CK_Products_Unit_NotBlank', N'6F64D0750561687B85B5FCA4BF33D64633826EC1167BD4E3F3096307DF561F19'), " +
        "(N'CK_Warehouses_Code_NotBlank', N'08DF8F041A746F6F91477CA9907AFE9EEE6A7CB992F10ED47274CB2D1E2FD46E'), " +
        "(N'CK_Warehouses_Name_NotBlank', N'86756261A05666C0F33B5F34F44AD3891DDA77644376073F0D76EDC8BA425176'), " +
        "(N'CK_Employees_Code_NotBlank', N'08DF8F041A746F6F91477CA9907AFE9EEE6A7CB992F10ED47274CB2D1E2FD46E'), " +
        "(N'CK_Employees_Name_NotBlank', N'86756261A05666C0F33B5F34F44AD3891DDA77644376073F0D76EDC8BA425176'), " +
        "(N'CK_Customers_Code_NotBlank', N'08DF8F041A746F6F91477CA9907AFE9EEE6A7CB992F10ED47274CB2D1E2FD46E'), " +
        "(N'CK_Customers_Name_NotBlank', N'86756261A05666C0F33B5F34F44AD3891DDA77644376073F0D76EDC8BA425176')) " +
        "AS expected(ConstraintName, DefinitionHash) " +
        "INNER JOIN sys.check_constraints AS candidate ON candidate.name = expected.ConstraintName " +
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
        "SELECT N'Function:CatalogTextContainsNul', OBJECT_ID(N'dbo.CatalogTextContainsNul', N'FN') UNION ALL " +
        "SELECT N'Invariant:CatalogNulChecks', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'CK_Products_Code_NoNul', N'A1900299F6F0350C720043B087F6714964EBD218B0314C015181D59F89966199'), " +
        "(N'CK_Products_Name_NoNul', N'C34902DB9A1B1E6B195FB924F3B15E10755CBE04971158C639C0C4294AE2BF1E'), " +
        "(N'CK_Products_Unit_NoNul', N'89A745006832C28F8AFF58A31DFC0CB50EA5242F778891D9DBCEB2D534231D7D'), " +
        "(N'CK_Warehouses_Code_NoNul', N'A1900299F6F0350C720043B087F6714964EBD218B0314C015181D59F89966199'), " +
        "(N'CK_Warehouses_Name_NoNul', N'C34902DB9A1B1E6B195FB924F3B15E10755CBE04971158C639C0C4294AE2BF1E'), " +
        "(N'CK_Employees_Code_NoNul', N'A1900299F6F0350C720043B087F6714964EBD218B0314C015181D59F89966199'), " +
        "(N'CK_Employees_Name_NoNul', N'C34902DB9A1B1E6B195FB924F3B15E10755CBE04971158C639C0C4294AE2BF1E'), " +
        "(N'CK_Customers_Code_NoNul', N'A1900299F6F0350C720043B087F6714964EBD218B0314C015181D59F89966199'), " +
        "(N'CK_Customers_Name_NoNul', N'C34902DB9A1B1E6B195FB924F3B15E10755CBE04971158C639C0C4294AE2BF1E')) " +
        "AS expected(ConstraintName, DefinitionHash) " +
        "INNER JOIN sys.check_constraints AS candidate ON candidate.name = expected.ConstraintName " +
        "WHERE SCHEMA_NAME(candidate.schema_id) = N'dbo' AND candidate.is_disabled = 0 " +
        "AND candidate.is_not_trusted = 0 AND " + CheckDefinitionHashSql +
        " = expected.DefinitionHash) = 9 THEN 1 END UNION ALL " +
        "SELECT N'Table:GoodsReceipts', OBJECT_ID(N'dbo.GoodsReceipts', N'U') UNION ALL " +
        "SELECT N'Table:GoodsReceiptLines', OBJECT_ID(N'dbo.GoodsReceiptLines', N'U') UNION ALL " +
        "SELECT N'Invariant:GoodsReceiptChecks', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'CK_GoodsReceipts_ReceiptNo_NotBlank', N'00EE6BD571110CD06F5A31C02126EC7EB66A4E0C13BB2B08404DCAFEAC4DECDB'), " +
        "(N'CK_GoodsReceipts_ReceiptNo_NoNul', N'278E3265DD76A4BC8DA0C3888718C29615EB7ED234BD75152DFC1DF77FFC3EA2'), " +
        "(N'CK_GoodsReceipts_Status', N'348D3846CFC4048CA4B9D048ECD4E908B549648F7DAA616325BBCFA070A6607E'), " +
        "(N'CK_GoodsReceipts_PostingAudit', N'51BFFAC329F9AABE30D08495FC8D30BB03FB777391EE6CB3E7122BAC4AB3255F'), " +
        "(N'CK_GoodsReceiptLines_LineNo', N'51254FD0B514B76C710C685F7547F2EEA3A7485DEF215224E2EF38336396EB85'), " +
        "(N'CK_GoodsReceiptLines_Quantity', N'4110CCAF327CFE913F134B3DEEC9938D39B89D0E2B46D8A2667080B113FDAFC5'), " +
        "(N'CK_GoodsReceiptLines_UnitCost', N'022388DF4EDA22571599130311FC8F3118B529C4DD316A54C3F47424F4B97DAD')) " +
        "AS expected(ConstraintName, DefinitionHash) " +
        "INNER JOIN sys.check_constraints AS candidate ON candidate.name = expected.ConstraintName " +
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
        "SELECT N'Sequence:GoodsReceiptNumberSequence', OBJECT_ID(N'dbo.GoodsReceiptNumberSequence', N'SO') UNION ALL " +
        "SELECT N'Table:StockLedger', OBJECT_ID(N'dbo.StockLedger', N'U') UNION ALL " +
        "SELECT N'Table:StockBalances', OBJECT_ID(N'dbo.StockBalances', N'U') UNION ALL " +
        "SELECT N'Invariant:InventoryChecks', CASE WHEN (SELECT COUNT(*) FROM (VALUES " +
        "(N'CK_StockLedger_DocumentType_NotBlank', N'414C299D02F2681BF4C48183E699FF4DE2CED06786DF0ACFEA3C4DE0FBAF5010'), " +
        "(N'CK_StockLedger_DocumentType_NoNul', N'5BD99E0033178BA6DBE339255F1258F6FED48EC2332AA8AB2B8496F40A6B991F'), " +
        "(N'CK_StockLedger_QuantityChange_NonZero', N'CEAF9F21B05EE6D05B36C8295321AAE80E40CEE8045D17402D8F8725664ED75E')) " +
        "AS expected(ConstraintName, DefinitionHash) INNER JOIN sys.check_constraints AS candidate " +
        "ON candidate.name = expected.ConstraintName WHERE SCHEMA_NAME(candidate.schema_id) = N'dbo' " +
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
        "AND UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(module.definition, N'[', N''), N']', N''), " +
        "N' ', N''), NCHAR(10), N''), NCHAR(13), N'')) = " +
        "N'CREATETRIGGERDBO.TR_STOCKLEDGER_APPENDONLYONDBO.STOCKLEDGERINSTEADOFUPDATE,DELETEASBEGINSETNOCOUNTON;THROW51000,''STOCKLEDGERISAPPEND-ONLY.'',1;END;')" +
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
        "N'GoodsReceipts', N'GoodsReceiptLines', N'StockLedger', N'StockBalances')) OR " +
        "(candidate.type = N'FN' AND candidate.name = N'CatalogTextContainsNul') OR " +
        "(candidate.type = N'SO' AND candidate.name = N'GoodsReceiptNumberSequence') OR " +
        "(candidate.type = N'TR' AND candidate.name = N'TR_StockLedger_AppendOnly' " +
        "AND candidate.parent_object_id = OBJECT_ID(N'dbo.StockLedger', N'U')))));";

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
