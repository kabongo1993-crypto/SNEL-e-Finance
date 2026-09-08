-- =============================================================================
-- OPTIONAL — PCT CATEGORIE_COMPTE : Orientation facultative
-- Base   : BD_SNEL (map-only EF — aucune migration EF Core)
-- Idempotent. Transactionnel. Aucun seed.
--
-- IdCategorieCompte reste BIGINT IDENTITY (identifiant technique).
-- Pas de Code. Pas de recréation de table.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'pct.CATEGORIE_COMPTE', N'U') IS NULL
BEGIN
    RAISERROR(N'pct.CATEGORIE_COMPTE est introuvable. Exécutez docs/sql/OPTIONAL_pct_parametrage_comptes.sql.', 16, 1);
    RETURN;
END;

PRINT N'=== RAPPORT PRÉ-AJUSTEMENT ===';
SELECT
    IdCategorieCompte,
    Libelle,
    Orientation,
    Actif
FROM pct.CATEGORIE_COMPTE
ORDER BY IdCategorieCompte;

SELECT COUNT(*) AS NbCompteCategorie FROM pct.COMPTE_CATEGORIE;

BEGIN TRAN;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'pct.CATEGORIE_COMPTE')
      AND name = N'Orientation'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE pct.CATEGORIE_COMPTE ALTER COLUMN Orientation NVARCHAR(200) NULL;
    PRINT N'CATEGORIE_COMPTE.Orientation est désormais NULLABLE.';
END
ELSE
    PRINT N'CATEGORIE_COMPTE.Orientation est déjà NULLABLE.';

-- Réparation d'encodage UTF-8 mal interprété (Ã© / Ã / Ã´) — données existantes uniquement.
UPDATE pct.CATEGORIE_COMPTE
SET
    Libelle = REPLACE(REPLACE(REPLACE(Libelle,
        NCHAR(195) + NCHAR(169), NCHAR(233)),
        NCHAR(195) + NCHAR(160), NCHAR(224)),
        NCHAR(195) + NCHAR(180), NCHAR(244)),
    Orientation = REPLACE(REPLACE(REPLACE(Orientation,
        NCHAR(195) + NCHAR(169), NCHAR(233)),
        NCHAR(195) + NCHAR(160), NCHAR(224)),
        NCHAR(195) + NCHAR(180), NCHAR(244)),
    DateModification = SYSUTCDATETIME()
WHERE Libelle LIKE N'%' + NCHAR(195) + NCHAR(169) + N'%'
   OR Libelle LIKE N'%' + NCHAR(195) + NCHAR(160) + N'%'
   OR Libelle LIKE N'%' + NCHAR(195) + NCHAR(180) + N'%'
   OR Orientation LIKE N'%' + NCHAR(195) + NCHAR(169) + N'%'
   OR Orientation LIKE N'%' + NCHAR(195) + NCHAR(160) + N'%'
   OR Orientation LIKE N'%' + NCHAR(195) + NCHAR(180) + N'%';

PRINT N'Lignes d''encodage réparées : ' + CAST(@@ROWCOUNT AS nvarchar(20));

COMMIT TRAN;

PRINT N'=== RAPPORT POST-AJUSTEMENT ===';
SELECT
    c.name,
    ty.name AS typ,
    c.is_nullable
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.object_id = OBJECT_ID(N'pct.CATEGORIE_COMPTE')
ORDER BY c.column_id;

SELECT
    IdCategorieCompte,
    Libelle,
    Orientation,
    Actif
FROM pct.CATEGORIE_COMPTE
ORDER BY IdCategorieCompte;
