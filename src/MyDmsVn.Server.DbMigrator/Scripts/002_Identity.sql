CREATE TABLE dbo.Users
(
    UserId int IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_Users PRIMARY KEY,
    Username nvarchar(100) NOT NULL,
    NormalizedUsername nvarchar(100) COLLATE Latin1_General_100_CI_AI NOT NULL,
    DisplayName nvarchar(100) NOT NULL,
    Email nvarchar(254) NULL,
    Source nvarchar(16) NOT NULL,
    PasswordHash nvarchar(255) NOT NULL,
    PasswordSalt nvarchar(255) NOT NULL,
    PasswordAlgorithm nvarchar(32) NOT NULL,
    LastDirectoryUpdateUtc datetime2(7) NULL,
    UserImage nvarchar(260) NULL,
    IsActive bit NOT NULL
        CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_Users_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_Users_Username_NotBlank CHECK (LEN(LTRIM(RTRIM(Username))) > 0),
    CONSTRAINT CK_Users_NormalizedUsername_NotBlank CHECK (LEN(LTRIM(RTRIM(NormalizedUsername))) > 0),
    CONSTRAINT CK_Users_DisplayName_NotBlank CHECK (LEN(LTRIM(RTRIM(DisplayName))) > 0),
    CONSTRAINT CK_Users_Source_NotBlank CHECK (LEN(LTRIM(RTRIM(Source))) > 0),
    CONSTRAINT CK_Users_PasswordAlgorithm_NotBlank CHECK (LEN(LTRIM(RTRIM(PasswordAlgorithm))) > 0)
);

CREATE UNIQUE INDEX UX_Users_NormalizedUsername
    ON dbo.Users (NormalizedUsername);

CREATE TABLE dbo.Roles
(
    RoleId int IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_Roles PRIMARY KEY,
    RoleName nvarchar(100) NOT NULL,
    NormalizedRoleName nvarchar(100) COLLATE Latin1_General_100_CI_AI NOT NULL,
    IsActive bit NOT NULL
        CONSTRAINT DF_Roles_IsActive DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_Roles_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_Roles_RoleName_NotBlank CHECK (LEN(LTRIM(RTRIM(RoleName))) > 0),
    CONSTRAINT CK_Roles_NormalizedRoleName_NotBlank CHECK (LEN(LTRIM(RTRIM(NormalizedRoleName))) > 0)
);

CREATE UNIQUE INDEX UX_Roles_NormalizedRoleName
    ON dbo.Roles (NormalizedRoleName);

CREATE TABLE dbo.UserRoles
(
    UserRoleId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_UserRoles PRIMARY KEY,
    UserId int NOT NULL,
    RoleId int NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_UserRoles_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NULL,
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId)
);

CREATE UNIQUE INDEX UX_UserRoles_UserId_RoleId
    ON dbo.UserRoles (UserId, RoleId);

CREATE INDEX IX_UserRoles_RoleId
    ON dbo.UserRoles (RoleId);

CREATE TABLE dbo.RolePermissions
(
    RolePermissionId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_RolePermissions PRIMARY KEY,
    RoleId int NOT NULL,
    PermissionKey nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_RolePermissions_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NULL,
    CONSTRAINT CK_RolePermissions_PermissionKey_NotBlank
        CHECK (LEN(LTRIM(RTRIM(PermissionKey))) > 0),
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId)
);

CREATE UNIQUE INDEX UX_RolePermissions_RoleId_PermissionKey
    ON dbo.RolePermissions (RoleId, PermissionKey);

CREATE TABLE dbo.UserPermissions
(
    UserPermissionId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_UserPermissions PRIMARY KEY,
    UserId int NOT NULL,
    PermissionKey nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Granted bit NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_UserPermissions_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId int NULL,
    UpdatedAtUtc datetime2(7) NULL,
    UpdatedByUserId int NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_UserPermissions_PermissionKey_NotBlank
        CHECK (LEN(LTRIM(RTRIM(PermissionKey))) > 0),
    CONSTRAINT FK_UserPermissions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId)
);

CREATE UNIQUE INDEX UX_UserPermissions_UserId_PermissionKey
    ON dbo.UserPermissions (UserId, PermissionKey);
