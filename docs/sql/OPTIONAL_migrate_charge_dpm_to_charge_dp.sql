-- =============================================================================
-- OPTIONAL — Migration progressive CHARGE_DPM → CHARGE_DP
-- Idempotent. Ne casse pas les utilisateurs encore en CHARGE_DPM.
-- Prérequis : OPTIONAL_socle_utilisateurs_roles_perimetres.sql
--             (CHECK CodeProfil doit déjà autoriser CHARGE_DP)
--
-- Stratégie :
--   1) Pour chaque utilisateur ayant CHARGE_DPM sans CHARGE_DP → ajouter CHARGE_DP
--   2) Retirer CHARGE_DPM une fois CHARGE_DP présent
-- Les permissions effectives restent identiques (PermissionsPourProfil).
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dpm.PROFIL_UTILISATEUR', N'U') IS NULL
BEGIN
    RAISERROR(N'dpm.PROFIL_UTILISATEUR introuvable. Exécutez d''abord OPTIONAL_circuit_dpm_charge_junior.sql puis OPTIONAL_socle_utilisateurs_roles_perimetres.sql.', 16, 1);
    RETURN;
END;

BEGIN TRAN;

-- 1) Ajouter CHARGE_DP là où CHARGE_DPM existe encore sans CHARGE_DP
INSERT INTO dpm.PROFIL_UTILISATEUR (FK_Utilisateur, CodeProfil)
SELECT p.FK_Utilisateur, N'CHARGE_DP'
FROM dpm.PROFIL_UTILISATEUR p
WHERE p.CodeProfil = N'CHARGE_DPM'
  AND NOT EXISTS (
        SELECT 1
        FROM dpm.PROFIL_UTILISATEUR x
        WHERE x.FK_Utilisateur = p.FK_Utilisateur
          AND x.CodeProfil = N'CHARGE_DP'
      );

-- 2) Retirer le profil historique une fois le profil cible en place
DELETE FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil = N'CHARGE_DPM'
  AND EXISTS (
        SELECT 1
        FROM dpm.PROFIL_UTILISATEUR x
        WHERE x.FK_Utilisateur = dpm.PROFIL_UTILISATEUR.FK_Utilisateur
          AND x.CodeProfil = N'CHARGE_DP'
      );

COMMIT TRAN;

-- Contrôle
SELECT CodeProfil, COUNT(*) AS Nb
FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil IN (N'CHARGE_DPM', N'CHARGE_DP')
GROUP BY CodeProfil;
