using System;
using Microsoft.Data.SqlClient;
namespace MyDmsVn.Server.DbMigrator;
// Administrative read-only admission on the same nonpooled connection that owns the migration lock.
// Operators establish/retain restriction; this validator never changes mode, kills sessions or credentials.
public static class Schema9MaintenanceValidator
{
    public static void Validate(SqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = @"
IF @@TRANCOUNT <> 0 SELECT N'Migration.MaintenanceTransaction';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND state=0 AND is_read_only=0 AND user_access=2 AND containment=0)
 SELECT N'Migration.MaintenanceAccessRequired';
ELSE IF ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,'VIEW SERVER STATE'),0)<>1
 OR ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,'VIEW ANY DEFINITION'),0)<>1
 OR (CONVERT(int,SERVERPROPERTY('ProductMajorVersion'))>=16 AND ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,'VIEW SERVER PERFORMANCE STATE'),0)<>1)
 SELECT N'Migration.MaintenanceVisibilityRequired';
ELSE IF EXISTS(SELECT 1 FROM sys.database_principals AS p
 LEFT JOIN sys.server_principals AS l ON l.sid=p.sid
 WHERE p.principal_id>4 AND p.type IN ('S','U','G','E','X')
 AND (p.type<>'S' OR p.authentication_type<>1 OR l.type<>'S' OR l.principal_id IS NULL))
 OR EXISTS(SELECT 1 FROM sys.database_principals WHERE principal_id>4 AND type IN ('A','C','K'))
 OR EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND (is_trustworthy_on=1 OR is_db_chaining_on=1))
 OR EXISTS(SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=USER_ID(N'guest') AND permission_name=N'CONNECT' AND state IN ('G','W'))
 SELECT N'Migration.UnsupportedMaintenanceIdentity';
ELSE IF EXISTS(SELECT 1 FROM sys.database_principals AS p JOIN sys.server_principals AS l ON l.sid=p.sid
 WHERE p.principal_id>4 AND p.type='S' AND l.sid<>SUSER_SID()
 AND (IS_ROLEMEMBER('db_owner',p.name)=1 OR IS_SRVROLEMEMBER('sysadmin',l.name)=1 OR IS_SRVROLEMEMBER('dbcreator',l.name)=1))
 SELECT N'Migration.ElevatedWriterIdentity';
ELSE IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions AS s WHERE s.is_user_process=1 AND s.session_id<>@@SPID
 AND (s.database_id=DB_ID() OR EXISTS(SELECT 1 FROM sys.database_principals AS p
 WHERE p.principal_id>4 AND p.type='S' AND p.sid=s.original_security_id AND p.sid<>SUSER_SID())))
 OR EXISTS(SELECT 1 FROM sys.dm_exec_requests WHERE session_id<>@@SPID AND database_id=DB_ID())
 OR EXISTS(SELECT 1 FROM sys.dm_tran_database_transactions AS t WHERE t.database_id=DB_ID() AND t.database_transaction_type<>3
 AND t.transaction_id<>(SELECT transaction_id FROM sys.dm_tran_current_transaction))
 OR EXISTS(SELECT 1 FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID() AND request_session_id<>@@SPID)
 SELECT N'Migration.WritersNotDrained';
ELSE SELECT N'OK';";
        var code = Convert.ToString(command.ExecuteScalar());
        if (!string.Equals(code, "OK", StringComparison.Ordinal))
        {
            var counters = string.Empty;
            if (code == "Migration.WritersNotDrained")
            {
                command.CommandText = @"SELECT CONCAT(N'S=',(SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process=1 AND session_id<>@@SPID AND (database_id=DB_ID() OR EXISTS(SELECT 1 FROM sys.database_principals AS p WHERE p.principal_id>4 AND p.type='S' AND p.sid=original_security_id AND p.sid<>SUSER_SID()))),
 N';R=',(SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id<>@@SPID AND database_id=DB_ID()),
 N';T=',(SELECT COUNT(*) FROM sys.dm_tran_database_transactions AS t WHERE t.database_id=DB_ID() AND t.database_transaction_type<>3 AND t.transaction_id<>(SELECT transaction_id FROM sys.dm_tran_current_transaction)),
 N';SelfT=',(SELECT COUNT(*) FROM sys.dm_tran_database_transactions WHERE database_id=DB_ID() AND transaction_id=(SELECT transaction_id FROM sys.dm_tran_current_transaction)),
 N';L=',(SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID() AND request_session_id<>@@SPID));";
                counters = Convert.ToString(command.ExecuteScalar());
            }
            throw new DatabaseMigrationPreflightException(code ?? "Migration.MaintenanceUnknown", code + " " + counters + ": Không thể xác minh cửa sổ bảo trì và toàn bộ writer đã drain. Migration đã dừng trước khi thay đổi dữ liệu.");
        }
    }
}
