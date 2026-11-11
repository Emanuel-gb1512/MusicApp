/* ============================================================
   TONYX - Script 004: catalogo musical
   Tablas: Genres, Artists, Albums, AlbumArtists, Tracks, TrackArtists,
           TrackGenres, TrackProviderRefs, Lyrics
   Es repetible: crea solo lo que falta y no duplica los generos iniciales.
   Requiere haber ejecutado los scripts 001, 002 y 003.
   ============================================================ */

USE TonyxDb;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ------------------------------------------------------------
   Genres: generos musicales.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Genres', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Genres
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        Name          NVARCHAR(100) NOT NULL,
        CreatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_Genres_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Genres PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Genres_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Genres_Name ON dbo.Genres (Name);
END
GO

/* Generos iniciales (solo se insertan los que faltan). */
INSERT INTO dbo.Genres (Name)
SELECT v.Name
FROM (VALUES
    (N'Pop'), (N'Rock'), (N'Hip-Hop / Rap'), (N'Electronica'), (N'Jazz'),
    (N'Clasica'), (N'Reggaeton'), (N'Salsa'), (N'R&B / Soul'), (N'Country'),
    (N'Folk'), (N'Metal'), (N'Indie'), (N'Latina'), (N'Blues')
) AS v (Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Genres AS g WHERE g.Name = v.Name);
GO

/* ------------------------------------------------------------
   Artists: artistas. Varios artistas pueden compartir nombre,
   por eso el nombre NO es unico.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Artists', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Artists
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        Name          NVARCHAR(300) NOT NULL,
        SortName      NVARCHAR(300) NULL,
        ImageUrl      NVARCHAR(500) NULL,
        CreatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_Artists_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_Artists_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]  ROWVERSION NOT NULL,
        CONSTRAINT PK_Artists PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Artists_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0)
    );

    CREATE NONCLUSTERED INDEX IX_Artists_Name ON dbo.Artists (Name);
END
GO

/* ------------------------------------------------------------
   Albums: albumes, sencillos, EPs y recopilaciones.
   AlbumType: 1 = Album, 2 = Sencillo, 3 = EP, 4 = Recopilacion.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Albums', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Albums
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        Title         NVARCHAR(300) NOT NULL,
        AlbumType     TINYINT NOT NULL
            CONSTRAINT DF_Albums_AlbumType DEFAULT (1),
        ReleaseDate   DATE NULL,
        CoverUrl      NVARCHAR(500) NULL,
        TotalTracks   SMALLINT NULL,
        CreatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_Albums_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_Albums_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]  ROWVERSION NOT NULL,
        CONSTRAINT PK_Albums PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Albums_Title CHECK (LEN(LTRIM(RTRIM(Title))) > 0),
        CONSTRAINT CK_Albums_AlbumType CHECK (AlbumType IN (1, 2, 3, 4)),
        CONSTRAINT CK_Albums_TotalTracks CHECK (TotalTracks IS NULL OR TotalTracks > 0)
    );

    CREATE NONCLUSTERED INDEX IX_Albums_Title ON dbo.Albums (Title);
END
GO

/* ------------------------------------------------------------
   AlbumArtists: quien hizo cada album (muchos a muchos).
   Position ordena a los artistas (0 = el primero).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.AlbumArtists', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AlbumArtists
    (
        AlbumId   BIGINT NOT NULL,
        ArtistId  BIGINT NOT NULL,
        Position  TINYINT NOT NULL
            CONSTRAINT DF_AlbumArtists_Position DEFAULT (0),
        CONSTRAINT PK_AlbumArtists PRIMARY KEY CLUSTERED (AlbumId, ArtistId),
        CONSTRAINT FK_AlbumArtists_Albums FOREIGN KEY (AlbumId)
            REFERENCES dbo.Albums (Id) ON DELETE CASCADE,
        CONSTRAINT FK_AlbumArtists_Artists FOREIGN KEY (ArtistId)
            REFERENCES dbo.Artists (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_AlbumArtists_ArtistId ON dbo.AlbumArtists (ArtistId);
END
GO

/* ------------------------------------------------------------
   Tracks: canciones. AlbumId puede ser NULL (archivos sueltos, locales).
   Si se borra el album, la cancion se conserva sin album.
   Isrc: codigo de grabacion (12 caracteres); NO es unico a proposito.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Tracks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tracks
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        Title         NVARCHAR(300) NOT NULL,
        AlbumId       BIGINT NULL,
        DiscNumber    SMALLINT NULL,
        TrackNumber   SMALLINT NULL,
        DurationMs    INT NULL,
        Isrc          CHAR(12) NULL,
        IsExplicit    BIT NOT NULL
            CONSTRAINT DF_Tracks_IsExplicit DEFAULT (0),
        CreatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_Tracks_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_Tracks_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]  ROWVERSION NOT NULL,
        CONSTRAINT PK_Tracks PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Tracks_Albums FOREIGN KEY (AlbumId)
            REFERENCES dbo.Albums (Id) ON DELETE SET NULL,
        CONSTRAINT CK_Tracks_Title CHECK (LEN(LTRIM(RTRIM(Title))) > 0),
        CONSTRAINT CK_Tracks_DiscNumber CHECK (DiscNumber IS NULL OR DiscNumber >= 1),
        CONSTRAINT CK_Tracks_TrackNumber CHECK (TrackNumber IS NULL OR TrackNumber >= 1),
        CONSTRAINT CK_Tracks_DurationMs CHECK (DurationMs IS NULL OR DurationMs > 0),
        CONSTRAINT CK_Tracks_Isrc CHECK (Isrc IS NULL OR LEN(Isrc) = 12)
    );

    CREATE NONCLUSTERED INDEX IX_Tracks_Title ON dbo.Tracks (Title);
    CREATE NONCLUSTERED INDEX IX_Tracks_AlbumId ON dbo.Tracks (AlbumId);
    CREATE NONCLUSTERED INDEX IX_Tracks_Isrc ON dbo.Tracks (Isrc) WHERE Isrc IS NOT NULL;
END
GO

/* ------------------------------------------------------------
   TrackArtists: quien interpreta cada cancion (muchos a muchos).
   Role: 1 = Principal, 2 = Invitado (featuring).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.TrackArtists', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TrackArtists
    (
        TrackId   BIGINT NOT NULL,
        ArtistId  BIGINT NOT NULL,
        Role      TINYINT NOT NULL
            CONSTRAINT DF_TrackArtists_Role DEFAULT (1),
        Position  TINYINT NOT NULL
            CONSTRAINT DF_TrackArtists_Position DEFAULT (0),
        CONSTRAINT PK_TrackArtists PRIMARY KEY CLUSTERED (TrackId, ArtistId),
        CONSTRAINT FK_TrackArtists_Tracks FOREIGN KEY (TrackId)
            REFERENCES dbo.Tracks (Id) ON DELETE CASCADE,
        CONSTRAINT FK_TrackArtists_Artists FOREIGN KEY (ArtistId)
            REFERENCES dbo.Artists (Id) ON DELETE CASCADE,
        CONSTRAINT CK_TrackArtists_Role CHECK (Role IN (1, 2))
    );

    CREATE NONCLUSTERED INDEX IX_TrackArtists_ArtistId ON dbo.TrackArtists (ArtistId);
END
GO

/* ------------------------------------------------------------
   TrackGenres: generos de cada cancion (muchos a muchos).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.TrackGenres', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TrackGenres
    (
        TrackId  BIGINT NOT NULL,
        GenreId  INT NOT NULL,
        CONSTRAINT PK_TrackGenres PRIMARY KEY CLUSTERED (TrackId, GenreId),
        CONSTRAINT FK_TrackGenres_Tracks FOREIGN KEY (TrackId)
            REFERENCES dbo.Tracks (Id) ON DELETE CASCADE,
        CONSTRAINT FK_TrackGenres_Genres FOREIGN KEY (GenreId)
            REFERENCES dbo.Genres (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_TrackGenres_GenreId ON dbo.TrackGenres (GenreId);
END
GO

/* ------------------------------------------------------------
   TrackProviderRefs: donde esta una cancion en cada proveedor.
   DownloadAllowed es 0 por defecto: solo se activa cuando el proveedor
   Y la licencia de ESA pista permiten la descarga.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.TrackProviderRefs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TrackProviderRefs
    (
        Id               BIGINT IDENTITY(1,1) NOT NULL,
        TrackId          BIGINT NOT NULL,
        ProviderId       SMALLINT NOT NULL,
        ExternalId       NVARCHAR(200) NOT NULL,
        ExternalUrl      NVARCHAR(500) NULL,
        DownloadAllowed  BIT NOT NULL
            CONSTRAINT DF_TrackProviderRefs_DownloadAllowed DEFAULT (0),
        LicenseInfo      NVARCHAR(200) NULL,
        CreatedAtUtc     DATETIME2(3) NOT NULL
            CONSTRAINT DF_TrackProviderRefs_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc     DATETIME2(3) NOT NULL
            CONSTRAINT DF_TrackProviderRefs_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]     ROWVERSION NOT NULL,
        CONSTRAINT PK_TrackProviderRefs PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_TrackProviderRefs_Tracks FOREIGN KEY (TrackId)
            REFERENCES dbo.Tracks (Id) ON DELETE CASCADE,
        CONSTRAINT FK_TrackProviderRefs_ConnectedProviders FOREIGN KEY (ProviderId)
            REFERENCES dbo.ConnectedProviders (Id),
        CONSTRAINT CK_TrackProviderRefs_ExternalId
            CHECK (LEN(LTRIM(RTRIM(ExternalId))) > 0)
    );

    -- Una misma pista de un proveedor no se registra dos veces.
    CREATE UNIQUE NONCLUSTERED INDEX UX_TrackProviderRefs_Provider_External
        ON dbo.TrackProviderRefs (ProviderId, ExternalId);
    CREATE NONCLUSTERED INDEX IX_TrackProviderRefs_TrackId
        ON dbo.TrackProviderRefs (TrackId);
END
GO

/* ------------------------------------------------------------
   Lyrics: letras.
   Kind: 1 = Normal, 2 = Sincronizada (LRC).
   SourceType: 1 = Importada por un usuario (PRIVADA, OwnerUserId obligatorio),
               2 = De un proveedor autorizado (ProviderId obligatorio).
   Las letras importadas por un usuario solo las ve ese usuario.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Lyrics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Lyrics
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        TrackId       BIGINT NOT NULL,
        Kind          TINYINT NOT NULL
            CONSTRAINT DF_Lyrics_Kind DEFAULT (1),
        LanguageCode  NVARCHAR(10) NULL,
        Content       NVARCHAR(MAX) NOT NULL,
        SourceType    TINYINT NOT NULL,
        OwnerUserId   UNIQUEIDENTIFIER NULL,
        ProviderId    SMALLINT NULL,
        LicenseInfo   NVARCHAR(200) NULL,
        CreatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_Lyrics_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_Lyrics_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]  ROWVERSION NOT NULL,
        CONSTRAINT PK_Lyrics PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Lyrics_Tracks FOREIGN KEY (TrackId)
            REFERENCES dbo.Tracks (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Lyrics_Users FOREIGN KEY (OwnerUserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Lyrics_ConnectedProviders FOREIGN KEY (ProviderId)
            REFERENCES dbo.ConnectedProviders (Id),
        CONSTRAINT CK_Lyrics_Kind CHECK (Kind IN (1, 2)),
        CONSTRAINT CK_Lyrics_SourceType CHECK (
            (SourceType = 1 AND OwnerUserId IS NOT NULL)
            OR (SourceType = 2 AND ProviderId IS NOT NULL)),
        CONSTRAINT CK_Lyrics_Content CHECK (LEN(Content) > 0)
    );

    CREATE NONCLUSTERED INDEX IX_Lyrics_Track_Kind ON dbo.Lyrics (TrackId, Kind);
    CREATE NONCLUSTERED INDEX IX_Lyrics_OwnerUserId
        ON dbo.Lyrics (OwnerUserId) WHERE OwnerUserId IS NOT NULL;
END
GO

/* ------------------------------------------------------------
   Registrar el script en el versionado del esquema.
   ------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptNumber = 4)
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptNumber, ScriptName)
    VALUES (4, N'004_music_catalog');
END
GO

/* ------------------------------------------------------------
   Resultado: tablas del catalogo y scripts aplicados.
   ------------------------------------------------------------ */
SELECT t.name AS Tabla, COUNT(c.column_id) AS Columnas
FROM sys.tables AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
WHERE t.name IN (N'Genres', N'Artists', N'Albums', N'AlbumArtists', N'Tracks',
                 N'TrackArtists', N'TrackGenres', N'TrackProviderRefs', N'Lyrics')
GROUP BY t.name
ORDER BY t.name;

SELECT ScriptNumber, ScriptName, AppliedAtUtc, AppliedBy
FROM dbo.SchemaVersions
ORDER BY ScriptNumber;
GO
