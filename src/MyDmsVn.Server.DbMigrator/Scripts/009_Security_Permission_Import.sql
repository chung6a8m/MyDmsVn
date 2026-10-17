CREATE PROCEDURE dbo.ReadSecurityPermissionImportTarget
    @TargetType nvarchar(8),
    @TargetId int,
    @ActorUserId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @TargetType NOT IN (N'User', N'Role') OR @TargetId <= 0 OR @ActorUserId <= 0
        THROW 51038, 'Invalid permission import target.', 1;

    DECLARE @ActorIsActive bit = NULL;
    DECLARE @ActorNormalizedUsername nvarchar(100) = NULL;
    DECLARE @Granted bit = NULL;
    DECLARE @RequiredPermissionKey nvarchar(100) =
        CASE WHEN @TargetType = N'User' THEN N'Security.Users.Manage' ELSE N'Security.Roles.Manage' END;

    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @ActorIsActive = IsActive, @ActorNormalizedUsername = NormalizedUsername
        FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE UserId = @ActorUserId;
        IF @ActorIsActive IS NULL OR @ActorIsActive = 0
            THROW 51035, 'The security actor is no longer active.', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM dbo.SystemSecuritySettings
            WHERE Id = 1 AND SuperuserUserId = @ActorUserId
              AND BoundNormalizedUsername = @ActorNormalizedUsername COLLATE Latin1_General_100_BIN2
        )
        BEGIN
            SELECT @Granted = Granted FROM dbo.UserPermissions WITH (UPDLOCK, HOLDLOCK)
            WHERE UserId = @ActorUserId
              AND PermissionKey = @RequiredPermissionKey COLLATE Latin1_General_100_BIN2;
            IF @Granted = 0 OR (@Granted IS NULL AND NOT EXISTS
            (
                SELECT 1 FROM dbo.UserRoles AS membership WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.Roles AS activeRole
                    ON activeRole.RoleId = membership.RoleId AND activeRole.IsActive = 1
                INNER JOIN dbo.RolePermissions AS grantRow
                    ON grantRow.RoleId = activeRole.RoleId
                WHERE membership.UserId = @ActorUserId
                  AND grantRow.PermissionKey = @RequiredPermissionKey COLLATE Latin1_General_100_BIN2
            ))
                THROW 51035, 'The security actor is not authorized for this operation.', 1;
        END;

        IF @TargetType = N'User'
        BEGIN
            SELECT target.UserId AS TargetId, target.Username AS TargetName,
                   target.IsActive, target.AssignmentRevision,
                   CONVERT(bit, CASE WHEN settings.SuperuserUserId = target.UserId THEN 1 ELSE 0 END) AS IsProtected
            FROM dbo.Users AS target WITH (UPDLOCK, HOLDLOCK)
            LEFT JOIN dbo.SystemSecuritySettings AS settings WITH (UPDLOCK, HOLDLOCK) ON settings.Id = 1
            WHERE target.UserId = @TargetId;
            IF @@ROWCOUNT = 0 THROW 51031, 'Security user was not found.', 1;

            SELECT PermissionKey, Granted FROM dbo.UserPermissions WITH (UPDLOCK, HOLDLOCK)
            WHERE UserId = @TargetId ORDER BY PermissionKey COLLATE Latin1_General_100_BIN2;
        END
        ELSE
        BEGIN
            SELECT RoleId AS TargetId, RoleName AS TargetName, IsActive,
                   AssignmentRevision, CONVERT(bit, 0) AS IsProtected
            FROM dbo.Roles WITH (UPDLOCK, HOLDLOCK) WHERE RoleId = @TargetId;
            IF @@ROWCOUNT = 0 THROW 51031, 'Security role was not found.', 1;

            SELECT PermissionKey, CONVERT(bit, 1) AS Granted FROM dbo.RolePermissions WITH (UPDLOCK, HOLDLOCK)
            WHERE RoleId = @TargetId ORDER BY PermissionKey COLLATE Latin1_General_100_BIN2;
        END;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE PROCEDURE dbo.ApplySecurityPermissionImport
    @TargetType nvarchar(8),
    @TargetId int,
    @ActorUserId int,
    @ExpectedRevision bigint,
    @EntriesJson nvarchar(max),
    @CorrelationId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @TargetType NOT IN (N'User', N'Role') OR @TargetId <= 0 OR @ActorUserId <= 0 OR
       @ExpectedRevision < 0 OR @EntriesJson IS NULL OR ISJSON(@EntriesJson) <> 1 OR
       @CorrelationId IS NULL
        THROW 51038, 'Invalid permission import request.', 1;

    DECLARE @Entries TABLE
    (
        PermissionKey nvarchar(100) COLLATE Latin1_General_100_BIN2 NULL,
        Granted bit NULL
    );
    DECLARE @Added TABLE
    (
        PermissionKey nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
        Granted bit NOT NULL
    );
    IF LEFT(LTRIM(@EntriesJson), 1) <> N'['
        THROW 51038, 'Permission import entries must be an array.', 1;

    INSERT @Entries (PermissionKey, Granted)
    SELECT JSON_VALUE(value, '$.PermissionKey'),
           CASE JSON_VALUE(value, '$.Granted')
               WHEN N'true' THEN CONVERT(bit, 1)
               WHEN N'false' THEN CONVERT(bit, 0)
               ELSE NULL
           END
    FROM OPENJSON(@EntriesJson);
    IF (SELECT COUNT(*) FROM @Entries) > 5000 OR
       EXISTS (SELECT 1 FROM OPENJSON(@EntriesJson) WHERE type <> 5) OR
       (SELECT COUNT(*) FROM OPENJSON(@EntriesJson)) <> (SELECT COUNT(*) FROM @Entries) OR
       EXISTS (SELECT PermissionKey FROM @Entries GROUP BY PermissionKey HAVING COUNT(*) > 1) OR
       EXISTS (SELECT 1 FROM @Entries WHERE PermissionKey IS NULL OR LEN(PermissionKey) = 0 OR
               LEN(PermissionKey) <> DATALENGTH(PermissionKey) / 2 OR Granted IS NULL OR
               LEFT(PermissionKey, 1) = N'.' OR RIGHT(PermissionKey, 1) = N'.' OR
               CHARINDEX(N'..', PermissionKey) > 0 OR
               PermissionKey COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Za-z0-9.]%') OR
       (@TargetType = N'Role' AND EXISTS (SELECT 1 FROM @Entries WHERE Granted = 0))
        THROW 51038, 'Permission import entries are invalid.', 1;

    DECLARE @ActorIsActive bit = NULL;
    DECLARE @ActorNormalizedUsername nvarchar(100) = NULL;
    DECLARE @Granted bit = NULL;
    DECLARE @RequiredPermissionKey nvarchar(100) =
        CASE WHEN @TargetType = N'User' THEN N'Security.Users.Manage' ELSE N'Security.Roles.Manage' END;
    DECLARE @CurrentRevision bigint = NULL;
    DECLARE @IsActive bit = NULL;
    DECLARE @IsProtected bit = 0;
    DECLARE @AddedCount int = 0;

    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @ActorIsActive = IsActive, @ActorNormalizedUsername = NormalizedUsername
        FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE UserId = @ActorUserId;
        IF @ActorIsActive IS NULL OR @ActorIsActive = 0
            THROW 51035, 'The security actor is no longer active.', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM dbo.SystemSecuritySettings
            WHERE Id = 1 AND SuperuserUserId = @ActorUserId
              AND BoundNormalizedUsername = @ActorNormalizedUsername COLLATE Latin1_General_100_BIN2
        )
        BEGIN
            SELECT @Granted = Granted FROM dbo.UserPermissions WITH (UPDLOCK, HOLDLOCK)
            WHERE UserId = @ActorUserId
              AND PermissionKey = @RequiredPermissionKey COLLATE Latin1_General_100_BIN2;
            IF @Granted = 0 OR (@Granted IS NULL AND NOT EXISTS
            (
                SELECT 1 FROM dbo.UserRoles AS membership WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.Roles AS activeRole
                    ON activeRole.RoleId = membership.RoleId AND activeRole.IsActive = 1
                INNER JOIN dbo.RolePermissions AS grantRow
                    ON grantRow.RoleId = activeRole.RoleId
                WHERE membership.UserId = @ActorUserId
                  AND grantRow.PermissionKey = @RequiredPermissionKey COLLATE Latin1_General_100_BIN2
            ))
                THROW 51035, 'The security actor is not authorized for this operation.', 1;
        END;

        IF @TargetType = N'User'
        BEGIN
            SELECT @CurrentRevision = target.AssignmentRevision, @IsActive = target.IsActive,
                   @IsProtected = CASE WHEN settings.SuperuserUserId = target.UserId THEN 1 ELSE 0 END
            FROM dbo.Users AS target WITH (UPDLOCK, HOLDLOCK)
            LEFT JOIN dbo.SystemSecuritySettings AS settings WITH (UPDLOCK, HOLDLOCK) ON settings.Id = 1
            WHERE target.UserId = @TargetId;
        END
        ELSE
        BEGIN
            SELECT @CurrentRevision = AssignmentRevision, @IsActive = IsActive
            FROM dbo.Roles WITH (UPDLOCK, HOLDLOCK) WHERE RoleId = @TargetId;
        END;
        IF @CurrentRevision IS NULL THROW 51031, 'Security import target was not found.', 1;
        IF @CurrentRevision <> @ExpectedRevision
            THROW 51030, 'Security assignment revision changed.', 1;
        IF @IsActive = 0 OR @IsProtected = 1
            THROW 51037, 'Security import target is inactive or protected.', 1;

        IF @TargetType = N'User'
        BEGIN
            IF EXISTS
            (
                SELECT 1 FROM @Entries AS input
                INNER JOIN dbo.UserPermissions AS existing WITH (UPDLOCK, HOLDLOCK)
                    ON existing.UserId = @TargetId
                   AND existing.PermissionKey = input.PermissionKey COLLATE Latin1_General_100_BIN2
                WHERE existing.Granted <> input.Granted
            )
                THROW 51036, 'An existing user decision conflicts with the import.', 1;

            INSERT dbo.UserPermissions (UserId, PermissionKey, Granted, CreatedByUserId)
            OUTPUT INSERTED.PermissionKey, INSERTED.Granted INTO @Added (PermissionKey, Granted)
            SELECT @TargetId, input.PermissionKey, input.Granted, @ActorUserId
            FROM @Entries AS input
            WHERE NOT EXISTS
            (
                SELECT 1 FROM dbo.UserPermissions AS existing WITH (UPDLOCK, HOLDLOCK)
                WHERE existing.UserId = @TargetId
                  AND existing.PermissionKey = input.PermissionKey COLLATE Latin1_General_100_BIN2
            );
            SET @AddedCount = @@ROWCOUNT;
        END
        ELSE
        BEGIN
            IF EXISTS
            (
                SELECT 1 FROM @Entries AS input
                INNER JOIN dbo.RolePermissions AS existing WITH (UPDLOCK, HOLDLOCK)
                    ON existing.RoleId = @TargetId
                   AND existing.PermissionKey = input.PermissionKey COLLATE Latin1_General_100_BIN2
                WHERE input.Granted = 0
            )
                THROW 51036, 'Role imports accept Allow decisions only.', 1;

            INSERT dbo.RolePermissions (RoleId, PermissionKey, CreatedByUserId)
            OUTPUT INSERTED.PermissionKey, CONVERT(bit, 1) INTO @Added (PermissionKey, Granted)
            SELECT @TargetId, input.PermissionKey, @ActorUserId
            FROM @Entries AS input
            WHERE NOT EXISTS
            (
                SELECT 1 FROM dbo.RolePermissions AS existing WITH (UPDLOCK, HOLDLOCK)
                WHERE existing.RoleId = @TargetId
                  AND existing.PermissionKey = input.PermissionKey COLLATE Latin1_General_100_BIN2
            );
            SET @AddedCount = @@ROWCOUNT;
        END;

        IF @AddedCount > 0
        BEGIN
            IF @TargetType = N'User'
                UPDATE dbo.Users SET AssignmentRevision = AssignmentRevision + 1,
                    UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
                WHERE UserId = @TargetId;
            ELSE
                UPDATE dbo.Roles SET AssignmentRevision = AssignmentRevision + 1,
                    UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
                WHERE RoleId = @TargetId;

            INSERT dbo.SecurityAuditEvents
                (ActorUserId, TargetType, TargetId, Operation, BeforeState, AfterState, CorrelationId)
            SELECT @ActorUserId, @TargetType, @TargetId, N'PermissionImport',
                   CONCAT(N'Permission=', PermissionKey, N';Decision=Inherit'),
                   CONCAT(N'Permission=', PermissionKey, N';Decision=',
                       CASE WHEN Granted = 1 THEN N'Allow' ELSE N'Deny' END),
                   @CorrelationId
            FROM @Added;
        END;

        COMMIT TRANSACTION;
        SELECT @CurrentRevision + CASE WHEN @AddedCount > 0 THEN 1 ELSE 0 END AS TargetRevision,
               @AddedCount AS AddedCount,
               CONVERT(bit, CASE WHEN @AddedCount > 0 THEN 1 ELSE 0 END) AS Changed;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
