SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Run as the owner of the exact schema13 rehearsal database.
-- SQLCMD values name three pre-existing database users and are T-SQL string-literal values.
IF DB_NAME() IN (N'master',N'model',N'msdb',N'tempdb') OR OBJECT_ID(N'dbo.SchemaVersions',N'U') IS NULL
    THROW 51311, 'An explicit schema13 application database is required.',1;

DECLARE @maintenance int=USER_ID(N'$(MaintenanceUser)');
DECLARE @operator int=USER_ID(N'$(OperatorUser)');
DECLARE @runtime int=USER_ID(N'$(WebRuntimeUser)');
IF @maintenance IS NULL OR @operator IS NULL OR @runtime IS NULL
    OR @maintenance<=4 OR @operator<=4 OR @runtime<=4
    OR @maintenance IN (@operator,@runtime) OR @operator=@runtime
    OR EXISTS(SELECT 1 FROM sys.database_principals WHERE principal_id IN (@operator,@runtime) AND type IN (N'R',N'A'))
    THROW 51311, 'Three distinct existing database users are required.',1;

DECLARE @maintenanceSid varbinary(85);
SELECT @maintenanceSid=sid FROM sys.database_principals
WHERE principal_id=@maintenance AND type=N'S' AND authentication_type=1;
IF @maintenanceSid IS NULL
    OR EXISTS(SELECT 1 FROM sys.database_principals WHERE principal_id IN (@operator,@runtime) AND sid=@maintenanceSid)
    OR EXISTS(SELECT 1 FROM sys.database_principals AS op JOIN sys.database_principals AS runtime ON runtime.sid=op.sid
        WHERE op.principal_id=@operator AND runtime.principal_id=@runtime)
    OR NOT EXISTS(SELECT 1 FROM sys.server_principals WHERE sid=@maintenanceSid AND type=N'S' AND is_disabled=0)
    OR EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND owner_sid=@maintenanceSid)
    OR EXISTS(SELECT 1 FROM sys.database_role_members WHERE member_principal_id=@maintenance)
    OR EXISTS(SELECT 1 FROM sys.server_role_members AS roles JOIN sys.server_principals AS member
        ON member.principal_id=roles.member_principal_id WHERE member.sid=@maintenanceSid)
    OR EXISTS(SELECT 1 FROM sys.server_permissions AS permission JOIN sys.server_principals AS login
        ON login.principal_id=permission.grantee_principal_id WHERE login.sid=@maintenanceSid
        AND NOT(permission.class=100 AND permission.major_id=0 AND permission.state=N'G'
            AND permission.permission_name IN (N'CONNECT SQL',N'VIEW ANY DATABASE',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',
                N'VIEW ANY COLUMN MASTER KEY DEFINITION')))
    OR EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=@maintenance
        AND NOT(class=0 AND major_id=0 AND minor_id=0 AND state=N'G' AND permission_name IN (N'CONNECT',N'CONTROL')))
    THROW 51311, 'A separate restricted SQL login and database user are required.',1;

DECLARE @expected TABLE (Ordinal int PRIMARY KEY, ScriptName nvarchar(128) NOT NULL);
INSERT @expected VALUES
(1,N'001_PersistenceFoundation.sql'),(2,N'002_Identity.sql'),(3,N'003_Catalog.sql'),
(4,N'004_Catalog_Nul_Constraints.sql'),(5,N'005_GoodsReceipt.sql'),(6,N'006_GoodsReceipt_Number_Sequence.sql'),
(7,N'007_Inventory_Stock.sql'),(8,N'008_Security_Administration.sql'),(9,N'009_Security_Permission_Import.sql'),
(10,N'010_DirectSalesAndValuation.sql'),(11,N'011_WebAuthSessions.sql'),(12,N'012_WebOperationJournal.sql'),
(13,N'013_WebOperationTerminalResolution.sql');
IF EXISTS(
    SELECT 1 FROM @expected AS expected
    FULL OUTER JOIN (SELECT ROW_NUMBER() OVER (ORDER BY Id) AS Ordinal,
        LTRIM(RTRIM(ScriptName)) COLLATE Latin1_General_100_BIN2 AS ScriptName FROM dbo.SchemaVersions) AS actual
        ON actual.Ordinal=expected.Ordinal
    WHERE expected.ScriptName IS NULL OR actual.ScriptName IS NULL
        OR actual.ScriptName NOT IN (expected.ScriptName,N'MyDmsVn.Server.DbMigrator.Scripts.'+expected.ScriptName)
)
    THROW 51311, 'The exact schema13 migration history is required.',1;

-- Definition inventory matches SqlSchemaDefinitionFingerprint.Schema13.
DECLARE @s nvarchar(max);
WITH signatures AS (
SELECT CONCAT(N'O|',SCHEMA_NAME(schema_id),N'|',name,N'|',type) AS signature FROM sys.objects WHERE is_ms_shipped=0
UNION ALL SELECT CONCAT(N'C|',OBJECT_SCHEMA_NAME(object_id),N'|',OBJECT_NAME(object_id),N'|',column_id,N'|',name,N'|',TYPE_NAME(user_type_id),N'|',max_length,N'|',precision,N'|',scale,N'|',is_nullable,N'|',is_identity,N'|',is_computed,N'|',is_ansi_padded,N'|',is_rowguidcol,N'|',is_filestream,N'|',is_replicated,N'|',is_sparse,N'|',is_column_set,N'|',generated_always_type,N'|',ISNULL(encryption_type,0),N'|',collation_name) FROM sys.columns WHERE OBJECTPROPERTY(object_id,'IsMSShipped')=0
UNION ALL SELECT CONCAT(N'D|',OBJECT_SCHEMA_NAME(parent_object_id),N'|',OBJECT_NAME(parent_object_id),N'|',name,N'|',parent_column_id,N'|',definition) FROM sys.default_constraints WHERE is_ms_shipped=0
UNION ALL SELECT CONCAT(N'K|',OBJECT_SCHEMA_NAME(parent_object_id),N'|',OBJECT_NAME(parent_object_id),N'|',name,N'|',is_disabled,N'|',is_not_trusted,N'|',is_not_for_replication,N'|',definition) FROM sys.check_constraints WHERE is_ms_shipped=0
UNION ALL SELECT CONCAT(N'F|',OBJECT_SCHEMA_NAME(parent_object_id),N'|',OBJECT_NAME(parent_object_id),N'|',name,N'|',OBJECT_SCHEMA_NAME(referenced_object_id),N'|',OBJECT_NAME(referenced_object_id),N'|',is_disabled,N'|',is_not_trusted,N'|',is_not_for_replication,N'|',delete_referential_action,N'|',update_referential_action) FROM sys.foreign_keys WHERE is_ms_shipped=0
UNION ALL SELECT CONCAT(N'FC|',OBJECT_NAME(f.constraint_object_id),N'|',f.constraint_column_id,N'|',COL_NAME(f.parent_object_id,f.parent_column_id),N'|',COL_NAME(f.referenced_object_id,f.referenced_column_id)) FROM sys.foreign_key_columns AS f
UNION ALL SELECT CONCAT(N'I|',OBJECT_SCHEMA_NAME(object_id),N'|',OBJECT_NAME(object_id),N'|',name,N'|',type,N'|',is_unique,N'|',is_primary_key,N'|',is_unique_constraint,N'|',is_disabled,N'|',is_hypothetical,N'|',has_filter,N'|',filter_definition,N'|',ignore_dup_key,N'|',allow_row_locks,N'|',allow_page_locks) FROM sys.indexes WHERE OBJECTPROPERTY(object_id,'IsMSShipped')=0
UNION ALL SELECT CONCAT(N'IC|',OBJECT_SCHEMA_NAME(c.object_id),N'|',OBJECT_NAME(c.object_id),N'|',i.name,N'|',c.index_column_id,N'|',COL_NAME(c.object_id,c.column_id),N'|',c.key_ordinal,N'|',c.is_descending_key,N'|',c.is_included_column) FROM sys.index_columns AS c JOIN sys.indexes AS i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE OBJECTPROPERTY(c.object_id,'IsMSShipped')=0
UNION ALL SELECT CONCAT(N'M|',OBJECT_SCHEMA_NAME(object_id),N'|',OBJECT_NAME(object_id),N'|',uses_ansi_nulls,N'|',uses_quoted_identifier,N'|',is_schema_bound,N'|',uses_database_collation,N'|',is_recompiled,N'|',null_on_null_input,N'|',CASE WHEN execute_as_principal_id>0 THEN USER_NAME(execute_as_principal_id) ELSE CONVERT(nvarchar(20),execute_as_principal_id) END,N'|',REPLACE(definition,CHAR(13)+CHAR(10),CHAR(10))) FROM sys.sql_modules WHERE OBJECTPROPERTY(object_id,'IsMSShipped')=0
UNION ALL SELECT CONCAT(N'T|',OBJECT_SCHEMA_NAME(object_id),N'|',name,N'|',parent_class,N'|',OBJECT_NAME(parent_id),N'|',is_disabled,N'|',is_instead_of_trigger,N'|',is_not_for_replication) FROM sys.triggers WHERE is_ms_shipped=0
UNION ALL SELECT CONCAT(N'TE|',OBJECT_NAME(object_id),N'|',type,N'|',is_first,N'|',is_last) FROM sys.trigger_events
UNION ALL SELECT CONCAT(N'S|',SCHEMA_NAME(schema_id),N'|',name,N'|',TYPE_NAME(user_type_id),N'|',CONVERT(nvarchar(100),start_value),N'|',CONVERT(nvarchar(100),increment),N'|',CONVERT(nvarchar(100),minimum_value),N'|',CONVERT(nvarchar(100),maximum_value),N'|',is_cycling) FROM sys.sequences WHERE is_ms_shipped=0
UNION ALL SELECT CONCAT(N'ID|',OBJECT_NAME(object_id),N'|',name,N'|',CONVERT(nvarchar(100),seed_value),N'|',CONVERT(nvarchar(100),increment_value),N'|',is_not_for_replication) FROM sys.identity_columns WHERE OBJECTPROPERTY(object_id,'IsMSShipped')=0
UNION ALL SELECT CONCAT(N'CC|',OBJECT_NAME(object_id),N'|',name,N'|',definition,N'|',is_persisted) FROM sys.computed_columns WHERE OBJECTPROPERTY(object_id,'IsMSShipped')=0
UNION ALL SELECT CONCAT(N'UT|',SCHEMA_NAME(schema_id),N'|',name) FROM sys.types WHERE is_user_defined=1
)
SELECT @s=STRING_AGG(CONVERT(nvarchar(max),signature) COLLATE Latin1_General_100_BIN2,N';') WITHIN GROUP(ORDER BY signature COLLATE Latin1_General_100_BIN2) FROM signatures;
IF ISNULL(CONVERT(varchar(64),HASHBYTES('SHA2_256',@s),2),'')<>'D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9'
    THROW 51311, 'The exact schema13 definition is required.',1;

DECLARE @grant nvarchar(512)=N'GRANT CONTROL TO '+QUOTENAME(USER_NAME(@maintenance))+N';';
EXEC sys.sp_executesql @grant;
