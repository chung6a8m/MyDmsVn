SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE TABLE dbo.WebDeploymentState
(
    Id tinyint NOT NULL CONSTRAINT PK_WebDeploymentState PRIMARY KEY,
    DeploymentEpoch uniqueidentifier NOT NULL,
    RotatedAtUtc datetime2(7) NOT NULL,
    CONSTRAINT CK_WebDeploymentState_Singleton CHECK (Id = 1)
);
INSERT dbo.WebDeploymentState(Id,DeploymentEpoch,RotatedAtUtc) VALUES(1,NEWID(),SYSUTCDATETIME());
CREATE TABLE dbo.WebOperationJournal
(
    ActorUserId int NOT NULL,
    OperationId uniqueidentifier NOT NULL,
    DeploymentEpoch uniqueidentifier NOT NULL,
    ContractVersion smallint NOT NULL,
    CodecVersion smallint NOT NULL,
    OperationKind tinyint NOT NULL,
    RequestTargetId bigint NULL,
    RequestFingerprint binary(32) NOT NULL,
    ResultDocumentId bigint NULL,
    Disposition varchar(9) NOT NULL,
    ResultBytes varbinary(max) NULL,
    CreatedAtUtc datetime2(7) NOT NULL,
    CompletedAtUtc datetime2(7) NOT NULL,
    ResultExpiresAtUtc datetime2(7) NULL,
    CONSTRAINT PK_WebOperationJournal PRIMARY KEY(ActorUserId,OperationId),
    CONSTRAINT CK_WebOperationJournal_Identity CHECK (ActorUserId > 0 AND ContractVersion = 1 AND CodecVersion = 1 AND OperationKind BETWEEN 1 AND 6),
    CONSTRAINT CK_WebOperationJournal_Target CHECK ((OperationKind IN (1,4) AND RequestTargetId IS NULL) OR (OperationKind IN (2,3,5,6) AND RequestTargetId IS NOT NULL AND RequestTargetId > 0)),
    CONSTRAINT CK_WebOperationJournal_Result CHECK (
        (Disposition = 'COMMITTED' AND ResultDocumentId IS NOT NULL AND ResultDocumentId > 0 AND ResultBytes IS NOT NULL AND DATALENGTH(ResultBytes) BETWEEN 1 AND 1048576 AND ResultExpiresAtUtc IS NOT NULL AND ResultExpiresAtUtc = DATEADD(day,7,CompletedAtUtc)) OR
        (Disposition = 'EXPIRED' AND ResultDocumentId IS NOT NULL AND ResultDocumentId > 0 AND ResultBytes IS NULL AND ResultExpiresAtUtc IS NOT NULL AND ResultExpiresAtUtc = DATEADD(day,7,CompletedAtUtc)) OR
        (Disposition = 'CANCELLED' AND ResultDocumentId IS NULL AND ResultBytes IS NULL AND ResultExpiresAtUtc IS NULL)),
    CONSTRAINT CK_WebOperationJournal_ResultTarget CHECK (RequestTargetId IS NULL OR ResultDocumentId IS NULL OR RequestTargetId = ResultDocumentId),
    CONSTRAINT CK_WebOperationJournal_Time CHECK (CreatedAtUtc <= CompletedAtUtc)
);
GO
CREATE TRIGGER dbo.TR_WebOperationJournal_Immutable ON dbo.WebOperationJournal AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.ActorUserId=d.ActorUserId AND i.OperationId=d.OperationId
        WHERE i.OperationId IS NULL OR i.DeploymentEpoch<>d.DeploymentEpoch OR i.ContractVersion<>d.ContractVersion OR i.CodecVersion<>d.CodecVersion
        OR i.OperationKind<>d.OperationKind OR ISNULL(i.RequestTargetId,0)<>ISNULL(d.RequestTargetId,0) OR i.RequestFingerprint<>d.RequestFingerprint
        OR ISNULL(i.ResultDocumentId,0)<>ISNULL(d.ResultDocumentId,0) OR i.CreatedAtUtc<>d.CreatedAtUtc OR i.CompletedAtUtc<>d.CompletedAtUtc
        OR ISNULL(i.ResultExpiresAtUtc,'0001-01-01')<>ISNULL(d.ResultExpiresAtUtc,'0001-01-01')
        OR d.Disposition<>'COMMITTED' OR i.Disposition<>'EXPIRED' OR i.ResultBytes IS NOT NULL OR d.ResultExpiresAtUtc>SYSUTCDATETIME())
        THROW 51201, 'Operation identity and terminal tombstones are immutable.', 1;
END;
