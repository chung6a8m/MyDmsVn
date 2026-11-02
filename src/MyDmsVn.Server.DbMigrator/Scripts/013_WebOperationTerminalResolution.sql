SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE TABLE dbo.WebOperationTerminalAudit
(
    ActorUserId int NOT NULL,
    OperationId uniqueidentifier NOT NULL,
    DeploymentEpoch uniqueidentifier NOT NULL,
    OperationKind tinyint NOT NULL,
    RequestTargetId bigint NULL,
    RequestFingerprint binary(32) NOT NULL,
    DecisionId uniqueidentifier NOT NULL,
    CaseId varchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
    ReasonCode varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    EvidenceReference varchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    OperatorDatabaseUser nvarchar(128) NOT NULL,
    OperatorLogin nvarchar(128) NOT NULL,
    DecidedAtUtc datetime2(7) NOT NULL,
    CONSTRAINT PK_WebOperationTerminalAudit PRIMARY KEY (ActorUserId,OperationId),
    CONSTRAINT UQ_WebOperationTerminalAudit_Decision UNIQUE (DecisionId),
    CONSTRAINT CK_WebOperationTerminalAudit_Identity CHECK (ActorUserId>0 AND OperationKind BETWEEN 1 AND 6
      AND SUBSTRING(CONVERT(varchar(36),OperationId),15,1)='4'
      AND LOWER(SUBSTRING(CONVERT(varchar(36),OperationId),20,1)) IN ('8','9','a','b')
      AND SUBSTRING(CONVERT(varchar(36),DecisionId),15,1)='4'
      AND LOWER(SUBSTRING(CONVERT(varchar(36),DecisionId),20,1)) IN ('8','9','a','b')),
    CONSTRAINT CK_WebOperationTerminalAudit_Target CHECK ((OperationKind IN(1,4) AND RequestTargetId IS NULL)
      OR (OperationKind IN(2,3,5,6) AND RequestTargetId IS NOT NULL AND RequestTargetId>0)),
    CONSTRAINT CK_WebOperationTerminalAudit_Metadata CHECK (
      DATALENGTH(CaseId) BETWEEN 1 AND 128 AND CaseId NOT LIKE '%[^A-Za-z0-9_-]%'
      AND DATALENGTH(ReasonCode) BETWEEN 1 AND 64 AND ReasonCode NOT LIKE '%[^A-Za-z0-9_-]%'
      AND DATALENGTH(EvidenceReference) BETWEEN 1 AND 256 AND EvidenceReference NOT LIKE '%[^A-Za-z0-9_.:/-]%')
);
GO
CREATE TRIGGER dbo.TR_WebOperationTerminalAudit_Immutable ON dbo.WebOperationTerminalAudit AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM deleted) THROW 51304, 'Terminal decisions are immutable.', 1;
END;
GO
CREATE TRIGGER dbo.TR_WebOperationJournal_TerminalAuthority ON dbo.WebOperationJournal AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM inserted i WHERE i.Disposition='CANCELLED' AND NOT EXISTS(
      SELECT 1 FROM dbo.WebOperationTerminalAudit a WHERE a.ActorUserId=i.ActorUserId AND a.OperationId=i.OperationId
      AND a.DeploymentEpoch=i.DeploymentEpoch AND a.OperationKind=i.OperationKind
      AND ISNULL(a.RequestTargetId,0)=ISNULL(i.RequestTargetId,0) AND a.RequestFingerprint=i.RequestFingerprint
      AND a.DecidedAtUtc=i.CreatedAtUtc AND a.DecidedAtUtc=i.CompletedAtUtc))
      THROW 51302, 'Matched operator authority is required.', 1;
END;
GO
CREATE ROLE MyDmsVnWebOperationOperatorRole AUTHORIZATION dbo;
GO
CREATE PROCEDURE dbo.RecordWebOperationNotExecuted
    @epoch uniqueidentifier, @actor int, @operation uniqueidentifier, @kind tinyint,
    @target bigint, @hash varbinary(max), @decision uniqueidentifier,
    @case varchar(max), @reason varchar(max), @evidence varchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 THROW 51301, 'One explicit maintenance transaction is required.', 1;
    IF ISNULL(IS_ROLEMEMBER(N'MyDmsVnWebOperationOperatorRole'),0)<>1
      AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONTROL'),0)<>1
      THROW 51302, 'Operator authority is required.', 1;
    IF @epoch IS NULL OR @epoch='00000000-0000-0000-0000-000000000000' OR @actor IS NULL OR @actor<=0
      OR @operation IS NULL OR @kind IS NULL OR @kind NOT BETWEEN 1 AND 6 OR @hash IS NULL OR DATALENGTH(@hash)<>32 OR @decision IS NULL
      OR (@kind IN(1,4) AND @target IS NOT NULL) OR (@kind IN(2,3,5,6) AND (@target IS NULL OR @target<=0))
      OR @case IS NULL OR @reason IS NULL OR @evidence IS NULL
      OR DATALENGTH(@case) NOT BETWEEN 1 AND 128 OR DATALENGTH(@reason) NOT BETWEEN 1 AND 64 OR DATALENGTH(@evidence) NOT BETWEEN 1 AND 256
      THROW 51301, 'An exact immutable identity and bounded audit are required.', 1;
    -- Same lock order and exact resource as SqlWebOperationJournal.FenceAsync.
    IF NOT EXISTS(SELECT 1 FROM dbo.WebDeploymentState WITH(HOLDLOCK) WHERE Id=1 AND DeploymentEpoch=@epoch)
      THROW 51305, 'Deployment epoch mismatch.', 1;
    DECLARE @resource nvarchar(255)=N'MyDmsVn.WebOperation:'+CONVERT(nvarchar(10),@actor)+N':'+LOWER(REPLACE(CONVERT(nvarchar(36),@operation),N'-',N''));
    DECLARE @lock int;
    EXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @lock<0 THROW 51306, 'Operation fence unavailable.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.Users WITH(HOLDLOCK) WHERE UserId=@actor AND IsActive=1)
      THROW 51301, 'Original actor unavailable.', 1;
    -- A committed original owns this same fence and journal transaction. Never convert it,
    -- an expired result or any prior decision; lost acknowledgement requires independent read.
    IF EXISTS(SELECT 1 FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation)
      OR EXISTS(SELECT 1 FROM dbo.WebOperationTerminalAudit WHERE ActorUserId=@actor AND OperationId=@operation)
      THROW 51303, 'Operation already has a durable disposition.', 1;
    DECLARE @now datetime2(7)=SYSUTCDATETIME();
    INSERT dbo.WebOperationTerminalAudit(ActorUserId,OperationId,DeploymentEpoch,OperationKind,RequestTargetId,RequestFingerprint,
      DecisionId,CaseId,ReasonCode,EvidenceReference,OperatorDatabaseUser,OperatorLogin,DecidedAtUtc)
    VALUES(@actor,@operation,@epoch,@kind,@target,@hash,@decision,@case,@reason,@evidence,USER_NAME(),ORIGINAL_LOGIN(),@now);
    INSERT dbo.WebOperationJournal(ActorUserId,OperationId,DeploymentEpoch,ContractVersion,CodecVersion,OperationKind,RequestTargetId,
      RequestFingerprint,ResultDocumentId,Disposition,ResultBytes,CreatedAtUtc,CompletedAtUtc,ResultExpiresAtUtc)
    VALUES(@actor,@operation,@epoch,1,1,@kind,@target,@hash,NULL,'CANCELLED',NULL,@now,@now,NULL);
END;
GO
GRANT EXECUTE ON OBJECT::dbo.RecordWebOperationNotExecuted TO MyDmsVnWebOperationOperatorRole;
GO
CREATE PROCEDURE dbo.InspectWebOperationResolution
    @epoch uniqueidentifier, @actor int, @operation uniqueidentifier, @hash varbinary(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 THROW 51301, 'One explicit maintenance transaction is required.', 1;
    IF ISNULL(IS_ROLEMEMBER(N'MyDmsVnWebOperationOperatorRole'),0)<>1
      AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONTROL'),0)<>1
      THROW 51302, 'Operator authority is required.', 1;
    IF @epoch IS NULL OR @epoch='00000000-0000-0000-0000-000000000000' OR @actor IS NULL OR @actor<=0
      OR @operation IS NULL OR SUBSTRING(CONVERT(varchar(36),@operation),15,1)<>'4'
      OR LOWER(SUBSTRING(CONVERT(varchar(36),@operation),20,1)) NOT IN ('8','9','a','b')
      OR @hash IS NULL OR DATALENGTH(@hash)<>32
      THROW 51301, 'An exact original identity is required.', 1;
    -- Inspection owns the same epoch then actor/key fence; it never infers an outcome
    -- from a row that is absent while an original transaction is still in flight.
    IF NOT EXISTS(SELECT 1 FROM dbo.WebDeploymentState WITH(HOLDLOCK) WHERE Id=1 AND DeploymentEpoch=@epoch)
      THROW 51305, 'Deployment epoch mismatch.', 1;
    DECLARE @resource nvarchar(255)=N'MyDmsVn.WebOperation:'+CONVERT(nvarchar(10),@actor)+N':'+LOWER(REPLACE(CONVERT(nvarchar(36),@operation),N'-',N''));
    DECLARE @lock int;
    EXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @lock<0 THROW 51306, 'Operation fence unavailable.', 1;
    IF EXISTS(SELECT 1 FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation AND DeploymentEpoch<>@epoch)
      THROW 51305, 'Deployment epoch mismatch.', 1;
    IF EXISTS(SELECT 1 FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation AND RequestFingerprint<>@hash)
      THROW 51307, 'Operation fingerprint mismatch.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation)
    BEGIN
      SELECT @epoch AS DeploymentEpoch,@actor AS ActorUserId,@operation AS OperationId,
        CONVERT(tinyint,NULL) AS OperationKind,CONVERT(bigint,NULL) AS RequestTargetId,
        CONVERT(varchar(13),'INDETERMINATE') AS Disposition,
        CONVERT(bit,0) AS OriginalResultRetained,CONVERT(bit,0) AS OriginalVersionRetained,CONVERT(bit,0) AS OriginalLineIdsRetained,
        CONVERT(uniqueidentifier,NULL) AS DecisionId,CONVERT(varchar(128),NULL) AS CaseId,CONVERT(datetime2(7),NULL) AS DecidedAtUtc;
      RETURN;
    END;
    -- These are retention hints, not an original result, editable version or negative
    -- certificate. The bounded inspection never returns result bytes or audit reasons.
    SELECT j.DeploymentEpoch,j.ActorUserId,j.OperationId,j.OperationKind,j.RequestTargetId,
      CONVERT(varchar(13),CASE WHEN j.Disposition='COMMITTED' AND j.ResultExpiresAtUtc<=SYSUTCDATETIME() THEN 'EXPIRED'
        WHEN j.Disposition='CANCELLED' AND a.DecisionId IS NULL THEN 'INDETERMINATE' ELSE j.Disposition END) AS Disposition,
      r.Retained AS OriginalResultRetained,
      CONVERT(bit,CASE WHEN r.Retained=1 AND j.OperationKind<>6 THEN 1 ELSE 0 END) AS OriginalVersionRetained,
      CONVERT(bit,CASE WHEN r.Retained=1 AND j.OperationKind IN(1,2,4,5) THEN 1 ELSE 0 END) AS OriginalLineIdsRetained,
      a.DecisionId,a.CaseId,a.DecidedAtUtc
    FROM dbo.WebOperationJournal j
    CROSS APPLY(SELECT CONVERT(bit,CASE WHEN j.Disposition='COMMITTED' AND j.ContractVersion=1 AND j.CodecVersion=1
      AND j.ResultExpiresAtUtc>SYSUTCDATETIME() AND DATALENGTH(j.ResultBytes) BETWEEN 1 AND 1048576
      THEN 1 ELSE 0 END) AS Retained) r
    LEFT JOIN dbo.WebOperationTerminalAudit a ON j.Disposition='CANCELLED' AND a.ActorUserId=j.ActorUserId AND a.OperationId=j.OperationId
      AND a.DeploymentEpoch=j.DeploymentEpoch AND a.OperationKind=j.OperationKind
      AND ISNULL(a.RequestTargetId,0)=ISNULL(j.RequestTargetId,0) AND a.RequestFingerprint=j.RequestFingerprint
      AND a.DecidedAtUtc=j.CreatedAtUtc AND a.DecidedAtUtc=j.CompletedAtUtc AND j.ContractVersion=1 AND j.CodecVersion=1
    WHERE j.ActorUserId=@actor AND j.OperationId=@operation;
END;
GO
GRANT EXECUTE ON OBJECT::dbo.InspectWebOperationResolution TO MyDmsVnWebOperationOperatorRole;
