-- =============================================================================
-- OPTIONAL — Routage nominatif DPM (Lot 3.1)
-- Base : BD_SNEL — schéma dpm
-- Idempotent. NE PAS exécuter automatiquement.
--
-- Ajoute :
--   - Colonne FK_UtilisateurAssigne sur dpm.DEMANDE_PAIEMENT
--   - Table dpm.DEMANDE_PAIEMENT_ROUTAGE
--   - Backfill assignation courante (sans modifier les statuts)
--   - Backfill routage historique (transmissions source → cible certaines uniquement)
--
-- Miroir C# : DemandePaiementAssignationBackfillRules (Domain)
-- JOURNAL_AUDIT : intact, non remplacé.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT', N'U') IS NULL
BEGIN
    RAISERROR(N'dpm.DEMANDE_PAIEMENT introuvable.', 16, 1);
    RETURN;
END;

-- ---------------------------------------------------------------------------
-- Colonne FK_UtilisateurAssigne
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'FK_UtilisateurAssigne') IS NULL
BEGIN
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD FK_UtilisateurAssigne BIGINT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_USER_ASSIGNE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_USER_ASSIGNE
        FOREIGN KEY (FK_UtilisateurAssigne) REFERENCES dbo.UTILISATEUR (IdUtilisateur);
GO

-- ---------------------------------------------------------------------------
-- Table dpm.DEMANDE_PAIEMENT_ROUTAGE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_ROUTAGE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.DEMANDE_PAIEMENT_ROUTAGE
    (
        IdRoutage              BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_DPM_DEMANDE_PAIEMENT_ROUTAGE PRIMARY KEY,

        FK_DemandePaiement     BIGINT        NOT NULL,

        FK_UtilisateurSource   BIGINT        NOT NULL,

        FK_UtilisateurCible    BIGINT        NOT NULL,

        StatutSource           VARCHAR(40)   NOT NULL,

        StatutCible            VARCHAR(40)   NOT NULL,

        Action                 VARCHAR(40)   NOT NULL,

        DateRoutage            DATETIME2     NOT NULL
            CONSTRAINT DF_DPM_ROUTAGE_DateRoutage DEFAULT (SYSDATETIME()),

        EstActif               BIT           NOT NULL
            CONSTRAINT DF_DPM_ROUTAGE_EstActif DEFAULT (1),

        Motif                  NVARCHAR(500) NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_ROUTAGE_Demande_Actif'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_ROUTAGE')
)
    CREATE INDEX IX_DPM_ROUTAGE_Demande_Actif
        ON dpm.DEMANDE_PAIEMENT_ROUTAGE (FK_DemandePaiement, EstActif)
        WHERE EstActif = 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_ROUTAGE_DEMANDE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_ROUTAGE
        ADD CONSTRAINT FK_DPM_ROUTAGE_DEMANDE
        FOREIGN KEY (FK_DemandePaiement) REFERENCES dpm.DEMANDE_PAIEMENT (IdDemandePaiement);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_ROUTAGE_USER_SOURCE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_ROUTAGE
        ADD CONSTRAINT FK_DPM_ROUTAGE_USER_SOURCE
        FOREIGN KEY (FK_UtilisateurSource) REFERENCES dbo.UTILISATEUR (IdUtilisateur);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_ROUTAGE_USER_CIBLE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_ROUTAGE
        ADD CONSTRAINT FK_DPM_ROUTAGE_USER_CIBLE
        FOREIGN KEY (FK_UtilisateurCible) REFERENCES dbo.UTILISATEUR (IdUtilisateur);
GO

-- ---------------------------------------------------------------------------
-- BACKFILL FK_UtilisateurAssigne (idempotent : uniquement si NULL)
-- Ne modifie aucun statut.
-- ---------------------------------------------------------------------------

-- BROUILLON / A_CORRIGER → créateur
UPDATE dp
SET FK_UtilisateurAssigne = dp.FK_UtilisateurCreation
FROM dpm.DEMANDE_PAIEMENT dp
WHERE dp.Statut IN (N'BROUILLON', N'A_CORRIGER')
  AND dp.FK_UtilisateurAssigne IS NULL;

-- EN_TRAITEMENT_DPM (+ alias historique RECEPTIONNEE_BUDGETS) → réceptionneur si connu
UPDATE dp
SET FK_UtilisateurAssigne = dp.FK_UtilisateurReception
FROM dpm.DEMANDE_PAIEMENT dp
WHERE dp.Statut IN (N'EN_TRAITEMENT_DPM', N'RECEPTIONNEE_BUDGETS')
  AND dp.FK_UtilisateurReception IS NOT NULL
  AND dp.FK_UtilisateurAssigne IS NULL;

-- EN_CONTROLE_BUDGETAIRE : audit CONTROLER > fallback FK_UtilisateurControle sans ORIENTER
IF OBJECT_ID(N'dbo.JOURNAL_AUDIT', N'U') IS NOT NULL
BEGIN
    ;WITH DernierControle AS (
        SELECT
            ja.IdEntite AS FK_DemandePaiement,
            ja.FK_Utilisateur,
            ROW_NUMBER() OVER (
                PARTITION BY ja.IdEntite
                ORDER BY ja.DateHeure DESC, ja.IdAudit DESC) AS rn
        FROM dbo.JOURNAL_AUDIT ja
        WHERE ja.Entite = N'DEMANDE_PAIEMENT'
          AND ja.Operation = N'CONTROLER'
    ),
    PossedeOrienter AS (
        SELECT DISTINCT ja.IdEntite AS FK_DemandePaiement
        FROM dbo.JOURNAL_AUDIT ja
        WHERE ja.Entite = N'DEMANDE_PAIEMENT'
          AND ja.Operation = N'ORIENTER'
    )
    UPDATE dp
    SET FK_UtilisateurAssigne = COALESCE(
            dc.FK_Utilisateur,
            CASE
                WHEN po.FK_DemandePaiement IS NULL AND dp.FK_UtilisateurControle IS NOT NULL
                    THEN dp.FK_UtilisateurControle
            END)
    FROM dpm.DEMANDE_PAIEMENT dp
    LEFT JOIN DernierControle dc
        ON dc.FK_DemandePaiement = dp.IdDemandePaiement AND dc.rn = 1
    LEFT JOIN PossedeOrienter po
        ON po.FK_DemandePaiement = dp.IdDemandePaiement
    WHERE dp.Statut = N'EN_CONTROLE_BUDGETAIRE'
      AND dp.FK_UtilisateurAssigne IS NULL
      AND (
            dc.FK_Utilisateur IS NOT NULL
            OR (po.FK_DemandePaiement IS NULL AND dp.FK_UtilisateurControle IS NOT NULL)
          );
END
ELSE
BEGIN
    -- Sans JOURNAL_AUDIT : fallback FK_UtilisateurControle uniquement (interprétation directe)
    UPDATE dp
    SET FK_UtilisateurAssigne = dp.FK_UtilisateurControle
    FROM dpm.DEMANDE_PAIEMENT dp
    WHERE dp.Statut = N'EN_CONTROLE_BUDGETAIRE'
      AND dp.FK_UtilisateurControle IS NOT NULL
      AND dp.FK_UtilisateurAssigne IS NULL;
END;
GO

-- Pool explicites : EN_VALIDATION_N1/N2, VALIDEE_ENTITE, SOUMISE, VISEE → pas d'assignation forcée
-- (FK_UtilisateurAssigne reste NULL si non renseigné)

-- ---------------------------------------------------------------------------
-- BACKFILL DEMANDE_PAIEMENT_ROUTAGE (transmissions certaines uniquement)
-- Idempotent : INSERT WHERE NOT EXISTS sur (demande, action, source, cible, statuts)
-- EstActif = 0 (historique backfill) — le lot 3.2 activera le routage runtime.
-- ---------------------------------------------------------------------------

-- RECEPTIONNER : soumissionnaire → réceptionneur
INSERT INTO dpm.DEMANDE_PAIEMENT_ROUTAGE (
    FK_DemandePaiement,
    FK_UtilisateurSource,
    FK_UtilisateurCible,
    StatutSource,
    StatutCible,
    Action,
    DateRoutage,
    EstActif,
    Motif)
SELECT
    dp.IdDemandePaiement,
    dp.FK_UtilisateurSoumission,
    dp.FK_UtilisateurReception,
    N'SOUMISE',
    N'EN_TRAITEMENT_DPM',
    N'RECEPTIONNER',
    COALESCE(dp.DateReception, dp.DateModification, dp.DateCreation),
    0,
    N'Backfill Lot 3.1 — réception après soumission'
FROM dpm.DEMANDE_PAIEMENT dp
WHERE dp.FK_UtilisateurSoumission IS NOT NULL
  AND dp.FK_UtilisateurReception IS NOT NULL
  AND NOT EXISTS (
        SELECT 1
        FROM dpm.DEMANDE_PAIEMENT_ROUTAGE r
        WHERE r.FK_DemandePaiement = dp.IdDemandePaiement
          AND r.Action = N'RECEPTIONNER'
          AND r.FK_UtilisateurSource = dp.FK_UtilisateurSoumission
          AND r.FK_UtilisateurCible = dp.FK_UtilisateurReception
          AND r.StatutSource = N'SOUMISE'
          AND r.StatutCible = N'EN_TRAITEMENT_DPM');

-- ENTRER_TRAITEMENT : créateur → réceptionneur (circuit charge direct sans soumission)
INSERT INTO dpm.DEMANDE_PAIEMENT_ROUTAGE (
    FK_DemandePaiement,
    FK_UtilisateurSource,
    FK_UtilisateurCible,
    StatutSource,
    StatutCible,
    Action,
    DateRoutage,
    EstActif,
    Motif)
SELECT
    dp.IdDemandePaiement,
    dp.FK_UtilisateurCreation,
    dp.FK_UtilisateurReception,
    N'BROUILLON',
    N'EN_TRAITEMENT_DPM',
    N'ENTRER_TRAITEMENT',
    COALESCE(dp.DateReception, dp.DateModification, dp.DateCreation),
    0,
    N'Backfill Lot 3.1 — entrée traitement directe'
FROM dpm.DEMANDE_PAIEMENT dp
WHERE dp.FK_UtilisateurReception IS NOT NULL
  AND dp.FK_UtilisateurSoumission IS NULL
  AND NOT EXISTS (
        SELECT 1
        FROM dpm.DEMANDE_PAIEMENT_ROUTAGE r
        WHERE r.FK_DemandePaiement = dp.IdDemandePaiement
          AND r.Action = N'ENTRER_TRAITEMENT'
          AND r.FK_UtilisateurSource = dp.FK_UtilisateurCreation
          AND r.FK_UtilisateurCible = dp.FK_UtilisateurReception);

-- CONTROLER (PrendreEnControle) : réceptionneur/orienteur → contrôleur (audit JOURNAL_AUDIT)
IF OBJECT_ID(N'dbo.JOURNAL_AUDIT', N'U') IS NOT NULL
BEGIN
    ;WITH AuditsControle AS (
        SELECT
            ja.IdEntite AS FK_DemandePaiement,
            ja.FK_Utilisateur AS FK_UtilisateurCible,
            ja.DateHeure,
            ja.IdAudit,
            ROW_NUMBER() OVER (
                PARTITION BY ja.IdEntite
                ORDER BY ja.DateHeure DESC, ja.IdAudit DESC) AS rn
        FROM dbo.JOURNAL_AUDIT ja
        WHERE ja.Entite = N'DEMANDE_PAIEMENT'
          AND ja.Operation = N'CONTROLER'
    )
    INSERT INTO dpm.DEMANDE_PAIEMENT_ROUTAGE (
        FK_DemandePaiement,
        FK_UtilisateurSource,
        FK_UtilisateurCible,
        StatutSource,
        StatutCible,
        Action,
        DateRoutage,
        EstActif,
        Motif)
    SELECT
        dp.IdDemandePaiement,
        COALESCE(dp.FK_UtilisateurReception, dp.FK_UtilisateurCreation),
        ac.FK_UtilisateurCible,
        N'EN_TRAITEMENT_DPM',
        N'EN_CONTROLE_BUDGETAIRE',
        N'CONTROLER',
        ac.DateHeure,
        0,
        N'Backfill Lot 3.1 — prise en contrôle (audit CONTROLER)'
    FROM dpm.DEMANDE_PAIEMENT dp
    INNER JOIN AuditsControle ac
        ON ac.FK_DemandePaiement = dp.IdDemandePaiement AND ac.rn = 1
    WHERE COALESCE(dp.FK_UtilisateurReception, dp.FK_UtilisateurCreation) IS NOT NULL
      AND ac.FK_UtilisateurCible IS NOT NULL
      AND NOT EXISTS (
            SELECT 1
            FROM dpm.DEMANDE_PAIEMENT_ROUTAGE r
            WHERE r.FK_DemandePaiement = dp.IdDemandePaiement
              AND r.Action = N'CONTROLER'
              AND r.FK_UtilisateurCible = ac.FK_UtilisateurCible
              AND r.StatutSource = N'EN_TRAITEMENT_DPM'
              AND r.StatutCible = N'EN_CONTROLE_BUDGETAIRE');
END;
GO

PRINT N'OPTIONAL_dpm_routage_assignation.sql — terminé.';
