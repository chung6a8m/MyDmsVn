CREATE TABLE dbo.Permissions
(
    PermissionKey nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL
        CONSTRAINT PK_Permissions PRIMARY KEY,
    Module nvarchar(100) NOT NULL,
    Description nvarchar(500) NOT NULL,
    CatalogStatus varchar(32) NOT NULL,
    AdministratorGrantPolicy varchar(16) NOT NULL,
    IntroducedReleaseId nvarchar(64) NULL,
    DeprecatedReleaseId nvarchar(64) NULL,
    ActivatedAtUtc datetime2(7) NULL,
    ActivatedBy nvarchar(128) NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_Permissions_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedBy nvarchar(128) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_Permissions_PermissionKey_NotBlank
        CHECK (LEN(LTRIM(RTRIM(PermissionKey))) > 0),
    CONSTRAINT CK_Permissions_Module_NotBlank
        CHECK (LEN(LTRIM(RTRIM(Module))) > 0),
    CONSTRAINT CK_Permissions_Description_NotBlank
        CHECK (LEN(LTRIM(RTRIM(Description))) > 0),
    CONSTRAINT CK_Permissions_CatalogStatus
        CHECK (CatalogStatus IN ('Unregistered', 'PendingActivation', 'Active', 'Deprecated')),
    CONSTRAINT CK_Permissions_AdministratorGrantPolicy
        CHECK (AdministratorGrantPolicy IN ('NotEligible', 'Eligible', 'Sensitive')),
    CONSTRAINT CK_Permissions_ActivationAudit
        CHECK ((CatalogStatus = 'Active' AND ActivatedAtUtc IS NOT NULL AND ActivatedBy IS NOT NULL)
            OR CatalogStatus <> 'Active')
);

CREATE TABLE dbo.PermissionLegacyAssignmentDrift
(
    PermissionKey nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL
        CONSTRAINT PK_PermissionLegacyAssignmentDrift PRIMARY KEY,
    RoleGrantCount int NOT NULL,
    UserGrantCount int NOT NULL,
    ReviewStatus varchar(16) NOT NULL,
    FirstObservedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_PermissionLegacyAssignmentDrift_FirstObservedAtUtc DEFAULT (SYSUTCDATETIME()),
    ReviewedAtUtc datetime2(7) NULL,
    ReviewedBy nvarchar(128) NULL,
    CONSTRAINT FK_PermissionLegacyAssignmentDrift_Permissions
        FOREIGN KEY (PermissionKey) REFERENCES dbo.Permissions (PermissionKey),
    CONSTRAINT CK_PermissionLegacyAssignmentDrift_Counts
        CHECK (RoleGrantCount >= 0 AND UserGrantCount >= 0),
    CONSTRAINT CK_PermissionLegacyAssignmentDrift_ReviewStatus
        CHECK (ReviewStatus IN ('Unreviewed', 'Adopted', 'Rejected'))
);

CREATE TABLE dbo.PermissionSynchronizationState
(
    StateId tinyint NOT NULL
        CONSTRAINT PK_PermissionSynchronizationState PRIMARY KEY,
    DatabaseIdentity uniqueidentifier NOT NULL,
    LastAppliedPlanId varchar(64) NULL,
    LastAppliedReleaseId nvarchar(64) NULL,
    LastAppliedAtUtc datetime2(7) NULL,
    LastAppliedBy nvarchar(128) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_PermissionSynchronizationState_Singleton CHECK (StateId = 1)
);

INSERT dbo.PermissionSynchronizationState (StateId, DatabaseIdentity)
VALUES (1, NEWID());

CREATE UNIQUE INDEX UX_PermissionSynchronizationState_DatabaseIdentity
    ON dbo.PermissionSynchronizationState (DatabaseIdentity);

CREATE TABLE dbo.SystemManagedRoles
(
    ManagedRoleId int IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_SystemManagedRoles PRIMARY KEY,
    SystemRoleKey varchar(64) NOT NULL,
    RoleId int NOT NULL,
    ExpectedNormalizedRoleName nvarchar(100) COLLATE Latin1_General_100_CI_AI NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_SystemManagedRoles_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedBy nvarchar(128) NOT NULL,
    CONSTRAINT FK_SystemManagedRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId),
    CONSTRAINT CK_SystemManagedRoles_SystemRoleKey_NotBlank
        CHECK (LEN(LTRIM(RTRIM(SystemRoleKey))) > 0),
    CONSTRAINT CK_SystemManagedRoles_Name_NotBlank
        CHECK (LEN(LTRIM(RTRIM(ExpectedNormalizedRoleName))) > 0)
);

CREATE UNIQUE INDEX UX_SystemManagedRoles_SystemRoleKey
    ON dbo.SystemManagedRoles (SystemRoleKey);

CREATE UNIQUE INDEX UX_SystemManagedRoles_RoleId
    ON dbo.SystemManagedRoles (RoleId);

CREATE TABLE dbo.PermissionGrantApprovals
(
    PermissionKey nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL
        CONSTRAINT PK_PermissionGrantApprovals PRIMARY KEY,
    CatalogDecision varchar(16) NOT NULL,
    AdministratorGrantDecision varchar(16) NOT NULL,
    DecisionReleaseId nvarchar(64) NOT NULL,
    DecisionBy nvarchar(128) NOT NULL,
    DecisionAtUtc datetime2(7) NOT NULL,
    ExpiresAtUtc datetime2(7) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT FK_PermissionGrantApprovals_Permissions
        FOREIGN KEY (PermissionKey) REFERENCES dbo.Permissions (PermissionKey),
    CONSTRAINT CK_PermissionGrantApprovals_CatalogDecision
        CHECK (CatalogDecision IN ('Pending', 'Approved', 'Rejected')),
    CONSTRAINT CK_PermissionGrantApprovals_AdministratorDecision
        CHECK (AdministratorGrantDecision IN ('Pending', 'Approved', 'Rejected')),
    CONSTRAINT CK_PermissionGrantApprovals_Audit_NotBlank
        CHECK (LEN(LTRIM(RTRIM(DecisionReleaseId))) > 0 AND LEN(LTRIM(RTRIM(DecisionBy))) > 0)
);

CREATE TABLE dbo.ManagedRolePermissionGrants
(
    ManagedRolePermissionGrantId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_ManagedRolePermissionGrants PRIMARY KEY,
    ManagedRoleId int NOT NULL,
    PermissionKey nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    GrantState varchar(16) NOT NULL,
    StateReleaseId nvarchar(64) NOT NULL,
    StateChangedAtUtc datetime2(7) NOT NULL,
    StateChangedBy nvarchar(128) NOT NULL,
    Reason nvarchar(500) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT FK_ManagedRolePermissionGrants_SystemManagedRoles
        FOREIGN KEY (ManagedRoleId) REFERENCES dbo.SystemManagedRoles (ManagedRoleId),
    CONSTRAINT FK_ManagedRolePermissionGrants_Permissions
        FOREIGN KEY (PermissionKey) REFERENCES dbo.Permissions (PermissionKey),
    CONSTRAINT CK_ManagedRolePermissionGrants_GrantState
        CHECK (GrantState IN ('NotGranted', 'Granted', 'Revoked')),
    CONSTRAINT CK_ManagedRolePermissionGrants_Audit_NotBlank
        CHECK (LEN(LTRIM(RTRIM(StateReleaseId))) > 0 AND LEN(LTRIM(RTRIM(StateChangedBy))) > 0)
);

CREATE UNIQUE INDEX UX_ManagedRolePermissionGrants_Role_Key
    ON dbo.ManagedRolePermissionGrants (ManagedRoleId, PermissionKey);

CREATE TABLE dbo.SecurityBootstrapState
(
    BootstrapKey varchar(64) NOT NULL
        CONSTRAINT PK_SecurityBootstrapState PRIMARY KEY,
    CompletedAtUtc datetime2(7) NOT NULL,
    CompletedBy nvarchar(128) NOT NULL,
    AdministratorUserId int NOT NULL,
    ReleaseId nvarchar(64) NOT NULL,
    CONSTRAINT CK_SecurityBootstrapState_Key_NotBlank
        CHECK (LEN(LTRIM(RTRIM(BootstrapKey))) > 0),
    CONSTRAINT CK_SecurityBootstrapState_Audit_NotBlank
        CHECK (LEN(LTRIM(RTRIM(CompletedBy))) > 0 AND LEN(LTRIM(RTRIM(ReleaseId))) > 0)
);

CREATE TABLE dbo.PermissionSecurityAudit
(
    PermissionSecurityAuditId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_PermissionSecurityAudit PRIMARY KEY,
    AttemptId uniqueidentifier NOT NULL,
    Operation varchar(32) NOT NULL,
    PlanId varchar(64) NULL,
    ReleaseId nvarchar(64) NULL,
    DatabaseIdentity uniqueidentifier NOT NULL,
    Actor nvarchar(128) NOT NULL,
    Outcome varchar(16) NOT NULL,
    ReasonCode varchar(100) NOT NULL,
    ChangeSummary nvarchar(max) NULL,
    OccurredAtUtc datetime2(7) NOT NULL,
    CONSTRAINT CK_PermissionSecurityAudit_Operation_NotBlank
        CHECK (LEN(LTRIM(RTRIM(Operation))) > 0),
    CONSTRAINT CK_PermissionSecurityAudit_Actor_NotBlank
        CHECK (LEN(LTRIM(RTRIM(Actor))) > 0),
    CONSTRAINT CK_PermissionSecurityAudit_Outcome
        CHECK (Outcome IN ('Succeeded', 'Rejected', 'Failed')),
    CONSTRAINT CK_PermissionSecurityAudit_ReasonCode_NotBlank
        CHECK (LEN(LTRIM(RTRIM(ReasonCode))) > 0)
);

CREATE UNIQUE INDEX UX_PermissionSecurityAudit_AttemptId
    ON dbo.PermissionSecurityAudit (AttemptId);

;WITH HistoricalKeys AS
(
    SELECT PermissionKey FROM dbo.RolePermissions
    UNION
    SELECT PermissionKey FROM dbo.UserPermissions
)
INSERT dbo.Permissions
(
    PermissionKey,
    Module,
    Description,
    CatalogStatus,
    AdministratorGrantPolicy
)
SELECT
    PermissionKey,
    N'Legacy',
    N'Historical permission assignment discovered during P3.1 migration.',
    'Unregistered',
    'NotEligible'
FROM HistoricalKeys;

INSERT dbo.PermissionLegacyAssignmentDrift
(
    PermissionKey,
    RoleGrantCount,
    UserGrantCount,
    ReviewStatus
)
SELECT
    permission.PermissionKey,
    (SELECT COUNT(*) FROM dbo.RolePermissions AS roleGrant
        WHERE roleGrant.PermissionKey = permission.PermissionKey),
    (SELECT COUNT(*) FROM dbo.UserPermissions AS userGrant
        WHERE userGrant.PermissionKey = permission.PermissionKey),
    'Unreviewed'
FROM dbo.Permissions AS permission;
