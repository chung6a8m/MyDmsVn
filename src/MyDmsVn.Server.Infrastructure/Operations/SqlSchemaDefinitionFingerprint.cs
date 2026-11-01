using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
namespace MyDmsVn.Server.Infrastructure.Operations;
// Definition-only inventory: excludes IDs, row data, identity/sequence current values and principal grants.
// It complements the frozen schema9 per-artifact checks, including all schema10 additions.
public static class SqlSchemaDefinitionFingerprint
{
    public const string Schema9 = "F4DEEC00E8CE56F7B031F12A203A6F0B33A5991DE60E489DEE598E12AB0F412B";
    public const string Schema10 = "D8119C144DE35212FD495CF7F7BE7060A7FE6CAE55106ABD25AF795717BC0349";
    public const string Schema11 = "1B1CF2B760E4651A06D10F162E44418BB2A0DEF2E425EC9A901F55B71B03EC4F";
    public const string Schema12 = "50552E3ADC1AF4C68591D9C582B5340ED4744BB6883F085E8A7EC7053308BC87";
    public static async Task<string> ReadAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = @"
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
SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',@s),2);";
        return (string)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }
}
