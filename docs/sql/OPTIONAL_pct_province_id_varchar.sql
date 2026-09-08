-- =============================================================================
-- OPTIONAL — PCT PROVINCE : IdProvince = identifiant métier VARCHAR(20) PK
-- Base   : BD_SNEL (map-only EF — aucune migration EF Core)
-- Idempotent. Transactionnel. Échoue si des IDs numériques existent
--          (aucune correspondance KIN/KIND/KISA n'est inventée).
--
-- Avant : pct.PROVINCE.IdProvince BIGINT IDENTITY  (PK)
--         pct.COMPTE.FK_Province BIGINT NULL → PROVINCE.IdProvince
-- Après : pct.PROVINCE.IdProvince VARCHAR(20) NOT NULL  (PK, fourni par l'utilisateur)
--         pct.COMPTE.FK_Province VARCHAR(20) NULL → PROVINCE.IdProvince
--         FK_PCT_COMPTE_Province NO ACTION / NO ACTION (pas de CASCADE)
--
-- Tables dépendantes identifiées : pct.COMPTE uniquement.
-- Aucun seed. Aucune troncature. Aucun ON UPDATE CASCADE.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;

IF OBJECT_ID(N'pct.PROVINCE', N'U') IS NULL
BEGIN
    RAISERROR(N'pct.PROVINCE est introuvable. Exécutez docs/sql/OPTIONAL_pct_parametrage_comptes.sql.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'pct.COMPTE', N'U') IS NULL
BEGIN
    RAISERROR(N'pct.COMPTE est introuvable. Exécutez docs/sql/OPTIONAL_pct_parametrage_comptes.sql.', 16, 1);
    RETURN;
END;

DECLARE @provinceCount INT = (SELECT COUNT(*) FROM pct.PROVINCE);
DECLARE @compteCount INT = (SELECT COUNT(*) FROM pct.COMPTE);
DECLARE @compteFkNonNull INT = (
    SELECT COUNT(*) FROM pct.COMPTE WHERE FK_Province IS NOT NULL
);

PRINT N'=== RAPPORT PRÉ-CONVERSION ===';
PRINT N'pct.PROVINCE              : ' + CAST(@provinceCount AS nvarchar(20)) + N' ligne(s).';
PRINT N'pct.COMPTE                : ' + CAST(@compteCount AS nvarchar(20)) + N' ligne(s).';
PRINT N'pct.COMPTE.FK_Province <> NULL : ' + CAST(@compteFkNonNull AS nvarchar(20)) + N' ligne(s).';

DECLARE @idType SYSNAME;
DECLARE @idMaxLen INT;
DECLARE @idIdentity BIT;
SELECT
    @idType = ty.name,
    @idMaxLen = CASE WHEN ty.name IN (N'varchar', N'nvarchar', N'char', N'nchar')
                     THEN c.max_length ELSE NULL END,
    @idIdentity = c.is_identity
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.object_id = OBJECT_ID(N'pct.PROVINCE')
  AND c.name = N'IdProvince';

IF @idType IS NULL
BEGIN
    RAISERROR(N'Colonne pct.PROVINCE.IdProvince introuvable.', 16, 1);
    RETURN;
END;

DECLARE @fkType SYSNAME;
DECLARE @fkMaxLen INT;
DECLARE @fkNullable BIT;
SELECT
    @fkType = ty.name,
    @fkMaxLen = CASE WHEN ty.name IN (N'varchar', N'nvarchar', N'char', N'nchar')
                     THEN c.max_length ELSE NULL END,
    @fkNullable = c.is_nullable
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.object_id = OBJECT_ID(N'pct.COMPTE')
  AND c.name = N'FK_Province';

IF @fkType IS NULL
BEGIN
    RAISERROR(N'Colonne pct.COMPTE.FK_Province introuvable.', 16, 1);
    RETURN;
END;

DECLARE @pkCol SYSNAME;
SELECT @pkCol = COL_NAME(ic.object_id, ic.column_id)
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic
    ON ic.object_id = kc.parent_object_id
   AND ic.index_id = kc.unique_index_id
WHERE kc.parent_object_id = OBJECT_ID(N'pct.PROVINCE')
  AND kc.type = N'PK'
  AND ic.key_ordinal = 1;

PRINT N'PROVINCE.IdProvince actuel : ' + @idType
    + N'(max_length=' + ISNULL(CAST(@idMaxLen AS nvarchar(10)), N'n/a')
    + N', identity=' + CASE WHEN @idIdentity = 1 THEN N'oui' ELSE N'non' END + N').';
PRINT N'PROVINCE PK colonne : ' + ISNULL(@pkCol, N'(aucune)');
PRINT N'COMPTE.FK_Province actuel : ' + @fkType
    + N'(max_length=' + ISNULL(CAST(@fkMaxLen AS nvarchar(10)), N'n/a')
    + N', nullable=' + CASE WHEN @fkNullable = 1 THEN N'oui' ELSE N'non' END + N').';

DECLARE @provinceDejaVarchar BIT =
    CASE WHEN @idType = N'varchar' AND @idMaxLen = 20 AND @idIdentity = 0 AND @pkCol = N'IdProvince'
         THEN 1 ELSE 0 END;
DECLARE @compteDejaVarchar BIT =
    CASE WHEN @fkType = N'varchar' AND @fkMaxLen = 20 AND @fkNullable = 1 THEN 1 ELSE 0 END;

IF @idType IN (N'varchar', N'nvarchar') AND @idMaxLen IS NOT NULL AND @idMaxLen > 20
BEGIN
    IF EXISTS (
        SELECT 1 FROM pct.PROVINCE
        WHERE LEN(IdProvince) > 20
    )
    BEGIN
        RAISERROR(N'Conversion refusée : des IdProvince dépassent 20 caractères. Aucune troncature. Données conservées.', 16, 1);
        RETURN;
    END;
END;

IF @provinceDejaVarchar = 1 AND @compteDejaVarchar = 1
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_PROVINCE_IdProvince')
        ALTER TABLE pct.PROVINCE
            ADD CONSTRAINT CK_PCT_PROVINCE_IdProvince
            CHECK (LEN(LTRIM(RTRIM(IdProvince))) > 0);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_PCT_COMPTE_FK_Province'
          AND object_id = OBJECT_ID(N'pct.COMPTE')
    )
        CREATE INDEX IX_PCT_COMPTE_FK_Province ON pct.COMPTE (FK_Province);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_Province')
    BEGIN
        ALTER TABLE pct.COMPTE
            ADD CONSTRAINT FK_PCT_COMPTE_Province
            FOREIGN KEY (FK_Province) REFERENCES pct.PROVINCE (IdProvince)
            ON DELETE NO ACTION
            ON UPDATE NO ACTION;
        PRINT N'FK_PCT_COMPTE_Province recréée (NO ACTION).';
    END;

    PRINT N'Conversion déjà appliquée — rien à faire.';
    RETURN;
END;

-- Données numériques : ne pas inventer KIN/KIND/KISA.
IF @idType IN (N'bigint', N'int', N'smallint', N'tinyint') AND @provinceCount > 0
BEGIN
    PRINT N'=== IdProvince numériques existants (aucune correspondance métier) ===';
    SELECT IdProvince, Libelle, Actif FROM pct.PROVINCE ORDER BY IdProvince;
    RAISERROR(N'Conversion refusée : pct.PROVINCE contient des identifiants numériques. Aucune correspondance KIN/KIND/KISA n''est inventée. Données conservées.', 16, 1);
    RETURN;
END;

IF @fkType IN (N'bigint', N'int', N'smallint', N'tinyint') AND @compteFkNonNull > 0
BEGIN
    PRINT N'=== COMPTE.FK_Province numériques existants ===';
    SELECT IdCompte, NumeroCompte, FK_Province FROM pct.COMPTE WHERE FK_Province IS NOT NULL ORDER BY IdCompte;
    RAISERROR(N'Conversion refusée : pct.COMPTE.FK_Province contient des IDs numériques. Aucune correspondance métier n''est inventée. Données conservées.', 16, 1);
    RETURN;
END;

BEGIN TRAN;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_Province')
BEGIN
    ALTER TABLE pct.COMPTE DROP CONSTRAINT FK_PCT_COMPTE_Province;
    PRINT N'FK_PCT_COMPTE_Province supprimée (recréation après conversion).';
END;

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_FK_Province'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    DROP INDEX IX_PCT_COMPTE_FK_Province ON pct.COMPTE;
    PRINT N'IX_PCT_COMPTE_FK_Province supprimé (recréation après conversion).';
END;

-- COMPTE.FK_Province → VARCHAR(20) NULL
IF @compteDejaVarchar = 0
BEGIN
    IF @fkType NOT IN (N'bigint', N'int', N'smallint', N'tinyint', N'varchar', N'nvarchar')
    BEGIN
        ROLLBACK TRAN;
        RAISERROR(N'Conversion refusée : type actuel de COMPTE.FK_Province non géré.', 16, 1);
        RETURN;
    END;

    EXEC(N'ALTER TABLE pct.COMPTE ALTER COLUMN FK_Province VARCHAR(20) NULL;');
    PRINT N'COMPTE.FK_Province converti en VARCHAR(20) NULL.';
END;

-- PROVINCE.IdProvince → VARCHAR(20) PK, plus d'IDENTITY
IF @provinceDejaVarchar = 0
BEGIN
    IF @idType NOT IN (N'bigint', N'int', N'smallint', N'tinyint', N'varchar', N'nvarchar')
    BEGIN
        ROLLBACK TRAN;
        RAISERROR(N'Conversion refusée : type actuel de PROVINCE.IdProvince non géré.', 16, 1);
        RETURN;
    END;

    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_PCT_PROVINCE')
        ALTER TABLE pct.PROVINCE DROP CONSTRAINT PK_PCT_PROVINCE;

    IF @idType IN (N'bigint', N'int', N'smallint', N'tinyint')
    BEGIN
        ALTER TABLE pct.PROVINCE DROP COLUMN IdProvince;
        ALTER TABLE pct.PROVINCE ADD IdProvince VARCHAR(20) NOT NULL;
        PRINT N'PROVINCE.IdProvince recréé en VARCHAR(20) (table vide, IDENTITY supprimé).';
    END
    ELSE
    BEGIN
        EXEC(N'ALTER TABLE pct.PROVINCE ALTER COLUMN IdProvince VARCHAR(20) NOT NULL;');
        PRINT N'PROVINCE.IdProvince ajusté en VARCHAR(20).';
    END;

    ALTER TABLE pct.PROVINCE
        ADD CONSTRAINT PK_PCT_PROVINCE PRIMARY KEY (IdProvince);
    PRINT N'PK_PCT_PROVINCE définie sur IdProvince VARCHAR.';
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_PROVINCE_IdProvince')
    ALTER TABLE pct.PROVINCE
        ADD CONSTRAINT CK_PCT_PROVINCE_IdProvince
        CHECK (LEN(LTRIM(RTRIM(IdProvince))) > 0);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_FK_Province'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
    CREATE INDEX IX_PCT_COMPTE_FK_Province ON pct.COMPTE (FK_Province);

IF EXISTS (
    SELECT 1
    FROM pct.COMPTE c
    WHERE c.FK_Province IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM pct.PROVINCE p WHERE p.IdProvince = c.FK_Province)
)
BEGIN
    ROLLBACK TRAN;
    RAISERROR(N'Conversion refusée : COMPTE.FK_Province orphelin après conversion. Données conservées.', 16, 1);
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_Province')
BEGIN
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT FK_PCT_COMPTE_Province
        FOREIGN KEY (FK_Province) REFERENCES pct.PROVINCE (IdProvince)
        ON DELETE NO ACTION
        ON UPDATE NO ACTION;
    PRINT N'FK_PCT_COMPTE_Province recréée vers PROVINCE.IdProvince (NO ACTION).';
END;

COMMIT TRAN;

PRINT N'=== CONVERSION TERMINÉE ===';
SELECT
    OBJECT_SCHEMA_NAME(c.object_id) AS TABLE_SCHEMA,
    OBJECT_NAME(c.object_id) AS TABLE_NAME,
    c.name AS COLUMN_NAME,
    ty.name AS DATA_TYPE,
    CASE WHEN ty.name IN (N'varchar', N'nvarchar', N'char', N'nchar') THEN c.max_length END AS CHARACTER_MAXIMUM_LENGTH,
    c.is_nullable,
    c.is_identity
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE (c.object_id = OBJECT_ID(N'pct.PROVINCE') AND c.name = N'IdProvince')
   OR (c.object_id = OBJECT_ID(N'pct.COMPTE') AND c.name = N'FK_Province');

SELECT IdProvince, Libelle, Actif, DateCreation, DateModification
FROM pct.PROVINCE
ORDER BY IdProvince;

SELECT c.IdCompte, c.NumeroCompte, c.FK_Province, p.IdProvince, p.Libelle
FROM pct.COMPTE c
LEFT JOIN pct.PROVINCE p ON p.IdProvince = c.FK_Province
ORDER BY c.IdCompte;
