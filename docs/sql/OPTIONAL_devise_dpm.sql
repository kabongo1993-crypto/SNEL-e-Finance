-- =============================================================================
-- OPTIONAL — Référentiel Devise DPM + liaison DEMANDE_PAIEMENT.FK_Devise
-- Schéma : dpm
-- Idempotent. Ne supprime aucune demande existante.
-- Ne supprime PAS la colonne Devise (conservée pour les calculs de taux).
-- Mapping historique : CDF→CDF, USD→USD, EURO/EUR→EUR.
--
-- Échec explicite si une Devise ne peut pas être mappée vers dpm.DEVISE
-- (valeur inconnue, NULL ou vide) — FK_Devise ne doit pas rester NULL
-- silencieusement pour une demande existante.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

-----------------------------------------------------------------------------
-- 1. Table référentiel
-----------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEVISE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.DEVISE
    (
        IdDevise BIGINT IDENTITY(1,1) NOT NULL,
        Code     VARCHAR(3)    NOT NULL,
        Libelle  NVARCHAR(100) NOT NULL,
        Symbole  NVARCHAR(10)  NULL,
        Actif    BIT           NOT NULL CONSTRAINT DF_DPM_DEVISE_Actif DEFAULT (1),
        CONSTRAINT PK_DPM_DEVISE PRIMARY KEY (IdDevise),
        CONSTRAINT CK_DPM_DEVISE_Code CHECK (LEN(Code) = 3)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_DEVISE_Code'
      AND object_id = OBJECT_ID(N'dpm.DEVISE')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_DEVISE_Code ON dpm.DEVISE (Code);
END;

-----------------------------------------------------------------------------
-- 2. Seed (CDF / USD / EUR)
-----------------------------------------------------------------------------
-- Libellés via NCHAR : résiste à sqlcmd sans -f 65001 (évite "amÃ©ricain").
MERGE dpm.DEVISE AS t
USING (VALUES
    ('CDF', N'Franc congolais',                                    N'FC',       1),
    ('USD', N'Dollar am' + NCHAR(233) + N'ricain',                 N'$',        1),
    ('EUR', N'Euro',                                               NCHAR(8364), 1)
) AS s (Code, Libelle, Symbole, Actif)
ON t.Code = s.Code
WHEN NOT MATCHED THEN
    INSERT (Code, Libelle, Symbole, Actif)
    VALUES (s.Code, s.Libelle, s.Symbole, s.Actif)
WHEN MATCHED AND (
       t.Libelle <> s.Libelle
    OR ISNULL(t.Symbole, N'') <> ISNULL(s.Symbole, N'')
) THEN
    UPDATE SET Libelle = s.Libelle, Symbole = s.Symbole;

-----------------------------------------------------------------------------
-- 3. Colonne FK_Devise (nullable côté schéma ; renseignée par backfill)
--    DDL via EXEC pour éviter « Invalid column name » dans le même batch.
-----------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT', N'U') IS NOT NULL
   AND COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'FK_Devise') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dpm.DEMANDE_PAIEMENT ADD FK_Devise BIGINT NULL;');
END;

-----------------------------------------------------------------------------
-- 4–7. Normalisation, backfill, validation, contrainte — SQL dynamique
--     (références à FK_Devise uniquement dans la chaîne EXEC)
-----------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT', N'U') IS NOT NULL
   AND COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'FK_Devise') IS NOT NULL
BEGIN
    EXEC(N'
SET NOCOUNT ON;

-- 4. Normaliser EURO → EUR (Devise conservée pour le métier / taux)
UPDATE dpm.DEMANDE_PAIEMENT
SET Devise = ''EUR''
WHERE UPPER(LTRIM(RTRIM(Devise))) IN (''EURO'', ''EUR'')
  AND Devise <> ''EUR'';

-- 5. Backfill / resync FK_Devise (NULL + incohérences Devise/FK)
UPDATE dp
SET FK_Devise = d.IdDevise
FROM dpm.DEMANDE_PAIEMENT dp
INNER JOIN dpm.DEVISE d
    ON d.Code = CASE
        WHEN UPPER(LTRIM(RTRIM(dp.Devise))) IN (''EURO'', ''EUR'') THEN ''EUR''
        ELSE UPPER(LTRIM(RTRIM(dp.Devise)))
    END
WHERE dp.FK_Devise IS NULL
   OR dp.FK_Devise <> d.IdDevise;

-- 6. Validation AVANT COMMIT
--    Métier : Devise obligatoire (VARCHAR(3) NOT NULL, LEN = 3).
--    NULL / vide / code inconnu / FK incohérente → THROW (pas de FK NULL silencieux).
DECLARE @NbNonMappees INT;
DECLARE @Exemples NVARCHAR(4000);

;WITH NonMappees AS (
    SELECT
        dp.IdDemandePaiement,
        dp.Reference,
        dp.Devise AS DeviseBrute,
        CASE
            WHEN dp.Devise IS NULL THEN N''<NULL>''
            WHEN LTRIM(RTRIM(dp.Devise)) = N'''' THEN N''<VIDE>''
            WHEN UPPER(LTRIM(RTRIM(dp.Devise))) IN (N''EURO'', N''EUR'') THEN N''EUR''
            ELSE UPPER(LTRIM(RTRIM(dp.Devise)))
        END AS CodeNormalise,
        dp.FK_Devise
    FROM dpm.DEMANDE_PAIEMENT dp
    WHERE dp.Devise IS NULL
       OR LTRIM(RTRIM(dp.Devise)) = N''''
       OR dp.FK_Devise IS NULL
       OR NOT EXISTS (
            SELECT 1
            FROM dpm.DEVISE d
            WHERE d.IdDevise = dp.FK_Devise
              AND d.Code = CASE
                  WHEN UPPER(LTRIM(RTRIM(dp.Devise))) IN (N''EURO'', N''EUR'') THEN N''EUR''
                  ELSE UPPER(LTRIM(RTRIM(dp.Devise)))
              END
       )
)
SELECT
    @NbNonMappees = COUNT(*),
    @Exemples = STRING_AGG(
        CONCAT(
            N''Id='', CAST(IdDemandePaiement AS NVARCHAR(20)),
            N'' Ref='', ISNULL(Reference, N''?''),
            N'' Devise=['', ISNULL(DeviseBrute, N''NULL''), N'']'',
            N'' Norm=['', CodeNormalise, N'']'',
            N'' FK='', ISNULL(CAST(FK_Devise AS NVARCHAR(20)), N''NULL'')
        ),
        N'' | ''
    )
FROM NonMappees;

IF @NbNonMappees > 0
BEGIN
    DECLARE @Msg NVARCHAR(4000) = CONCAT(
        N''Migration Devise échouée : '',
        CAST(@NbNonMappees AS NVARCHAR(20)),
        N'' demande(s) de paiement avec Devise NULL, vide, inconnue ou incohérente '',
        N''(FK_Devise non mappable vers dpm.DEVISE). '',
        N''Corriger les données puis relancer. Exemples : '',
        ISNULL(@Exemples, N''(aucun détail)'')
    );
    THROW 50001, @Msg, 1;
END;

-- 7. Contrainte FK + index
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N''FK_DPM_DP_Devise''
      AND parent_object_id = OBJECT_ID(N''dpm.DEMANDE_PAIEMENT'')
)
BEGIN
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_Devise
        FOREIGN KEY (FK_Devise) REFERENCES dpm.DEVISE (IdDevise);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N''IX_DPM_DP_FK_Devise''
      AND object_id = OBJECT_ID(N''dpm.DEMANDE_PAIEMENT'')
)
BEGIN
    CREATE INDEX IX_DPM_DP_FK_Devise ON dpm.DEMANDE_PAIEMENT (FK_Devise);
END;
');
END;

COMMIT TRAN;
GO

-----------------------------------------------------------------------------
-- 8. Contrôles post-migration (lecture seule — hors transaction)
-----------------------------------------------------------------------------
PRINT N'=== Contrôles migration Devise DPM ===';

IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT', N'U') IS NULL
   OR COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'FK_Devise') IS NULL
BEGIN
    PRINT N'dpm.DEMANDE_PAIEMENT / FK_Devise indisponible — contrôles ignorés.';
END
ELSE
BEGIN
    -- Totaux
    SELECT
        COUNT(*) AS NbDemandesTotal,
        SUM(CASE WHEN FK_Devise IS NOT NULL THEN 1 ELSE 0 END) AS NbAvecFkDevise,
        SUM(CASE WHEN FK_Devise IS NULL THEN 1 ELSE 0 END) AS NbNonMappees
    FROM dpm.DEMANDE_PAIEMENT;

    -- Correspondance Devise (code stocké) ↔ DEVISE.Code via FK
    SELECT
        dp.Devise AS CodeStocke,
        d.Code AS CodeReferentiel,
        d.Libelle,
        COUNT(*) AS NbDemandes,
        SUM(CASE
            WHEN UPPER(LTRIM(RTRIM(dp.Devise))) = d.Code
              OR (UPPER(LTRIM(RTRIM(dp.Devise))) = N'EURO' AND d.Code = N'EUR')
            THEN 1 ELSE 0
        END) AS NbCorrespondanceOk,
        SUM(CASE
            WHEN UPPER(LTRIM(RTRIM(dp.Devise))) = d.Code
              OR (UPPER(LTRIM(RTRIM(dp.Devise))) = N'EURO' AND d.Code = N'EUR')
            THEN 0 ELSE 1
        END) AS NbIncoherence
    FROM dpm.DEMANDE_PAIEMENT dp
    LEFT JOIN dpm.DEVISE d ON d.IdDevise = dp.FK_Devise
    GROUP BY dp.Devise, d.Code, d.Libelle
    ORDER BY dp.Devise, d.Code;

    -- Lignes encore incohérentes (doit être vide après une migration réussie)
    SELECT
        dp.IdDemandePaiement,
        dp.Reference,
        dp.Devise,
        dp.FK_Devise,
        d.Code AS CodeViaFk
    FROM dpm.DEMANDE_PAIEMENT dp
    LEFT JOIN dpm.DEVISE d ON d.IdDevise = dp.FK_Devise
    WHERE dp.FK_Devise IS NULL
       OR d.IdDevise IS NULL
       OR (
            UPPER(LTRIM(RTRIM(dp.Devise))) <> d.Code
            AND NOT (
                UPPER(LTRIM(RTRIM(dp.Devise))) = N'EURO'
                AND d.Code = N'EUR'
            )
       );
END;
GO
