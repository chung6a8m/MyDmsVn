SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

CREATE TABLE dbo.WebAuthSessions
(
    SessionId uniqueidentifier NOT NULL CONSTRAINT PK_WebAuthSessions PRIMARY KEY,
    UserId int NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL,
    AbsoluteExpiresAtUtc datetime2(7) NOT NULL,
    IdleExpiresAtUtc datetime2(7) NOT NULL,
    RevokedAtUtc datetime2(7) NULL,
    CONSTRAINT FK_WebAuthSessions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_WebAuthSessions_Expiry CHECK (CreatedAtUtc < AbsoluteExpiresAtUtc AND CreatedAtUtc < IdleExpiresAtUtc AND IdleExpiresAtUtc <= AbsoluteExpiresAtUtc),
    CONSTRAINT CK_WebAuthSessions_Revocation CHECK (RevokedAtUtc IS NULL OR RevokedAtUtc >= CreatedAtUtc)
);
CREATE INDEX IX_WebAuthSessions_UserId ON dbo.WebAuthSessions(UserId);
CREATE INDEX IX_WebAuthSessions_Retention ON dbo.WebAuthSessions(AbsoluteExpiresAtUtc, SessionId);

CREATE TABLE dbo.WebRefreshTokens
(
    TokenHash binary(32) NOT NULL CONSTRAINT PK_WebRefreshTokens PRIMARY KEY,
    SessionId uniqueidentifier NOT NULL,
    IssuedAtUtc datetime2(7) NOT NULL,
    ExpiresAtUtc datetime2(7) NOT NULL,
    ConsumedAtUtc datetime2(7) NULL,
    ReplacedByTokenHash binary(32) NULL,
    RevokedAtUtc datetime2(7) NULL,
    CONSTRAINT FK_WebRefreshTokens_Sessions FOREIGN KEY (SessionId) REFERENCES dbo.WebAuthSessions(SessionId),
    CONSTRAINT FK_WebRefreshTokens_Replacement FOREIGN KEY (ReplacedByTokenHash) REFERENCES dbo.WebRefreshTokens(TokenHash),
    CONSTRAINT CK_WebRefreshTokens_Expiry CHECK (IssuedAtUtc < ExpiresAtUtc),
    CONSTRAINT CK_WebRefreshTokens_Consumption CHECK ((ConsumedAtUtc IS NULL AND ReplacedByTokenHash IS NULL) OR (ConsumedAtUtc IS NOT NULL AND ConsumedAtUtc >= IssuedAtUtc AND ReplacedByTokenHash IS NOT NULL AND ReplacedByTokenHash <> TokenHash)),
    CONSTRAINT CK_WebRefreshTokens_Revocation CHECK (RevokedAtUtc IS NULL OR RevokedAtUtc >= IssuedAtUtc)
);
CREATE INDEX IX_WebRefreshTokens_SessionId ON dbo.WebRefreshTokens(SessionId);
CREATE UNIQUE INDEX UX_WebRefreshTokens_Replacement ON dbo.WebRefreshTokens(ReplacedByTokenHash) WHERE ReplacedByTokenHash IS NOT NULL;

GO
CREATE TRIGGER dbo.TR_Users_RevokeWebSessions
ON dbo.Users
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    -- Re-enabling an account never revives its earlier sessions.
    UPDATE sessions
    SET RevokedAtUtc = COALESCE(sessions.RevokedAtUtc, CASE WHEN SYSUTCDATETIME() < sessions.CreatedAtUtc THEN sessions.CreatedAtUtc ELSE SYSUTCDATETIME() END)
    FROM dbo.WebAuthSessions AS sessions
    INNER JOIN inserted AS currentUser ON currentUser.UserId = sessions.UserId
    INNER JOIN deleted AS previousUser ON previousUser.UserId = currentUser.UserId
    WHERE previousUser.IsActive = 1 AND currentUser.IsActive = 0;
END;
