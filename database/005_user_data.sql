/* ============================================================
   TONYX - Script 005: datos del usuario
   Tablas: UserSettings, UserLibraryItems, Playlists, PlaylistTracks,
           Favorites, ListeningHistory, DownloadHistory
   Es repetible: cada tabla se crea solo si no existe.
   Requiere haber ejecutado los scripts 001 a 004.

   Reglas generales:
   - Los IDs los puede generar el dispositivo (funciona sin internet).
   - IsDeleted = borrado logico, para que otros dispositivos se enteren.
   - UpdatedAtUtc lo mantiene la API (servidor), no el dispositivo.
   - Las columnas DeviceId son informativas, SIN clave foranea.
   - Las referencias al catalogo (Tracks, Albums, Artists) NO tienen
     cascada: el catalogo no se borra si alguien lo esta usando.
   ============================================================ */

USE TonyxDb;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ------------------------------------------------------------
   UserSettings: ajustes del usuario como pares clave y valor.
   El valor es texto (normalmente JSON). Cada clave se sincroniza sola.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.UserSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSettings
    (
        UserId                 UNIQUEIDENTIFIER NOT NULL,
        SettingKey             NVARCHAR(100) NOT NULL,
        SettingValue           NVARCHAR(MAX) NULL,
        LastModifiedByDeviceId UNIQUEIDENTIFIER NULL,
        CreatedAtUtc           DATETIME2(3) NOT NULL
            CONSTRAINT DF_UserSettings_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc           DATETIME2(3) NOT NULL
            CONSTRAINT DF_UserSettings_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]           ROWVERSION NOT NULL,
        CONSTRAINT PK_UserSettings PRIMARY KEY CLUSTERED (UserId, SettingKey),
        CONSTRAINT FK_UserSettings_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT CK_UserSettings_Key CHECK (LEN(LTRIM(RTRIM(SettingKey))) > 0)
    );

    CREATE NONCLUSTERED INDEX IX_UserSettings_User_Updated
        ON dbo.UserSettings (UserId, UpdatedAtUtc);
END
GO

/* ------------------------------------------------------------
   UserLibraryItems: referencias a archivos locales del usuario.
   SOLO metadatos y una huella; el audio NUNCA se sube.
   DeviceFileKey identifica el archivo dentro de ese dispositivo.
   ContentHash (SHA-256 en hexadecimal) sirve para detectar duplicados.
   TrackId enlaza opcionalmente con el catalogo (sin cascada).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.UserLibraryItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserLibraryItems
    (
        Id             UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_UserLibraryItems_Id DEFAULT (NEWSEQUENTIALID()),
        UserId         UNIQUEIDENTIFIER NOT NULL,
        DeviceId       UNIQUEIDENTIFIER NOT NULL,
        DeviceFileKey  NVARCHAR(450) NOT NULL,
        ContentHash    CHAR(64) NULL,
        Title          NVARCHAR(300) NOT NULL,
        ArtistName     NVARCHAR(300) NULL,
        AlbumName      NVARCHAR(300) NULL,
        DurationMs     INT NULL,
        FileSizeBytes  BIGINT NULL,
        Format         NVARCHAR(10) NULL,
        TrackId        BIGINT NULL,
        IsDeleted      BIT NOT NULL
            CONSTRAINT DF_UserLibraryItems_IsDeleted DEFAULT (0),
        DeletedAtUtc   DATETIME2(3) NULL,
        CreatedAtUtc   DATETIME2(3) NOT NULL
            CONSTRAINT DF_UserLibraryItems_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc   DATETIME2(3) NOT NULL
            CONSTRAINT DF_UserLibraryItems_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]   ROWVERSION NOT NULL,
        CONSTRAINT PK_UserLibraryItems PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_UserLibraryItems_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserLibraryItems_Tracks FOREIGN KEY (TrackId)
            REFERENCES dbo.Tracks (Id),
        CONSTRAINT CK_UserLibraryItems_Title CHECK (LEN(LTRIM(RTRIM(Title))) > 0),
        CONSTRAINT CK_UserLibraryItems_FileKey CHECK (LEN(LTRIM(RTRIM(DeviceFileKey))) > 0),
        CONSTRAINT CK_UserLibraryItems_DurationMs CHECK (DurationMs IS NULL OR DurationMs > 0),
        CONSTRAINT CK_UserLibraryItems_FileSize CHECK (FileSizeBytes IS NULL OR FileSizeBytes >= 0)
    );

    -- El mismo archivo de un dispositivo no se registra dos veces.
    CREATE UNIQUE NONCLUSTERED INDEX UX_UserLibraryItems_User_Device_File
        ON dbo.UserLibraryItems (UserId, DeviceId, DeviceFileKey)
        WHERE IsDeleted = 0;

    CREATE NONCLUSTERED INDEX IX_UserLibraryItems_User_Updated
        ON dbo.UserLibraryItems (UserId, UpdatedAtUtc);

    -- Para encontrar duplicados por contenido.
    CREATE NONCLUSTERED INDEX IX_UserLibraryItems_User_Hash
        ON dbo.UserLibraryItems (UserId, ContentHash)
        WHERE ContentHash IS NOT NULL AND IsDeleted = 0;

    CREATE NONCLUSTERED INDEX IX_UserLibraryItems_TrackId
        ON dbo.UserLibraryItems (TrackId) WHERE TrackId IS NOT NULL;
END
GO

/* ------------------------------------------------------------
   Playlists: playlists del usuario.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Playlists', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Playlists
    (
        Id                     UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Playlists_Id DEFAULT (NEWSEQUENTIALID()),
        UserId                 UNIQUEIDENTIFIER NOT NULL,
        Name                   NVARCHAR(200) NOT NULL,
        Description            NVARCHAR(1000) NULL,
        CoverUrl               NVARCHAR(500) NULL,
        SortOrder              INT NOT NULL
            CONSTRAINT DF_Playlists_SortOrder DEFAULT (0),
        IsDeleted              BIT NOT NULL
            CONSTRAINT DF_Playlists_IsDeleted DEFAULT (0),
        DeletedAtUtc           DATETIME2(3) NULL,
        LastModifiedByDeviceId UNIQUEIDENTIFIER NULL,
        CreatedAtUtc           DATETIME2(3) NOT NULL
            CONSTRAINT DF_Playlists_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc           DATETIME2(3) NOT NULL
            CONSTRAINT DF_Playlists_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]           ROWVERSION NOT NULL,
        CONSTRAINT PK_Playlists PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Playlists_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT CK_Playlists_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0)
    );

    CREATE NONCLUSTERED INDEX IX_Playlists_User_Updated
        ON dbo.Playlists (UserId, UpdatedAtUtc);

    CREATE NONCLUSTERED INDEX IX_Playlists_User_Active
        ON dbo.Playlists (UserId, SortOrder) WHERE IsDeleted = 0;
END
GO

/* ------------------------------------------------------------
   PlaylistTracks: canciones de una playlist.
   Cada entrada es EXACTAMENTE una de dos cosas:
     - una cancion del catalogo (TrackId), o
     - un archivo local del usuario (LibraryItemId).
   La misma cancion puede repetirse en una playlist.
   TrackId y LibraryItemId no llevan cascada (ver reglas arriba).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.PlaylistTracks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlaylistTracks
    (
        Id             UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_PlaylistTracks_Id DEFAULT (NEWSEQUENTIALID()),
        PlaylistId     UNIQUEIDENTIFIER NOT NULL,
        TrackId        BIGINT NULL,
        LibraryItemId  UNIQUEIDENTIFIER NULL,
        Position       INT NOT NULL,
        IsDeleted      BIT NOT NULL
            CONSTRAINT DF_PlaylistTracks_IsDeleted DEFAULT (0),
        DeletedAtUtc   DATETIME2(3) NULL,
        CreatedAtUtc   DATETIME2(3) NOT NULL
            CONSTRAINT DF_PlaylistTracks_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc   DATETIME2(3) NOT NULL
            CONSTRAINT DF_PlaylistTracks_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]   ROWVERSION NOT NULL,
        CONSTRAINT PK_PlaylistTracks PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PlaylistTracks_Playlists FOREIGN KEY (PlaylistId)
            REFERENCES dbo.Playlists (Id) ON DELETE CASCADE,
        CONSTRAINT FK_PlaylistTracks_Tracks FOREIGN KEY (TrackId)
            REFERENCES dbo.Tracks (Id),
        CONSTRAINT FK_PlaylistTracks_UserLibraryItems FOREIGN KEY (LibraryItemId)
            REFERENCES dbo.UserLibraryItems (Id),
        CONSTRAINT CK_PlaylistTracks_Position CHECK (Position >= 0),
        CONSTRAINT CK_PlaylistTracks_Target CHECK (
            (TrackId IS NOT NULL AND LibraryItemId IS NULL)
            OR (TrackId IS NULL AND LibraryItemId IS NOT NULL))
    );

    CREATE NONCLUSTERED INDEX IX_PlaylistTracks_Playlist_Position
        ON dbo.PlaylistTracks (PlaylistId, Position) WHERE IsDeleted = 0;

    CREATE NONCLUSTERED INDEX IX_PlaylistTracks_Playlist_Updated
        ON dbo.PlaylistTracks (PlaylistId, UpdatedAtUtc);

    CREATE NONCLUSTERED INDEX IX_PlaylistTracks_TrackId
        ON dbo.PlaylistTracks (TrackId) WHERE TrackId IS NOT NULL;

    CREATE NONCLUSTERED INDEX IX_PlaylistTracks_LibraryItemId
        ON dbo.PlaylistTracks (LibraryItemId) WHERE LibraryItemId IS NOT NULL;
END
GO

/* ------------------------------------------------------------
   Favorites: favoritos del usuario.
   ItemType: 1 = Cancion, 2 = Album, 3 = Artista, 4 = Archivo local.
   Solo una de las columnas de destino va rellena, segun el tipo.
   Un mismo elemento no es favorito dos veces (entre los no borrados).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Favorites', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Favorites
    (
        Id             UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Favorites_Id DEFAULT (NEWSEQUENTIALID()),
        UserId         UNIQUEIDENTIFIER NOT NULL,
        ItemType       TINYINT NOT NULL,
        TrackId        BIGINT NULL,
        AlbumId        BIGINT NULL,
        ArtistId       BIGINT NULL,
        LibraryItemId  UNIQUEIDENTIFIER NULL,
        IsDeleted      BIT NOT NULL
            CONSTRAINT DF_Favorites_IsDeleted DEFAULT (0),
        DeletedAtUtc   DATETIME2(3) NULL,
        CreatedAtUtc   DATETIME2(3) NOT NULL
            CONSTRAINT DF_Favorites_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc   DATETIME2(3) NOT NULL
            CONSTRAINT DF_Favorites_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]   ROWVERSION NOT NULL,
        CONSTRAINT PK_Favorites PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Favorites_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Favorites_Tracks FOREIGN KEY (TrackId)
            REFERENCES dbo.Tracks (Id),
        CONSTRAINT FK_Favorites_Albums FOREIGN KEY (AlbumId)
            REFERENCES dbo.Albums (Id),
        CONSTRAINT FK_Favorites_Artists FOREIGN KEY (ArtistId)
            REFERENCES dbo.Artists (Id),
        CONSTRAINT FK_Favorites_UserLibraryItems FOREIGN KEY (LibraryItemId)
            REFERENCES dbo.UserLibraryItems (Id),
        CONSTRAINT CK_Favorites_ItemType CHECK (ItemType IN (1, 2, 3, 4)),
        CONSTRAINT CK_Favorites_Target CHECK (
            (ItemType = 1 AND TrackId IS NOT NULL AND AlbumId IS NULL
                AND ArtistId IS NULL AND LibraryItemId IS NULL)
            OR (ItemType = 2 AND AlbumId IS NOT NULL AND TrackId IS NULL
                AND ArtistId IS NULL AND LibraryItemId IS NULL)
            OR (ItemType = 3 AND ArtistId IS NOT NULL AND TrackId IS NULL
                AND AlbumId IS NULL AND LibraryItemId IS NULL)
            OR (ItemType = 4 AND LibraryItemId IS NOT NULL AND TrackId IS NULL
                AND AlbumId IS NULL AND ArtistId IS NULL))
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Favorites_User_Track
        ON dbo.Favorites (UserId, TrackId) WHERE IsDeleted = 0 AND TrackId IS NOT NULL;
    CREATE UNIQUE NONCLUSTERED INDEX UX_Favorites_User_Album
        ON dbo.Favorites (UserId, AlbumId) WHERE IsDeleted = 0 AND AlbumId IS NOT NULL;
    CREATE UNIQUE NONCLUSTERED INDEX UX_Favorites_User_Artist
        ON dbo.Favorites (UserId, ArtistId) WHERE IsDeleted = 0 AND ArtistId IS NOT NULL;
    CREATE UNIQUE NONCLUSTERED INDEX UX_Favorites_User_LibraryItem
        ON dbo.Favorites (UserId, LibraryItemId) WHERE IsDeleted = 0 AND LibraryItemId IS NOT NULL;

    CREATE NONCLUSTERED INDEX IX_Favorites_User_Updated
        ON dbo.Favorites (UserId, UpdatedAtUtc);
END
GO

/* ------------------------------------------------------------
   ListeningHistory: reproducciones del usuario.
   El Id lo genera el dispositivo: si se reintenta el envio, no se duplica.
   Hay que indicar la cancion del catalogo o el archivo local (o ambos).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.ListeningHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ListeningHistory
    (
        Id             UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_ListeningHistory_Id DEFAULT (NEWSEQUENTIALID()),
        UserId         UNIQUEIDENTIFIER NOT NULL,
        TrackId        BIGINT NULL,
        LibraryItemId  UNIQUEIDENTIFIER NULL,
        ProviderId     SMALLINT NULL,
        DeviceId       UNIQUEIDENTIFIER NULL,
        PlayedAtUtc    DATETIME2(3) NOT NULL,
        PlayedMs       INT NOT NULL
            CONSTRAINT DF_ListeningHistory_PlayedMs DEFAULT (0),
        Completed      BIT NOT NULL
            CONSTRAINT DF_ListeningHistory_Completed DEFAULT (0),
        CreatedAtUtc   DATETIME2(3) NOT NULL
            CONSTRAINT DF_ListeningHistory_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ListeningHistory PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ListeningHistory_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT FK_ListeningHistory_Tracks FOREIGN KEY (TrackId)
            REFERENCES dbo.Tracks (Id),
        CONSTRAINT FK_ListeningHistory_UserLibraryItems FOREIGN KEY (LibraryItemId)
            REFERENCES dbo.UserLibraryItems (Id),
        CONSTRAINT FK_ListeningHistory_ConnectedProviders FOREIGN KEY (ProviderId)
            REFERENCES dbo.ConnectedProviders (Id),
        CONSTRAINT CK_ListeningHistory_PlayedMs CHECK (PlayedMs >= 0),
        CONSTRAINT CK_ListeningHistory_Target CHECK (TrackId IS NOT NULL OR LibraryItemId IS NOT NULL)
    );

    -- "Escuchado recientemente" y "mas reproducidas".
    CREATE NONCLUSTERED INDEX IX_ListeningHistory_User_PlayedAt
        ON dbo.ListeningHistory (UserId, PlayedAtUtc DESC);
    CREATE NONCLUSTERED INDEX IX_ListeningHistory_User_Track
        ON dbo.ListeningHistory (UserId, TrackId) WHERE TrackId IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_ListeningHistory_TrackId
        ON dbo.ListeningHistory (TrackId) WHERE TrackId IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_ListeningHistory_LibraryItemId
        ON dbo.ListeningHistory (LibraryItemId) WHERE LibraryItemId IS NOT NULL;
END
GO

/* ------------------------------------------------------------
   DownloadHistory: registro de descargas AUTORIZADAS.
   Status: 1 = En cola, 2 = Descargando, 3 = Completada, 4 = Fallida, 5 = Cancelada.
   La API solo crea filas para pistas con TrackProviderRefs.DownloadAllowed = 1.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.DownloadHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DownloadHistory
    (
        Id               UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_DownloadHistory_Id DEFAULT (NEWSEQUENTIALID()),
        UserId           UNIQUEIDENTIFIER NOT NULL,
        TrackId          BIGINT NOT NULL,
        ProviderId       SMALLINT NOT NULL,
        DeviceId         UNIQUEIDENTIFIER NULL,
        Status           TINYINT NOT NULL
            CONSTRAINT DF_DownloadHistory_Status DEFAULT (1),
        Quality          NVARCHAR(30) NULL,
        Format           NVARCHAR(10) NULL,
        FileSizeBytes    BIGINT NULL,
        LicenseInfo      NVARCHAR(200) NULL,
        ErrorMessage     NVARCHAR(500) NULL,
        StartedAtUtc     DATETIME2(3) NULL,
        CompletedAtUtc   DATETIME2(3) NULL,
        IsDeleted        BIT NOT NULL
            CONSTRAINT DF_DownloadHistory_IsDeleted DEFAULT (0),
        DeletedAtUtc     DATETIME2(3) NULL,
        CreatedAtUtc     DATETIME2(3) NOT NULL
            CONSTRAINT DF_DownloadHistory_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc     DATETIME2(3) NOT NULL
            CONSTRAINT DF_DownloadHistory_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]     ROWVERSION NOT NULL,
        CONSTRAINT PK_DownloadHistory PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_DownloadHistory_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT FK_DownloadHistory_Tracks FOREIGN KEY (TrackId)
            REFERENCES dbo.Tracks (Id),
        CONSTRAINT FK_DownloadHistory_ConnectedProviders FOREIGN KEY (ProviderId)
            REFERENCES dbo.ConnectedProviders (Id),
        CONSTRAINT CK_DownloadHistory_Status CHECK (Status IN (1, 2, 3, 4, 5)),
        CONSTRAINT CK_DownloadHistory_FileSize CHECK (FileSizeBytes IS NULL OR FileSizeBytes >= 0)
    );

    CREATE NONCLUSTERED INDEX IX_DownloadHistory_User_Updated
        ON dbo.DownloadHistory (UserId, UpdatedAtUtc);
    CREATE NONCLUSTERED INDEX IX_DownloadHistory_TrackId
        ON dbo.DownloadHistory (TrackId);
END
GO

/* ------------------------------------------------------------
   Registrar el script en el versionado del esquema.
   ------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptNumber = 5)
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptNumber, ScriptName)
    VALUES (5, N'005_user_data');
END
GO

/* ------------------------------------------------------------
   Resultado: tablas nuevas y scripts aplicados.
   ------------------------------------------------------------ */
SELECT t.name AS Tabla, COUNT(c.column_id) AS Columnas
FROM sys.tables AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
WHERE t.name IN (N'UserSettings', N'UserLibraryItems', N'Playlists', N'PlaylistTracks',
                 N'Favorites', N'ListeningHistory', N'DownloadHistory')
GROUP BY t.name
ORDER BY t.name;

SELECT COUNT(*) AS TablasEnLaBase FROM sys.tables;

SELECT ScriptNumber, ScriptName, AppliedAtUtc, AppliedBy
FROM dbo.SchemaVersions
ORDER BY ScriptNumber;
GO

/* ------------------------------------------------------------
   AUTOTEST: comprueba que al borrar un usuario se limpian en cascada
   sus datos. Todo se DESHACE al final (ROLLBACK): no queda nada.
   ------------------------------------------------------------ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @u  UNIQUEIDENTIFIER = NEWID();
DECLARE @li UNIQUEIDENTIFIER = NEWID();
DECLARE @pl UNIQUEIDENTIFIER = NEWID();

INSERT INTO dbo.Users (Id, Email) VALUES (@u, N'selftest@tonyx.local');
INSERT INTO dbo.UserLibraryItems (Id, UserId, DeviceId, DeviceFileKey, Title)
    VALUES (@li, @u, NEWID(), N'selftest/a.mp3', N'Prueba');
INSERT INTO dbo.Playlists (Id, UserId, Name) VALUES (@pl, @u, N'Prueba');
INSERT INTO dbo.PlaylistTracks (PlaylistId, LibraryItemId, Position) VALUES (@pl, @li, 0);
INSERT INTO dbo.Favorites (UserId, ItemType, LibraryItemId) VALUES (@u, 4, @li);
INSERT INTO dbo.ListeningHistory (UserId, LibraryItemId, PlayedAtUtc, PlayedMs)
    VALUES (@u, @li, SYSUTCDATETIME(), 1000);

DELETE FROM dbo.Users WHERE Id = @u;

SELECT CASE
    WHEN (SELECT COUNT(*) FROM dbo.UserLibraryItems WHERE UserId = @u) = 0
     AND (SELECT COUNT(*) FROM dbo.Playlists        WHERE UserId = @u) = 0
     AND (SELECT COUNT(*) FROM dbo.PlaylistTracks   WHERE PlaylistId = @pl) = 0
     AND (SELECT COUNT(*) FROM dbo.Favorites        WHERE UserId = @u) = 0
     AND (SELECT COUNT(*) FROM dbo.ListeningHistory WHERE UserId = @u) = 0
    THEN N'AUTOTEST OK: la cascada limpia todos los datos del usuario'
    ELSE N'AUTOTEST FALLO: quedaron datos huerfanos'
END AS Resultado;

ROLLBACK TRANSACTION;
SET XACT_ABORT OFF;
GO
