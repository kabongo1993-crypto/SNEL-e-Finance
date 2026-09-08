-- =============================================================================
-- OPTIONAL — Circuit validation entité émettrice DPM (N1 / N2)
-- Base : BD_SNEL — schéma dpm
-- Idempotent. NE PAS exécuter automatiquement.
--
-- Ajoute :
--   - Statuts intermédiaires entité sur DEMANDE_PAIEMENT
--   - Table DEMANDE_PAIEMENT_VALIDATION (N1 / N2, électronique / physique)
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT', N'U') IS NULL
BEGIN
    RAISERROR(N'dpm.DEMANDE_PAIEMENT introuvable.', 16, 1);
    RETURN;
END;

-- ---------------------------------------------------------------------------
-- Statuts entité sur DEMANDE_PAIEMENT
-- ---------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_DPM_DP_Statut'
      AND parent_object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
)
    ALTER TABLE dpm.DEMANDE_PAIEMENT DROP CONSTRAINT CK_DPM_DP_Statut;
GO

ALTER TABLE dpm.DEMANDE_PAIEMENT
    ADD CONSTRAINT CK_DPM_DP_Statut CHECK (Statut IN (
        N'BROUILLON',
        N'EN_VALIDATION_N1',
        N'EN_VALIDATION_N2',
        N'VALIDEE_ENTITE',
        N'SOUMISE',
        N'EN_TRAITEMENT_DPM',
        N'RECEPTIONNEE_BUDGETS',
        N'EN_CONTROLE_BUDGETAIRE',
        N'A_CORRIGER',
        N'VISEE_BUDGETAIREMENT'
    ));
GO

-- ---------------------------------------------------------------------------
-- dpm.DEMANDE_PAIEMENT_VALIDATION
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_VALIDATION', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.DEMANDE_PAIEMENT_VALIDATION
    (
        IdValidation               BIGINT IDENTITY(1,1) NOT NULL,
        FK_DemandePaiement         BIGINT        NOT NULL,
        Niveau                     TINYINT       NOT NULL,
        Ordre                      TINYINT       NOT NULL,
        Statut                     VARCHAR(20)   NOT NULL,
        ModeValidation             VARCHAR(20)   NULL,
        FK_UtilisateurValidateur   BIGINT        NULL,
        FK_UtilisateurDeclarant    BIGINT        NULL,
        NomSignatairePhysique      NVARCHAR(200) NULL,
        FonctionSignatairePhysique NVARCHAR(200) NULL,
        DateSignaturePhysique      DATE          NULL,
        DateValidation             DATETIME2     NULL,
        Commentaire                NVARCHAR(1000) NULL,
        EmpreinteDonnees           VARCHAR(64)   NULL,
        CONSTRAINT PK_DPM_DEMANDE_PAIEMENT_VALIDATION PRIMARY KEY (IdValidation),
        CONSTRAINT CK_DPM_VAL_Niveau CHECK (Niveau IN (1, 2)),
        CONSTRAINT CK_DPM_VAL_Ordre CHECK (Ordre IN (1, 2)),
        CONSTRAINT CK_DPM_VAL_Statut CHECK (Statut IN (N'EN_ATTENTE', N'VALIDEE', N'REJETEE')),
        CONSTRAINT CK_DPM_VAL_Mode CHECK (
            ModeValidation IS NULL OR ModeValidation IN (N'ELECTRONIQUE', N'PHYSIQUE'))
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_VAL_Demande_Niveau'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_VALIDATION')
)
    CREATE UNIQUE INDEX UX_DPM_VAL_Demande_Niveau
        ON dpm.DEMANDE_PAIEMENT_VALIDATION (FK_DemandePaiement, Niveau);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_VAL_DEMANDE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_VALIDATION
        ADD CONSTRAINT FK_DPM_VAL_DEMANDE
        FOREIGN KEY (FK_DemandePaiement) REFERENCES dpm.DEMANDE_PAIEMENT (IdDemandePaiement);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_VAL_USER_VALIDATEUR')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_VALIDATION
        ADD CONSTRAINT FK_DPM_VAL_USER_VALIDATEUR
        FOREIGN KEY (FK_UtilisateurValidateur) REFERENCES dbo.UTILISATEUR (IdUtilisateur);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_VAL_USER_DECLARANT')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_VALIDATION
        ADD CONSTRAINT FK_DPM_VAL_USER_DECLARANT
        FOREIGN KEY (FK_UtilisateurDeclarant) REFERENCES dbo.UTILISATEUR (IdUtilisateur);
GO

PRINT N'OPTIONAL_circuit_validation_entite_dpm.sql — terminé.';
