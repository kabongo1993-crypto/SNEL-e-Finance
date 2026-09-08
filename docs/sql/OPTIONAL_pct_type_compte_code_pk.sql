-- =============================================================================
-- OPTIONAL — PCT TYPE_COMPTE : Code = identifiant métier (PK)
-- Base   : BD_SNEL (map-only EF — aucune migration EF Core)
-- Idempotent. Transactionnel. Échoue si des données sont incompatibles.
--
-- Avant : pct.TYPE_COMPTE.IdTypeCompte BIGINT IDENTITY  (PK)
--         pct.TYPE_COMPTE.Code VARCHAR(20) UNIQUE
--         pct.COMPTE.FK_TypeCompte BIGINT → TYPE_COMPTE.IdTypeCompte
-- Après : pct.TYPE_COMPTE.Code VARCHAR(20) NOT NULL  (PK, fourni par l'utilisateur)
--         plus d'IdTypeCompte
--         pct.COMPTE.FK_TypeCompte VARCHAR(20) → TYPE_COMPTE.Code
--         FK_PCT_COMPTE_TypeCompte NO ACTION / NO ACTION (pas de CASCADE)
--
-- Les lignes existantes (FCT, CAISSE, TESTTYPE, …) sont conservées.
-- Aucun re-seed. Aucun ON UPDATE CASCADE.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;

IF OBJECT_ID(N'pct.TYPE_COMPTE', N'U') IS NULL
BEGIN
    RAISERROR(N'pct.TYPE_COMPTE est introuvable. Exécutez docs/sql/OPTIONAL_pct_parametrage_comptes.sql.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'pct.COMPTE', N'U') IS NULL
BEGIN
    RAISERROR(N'pct.COMPTE est introuvable. Exécutez docs/sql/OPTIONAL_pct_parametrage_comptes.sql.', 16, 1);
    RETURN;
END;

DECLARE @typeCount INT = (SELECT COUNT(*) FROM pct.TYPE_COMPTE);
DECLARE @compteCount INT = (SELECT COUNT(*) FROM pct.COMPTE);

PRINT N'=== RAPPORT PRÉ-CONVERSION ===';
PRINT N'pct.TYPE_COMPTE : ' + CAST(@typeCount AS nvarchar(20)) + N' ligne(s).';
PRINT N'pct.COMPTE      : ' + CAST(@compteCount AS nvarchar(20)) + N' ligne(s).';

DECLARE @hasIdTypeCompte BIT =
    CASE WHEN COL_LENGTH(N'pct.TYPE_COMPTE', N'IdTypeCompte') IS NULL THEN 0 ELSE 1 END;

DECLARE @codeType SYSNAME;
DECLARE @codeMaxLen INT;
SELECT
    @codeType = ty.name,
    @codeMaxLen = c.max_length
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.object_id = OBJECT_ID(N'pct.TYPE_COMPTE')
  AND c.name = N'Code';

IF @codeType IS NULL
BEGIN
    RAISERROR(N'Colonne pct.TYPE_COMPTE.Code introuvable.', 16, 1);
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
  AND c.name = N'FK_TypeCompte';

IF @fkType IS NULL
BEGIN
    RAISERROR(N'Colonne pct.COMPTE.FK_TypeCompte introuvable.', 16, 1);
    RETURN;
END;

DECLARE @pkCol SYSNAME;
SELECT @pkCol = COL_NAME(ic.object_id, ic.column_id)
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic
    ON ic.object_id = kc.parent_object_id
   AND ic.index_id = kc.unique_index_id
WHERE kc.parent_object_id = OBJECT_ID(N'pct.TYPE_COMPTE')
  AND kc.type = N'PK'
  AND ic.key_ordinal = 1;

PRINT N'TYPE_COMPTE.IdTypeCompte présent : ' + CASE WHEN @hasIdTypeCompte = 1 THEN N'oui' ELSE N'non' END + N'.';
PRINT N'TYPE_COMPTE.Code actuel : ' + @codeType
    + N'(max_length=' + CAST(@codeMaxLen AS nvarchar(10)) + N').';
PRINT N'TYPE_COMPTE PK colonne : ' + ISNULL(@pkCol, N'(aucune)');
PRINT N'COMPTE.FK_TypeCompte actuel : ' + @fkType
    + N'(max_length=' + CAST(@fkMaxLen AS nvarchar(10)) + N').';

DECLARE @typeDejaConverti BIT =
    CASE WHEN @hasIdTypeCompte = 0 AND @pkCol = N'Code' THEN 1 ELSE 0 END;
DECLARE @compteDejaVarchar BIT =
    CASE WHEN @fkType = N'varchar' AND @fkMaxLen = 20 THEN 1 ELSE 0 END;

IF @typeDejaConverti = 1 AND @compteDejaVarchar = 1
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_PCT_TYPE_COMPTE_Code'
          AND object_id = OBJECT_ID(N'pct.TYPE_COMPTE')
    )
        DROP INDEX UX_PCT_TYPE_COMPTE_Code ON pct.TYPE_COMPTE;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_TypeCompte')
    BEGIN
        ALTER TABLE pct.COMPTE
            ADD CONSTRAINT FK_PCT_COMPTE_TypeCompte
            FOREIGN KEY (FK_TypeCompte) REFERENCES pct.TYPE_COMPTE (Code)
            ON DELETE NO ACTION
            ON UPDATE NO ACTION;
        PRINT N'FK_PCT_COMPTE_TypeCompte recréée (NO ACTION).';
    END;

    PRINT N'Conversion déjà appliquée — rien à faire.';
    RETURN;
END;

BEGIN TRAN;

-- ---------------------------------------------------------------------------
-- 1) Retirer la FK COMPTE → TYPE_COMPTE et l'index associé
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_TypeCompte')
BEGIN
    ALTER TABLE pct.COMPTE DROP CONSTRAINT FK_PCT_COMPTE_TypeCompte;
    PRINT N'FK_PCT_COMPTE_TypeCompte supprimée (recréation après conversion).';
END;

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_FK_TypeCompte'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    DROP INDEX IX_PCT_COMPTE_FK_TypeCompte ON pct.COMPTE;
    PRINT N'IX_PCT_COMPTE_FK_TypeCompte supprimé (recréation après conversion).';
END;

-- ---------------------------------------------------------------------------
-- 2) Convertir COMPTE.FK_TypeCompte (mapper les IDs numériques vers Code
--    TANT QUE IdTypeCompte existe encore).
-- ---------------------------------------------------------------------------
IF @compteDejaVarchar = 0
BEGIN
    IF @fkType NOT IN (N'bigint', N'int', N'smallint', N'tinyint', N'varchar', N'nvarchar')
    BEGIN
        ROLLBACK TRAN;
        RAISERROR(N'Conversion refusée : type actuel de COMPTE.FK_TypeCompte non géré.', 16, 1);
        RETURN;
    END;

    IF @fkType IN (N'bigint', N'int', N'smallint', N'tinyint')
    BEGIN
        IF @hasIdTypeCompte = 0 AND @compteCount > 0
        BEGIN
            ROLLBACK TRAN;
            RAISERROR(N'Conversion refusée : COMPTE.FK_TypeCompte est encore numérique mais IdTypeCompte a déjà disparu. Données conservées.', 16, 1);
            RETURN;
        END;

        IF @compteCount > 0 AND @hasIdTypeCompte = 1
        BEGIN
            IF COL_LENGTH(N'pct.COMPTE', N'FK_TypeCompteCode') IS NOT NULL
                EXEC(N'ALTER TABLE pct.COMPTE DROP COLUMN FK_TypeCompteCode;');

            EXEC(N'ALTER TABLE pct.COMPTE ADD FK_TypeCompteCode VARCHAR(20) NULL;');
            EXEC(N'
                UPDATE C
                SET C.FK_TypeCompteCode = T.Code
                FROM pct.COMPTE C
                INNER JOIN pct.TYPE_COMPTE T
                    ON T.IdTypeCompte = C.FK_TypeCompte;
            ');
            EXEC(N'
                IF EXISTS (
                    SELECT 1 FROM pct.COMPTE
                    WHERE FK_TypeCompteCode IS NULL OR LEN(LTRIM(RTRIM(FK_TypeCompteCode))) = 0
                )
                    THROW 50011, N''Conversion refusee : COMPTE.FK_TypeCompte orphelin ou non convertible. Donnees conservees.'', 1;
            ');
            EXEC(N'ALTER TABLE pct.COMPTE DROP COLUMN FK_TypeCompte;');
            EXEC(N'EXEC sys.sp_rename N''pct.COMPTE.FK_TypeCompteCode'', N''FK_TypeCompte'', N''COLUMN'';');
            EXEC(N'ALTER TABLE pct.COMPTE ALTER COLUMN FK_TypeCompte VARCHAR(20) NOT NULL;');
            PRINT N'COMPTE.FK_TypeCompte converti en VARCHAR(20) (IDs numériques → Code).';
        END
        ELSE
        BEGIN
            EXEC(N'ALTER TABLE pct.COMPTE ALTER COLUMN FK_TypeCompte VARCHAR(20) NOT NULL;');
            PRINT N'COMPTE.FK_TypeCompte converti en VARCHAR(20) (table vide ou sans mapping).';
        END;
    END
    ELSE
    BEGIN
        EXEC(N'ALTER TABLE pct.COMPTE ALTER COLUMN FK_TypeCompte VARCHAR(20) NOT NULL;');
        PRINT N'COMPTE.FK_TypeCompte ajusté en VARCHAR(20).';
    END;
END;

-- ---------------------------------------------------------------------------
-- 3) TYPE_COMPTE : supprimer IdTypeCompte, PK = Code
-- ---------------------------------------------------------------------------
IF @hasIdTypeCompte = 1
BEGIN
    EXEC(N'
        IF EXISTS (
            SELECT 1 FROM pct.TYPE_COMPTE
            WHERE Code IS NULL OR LEN(LTRIM(RTRIM(Code))) = 0
        )
            THROW 50012, N''Conversion refusee : TYPE_COMPTE.Code vide. Donnees conservees.'', 1;
        IF EXISTS (
            SELECT Code FROM pct.TYPE_COMPTE GROUP BY Code HAVING COUNT(*) > 1
        )
            THROW 50013, N''Conversion refusee : doublons de TYPE_COMPTE.Code. Donnees conservees.'', 1;
    ');

    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_PCT_TYPE_COMPTE_Code'
          AND object_id = OBJECT_ID(N'pct.TYPE_COMPTE')
    )
        DROP INDEX UX_PCT_TYPE_COMPTE_Code ON pct.TYPE_COMPTE;

    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_PCT_TYPE_COMPTE')
        ALTER TABLE pct.TYPE_COMPTE DROP CONSTRAINT PK_PCT_TYPE_COMPTE;

    ALTER TABLE pct.TYPE_COMPTE DROP COLUMN IdTypeCompte;

    ALTER TABLE pct.TYPE_COMPTE
        ADD CONSTRAINT PK_PCT_TYPE_COMPTE PRIMARY KEY (Code);

    PRINT N'TYPE_COMPTE.IdTypeCompte supprimé. PK = Code.';
END
ELSE IF @pkCol IS NULL OR @pkCol <> N'Code'
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_PCT_TYPE_COMPTE_Code'
          AND object_id = OBJECT_ID(N'pct.TYPE_COMPTE')
    )
        DROP INDEX UX_PCT_TYPE_COMPTE_Code ON pct.TYPE_COMPTE;

    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_PCT_TYPE_COMPTE')
        ALTER TABLE pct.TYPE_COMPTE DROP CONSTRAINT PK_PCT_TYPE_COMPTE;

    ALTER TABLE pct.TYPE_COMPTE
        ADD CONSTRAINT PK_PCT_TYPE_COMPTE PRIMARY KEY (Code);
    PRINT N'PK_PCT_TYPE_COMPTE définie sur Code.';
END;

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PCT_TYPE_COMPTE_Code'
      AND object_id = OBJECT_ID(N'pct.TYPE_COMPTE')
)
    DROP INDEX UX_PCT_TYPE_COMPTE_Code ON pct.TYPE_COMPTE;

-- ---------------------------------------------------------------------------
-- 4) Recréer index + FK vers TYPE_COMPTE.Code (NO ACTION)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_FK_TypeCompte'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
    CREATE INDEX IX_PCT_COMPTE_FK_TypeCompte
        ON pct.COMPTE (FK_TypeCompte);

IF EXISTS (
    SELECT 1
    FROM pct.COMPTE c
    WHERE NOT EXISTS (
        SELECT 1 FROM pct.TYPE_COMPTE t WHERE t.Code = c.FK_TypeCompte
    )
)
BEGIN
    ROLLBACK TRAN;
    RAISERROR(N'Conversion refusée : COMPTE.FK_TypeCompte orphelin après conversion. Données conservées.', 16, 1);
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_TypeCompte')
BEGIN
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT FK_PCT_COMPTE_TypeCompte
        FOREIGN KEY (FK_TypeCompte) REFERENCES pct.TYPE_COMPTE (Code)
        ON DELETE NO ACTION
        ON UPDATE NO ACTION;
    PRINT N'FK_PCT_COMPTE_TypeCompte recréée vers TYPE_COMPTE.Code (NO ACTION).';
END;

COMMIT TRAN;

PRINT N'=== CONVERSION TERMINÉE ===';
SELECT
    c.name,
    ty.name AS typ,
    c.max_length,
    c.is_identity
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE (c.object_id = OBJECT_ID(N'pct.TYPE_COMPTE') AND c.name IN (N'Code', N'IdTypeCompte'))
   OR (c.object_id = OBJECT_ID(N'pct.COMPTE') AND c.name = N'FK_TypeCompte')
ORDER BY c.object_id, c.column_id;

SELECT Code, Libelle, FK_GroupeTypeCompte, Actif
FROM pct.TYPE_COMPTE
ORDER BY Code;

SELECT IdCompte, NumeroCompte, FK_TypeCompte
FROM pct.COMPTE;
