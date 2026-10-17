ALTER TABLE dbo.Users
    ADD AssignmentRevision bigint NOT NULL
        CONSTRAINT DF_Users_AssignmentRevision DEFAULT (0);

ALTER TABLE dbo.Roles
    ADD AssignmentRevision bigint NOT NULL
        CONSTRAINT DF_Roles_AssignmentRevision DEFAULT (0);

CREATE TABLE dbo.SecurityAuditEvents
(
    AuditEventId bigint IDENTITY(1, 1) NOT NULL
        CONSTRAINT PK_SecurityAuditEvents PRIMARY KEY,
    ActorUserId int NOT NULL,
    TargetType nvarchar(8) COLLATE Latin1_General_100_BIN2 NOT NULL,
    TargetId int NOT NULL,
    Operation nvarchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    BeforeState nvarchar(1000) NULL,
    AfterState nvarchar(1000) NULL,
    OccurredAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_SecurityAuditEvents_OccurredAtUtc DEFAULT (SYSUTCDATETIME()),
    CorrelationId uniqueidentifier NOT NULL,
    CONSTRAINT CK_SecurityAuditEvents_TargetType
        CHECK (TargetType IN (N'User', N'Role')),
    CONSTRAINT CK_SecurityAuditEvents_TargetId CHECK (TargetId > 0),
    CONSTRAINT CK_SecurityAuditEvents_Operation_NotBlank
        CHECK (LEN(LTRIM(RTRIM(Operation))) > 0),
    CONSTRAINT FK_SecurityAuditEvents_Actor
        FOREIGN KEY (ActorUserId) REFERENCES dbo.Users (UserId)
);

CREATE INDEX IX_SecurityAuditEvents_Target
    ON dbo.SecurityAuditEvents (TargetType, TargetId, AuditEventId DESC);

GO

CREATE PROCEDURE dbo.ApplySecurityAdministration
    @Target nvarchar(8),
    @Operation nvarchar(32),
    @ActorUserId int,
    @TargetId int = 0,
    @RelatedId int = 0,
    @Name nvarchar(100) = NULL,
    @NormalizedName nvarchar(100) = NULL,
    @DisplayName nvarchar(100) = NULL,
    @Email nvarchar(254) = NULL,
    @IsActive bit = NULL,
    @PasswordHash nvarchar(255) = NULL,
    @PasswordAlgorithm nvarchar(32) = NULL,
    @PermissionKey nvarchar(100) = NULL,
    @Decision bit = NULL,
    @ExpectedRevision bigint = NULL,
    @ExpectedRelatedRevision bigint = NULL,
    @CorrelationId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Target NOT IN (N'User', N'Role') OR
       @Operation NOT IN (N'Create', N'Update', N'SetActive', N'SetPassword', N'SetMembership', N'SetPermission') OR
       @ActorUserId <= 0 OR @CorrelationId IS NULL
        THROW 51032, 'Invalid security administration request.', 1;

    IF @Operation = N'SetPermission' AND
       (@PermissionKey IS NULL OR LEN(@PermissionKey) = 0 OR
        LEN(@PermissionKey) <> DATALENGTH(@PermissionKey) / 2 OR
        LEFT(@PermissionKey, 1) = N'.' OR RIGHT(@PermissionKey, 1) = N'.' OR
        CHARINDEX(N'..', @PermissionKey) > 0 OR
        @PermissionKey COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Za-z0-9.]%')
        THROW 51032, 'Invalid canonical permission key.', 1;

    DECLARE @Changed bit = 0;
    DECLARE @NewRevision bigint = 0;
    DECLARE @NewRelatedRevision bigint = NULL;
    DECLARE @BeforeState nvarchar(1000) = NULL;
    DECLARE @AfterState nvarchar(1000) = NULL;
    DECLARE @BoundUserId int = NULL;
    DECLARE @BoundName nvarchar(100) = NULL;
    DECLARE @CurrentRevision bigint = NULL;
    DECLARE @RelatedRevision bigint = NULL;
    DECLARE @CurrentValue bit = NULL;
    DECLARE @ActorIsActive bit = NULL;
    DECLARE @ActorNormalizedUsername nvarchar(100) = NULL;
    DECLARE @RequiredPermissionKey nvarchar(100) =
        CASE WHEN @Target = N'User' THEN N'Security.Users.Manage' ELSE N'Security.Roles.Manage' END;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @ActorIsActive = IsActive,
               @ActorNormalizedUsername = NormalizedUsername
        FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
        WHERE UserId = @ActorUserId;
        IF @ActorIsActive IS NULL OR @ActorIsActive = 0
            THROW 51035, 'The security actor is no longer active.', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM dbo.SystemSecuritySettings
            WHERE Id = 1 AND SuperuserUserId = @ActorUserId
              AND BoundNormalizedUsername = @ActorNormalizedUsername COLLATE Latin1_General_100_BIN2
        )
        BEGIN
            SELECT @CurrentValue = Granted
            FROM dbo.UserPermissions WITH (UPDLOCK, HOLDLOCK)
            WHERE UserId = @ActorUserId
              AND PermissionKey = @RequiredPermissionKey COLLATE Latin1_General_100_BIN2;
            IF @CurrentValue IS NOT NULL AND @CurrentValue = 0
                THROW 51035, 'The security actor is not authorized for this operation.', 1;
            IF @CurrentValue IS NULL AND NOT EXISTS
            (
                SELECT 1
                FROM dbo.UserRoles AS membership WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.Roles AS activeRole
                    ON activeRole.RoleId = membership.RoleId AND activeRole.IsActive = 1
                INNER JOIN dbo.RolePermissions AS grantRow
                    ON grantRow.RoleId = activeRole.RoleId
                WHERE membership.UserId = @ActorUserId
                  AND grantRow.PermissionKey = @RequiredPermissionKey COLLATE Latin1_General_100_BIN2
            )
                THROW 51035, 'The security actor is not authorized for this operation.', 1;
        END;

        IF @Target = N'User' AND @Operation = N'Create'
        BEGIN
            IF @Name IS NULL OR @NormalizedName IS NULL OR @DisplayName IS NULL OR
               @PasswordHash IS NULL OR @PasswordAlgorithm IS NULL OR @IsActive IS NULL OR
               @ExpectedRevision IS NOT NULL OR
               LEN(@Name) = 0 OR LEN(@Name) <> DATALENGTH(@Name) / 2 OR
               @NormalizedName <> UPPER(LTRIM(RTRIM(@Name))) COLLATE Latin1_General_100_BIN2 OR
               LEN(@DisplayName) = 0 OR LEN(@DisplayName) <> DATALENGTH(@DisplayName) / 2
                THROW 51032, 'Invalid user creation request.', 1;

            SELECT @BoundName = BoundNormalizedUsername
            FROM dbo.SystemSecuritySettings WITH (UPDLOCK, HOLDLOCK)
            WHERE Id = 1;
            IF @BoundName IS NOT NULL AND @NormalizedName = @BoundName COLLATE Latin1_General_100_BIN2
                THROW 51033, 'The configured superuser name is reserved.', 1;

            INSERT dbo.Users
                (Username, NormalizedUsername, DisplayName, Email, Source, PasswordHash,
                 PasswordSalt, PasswordAlgorithm, IsActive, CreatedByUserId)
            VALUES
                (@Name, @NormalizedName, @DisplayName, @Email, N'Local', @PasswordHash,
                 N'', @PasswordAlgorithm, @IsActive, @ActorUserId);
            SET @TargetId = CONVERT(int, SCOPE_IDENTITY());
            SET @Changed = 1;
            SET @AfterState = CONCAT(N'Username=', @NormalizedName, N';IsActive=', @IsActive);
        END
        ELSE IF @Target = N'Role' AND @Operation = N'Create'
        BEGIN
            IF @Name IS NULL OR @NormalizedName IS NULL OR @IsActive IS NULL OR
               @ExpectedRevision IS NOT NULL OR LEN(@Name) = 0 OR
               LEN(@Name) <> DATALENGTH(@Name) / 2 OR
               @NormalizedName <> UPPER(LTRIM(RTRIM(@Name))) COLLATE Latin1_General_100_BIN2
                THROW 51032, 'Invalid role creation request.', 1;

            INSERT dbo.Roles (RoleName, NormalizedRoleName, IsActive, CreatedByUserId)
            VALUES (@Name, @NormalizedName, @IsActive, @ActorUserId);
            SET @TargetId = CONVERT(int, SCOPE_IDENTITY());
            SET @Changed = 1;
            SET @AfterState = CONCAT(N'Name=', @NormalizedName, N';IsActive=', @IsActive);
        END
        ELSE IF @Target = N'User'
        BEGIN
            SELECT @CurrentRevision = AssignmentRevision,
                   @BoundUserId = settings.SuperuserUserId,
                   @BoundName = settings.BoundNormalizedUsername,
                   @BeforeState = CONCAT(N'Username=', userRow.NormalizedUsername, N';IsActive=', userRow.IsActive)
            FROM dbo.Users AS userRow WITH (UPDLOCK, HOLDLOCK)
            LEFT JOIN dbo.SystemSecuritySettings AS settings WITH (UPDLOCK, HOLDLOCK)
                ON settings.Id = 1
            WHERE userRow.UserId = @TargetId;
            IF @CurrentRevision IS NULL THROW 51031, 'Security user was not found.', 1;
            IF @ExpectedRevision IS NULL OR @ExpectedRevision <> @CurrentRevision
                THROW 51030, 'Security user revision changed.', 1;
            SET @NewRevision = @CurrentRevision;

            IF @Operation IN (N'SetActive', N'SetPassword') AND @BoundUserId = @TargetId
                THROW 51033, 'The bound superuser requires maintenance operations.', 1;

            IF @Operation = N'Update'
            BEGIN
                IF @Name IS NULL OR @NormalizedName IS NULL OR @DisplayName IS NULL OR
                   LEN(@Name) = 0 OR LEN(@Name) <> DATALENGTH(@Name) / 2 OR
                   @NormalizedName <> UPPER(LTRIM(RTRIM(@Name))) COLLATE Latin1_General_100_BIN2 OR
                   LEN(@DisplayName) = 0 OR LEN(@DisplayName) <> DATALENGTH(@DisplayName) / 2
                    THROW 51032, 'Invalid user update request.', 1;
                IF (@BoundUserId = @TargetId AND @NormalizedName <> @BoundName COLLATE Latin1_General_100_BIN2) OR
                   (@BoundUserId <> @TargetId AND @BoundName IS NOT NULL AND
                    @NormalizedName = @BoundName COLLATE Latin1_General_100_BIN2)
                    THROW 51033, 'The configured superuser name is reserved.', 1;
                IF EXISTS
                (
                    SELECT 1 FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
                    WHERE NormalizedUsername = @NormalizedName COLLATE Latin1_General_100_CI_AI
                      AND UserId <> @TargetId
                )
                    THROW 51034, 'Username already exists.', 1;
                UPDATE dbo.Users
                SET Username = @Name,
                    NormalizedUsername = @NormalizedName,
                    DisplayName = @DisplayName,
                    Email = @Email,
                    UpdatedAtUtc = SYSUTCDATETIME(),
                    UpdatedByUserId = @ActorUserId,
                    AssignmentRevision = AssignmentRevision + 1
                WHERE UserId = @TargetId;
                SET @Changed = 1;
                SET @NewRevision = @CurrentRevision + 1;
                SET @AfterState = CONCAT(N'Username=', @NormalizedName, N';IsActive=', (SELECT IsActive FROM dbo.Users WHERE UserId = @TargetId));
            END
            ELSE IF @Operation = N'SetActive'
            BEGIN
                IF @IsActive IS NULL THROW 51032, 'An active state is required.', 1;
                IF (SELECT IsActive FROM dbo.Users WHERE UserId = @TargetId) <> @IsActive
                BEGIN
                    UPDATE dbo.Users
                    SET IsActive = @IsActive,
                        UpdatedAtUtc = SYSUTCDATETIME(),
                        UpdatedByUserId = @ActorUserId,
                        AssignmentRevision = AssignmentRevision + 1
                    WHERE UserId = @TargetId;
                    SET @Changed = 1;
                    SET @NewRevision = @CurrentRevision + 1;
                    SET @AfterState = CONCAT(N'IsActive=', @IsActive);
                END
            END
            ELSE IF @Operation = N'SetPassword'
            BEGIN
                IF @PasswordHash IS NULL OR @PasswordAlgorithm IS NULL
                    THROW 51032, 'A hashed password is required.', 1;
                SET @BeforeState = N'PasswordCredential=Present';
                UPDATE dbo.Users
                SET PasswordHash = @PasswordHash,
                    PasswordSalt = N'',
                    PasswordAlgorithm = @PasswordAlgorithm,
                    UpdatedAtUtc = SYSUTCDATETIME(),
                    UpdatedByUserId = @ActorUserId,
                    AssignmentRevision = AssignmentRevision + 1
                WHERE UserId = @TargetId;
                SET @Changed = 1;
                SET @NewRevision = @CurrentRevision + 1;
                SET @AfterState = N'PasswordChanged';
            END
            ELSE IF @Operation = N'SetMembership'
            BEGIN
                IF @RelatedId <= 0 OR @IsActive IS NULL OR @ExpectedRelatedRevision IS NULL
                    THROW 51032, 'Invalid membership request.', 1;
                SELECT @RelatedRevision = AssignmentRevision
                FROM dbo.Roles WITH (UPDLOCK, HOLDLOCK)
                WHERE RoleId = @RelatedId;
                IF @RelatedRevision IS NULL THROW 51031, 'Security role was not found.', 1;
                IF @ExpectedRelatedRevision <> @RelatedRevision
                    THROW 51030, 'Security role revision changed.', 1;
                SET @NewRelatedRevision = @RelatedRevision;
                SET @CurrentValue = CASE WHEN EXISTS
                    (SELECT 1 FROM dbo.UserRoles WHERE UserId = @TargetId AND RoleId = @RelatedId)
                    THEN 1 ELSE 0 END;
                SET @BeforeState = CONCAT(N'RoleId=', @RelatedId, N';Member=',
                    CASE WHEN @CurrentValue = 1 THEN N'True' ELSE N'False' END);
                IF @CurrentValue <> @IsActive
                BEGIN
                    IF @IsActive = 1
                        INSERT dbo.UserRoles (UserId, RoleId, CreatedByUserId)
                        VALUES (@TargetId, @RelatedId, @ActorUserId);
                    ELSE
                        DELETE dbo.UserRoles WHERE UserId = @TargetId AND RoleId = @RelatedId;
                    UPDATE dbo.Users SET AssignmentRevision = AssignmentRevision + 1,
                        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
                    WHERE UserId = @TargetId;
                    UPDATE dbo.Roles SET AssignmentRevision = AssignmentRevision + 1,
                        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
                    WHERE RoleId = @RelatedId;
                    SET @Changed = 1;
                    SET @NewRevision = @CurrentRevision + 1;
                    SET @NewRelatedRevision = @RelatedRevision + 1;
                    SET @AfterState = CONCAT(N'RoleId=', @RelatedId, N';Member=',
                        CASE WHEN @IsActive = 1 THEN N'True' ELSE N'False' END);
                END
            END
            ELSE IF @Operation = N'SetPermission'
            BEGIN
                IF @PermissionKey IS NULL
                    THROW 51032, 'A permission key is required.', 1;
                SET @CurrentValue = NULL;
                SELECT @CurrentValue = Granted FROM dbo.UserPermissions WITH (UPDLOCK, HOLDLOCK)
                WHERE UserId = @TargetId AND PermissionKey = @PermissionKey COLLATE Latin1_General_100_BIN2;
                SET @BeforeState = CONCAT(N'Permission=', @PermissionKey, N';Decision=',
                    CASE WHEN @CurrentValue IS NULL THEN N'Inherit' WHEN @CurrentValue = 1 THEN N'Allow' ELSE N'Deny' END);
                IF @CurrentValue IS NULL AND @Decision IS NOT NULL
                BEGIN
                    INSERT dbo.UserPermissions (UserId, PermissionKey, Granted, CreatedByUserId)
                    VALUES (@TargetId, @PermissionKey, @Decision, @ActorUserId);
                    SET @Changed = 1;
                END
                ELSE IF @CurrentValue IS NOT NULL AND @Decision IS NULL
                BEGIN
                    DELETE dbo.UserPermissions
                    WHERE UserId = @TargetId AND PermissionKey = @PermissionKey COLLATE Latin1_General_100_BIN2;
                    SET @Changed = 1;
                END
                ELSE IF @CurrentValue <> @Decision
                BEGIN
                    UPDATE dbo.UserPermissions SET Granted = @Decision,
                        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
                    WHERE UserId = @TargetId AND PermissionKey = @PermissionKey COLLATE Latin1_General_100_BIN2;
                    SET @Changed = 1;
                END
                IF @Changed = 1
                BEGIN
                    UPDATE dbo.Users SET AssignmentRevision = AssignmentRevision + 1,
                        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
                    WHERE UserId = @TargetId;
                    SET @NewRevision = @CurrentRevision + 1;
                    SET @AfterState = CONCAT(N'Permission=', @PermissionKey, N';Decision=',
                        CASE WHEN @Decision IS NULL THEN N'Inherit' WHEN @Decision = 1 THEN N'Allow' ELSE N'Deny' END);
                END
            END
            ELSE THROW 51032, 'Unsupported user operation.', 1;
        END
        ELSE IF @Target = N'Role'
        BEGIN
            SELECT @CurrentRevision = AssignmentRevision,
                   @BeforeState = CONCAT(N'Name=', roleRow.NormalizedRoleName, N';IsActive=', roleRow.IsActive)
            FROM dbo.Roles AS roleRow WITH (UPDLOCK, HOLDLOCK)
            WHERE roleRow.RoleId = @TargetId;
            IF @CurrentRevision IS NULL THROW 51031, 'Security role was not found.', 1;
            IF @ExpectedRevision IS NULL OR @ExpectedRevision <> @CurrentRevision
                THROW 51030, 'Security role revision changed.', 1;
            SET @NewRevision = @CurrentRevision;

            IF @Operation = N'Update'
            BEGIN
                IF @Name IS NULL OR @NormalizedName IS NULL OR LEN(@Name) = 0 OR
                   LEN(@Name) <> DATALENGTH(@Name) / 2 OR
                   @NormalizedName <> UPPER(LTRIM(RTRIM(@Name))) COLLATE Latin1_General_100_BIN2
                    THROW 51032, 'Invalid role update request.', 1;
                IF EXISTS (SELECT 1 FROM dbo.Roles WITH (UPDLOCK, HOLDLOCK)
                           WHERE NormalizedRoleName = @NormalizedName COLLATE Latin1_General_100_CI_AI
                             AND RoleId <> @TargetId)
                    THROW 51034, 'Role name already exists.', 1;
                UPDATE dbo.Roles SET RoleName = @Name, NormalizedRoleName = @NormalizedName,
                    AssignmentRevision = AssignmentRevision + 1,
                    UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
                WHERE RoleId = @TargetId;
                SET @Changed = 1;
                SET @NewRevision = @CurrentRevision + 1;
                SET @AfterState = CONCAT(N'Name=', @NormalizedName);
            END
            ELSE IF @Operation = N'SetActive'
            BEGIN
                IF @IsActive IS NULL THROW 51032, 'An active state is required.', 1;
                IF (SELECT IsActive FROM dbo.Roles WHERE RoleId = @TargetId) <> @IsActive
                BEGIN
                    UPDATE dbo.Roles SET IsActive = @IsActive, AssignmentRevision = AssignmentRevision + 1,
                        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
                    WHERE RoleId = @TargetId;
                    SET @Changed = 1;
                    SET @NewRevision = @CurrentRevision + 1;
                    SET @AfterState = CONCAT(N'IsActive=', @IsActive);
                END
            END
            ELSE IF @Operation = N'SetPermission'
            BEGIN
                IF @PermissionKey IS NULL OR @Decision IS NULL
                    THROW 51032, 'Role assignments require an Active permission and Allow decision.', 1;
                SET @CurrentValue = CASE WHEN EXISTS
                    (SELECT 1 FROM dbo.RolePermissions WITH (UPDLOCK, HOLDLOCK)
                     WHERE RoleId = @TargetId AND PermissionKey = @PermissionKey COLLATE Latin1_General_100_BIN2)
                    THEN 1 ELSE 0 END;
                SET @BeforeState = CONCAT(N'Permission=', @PermissionKey, N';Decision=',
                    CASE WHEN @CurrentValue = 1 THEN N'Allow' ELSE N'Inherit' END);
                IF @CurrentValue <> @Decision
                BEGIN
                    IF @Decision = 1
                        INSERT dbo.RolePermissions (RoleId, PermissionKey, CreatedByUserId)
                        VALUES (@TargetId, @PermissionKey, @ActorUserId);
                    ELSE
                        DELETE dbo.RolePermissions
                        WHERE RoleId = @TargetId AND PermissionKey = @PermissionKey COLLATE Latin1_General_100_BIN2;
                    UPDATE dbo.Roles SET AssignmentRevision = AssignmentRevision + 1,
                        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
                    WHERE RoleId = @TargetId;
                    SET @Changed = 1;
                    SET @NewRevision = @CurrentRevision + 1;
                    SET @AfterState = CONCAT(N'Permission=', @PermissionKey, N';Decision=',
                        CASE WHEN @Decision = 1 THEN N'Allow' ELSE N'Inherit' END);
                END
            END
            ELSE THROW 51032, 'Unsupported role operation.', 1;
        END

        IF @Changed = 1
        BEGIN
            INSERT dbo.SecurityAuditEvents
                (ActorUserId, TargetType, TargetId, Operation, BeforeState, AfterState, CorrelationId)
            VALUES
                (@ActorUserId, @Target, @TargetId, @Operation, @BeforeState, @AfterState, @CorrelationId);
        END;

        COMMIT TRANSACTION;
        SELECT @NewRevision AS AssignmentRevision,
               @Changed AS Changed,
               @NewRelatedRevision AS RelatedAssignmentRevision;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
