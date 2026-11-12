/* ============================================================
   TONYX - Script 006: sincronizacion, copias de seguridad y auditoria
   Tablas: SynchronizationRecords, SyncChanges, BackupRecords, AuditLogs
   Procedimiento: usp_PurgeExpiredData (politica de retencion)
   Es repetible: crea solo lo que falta.
   Requiere haber ejecutado los scripts 001 a 005.

   Reglas generales:
   - SyncChanges.Id es el CURSOR de sincronizacion (crece sin repetirse).
   - SyncChanges guarda QUE cambio, no el contenido del cambio.
   - Las copias de seguridad se guardan en ARCHIVO; aqui solo hay metadatos.
   - AuditLogs.Details NUNCA debe contener tokens, contrasenas ni correos.
   ============================================================ */

USE TonyxDb;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ------------------------------------------------------------
   SynchronizationRecords: estado de sincronizacion de cada dispositivo.
   LastCursor = ultimo SyncChanges.Id que ese dispositivo ya recibio.
   LastStatus: 0 = Nunca, 1 = Correcta, 2 = Parcial, 3 = Fallida.
   UserId va SIN cascada (la cascada llega por UserDevices) para evitar
   el error de multiples rutas de cascada de SQL Server.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.SynchronizationRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SynchronizationRecords
    (
        Id                       UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_SynchronizationRecords_Id DEFAULT (NEWSEQUENTIALID()),
        UserId                   UNIQUEIDENTIFIER NOT NULL,
        DeviceId                 UNIQUEIDENTIFIER NOT NULL,
        LastCursor               BIGINT NOT NULL
            CONSTRAINT DF_SynchronizationRecords_LastCursor DEFAULT (0),
        LastSyncStartedAtUtc     DATETIME2(3) NULL,
        LastSyncCompletedAtUtc   DATETIME2(3) NULL,
        LastStatus               TINYINT NOT NULL
            CONSTRAINT DF_SynchronizationRecords_LastStatus DEFAULT (0),
        LastErrorMessage         NVARCHAR(500) NULL,
        ConsecutiveFailures      INT NOT NULL
            CONSTRAINT DF_SynchronizationRecords_ConsecutiveFailures DEFAULT (0),
        CreatedAtUtc             DATETIME2(3) NOT NULL
            CONSTRAINT DF_SynchronizationRecords_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc             DATETIME2(3) NOT NULL
            CONSTRAINT DF_SynchronizationRecords_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]             ROWVERSION NOT NULL,
        CONSTRAINT PK_SynchronizationRecords PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_SynchronizationRecords_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id),
        CONSTRAINT FK_SynchronizationRecords_UserDevices FOREIGN KEY (DeviceId)
            REFERENCES dbo.UserDevices (Id) ON DELETE CASCADE,
        CONSTRAINT CK_SynchronizationRecords_LastCursor CHECK (LastCursor >= 0),
        CONSTRAINT CK_SynchronizationRecords_LastStatus CHECK (LastStatus IN (0, 1, 2, 3)),
        CONSTRAINT CK_SynchronizationRecords_Failures CHECK (ConsecutiveFailures >= 0)
    );

    -- Un registro por usuario y dispositivo.
    CREATE UNIQUE NONCLUSTERED INDEX UX_SynchronizationRecords_User_Device
        ON dbo.SynchronizationRecords (UserId, DeviceId);
    CREATE NONCLUSTERED INDEX IX_SynchronizationRecords_DeviceId
        ON dbo.SynchronizationRecords (DeviceId);
END
GO

/* ------------------------------------------------------------
   SyncChanges: registro ordenado de cambios por usuario.
   EntityType: 1 = Playlist, 2 = Cancion de playlist, 3 = Favorito,
               4 = Archivo de biblioteca, 5 = Ajuste, 6 = Historial,
               7 = Descarga.
   EntityKey: el Id (como texto) o, para ajustes, la clave del ajuste.
   Operation: 1 = Crear o modificar, 2 = Borrar.
   OriginDeviceId: quien hizo el cambio (informativo, SIN clave foranea).
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.SyncChanges', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SyncChanges
    (
        Id              BIGINT IDENTITY(1,1) NOT NULL,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        EntityType      TINYINT NOT NULL,
        EntityKey       NVARCHAR(200) NOT NULL,
        Operation       TINYINT NOT NULL,
        OriginDeviceId  UNIQUEIDENTIFIER NULL,
        ChangedAtUtc    DATETIME2(3) NOT NULL
            CONSTRAINT DF_SyncChanges_ChangedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SyncChanges PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_SyncChanges_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT CK_SyncChanges_EntityType CHECK (EntityType IN (1, 2, 3, 4, 5, 6, 7)),
        CONSTRAINT CK_SyncChanges_Operation CHECK (Operation IN (1, 2)),
        CONSTRAINT CK_SyncChanges_EntityKey CHECK (LEN(LTRIM(RTRIM(EntityKey))) > 0)
    );

    -- "Dame lo que cambio despues del cursor X" para un usuario.
    CREATE NONCLUSTERED INDEX IX_SyncChanges_User_Id
        ON dbo.SyncChanges (UserId, Id)
        INCLUDE (EntityType, EntityKey, Operation, OriginDeviceId);

    -- Para la limpieza por antiguedad.
    CREATE NONCLUSTERED INDEX IX_SyncChanges_ChangedAtUtc
        ON dbo.SyncChanges (ChangedAtUtc);
END
GO

/* ------------------------------------------------------------
   BackupRecords: metadatos de las copias de seguridad del usuario.
   El contenido (JSON comprimido) va a ARCHIVO; StorageKey dice donde.
   Kind: 1 = Manual, 2 = Automatica.
   Status: 1 = Pendiente, 2 = Completada, 3 = Fallida.
   SchemaVersion: ultima version del esquema (SchemaVersions) al crearla.
   Nunca incluye archivos de audio.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.BackupRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BackupRecords
    (
        Id                  UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_BackupRecords_Id DEFAULT (NEWSEQUENTIALID()),
        UserId              UNIQUEIDENTIFIER NOT NULL,
        Name                NVARCHAR(200) NOT NULL,
        Kind                TINYINT NOT NULL
            CONSTRAINT DF_BackupRecords_Kind DEFAULT (1),
        Status              TINYINT NOT NULL
            CONSTRAINT DF_BackupRecords_Status DEFAULT (1),
        IncludesPlaylists   BIT NOT NULL
            CONSTRAINT DF_BackupRecords_IncludesPlaylists DEFAULT (1),
        IncludesFavorites   BIT NOT NULL
            CONSTRAINT DF_BackupRecords_IncludesFavorites DEFAULT (1),
        IncludesHistory     BIT NOT NULL
            CONSTRAINT DF_BackupRecords_IncludesHistory DEFAULT (1),
        IncludesSettings    BIT NOT NULL
            CONSTRAINT DF_BackupRecords_IncludesSettings DEFAULT (1),
        IncludesLibrary     BIT NOT NULL
            CONSTRAINT DF_BackupRecords_IncludesLibrary DEFAULT (1),
        FormatVersion       SMALLINT NOT NULL
            CONSTRAINT DF_BackupRecords_FormatVersion DEFAULT (1),
        SchemaVersion       INT NOT NULL,
        StorageKey          NVARCHAR(500) NULL,
        SizeBytes           BIGINT NULL,
        ContentHash         CHAR(64) NULL,
        CreatedByDeviceId   UNIQUEIDENTIFIER NULL,
        ErrorMessage        NVARCHAR(500) NULL,
        CreatedAtUtc        DATETIME2(3) NOT NULL
            CONSTRAINT DF_BackupRecords_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CompletedAtUtc      DATETIME2(3) NULL,
        ExpiresAtUtc        DATETIME2(3) NULL,
        LastRestoredAtUtc   DATETIME2(3) NULL,
        UpdatedAtUtc        DATETIME2(3) NOT NULL
            CONSTRAINT DF_BackupRecords_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        [RowVersion]        ROWVERSION NOT NULL,
        CONSTRAINT PK_BackupRecords PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_BackupRecords_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT CK_BackupRecords_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
        CONSTRAINT CK_BackupRecords_Kind CHECK (Kind IN (1, 2)),
        CONSTRAINT CK_BackupRecords_Status CHECK (Status IN (1, 2, 3)),
        CONSTRAINT CK_BackupRecords_SizeBytes CHECK (SizeBytes IS NULL OR SizeBytes >= 0),
        CONSTRAINT CK_BackupRecords_Scope CHECK (
            IncludesPlaylists = 1 OR IncludesFavorites = 1 OR IncludesHistory = 1
            OR IncludesSettings = 1 OR IncludesLibrary = 1),
        CONSTRAINT CK_BackupRecords_Completed CHECK (
            Status <> 2
            OR (StorageKey IS NOT NULL AND SizeBytes IS NOT NULL AND CompletedAtUtc IS NOT NULL))
    );

    CREATE NONCLUSTERED INDEX IX_BackupRecords_User_Created
        ON dbo.BackupRecords (UserId, CreatedAtUtc DESC);
END
GO

/* ------------------------------------------------------------
   AuditLogs: eventos de seguridad.
   Si el usuario se elimina, UserId pasa a NULL: se conserva el rastro
   sin vincularlo a una persona.
   NUNCA guardar en Details tokens, contrasenas ni correos.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        UserId        UNIQUEIDENTIFIER NULL,
        EventType     NVARCHAR(60) NOT NULL,
        Succeeded     BIT NOT NULL
            CONSTRAINT DF_AuditLogs_Succeeded DEFAULT (1),
        DeviceId      UNIQUEIDENTIFIER NULL,
        IpAddress     NVARCHAR(45) NULL,
        UserAgent     NVARCHAR(300) NULL,
        Details       NVARCHAR(1000) NULL,
        CreatedAtUtc  DATETIME2(3) NOT NULL
            CONSTRAINT DF_AuditLogs_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE SET NULL,
        CONSTRAINT CK_AuditLogs_EventType CHECK (LEN(LTRIM(RTRIM(EventType))) > 0)
    );

    CREATE NONCLUSTERED INDEX IX_AuditLogs_User_Created
        ON dbo.AuditLogs (UserId, CreatedAtUtc DESC) WHERE UserId IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_AuditLogs_EventType_Created
        ON dbo.AuditLogs (EventType, CreatedAtUtc DESC);
    -- Para la limpieza por antiguedad.
    CREATE NONCLUSTERED INDEX IX_AuditLogs_CreatedAtUtc
        ON dbo.AuditLogs (CreatedAtUtc);
END
GO

/* ------------------------------------------------------------
   usp_PurgeExpiredData: politica de retencion.
   Borra: tokens de verificacion y de recuperacion ya vencidos o usados,
   eventos de auditoria antiguos y cambios de sincronizacion antiguos.
   Un dispositivo que pase mas dias sin sincronizar que
   @SyncChangeRetentionDays hara una sincronizacion completa.
   (La limpieza de sesiones y tokens de renovacion se anade en la Fase 4.)
   Devuelve una fila con cuantas filas borro de cada tipo.
   ------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_PurgeExpiredData
    @TokenRetentionDays       INT = 7,
    @AuditLogRetentionDays    INT = 180,
    @SyncChangeRetentionDays  INT = 90
AS
BEGIN
    SET NOCOUNT ON;

    IF @TokenRetentionDays < 1 OR @AuditLogRetentionDays < 1 OR @SyncChangeRetentionDays < 1
    BEGIN
        THROW 50001, N'Los dias de retencion deben ser 1 o mas.', 1;
    END;

    DECLARE @now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @tokenCutoff DATETIME2(3) = DATEADD(DAY, -@TokenRetentionDays, @now);
    DECLARE @auditCutoff DATETIME2(3) = DATEADD(DAY, -@AuditLogRetentionDays, @now);
    DECLARE @syncCutoff  DATETIME2(3) = DATEADD(DAY, -@SyncChangeRetentionDays, @now);
    DECLARE @emailTokens INT, @resetTokens INT, @auditLogs INT, @syncChanges INT;

    DELETE FROM dbo.EmailVerificationTokens
    WHERE ExpiresAtUtc < @tokenCutoff OR UsedAtUtc < @tokenCutoff;
    SET @emailTokens = @@ROWCOUNT;

    DELETE FROM dbo.PasswordResetTokens
    WHERE ExpiresAtUtc < @tokenCutoff OR UsedAtUtc < @tokenCutoff;
    SET @resetTokens = @@ROWCOUNT;

    DELETE FROM dbo.AuditLogs WHERE CreatedAtUtc < @auditCutoff;
    SET @auditLogs = @@ROWCOUNT;

    DELETE FROM dbo.SyncChanges WHERE ChangedAtUtc < @syncCutoff;
    SET @syncChanges = @@ROWCOUNT;

    SELECT @emailTokens AS EmailVerificationTokens,
           @resetTokens AS PasswordResetTokens,
           @auditLogs   AS AuditLogs,
           @syncChanges AS SyncChanges;
END
GO

/* ------------------------------------------------------------
   Registrar el script en el versionado del esquema.
   ------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptNumber = 6)
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptNumber, ScriptName)
    VALUES (6, N'006_sync_backup_audit');
END
GO

/* ------------------------------------------------------------
   Resultado: tablas nuevas y scripts aplicados.
   ------------------------------------------------------------ */
SELECT t.name AS Tabla, COUNT(c.column_id) AS Columnas
FROM sys.tables AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
WHERE t.name IN (N'SynchronizationRecords', N'SyncChanges', N'BackupRecords', N'AuditLogs')
GROUP BY t.name
ORDER BY t.name;

SELECT COUNT(*) AS TablasEnLaBase FROM sys.tables;

SELECT ScriptNumber, ScriptName, AppliedAtUtc, AppliedBy
FROM dbo.SchemaVersions
ORDER BY ScriptNumber;
GO

/* ------------------------------------------------------------
   AUTOTEST: comprueba (1) que la limpieza borra solo lo antiguo,
   (2) que al borrar un usuario se limpian en cascada sus dispositivos,
   sesiones, tokens, sincronizacion y copias, y (3) que sus eventos de
   auditoria se conservan SIN UserId. Todo se DESHACE al final (ROLLBACK).
   ------------------------------------------------------------ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @u UNIQUEIDENTIFIER = NEWID();
DECLARE @d UNIQUEIDENTIFIER = NEWID();
DECLARE @s UNIQUEIDENTIFIER = NEWID();

INSERT INTO dbo.Users (Id, Email) VALUES (@u, N'selftest@tonyx.local');
INSERT INTO dbo.UserDevices (Id, UserId, DeviceIdentifier, DeviceName, Platform)
    VALUES (@d, @u, N'selftest-device', N'Prueba', 3);
INSERT INTO dbo.UserSessions (Id, UserId, DeviceId, ExpiresAtUtc)
    VALUES (@s, @u, @d, DATEADD(DAY, 1, SYSUTCDATETIME()));
INSERT INTO dbo.RefreshTokens (SessionId, UserId, TokenHash, ExpiresAtUtc)
    VALUES (@s, @u, REPLICATE(N'a', 64), DATEADD(DAY, 1, SYSUTCDATETIME()));
INSERT INTO dbo.SynchronizationRecords (UserId, DeviceId) VALUES (@u, @d);
INSERT INTO dbo.SyncChanges (UserId, EntityType, EntityKey, Operation)
    VALUES (@u, 1, N'selftest-new', 1);
INSERT INTO dbo.SyncChanges (UserId, EntityType, EntityKey, Operation, ChangedAtUtc)
    VALUES (@u, 1, N'selftest-old', 1, DATEADD(DAY, -400, SYSUTCDATETIME()));
INSERT INTO dbo.BackupRecords (UserId, Name, SchemaVersion)
    VALUES (@u, N'Copia de prueba', 6);
INSERT INTO dbo.AuditLogs (UserId, EventType, Succeeded)
    VALUES (@u, N'selftest', 1);
INSERT INTO dbo.AuditLogs (UserId, EventType, Succeeded, CreatedAtUtc)
    VALUES (NULL, N'selftest_old', 1, DATEADD(DAY, -400, SYSUTCDATETIME()));

-- (1) La limpieza borra lo antiguo y respeta lo reciente.
CREATE TABLE #purge
(
    EmailVerificationTokens INT, PasswordResetTokens INT, AuditLogs INT, SyncChanges INT
);
INSERT INTO #purge EXEC dbo.usp_PurgeExpiredData;

DECLARE @ok1 BIT = CASE
    WHEN NOT EXISTS (SELECT 1 FROM dbo.AuditLogs WHERE EventType = N'selftest_old')
     AND NOT EXISTS (SELECT 1 FROM dbo.SyncChanges WHERE EntityKey = N'selftest-old')
     AND EXISTS (SELECT 1 FROM dbo.AuditLogs WHERE EventType = N'selftest')
     AND EXISTS (SELECT 1 FROM dbo.SyncChanges WHERE EntityKey = N'selftest-new')
    THEN 1 ELSE 0 END;

-- (2) y (3) Borrar al usuario.
DELETE FROM dbo.Users WHERE Id = @u;

DECLARE @ok2 BIT = CASE
    WHEN NOT EXISTS (SELECT 1 FROM dbo.UserDevices WHERE UserId = @u)
     AND NOT EXISTS (SELECT 1 FROM dbo.UserSessions WHERE UserId = @u)
     AND NOT EXISTS (SELECT 1 FROM dbo.RefreshTokens WHERE UserId = @u)
     AND NOT EXISTS (SELECT 1 FROM dbo.SynchronizationRecords WHERE UserId = @u)
     AND NOT EXISTS (SELECT 1 FROM dbo.SyncChanges WHERE UserId = @u)
     AND NOT EXISTS (SELECT 1 FROM dbo.BackupRecords WHERE UserId = @u)
     AND EXISTS (SELECT 1 FROM dbo.AuditLogs WHERE EventType = N'selftest' AND UserId IS NULL)
    THEN 1 ELSE 0 END;

SELECT CASE
    WHEN @ok1 = 1 AND @ok2 = 1
        THEN N'AUTOTEST OK: limpieza, cascada y auditoria sin usuario funcionan'
    WHEN @ok1 = 0 AND @ok2 = 1
        THEN N'AUTOTEST FALLO: la limpieza no se comporto como se esperaba'
    WHEN @ok1 = 1 AND @ok2 = 0
        THEN N'AUTOTEST FALLO: el borrado del usuario dejo datos huerfanos o la auditoria no se conservo'
    ELSE N'AUTOTEST FALLO: fallaron la limpieza y el borrado del usuario'
END AS Resultado;

ROLLBACK TRANSACTION;
SET XACT_ABORT OFF;
GO
