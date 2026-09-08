-- =============================================================================
-- OPTIONAL — Destination budgétaire sollicitée + mode paiement à l'initialisation DPM
-- Schéma : dpm
-- Idempotent. Ne supprime aucune donnée.
-- Prérequis : docs/sql/OPTIONAL_demande_paiement.sql déjà appliqué.
--
-- Règle métier :
--   Le demandeur indique TypeBudgetSollicite (DC / AE / BI) et ItemSollicite (AE/BI).
--   ModePaiementSollicite (CAISSE / BANQUE) est renseigné à la création.
--   Ces champs sont distincts de dpm.DEMANDE_PAIEMENT_IMPUTATION (Gestionnaire Junior).
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;

-- TypeBudgetSollicite : DC | AE | BI (déclaratif demandeur, sans FK référentiel)
IF COL_LENGTH('dpm.DEMANDE_PAIEMENT', 'TypeBudgetSollicite') IS NULL
BEGIN
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD TypeBudgetSollicite VARCHAR(3) NULL;
END;
GO

-- ItemSollicite : texte libre (Item N° déclaré par le demandeur pour AE/BI)
IF COL_LENGTH('dpm.DEMANDE_PAIEMENT', 'ItemSollicite') IS NULL
BEGIN
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD ItemSollicite NVARCHAR(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_DPM_DP_TypeBudgetSollicite'
      AND parent_object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
)
BEGIN
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT CK_DPM_DP_TypeBudgetSollicite CHECK (
            TypeBudgetSollicite IS NULL
            OR TypeBudgetSollicite IN ('DC', 'AE', 'BI'));
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_DPM_DP_ItemSollicite_Coherence'
      AND parent_object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
)
BEGIN
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT CK_DPM_DP_ItemSollicite_Coherence CHECK (
            TypeBudgetSollicite IS NULL
            OR (TypeBudgetSollicite = 'DC' AND ItemSollicite IS NULL)
            OR (TypeBudgetSollicite IN ('AE', 'BI') AND ItemSollicite IS NOT NULL AND LEN(LTRIM(RTRIM(ItemSollicite))) >= 1));
END;
GO
