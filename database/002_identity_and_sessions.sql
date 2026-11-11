/* ============================================================
   TONYX - Script 002: identidad, dispositivos y sesiones
   Tablas: Users, UserProfiles, UserDevices, UserSessions,
           RefreshTokens, EmailVerificationTokens, PasswordResetTokens
   Es repetible: cada tabla se crea solo si no existe.
   Requiere haber ejecutado el script 001.
   ============================================================ */

USE TonyxDb;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ------------------------------------------------------------
   Users: cuenta de acceso. Platform de login, no perfil.
   Status: 1 = Activo, 2 = Suspendido, 3 = Eliminado.
   Email se compara sin distinguir mayusculas, pero CON acentos.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id               UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Users_Id DEFAULT (NEWSEQUENTIALID()),
        Email            NVARCHAR(256) COLLATE Latin1_General_100_CI_AS NOT NULL,
        PasswordHash     NVARCHAR(500) NULL,
        EmailConfirmed   BIT NOT NULL
            CONSTRAINT DF_Users_EmailConfirmed DEFAULT (0),
        Status           TINYINT NOT NULL
            CONSTRAINT DF_Users_Status DEFAULT (1),
        FailedLoginCount INT NOT NULL
            CONSTRAINT DF_Users_FailedLoginCount DEFAULT (0),
        LockoutEndUtc    DATETIME2(3) NULL,
        LastLoginAtUtc   DATETIME2(3) NULL,
        CreatedAtUtc     DATETIME2(3) NOT NULL
            CONSTRAINT DF_Users_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc     DATETIME2(3) NOT NULL
            CONSTRAINT DF_Users_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]     ROWVERSION NOT NULL,
        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Users_Status CHECK (Status IN (1, 2, 3)),
        CONSTRAINT CK_Users_FailedLoginCount CHECK (FailedLoginCount >= 0),
        CONSTRAINT CK_Users_Email CHECK (Email LIKE N'%_@_%._%')
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Users_Email ON dbo.Users (Email);
END
GO

/* ------------------------------------------------------------
   UserProfiles: relacion 1 a 1 con Users (datos visibles del usuario).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.UserProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserProfiles
    (
        UserId        UNIQUEIDENTIFIER NOT NULL,
        DisplayName   NVARCHAR(100) NOT NULL,
        AvatarUrl     NVARCHAR(500) NULL,
        CountryCode   CHAR(2) NULL,
        LanguageCode  NVARCHAR(10) NULL,
        CreatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_UserProfiles_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_UserProfiles_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]  ROWVERSION NOT NULL,
        CONSTRAINT PK_UserProfiles PRIMARY KEY CLUSTERED (UserId),
        CONSTRAINT FK_UserProfiles_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT CK_UserProfiles_DisplayName CHECK (LEN(LTRIM(RTRIM(DisplayName))) > 0)
    );
END
GO

/* ------------------------------------------------------------
   UserDevices: cada instalacion de la app de un usuario.
   Platform: 1 = Android, 2 = iOS, 3 = Windows, 4 = Web.
   DeviceIdentifier: identificador generado por la app al instalarse.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.UserDevices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserDevices
    (
        Id                UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_UserDevices_Id DEFAULT (NEWSEQUENTIALID()),
        UserId            UNIQUEIDENTIFIER NOT NULL,
        DeviceIdentifier  NVARCHAR(200) NOT NULL,
        DeviceName        NVARCHAR(100) NOT NULL,
        Platform          TINYINT NOT NULL,
        AppVersion        NVARCHAR(30) NULL,
        LastSeenAtUtc     DATETIME2(3) NULL,
        CreatedAtUtc      DATETIME2(3) NOT NULL
            CONSTRAINT DF_UserDevices_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc      DATETIME2(3) NOT NULL
            CONSTRAINT DF_UserDevices_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]      ROWVERSION NOT NULL,
        CONSTRAINT PK_UserDevices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_UserDevices_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT CK_UserDevices_Platform CHECK (Platform IN (1, 2, 3, 4))
    );

    -- Un mismo dispositivo no se registra dos veces para el mismo usuario.
    CREATE UNIQUE NONCLUSTERED INDEX UX_UserDevices_User_Device
        ON dbo.UserDevices (UserId, DeviceIdentifier);
END
GO

/* ------------------------------------------------------------
   UserSessions: una sesion iniciada en un dispositivo.
   Cerrar sesion = rellenar RevokedAtUtc.
   UserId va SIN cascada (la cascada llega por UserDevices) para
   evitar el error de multiples rutas de cascada de SQL Server.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.UserSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSessions
    (
        Id                UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_UserSessions_Id DEFAULT (NEWSEQUENTIALID()),
        UserId            UNIQUEIDENTIFIER NOT NULL,
        DeviceId          UNIQUEIDENTIFIER NOT NULL,
        CreatedAtUtc      DATETIME2(3) NOT NULL
            CONSTRAINT DF_UserSessions_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        LastActivityAtUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_UserSessions_LastActivityAtUtc DEFAULT (SYSUTCDATETIME()),
        ExpiresAtUtc      DATETIME2(3) NOT NULL,
        RevokedAtUtc      DATETIME2(3) NULL,
        RevokedReason     NVARCHAR(100) NULL,
        IpAddress         NVARCHAR(45) NULL,
        UserAgent         NVARCHAR(300) NULL,
        CONSTRAINT PK_UserSessions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id),
        CONSTRAINT FK_UserSessions_UserDevices FOREIGN KEY (DeviceId)
            REFERENCES dbo.UserDevices (Id) ON DELETE CASCADE,
        CONSTRAINT CK_UserSessions_Expiry CHECK (ExpiresAtUtc > CreatedAtUtc)
    );

    CREATE NONCLUSTERED INDEX IX_UserSessions_DeviceId
        ON dbo.UserSessions (DeviceId);

    -- Acelera "mis sesiones activas".
    CREATE NONCLUSTERED INDEX IX_UserSessions_UserId_Active
        ON dbo.UserSessions (UserId)
        WHERE RevokedAtUtc IS NULL;
END
GO

/* ------------------------------------------------------------
   RefreshTokens: tokens de renovacion. Se guarda SOLO el hash SHA-256
   en hexadecimal (64 caracteres), nunca el token.
   ReplacedByTokenId permite detectar reutilizacion de tokens (rotacion).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens
    (
        Id                 UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_RefreshTokens_Id DEFAULT (NEWSEQUENTIALID()),
        SessionId          UNIQUEIDENTIFIER NOT NULL,
        UserId             UNIQUEIDENTIFIER NOT NULL,
        TokenHash          CHAR(64) NOT NULL,
        CreatedAtUtc       DATETIME2(3) NOT NULL
            CONSTRAINT DF_RefreshTokens_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ExpiresAtUtc       DATETIME2(3) NOT NULL,
        UsedAtUtc          DATETIME2(3) NULL,
        RevokedAtUtc       DATETIME2(3) NULL,
        ReplacedByTokenId  UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RefreshTokens_UserSessions FOREIGN KEY (SessionId)
            REFERENCES dbo.UserSessions (Id) ON DELETE CASCADE,
        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id),
        CONSTRAINT FK_RefreshTokens_ReplacedBy FOREIGN KEY (ReplacedByTokenId)
            REFERENCES dbo.RefreshTokens (Id),
        CONSTRAINT CK_RefreshTokens_Expiry CHECK (ExpiresAtUtc > CreatedAtUtc)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_RefreshTokens_TokenHash
        ON dbo.RefreshTokens (TokenHash);
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_SessionId
        ON dbo.RefreshTokens (SessionId);
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_UserId
        ON dbo.RefreshTokens (UserId);
END
GO

/* ------------------------------------------------------------
   EmailVerificationTokens: verificacion del correo (un solo uso).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.EmailVerificationTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailVerificationTokens
    (
        Id            UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_EmailVerificationTokens_Id DEFAULT (NEWSEQUENTIALID()),
        UserId        UNIQUEIDENTIFIER NOT NULL,
        TokenHash     CHAR(64) NOT NULL,
        CreatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_EmailVerificationTokens_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ExpiresAtUtc  DATETIME2(3) NOT NULL,
        UsedAtUtc     DATETIME2(3) NULL,
        CONSTRAINT PK_EmailVerificationTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_EmailVerificationTokens_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT CK_EmailVerificationTokens_Expiry CHECK (ExpiresAtUtc > CreatedAtUtc)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_EmailVerificationTokens_TokenHash
        ON dbo.EmailVerificationTokens (TokenHash);
    CREATE NONCLUSTERED INDEX IX_EmailVerificationTokens_UserId
        ON dbo.EmailVerificationTokens (UserId);
END
GO

/* ------------------------------------------------------------
   PasswordResetTokens: recuperacion de contrasena (un solo uso).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        Id            UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_PasswordResetTokens_Id DEFAULT (NEWSEQUENTIALID()),
        UserId        UNIQUEIDENTIFIER NOT NULL,
        TokenHash     CHAR(64) NOT NULL,
        CreatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_PasswordResetTokens_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ExpiresAtUtc  DATETIME2(3) NOT NULL,
        UsedAtUtc     DATETIME2(3) NULL,
        CONSTRAINT PK_PasswordResetTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PasswordResetTokens_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT CK_PasswordResetTokens_Expiry CHECK (ExpiresAtUtc > CreatedAtUtc)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_PasswordResetTokens_TokenHash
        ON dbo.PasswordResetTokens (TokenHash);
    CREATE NONCLUSTERED INDEX IX_PasswordResetTokens_UserId
        ON dbo.PasswordResetTokens (UserId);
END
GO

/* ------------------------------------------------------------
   Registrar el script en el versionado del esquema.
   ------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptNumber = 2)
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptNumber, ScriptName)
    VALUES (2, N'002_identity_and_sessions');
END
GO

/* ------------------------------------------------------------
   Resultado: tablas existentes y scripts aplicados.
   ------------------------------------------------------------ */
SELECT t.name AS Tabla, COUNT(c.column_id) AS Columnas
FROM sys.tables AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
GROUP BY t.name
ORDER BY t.name;

SELECT ScriptNumber, ScriptName, AppliedAtUtc, AppliedBy
FROM dbo.SchemaVersions
ORDER BY ScriptNumber;
GO
