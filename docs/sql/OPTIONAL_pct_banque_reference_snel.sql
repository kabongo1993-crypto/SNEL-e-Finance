-- =============================================================================
-- OPTIONAL — PCT BANQUE : IdBanque = référence historique SNEL (VARCHAR)
-- Base   : BD_SNEL (map-only EF — aucune migration EF Core)
-- Idempotent. Transactionnel. Échoue si des données sont incompatibles.
--
-- Avant : pct.BANQUE.IdBanque BIGINT IDENTITY
--         pct.COMPTE.FK_Banque BIGINT
-- Après : pct.BANQUE.IdBanque VARCHAR(50) NOT NULL  (clé fournie, pas IDENTITY)
--         pct.COMPTE.FK_Banque VARCHAR(50) NOT NULL
--         FK_PCT_COMPTE_Banque conservée (NO ACTION / RESTRICT)
--
-- Aucune donnée métier n'est inventée. Les tables étaient vides au moment
-- de la conception ; si des lignes existent, elles sont converties
-- (CAST) ou le script s'arrête.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;

IF OBJECT_ID(N'pct.BANQUE', N'U') IS NULL
BEGIN
    RAISERROR(N'pct.BANQUE est introuvable. Exécutez docs/sql/OPTIONAL_pct_parametrage_comptes.sql.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'pct.COMPTE', N'U') IS NULL
BEGIN
    RAISERROR(N'pct.COMPTE est introuvable. Exécutez docs/sql/OPTIONAL_pct_parametrage_comptes.sql.', 16, 1);
    RETURN;
END;

DECLARE @banqueCount INT = (SELECT COUNT(*) FROM pct.BANQUE);
DECLARE @compteCount INT = (SELECT COUNT(*) FROM pct.COMPTE);

PRINT N'=== RAPPORT PRÉ-CONVERSION ===';
PRINT N'pct.BANQUE : ' + CAST(@banqueCount AS nvarchar(20)) + N' ligne(s).';
PRINT N'pct.COMPTE : ' + CAST(@compteCount AS nvarchar(20)) + N' ligne(s).';

DECLARE @idType SYSNAME;
DECLARE @idMaxLen INT;
DECLARE @idIdentity BIT;
SELECT
    @idType = ty.name,
    @idMaxLen = c.max_length,
    @idIdentity = c.is_identity
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.object_id = OBJECT_ID(N'pct.BANQUE')
  AND c.name = N'IdBanque';

IF @idType IS NULL
BEGIN
    RAISERROR(N'Colonne pct.BANQUE.IdBanque introuvable.', 16, 1);
    RETURN;
END;

DECLARE @fkType SYSNAME;
DECLARE @fkMaxLen INT;
SELECT
    @fkType = ty.name,
    @fkMaxLen = c.max_length
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.object_id = OBJECT_ID(N'pct.COMPTE')
  AND c.name = N'FK_Banque';

IF @fkType IS NULL
BEGIN
    RAISERROR(N'Colonne pct.COMPTE.FK_Banque introuvable.', 16, 1);
    RETURN;
END;

PRINT N'BANQUE.IdBanque actuel : ' + @idType
    + N'(max_length=' + CAST(@idMaxLen AS nvarchar(10)) + N', identity='
    + CAST(@idIdentity AS nvarchar(1)) + N').';
PRINT N'COMPTE.FK_Banque actuel : ' + @fkType
    + N'(max_length=' + CAST(@fkMaxLen AS nvarchar(10)) + N').';

DECLARE @banqueDejaVarchar BIT =
    CASE WHEN @idType = N'varchar' AND @idMaxLen = 50 AND @idIdentity = 0 THEN 1 ELSE 0 END;
DECLARE @compteDejaVarchar BIT =
    CASE WHEN @fkType = N'varchar' AND @fkMaxLen = 50 THEN 1 ELSE 0 END;

IF @banqueDejaVarchar = 1 AND @compteDejaVarchar = 1
BEGIN
    IF OBJECT_ID(N'pct.BANQUE', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_BANQUE_IdBanque')
    BEGIN
        ALTER TABLE pct.BANQUE
            ADD CONSTRAINT CK_PCT_BANQUE_IdBanque
            CHECK (LEN(LTRIM(RTRIM(IdBanque))) > 0);
        PRINT N'CK_PCT_BANQUE_IdBanque ajoutée.';
    END;

    PRINT N'Conversion déjà appliquée — rien à faire.';
    RETURN;
END;

BEGIN TRAN;

-- ---------------------------------------------------------------------------
-- 1) Retirer la FK COMPTE → BANQUE
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_Banque')
BEGIN
    ALTER TABLE pct.COMPTE DROP CONSTRAINT FK_PCT_COMPTE_Banque;
    PRINT N'FK_PCT_COMPTE_Banque supprimée (recréation après conversion).';
END;

-- Index unique (FK_Banque, NumeroCompte) : à recréer après ALTER COLUMN
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PCT_COMPTE_Banque_NumeroCompte'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    DROP INDEX UX_PCT_COMPTE_Banque_NumeroCompte ON pct.COMPTE;
    PRINT N'UX_PCT_COMPTE_Banque_NumeroCompte supprimé (recréation après conversion).';
END;

-- ---------------------------------------------------------------------------
-- 2) Convertir BANQUE.IdBanque
-- ---------------------------------------------------------------------------
IF @banqueDejaVarchar = 0
BEGIN
    IF @idType NOT IN (N'bigint', N'int', N'smallint', N'tinyint', N'varchar', N'nvarchar')
    BEGIN
        ROLLBACK TRAN;
        RAISERROR(N'Conversion refusée : type actuel de BANQUE.IdBanque non géré.', 16, 1);
        RETURN;
    END;

    -- Lots dynamiques : SQL Server compile le batch contre le schéma initial.
    IF COL_LENGTH(N'pct.BANQUE', N'IdBanqueTxt') IS NOT NULL
        EXEC(N'ALTER TABLE pct.BANQUE DROP COLUMN IdBanqueTxt;');

    EXEC(N'ALTER TABLE pct.BANQUE ADD IdBanqueTxt VARCHAR(50) NULL;');

    IF @idType IN (N'varchar', N'nvarchar')
        EXEC(N'UPDATE pct.BANQUE SET IdBanqueTxt = CONVERT(VARCHAR(50), LTRIM(RTRIM(IdBanque)));');
    ELSE
        EXEC(N'UPDATE pct.BANQUE SET IdBanqueTxt = CONVERT(VARCHAR(50), IdBanque);');

    EXEC(N'
        IF EXISTS (
            SELECT 1 FROM pct.BANQUE
            WHERE IdBanqueTxt IS NULL OR LEN(LTRIM(RTRIM(IdBanqueTxt))) = 0
        )
            THROW 50001, N''Conversion refusee : IdBanque vide ou non convertible (VARCHAR(50)). Donnees conservees.'', 1;
        IF EXISTS (
            SELECT IdBanqueTxt FROM pct.BANQUE GROUP BY IdBanqueTxt HAVING COUNT(*) > 1
        )
            THROW 50002, N''Conversion refusee : doublons d IdBanque apres conversion. Donnees conservees.'', 1;
    ');

    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_PCT_BANQUE')
        EXEC(N'ALTER TABLE pct.BANQUE DROP CONSTRAINT PK_PCT_BANQUE;');

    EXEC(N'ALTER TABLE pct.BANQUE DROP COLUMN IdBanque;');
    EXEC(N'EXEC sys.sp_rename N''pct.BANQUE.IdBanqueTxt'', N''IdBanque'', N''COLUMN'';');
    EXEC(N'ALTER TABLE pct.BANQUE ALTER COLUMN IdBanque VARCHAR(50) NOT NULL;');
    EXEC(N'ALTER TABLE pct.BANQUE ADD CONSTRAINT PK_PCT_BANQUE PRIMARY KEY (IdBanque);');
    PRINT N'BANQUE.IdBanque converti en VARCHAR(50) (sans IDENTITY).';
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_BANQUE_IdBanque')
BEGIN
    ALTER TABLE pct.BANQUE
        ADD CONSTRAINT CK_PCT_BANQUE_IdBanque
        CHECK (LEN(LTRIM(RTRIM(IdBanque))) > 0);
END;

-- ---------------------------------------------------------------------------
-- 3) Convertir COMPTE.FK_Banque
-- ---------------------------------------------------------------------------
IF @compteDejaVarchar = 0
BEGIN
    IF @fkType NOT IN (N'bigint', N'int', N'smallint', N'tinyint', N'varchar', N'nvarchar')
    BEGIN
        ROLLBACK TRAN;
        RAISERROR(N'Conversion refusée : type actuel de COMPTE.FK_Banque non géré.', 16, 1);
        RETURN;
    END;

    ALTER TABLE pct.COMPTE ALTER COLUMN FK_Banque VARCHAR(50) NOT NULL;

    IF EXISTS (
        SELECT 1 FROM pct.COMPTE
        WHERE FK_Banque IS NULL OR LEN(LTRIM(RTRIM(FK_Banque))) = 0
    )
    BEGIN
        ROLLBACK TRAN;
        RAISERROR(N'Conversion refusée : COMPTE.FK_Banque vide après conversion. Aucune donnée n''a été perdue.', 16, 1);
        RETURN;
    END;

    IF EXISTS (
        SELECT 1
        FROM pct.COMPTE c
        WHERE NOT EXISTS (
            SELECT 1 FROM pct.BANQUE b WHERE b.IdBanque = c.FK_Banque
        )
    )
    BEGIN
        ROLLBACK TRAN;
        RAISERROR(N'Conversion refusée : COMPTE.FK_Banque orphelin (aucune BANQUE correspondante). Aucune donnée n''a été perdue.', 16, 1);
        RETURN;
    END;

    PRINT N'COMPTE.FK_Banque converti en VARCHAR(50).';
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PCT_COMPTE_Banque_NumeroCompte'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    CREATE UNIQUE INDEX UX_PCT_COMPTE_Banque_NumeroCompte
        ON pct.COMPTE (FK_Banque, NumeroCompte);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_Banque')
BEGIN
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT FK_PCT_COMPTE_Banque
        FOREIGN KEY (FK_Banque) REFERENCES pct.BANQUE (IdBanque);
    PRINT N'FK_PCT_COMPTE_Banque recréée (NO ACTION).';
END;

COMMIT TRAN;

PRINT N'=== CONVERSION TERMINÉE ===';
SELECT c.name, ty.name AS typ, c.max_length, c.is_identity
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE (c.object_id = OBJECT_ID(N'pct.BANQUE') AND c.name = N'IdBanque')
   OR (c.object_id = OBJECT_ID(N'pct.COMPTE') AND c.name = N'FK_Banque')
ORDER BY c.object_id, c.column_id;
