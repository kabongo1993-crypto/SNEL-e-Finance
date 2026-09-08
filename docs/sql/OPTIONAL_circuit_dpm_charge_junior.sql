-- =============================================================================
-- OPTIONAL — Circuit DPM Chargé / Junior (statuts, paiement, profils)
-- Idempotent. Ne supprime aucune donnée.
-- Prérequis : OPTIONAL_demande_paiement.sql + OPTIONAL_demandeur_dpm.sql
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

-- ---------------------------------------------------------------------------
-- dpm.PROFIL_UTILISATEUR — affectation de profils sans matricule
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.PROFIL_UTILISATEUR', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.PROFIL_UTILISATEUR
    (
        IdProfilUtilisateur BIGINT IDENTITY(1,1) NOT NULL,
        FK_Utilisateur      BIGINT      NOT NULL,
        CodeProfil          VARCHAR(40) NOT NULL,
        CONSTRAINT PK_DPM_PROFIL_UTILISATEUR PRIMARY KEY (IdProfilUtilisateur),
        CONSTRAINT CK_DPM_PROFIL_Code CHECK (CodeProfil IN (
            'DEMANDEUR', 'CHARGE_DPM',
            'GESTIONNAIRE_JUNIOR_DC', 'GESTIONNAIRE_JUNIOR_AE', 'GESTIONNAIRE_JUNIOR_BI',
            'CONTROLE_BUDGET', 'ADMIN'))
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_PROFIL_USER_CODE'
      AND object_id = OBJECT_ID(N'dpm.PROFIL_UTILISATEUR')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_PROFIL_USER_CODE
        ON dpm.PROFIL_UTILISATEUR (FK_Utilisateur, CodeProfil);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PROFIL_UTILISATEUR')
    ALTER TABLE dpm.PROFIL_UTILISATEUR
        ADD CONSTRAINT FK_DPM_PROFIL_UTILISATEUR
        FOREIGN KEY (FK_Utilisateur) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

-- ---------------------------------------------------------------------------
-- Colonnes traitement Chargé DPM
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'TypeInstrumentPaiement') IS NULL
    ALTER TABLE dpm.DEMANDE_PAIEMENT ADD TypeInstrumentPaiement VARCHAR(40) NULL;

IF COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'DevisePaiement') IS NULL
    ALTER TABLE dpm.DEMANDE_PAIEMENT ADD DevisePaiement VARCHAR(3) NULL;

IF COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'MontantPaiement') IS NULL
    ALTER TABLE dpm.DEMANDE_PAIEMENT ADD MontantPaiement DECIMAL(19,4) NULL;

IF COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'TauxPaiement') IS NULL
    ALTER TABLE dpm.DEMANDE_PAIEMENT ADD TauxPaiement DECIMAL(19,8) NULL;

IF COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'FK_TauxChangePaiement') IS NULL
    ALTER TABLE dpm.DEMANDE_PAIEMENT ADD FK_TauxChangePaiement BIGINT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_TAUX_PAIEMENT')
    EXEC(N'ALTER TABLE dpm.DEMANDE_PAIEMENT ADD CONSTRAINT FK_DPM_DP_TAUX_PAIEMENT
        FOREIGN KEY (FK_TauxChangePaiement) REFERENCES dpm.TAUX_CHANGE (IdTauxChange)');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_Instrument')
    EXEC(N'ALTER TABLE dpm.DEMANDE_PAIEMENT ADD CONSTRAINT CK_DPM_DP_Instrument CHECK (
            TypeInstrumentPaiement IS NULL
            OR TypeInstrumentPaiement IN (''PIECE_CAISSE'', ''BON_PROVISOIRE'', ''MINUTE_CHEQUE''))');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_DevisePaiement')
    EXEC(N'ALTER TABLE dpm.DEMANDE_PAIEMENT ADD CONSTRAINT CK_DPM_DP_DevisePaiement CHECK (
            DevisePaiement IS NULL OR LEN(DevisePaiement) = 3)');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_MontantPaiement')
    EXEC(N'ALTER TABLE dpm.DEMANDE_PAIEMENT ADD CONSTRAINT CK_DPM_DP_MontantPaiement CHECK (
            MontantPaiement IS NULL OR MontantPaiement >= 0)');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_TauxPaiement')
    EXEC(N'ALTER TABLE dpm.DEMANDE_PAIEMENT ADD CONSTRAINT CK_DPM_DP_TauxPaiement CHECK (
            TauxPaiement IS NULL OR TauxPaiement > 0)');

-- ---------------------------------------------------------------------------
-- Statut EN_TRAITEMENT_DPM (remplace RECEPTIONNEE_BUDGETS)
-- ---------------------------------------------------------------------------
UPDATE dpm.DEMANDE_PAIEMENT
SET Statut = N'EN_TRAITEMENT_DPM'
WHERE Statut = N'RECEPTIONNEE_BUDGETS';

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_Statut')
    ALTER TABLE dpm.DEMANDE_PAIEMENT DROP CONSTRAINT CK_DPM_DP_Statut;

EXEC(N'ALTER TABLE dpm.DEMANDE_PAIEMENT ADD CONSTRAINT CK_DPM_DP_Statut CHECK (Statut IN (
        ''BROUILLON'', ''SOUMISE'', ''EN_TRAITEMENT_DPM'',
        ''EN_CONTROLE_BUDGETAIRE'', ''A_CORRIGER'', ''VISEE_BUDGETAIREMENT''))');

COMMIT TRAN;

-- ---------------------------------------------------------------------------
-- CONFIGURATION RESTANTE (hors script automatique) :
-- Affecter les profils aux utilisateurs réels, sans matricules codés :
--
--   INSERT INTO dpm.PROFIL_UTILISATEUR (FK_Utilisateur, CodeProfil)
--   VALUES (@IdUtilisateur, 'CHARGE_DPM');
--
-- Codes autorisés : DEMANDEUR, CHARGE_DPM, GESTIONNAIRE_JUNIOR_DC,
-- GESTIONNAIRE_JUNIOR_AE, GESTIONNAIRE_JUNIOR_BI, CONTROLE_BUDGET, ADMIN.
-- ---------------------------------------------------------------------------

