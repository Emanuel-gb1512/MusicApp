/* ============================================================
   TONYX - Script 001: crear la base de datos TonyxDb
   Ejecutar conectado a la instancia (se usa la base master).
   Es repetible: si ya existe, no se vuelve a crear.
   ============================================================ */

USE master;
GO

IF DB_ID(N'TonyxDb') IS NULL
BEGIN
    CREATE DATABASE TonyxDb COLLATE Latin1_General_100_CI_AI_SC;
    PRINT N'Base de datos TonyxDb creada.';
END
ELSE
BEGIN
    PRINT N'La base de datos TonyxDb ya existe. No se vuelve a crear.';
END
GO

-- Recuperacion SIMPLE: suficiente con backups completos periodicos.
ALTER DATABASE TonyxDb SET RECOVERY SIMPLE;
GO

-- Evita bloqueos entre lecturas y escrituras (solo si aun no esta activo).
IF EXISTS (SELECT 1 FROM sys.databases
           WHERE name = N'TonyxDb' AND is_read_committed_snapshot_on = 0)
BEGIN
    ALTER DATABASE TonyxDb SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
END
GO

USE TonyxDb;
GO

/* ------------------------------------------------------------
   Tabla de versionado del esquema: registra cada script aplicado.
   ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersions
    (
        ScriptNumber  INT           NOT NULL,
        ScriptName    NVARCHAR(200) NOT NULL,
        AppliedAtUtc  DATETIME2(3)  NOT NULL
            CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT (SYSUTCDATETIME()),
        AppliedBy     NVARCHAR(128) NOT NULL
            CONSTRAINT DF_SchemaVersions_AppliedBy DEFAULT (SUSER_SNAME()),
        CONSTRAINT PK_SchemaVersions PRIMARY KEY CLUSTERED (ScriptNumber)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptNumber = 1)
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptNumber, ScriptName)
    VALUES (1, N'001_create_database');
END
GO

/* ------------------------------------------------------------
   Resultado: debe mostrar la base y el script 001 registrado.
   ------------------------------------------------------------ */
SELECT name, collation_name, recovery_model_desc, is_read_committed_snapshot_on
FROM sys.databases
WHERE name = N'TonyxDb';

SELECT ScriptNumber, ScriptName, AppliedAtUtc, AppliedBy
FROM dbo.SchemaVersions
ORDER BY ScriptNumber;
GO
