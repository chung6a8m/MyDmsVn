SET NOCOUNT ON;
SET XACT_ABORT ON;
-- SQLCMD OperatorUser and WebRuntimeUser must name pre-existing, distinct database users.
IF DB_NAME() IN(N'master',N'model',N'msdb',N'tempdb') THROW 51310, 'An application database is required.',1;
IF USER_ID(N'$(OperatorUser)') IS NULL OR USER_ID(N'$(WebRuntimeUser)') IS NULL
 OR USER_ID(N'$(OperatorUser)')<=4 OR USER_ID(N'$(OperatorUser)')=USER_ID(N'$(WebRuntimeUser)')
 THROW 51310, 'A separate existing operator user is required.',1;
IF OBJECT_ID(N'dbo.RecordWebOperationNotExecuted',N'P') IS NULL
 OR OBJECT_ID(N'dbo.InspectWebOperationResolution',N'P') IS NULL
 OR OBJECT_ID(N'dbo.WebOperationTerminalAudit',N'U') IS NULL
 OR DATABASE_PRINCIPAL_ID(N'MyDmsVnWebOperationOperatorRole') IS NULL
 THROW 51310, 'Explicit schema13 is required.',1;
IF IS_ROLEMEMBER(N'MyDmsVnWebRuntimeRole',N'$(OperatorUser)')=1
 THROW 51310, 'Runtime cannot be the operator.',1;
IF IS_ROLEMEMBER(N'MyDmsVnWebOperationOperatorRole',N'$(OperatorUser)')<>1
 ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER [$(OperatorUser)];
GRANT EXECUTE ON OBJECT::dbo.RecordWebOperationNotExecuted TO MyDmsVnWebOperationOperatorRole;
GRANT EXECUTE ON OBJECT::dbo.InspectWebOperationResolution TO MyDmsVnWebOperationOperatorRole;
-- No table DML, broad database role, migration, journal deletion or browser authority.
