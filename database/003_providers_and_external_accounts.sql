/* ============================================================
   TONYX - Script 003: proveedores y cuentas externas
   Tablas: ConnectedProviders (catalogo), ExternalAccounts (cuentas vinculadas)
   Es repetible: crea solo lo que falta y no duplica los datos iniciales.
   Requiere haber ejecutado los scripts 001 y 002.
   ============================================================ */

USE TonyxDb;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ------------------------------------------------------------
   ConnectedProviders: catalogo de servicios y sus capacidades.
   Kind: 1 = Musica, 2 = Identidad (solo inicio de sesion).
   Las capacidades indican lo que el servicio permite OFICIALMENTE.
   Id es fijo (no IDENTITY) para coincidir con un enum en C#.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.ConnectedProviders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConnectedProviders
    (
        Id                      SMALLINT NOT NULL,
        Code                    NVARCHAR(30) NOT NULL,
        DisplayName             NVARCHAR(60) NOT NULL,
        Kind                    TINYINT NOT NULL,
        RequiresOAuth           BIT NOT NULL
            CONSTRAINT DF_ConnectedProviders_RequiresOAuth DEFAULT (0),
        SupportsSearch          BIT NOT NULL
            CONSTRAINT DF_ConnectedProviders_SupportsSearch DEFAULT (0),
        SupportsLibrary         BIT NOT NULL
            CONSTRAINT DF_ConnectedProviders_SupportsLibrary DEFAULT (0),
        SupportsPlaylists       BIT NOT NULL
            CONSTRAINT DF_ConnectedProviders_SupportsPlaylists DEFAULT (0),
        SupportsPlaybackControl BIT NOT NULL
            CONSTRAINT DF_ConnectedProviders_SupportsPlaybackControl DEFAULT (0),
        SupportsDownloads       BIT NOT NULL
            CONSTRAINT DF_ConnectedProviders_SupportsDownloads DEFAULT (0),
        SupportsOpenInApp       BIT NOT NULL
            CONSTRAINT DF_ConnectedProviders_SupportsOpenInApp DEFAULT (0),
        IsEnabled               BIT NOT NULL
            CONSTRAINT DF_ConnectedProviders_IsEnabled DEFAULT (1),
        CreatedAtUtc            DATETIME2(3) NOT NULL
            CONSTRAINT DF_ConnectedProviders_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc            DATETIME2(3) NOT NULL
            CONSTRAINT DF_ConnectedProviders_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]            ROWVERSION NOT NULL,
        CONSTRAINT PK_ConnectedProviders PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_ConnectedProviders_Kind CHECK (Kind IN (1, 2)),
        CONSTRAINT CK_ConnectedProviders_Code CHECK (LEN(LTRIM(RTRIM(Code))) > 0)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_ConnectedProviders_Code
        ON dbo.ConnectedProviders (Code);
END
GO

/* ------------------------------------------------------------
   Datos iniciales del catalogo (solo se insertan los que faltan).
   Columnas: Id, Code, DisplayName, Kind, RequiresOAuth, Search,
             Library, Playlists, PlaybackControl, Downloads, OpenInApp
   ------------------------------------------------------------ */
INSERT INTO dbo.ConnectedProviders
    (Id, Code, DisplayName, Kind, RequiresOAuth, SupportsSearch, SupportsLibrary,
     SupportsPlaylists, SupportsPlaybackControl, SupportsDownloads, SupportsOpenInApp)
SELECT v.Id, v.Code, v.DisplayName, v.Kind, v.RequiresOAuth, v.SupportsSearch,
       v.SupportsLibrary, v.SupportsPlaylists, v.SupportsPlaybackControl,
       v.SupportsDownloads, v.SupportsOpenInApp
FROM (VALUES
    (1, N'spotify',          N'Spotify',          1, 1, 1, 1, 1, 1, 0, 1),
    (2, N'youtube',          N'YouTube',          1, 1, 1, 0, 1, 0, 0, 1),
    (3, N'youtube_music',    N'YouTube Music',    1, 0, 0, 0, 0, 0, 0, 1),
    (4, N'jamendo',          N'Jamendo',          1, 0, 1, 0, 0, 0, 1, 0),
    (5, N'internet_archive', N'Internet Archive', 1, 0, 1, 0, 0, 0, 1, 0),
    (6, N'google',           N'Google',           2, 1, 0, 0, 0, 0, 0, 0),
    (7, N'apple',            N'Apple',            2, 1, 0, 0, 0, 0, 0, 0)
) AS v (Id, Code, DisplayName, Kind, RequiresOAuth, SupportsSearch, SupportsLibrary,
        SupportsPlaylists, SupportsPlaybackControl, SupportsDownloads, SupportsOpenInApp)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ConnectedProviders AS p WHERE p.Id = v.Id);
GO

/* ------------------------------------------------------------
   ExternalAccounts: cuenta de un usuario vinculada a un proveedor.
   Status: 1 = Vinculada, 2 = Requiere volver a autorizar, 3 = Desvinculada.
   Los tokens se guardan CIFRADOS por la API; la base nunca ve un token en claro.
   Para Google/Apple (solo inicio de sesion) los tokens pueden ser NULL.
   NUNCA se guardan contrasenas de servicios externos.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.ExternalAccounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExternalAccounts
    (
        Id                     UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_ExternalAccounts_Id DEFAULT (NEWSEQUENTIALID()),
        UserId                 UNIQUEIDENTIFIER NOT NULL,
        ProviderId             SMALLINT NOT NULL,
        ExternalUserId         NVARCHAR(200) NOT NULL,
        DisplayName            NVARCHAR(150) NULL,
        AccessTokenEncrypted   VARBINARY(MAX) NULL,
        RefreshTokenEncrypted  VARBINARY(MAX) NULL,
        TokenExpiresAtUtc      DATETIME2(3) NULL,
        Scopes                 NVARCHAR(1000) NULL,
        Status                 TINYINT NOT NULL
            CONSTRAINT DF_ExternalAccounts_Status DEFAULT (1),
        LinkedAtUtc            DATETIME2(3) NOT NULL
            CONSTRAINT DF_ExternalAccounts_LinkedAtUtc DEFAULT (SYSUTCDATETIME()),
        UnlinkedAtUtc          DATETIME2(3) NULL,
        LastSyncAtUtc          DATETIME2(3) NULL,
        CreatedAtUtc           DATETIME2(3) NOT NULL
            CONSTRAINT DF_ExternalAccounts_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc           DATETIME2(3) NOT NULL
            CONSTRAINT DF_ExternalAccounts_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]           ROWVERSION NOT NULL,
        CONSTRAINT PK_ExternalAccounts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ExternalAccounts_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT FK_ExternalAccounts_ConnectedProviders FOREIGN KEY (ProviderId)
            REFERENCES dbo.ConnectedProviders (Id),
        CONSTRAINT CK_ExternalAccounts_Status CHECK (Status IN (1, 2, 3)),
        CONSTRAINT CK_ExternalAccounts_ExternalUserId
            CHECK (LEN(LTRIM(RTRIM(ExternalUserId))) > 0)
    );

    -- La misma cuenta externa no se vincula dos veces al mismo usuario.
    CREATE UNIQUE NONCLUSTERED INDEX UX_ExternalAccounts_User_Provider_External
        ON dbo.ExternalAccounts (UserId, ProviderId, ExternalUserId);

    CREATE NONCLUSTERED INDEX IX_ExternalAccounts_ProviderId
        ON dbo.ExternalAccounts (ProviderId);

    -- Para el inicio de sesion con Google/Apple: buscar por proveedor + id externo.
    CREATE NONCLUSTERED INDEX IX_ExternalAccounts_Provider_External
        ON dbo.ExternalAccounts (ProviderId, ExternalUserId);
END
GO

/* ------------------------------------------------------------
   Registrar el script en el versionado del esquema.
   ------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptNumber = 3)
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptNumber, ScriptName)
    VALUES (3, N'003_providers_and_external_accounts');
END
GO

/* ------------------------------------------------------------
   Resultado: catalogo de proveedores y scripts aplicados.
   ------------------------------------------------------------ */
SELECT Id, Code, DisplayName, Kind, RequiresOAuth, SupportsSearch, SupportsDownloads, SupportsOpenInApp
FROM dbo.ConnectedProviders
ORDER BY Id;

SELECT ScriptNumber, ScriptName, AppliedAtUtc, AppliedBy
FROM dbo.SchemaVersions
ORDER BY ScriptNumber;
GO
