-- =============================================================================
-- OPTIONAL — Migration / préparation Entité Initiatrice
--   DEMANDEUR → SERVICE_DEMANDEUR
--   + inventaire RESPONSABLE_SERVICE_DEMANDEUR / RESPONSABLE_ENTITE_INITIATRICE
-- Idempotent. Prérequis : OPTIONAL_socle_utilisateurs_roles_perimetres.sql
--
-- Stratégie :
--   1) Pour chaque utilisateur DEMANDEUR sans SERVICE_DEMANDEUR → ajouter SERVICE_DEMANDEUR
--   2) Retirer DEMANDEUR une fois SERVICE_DEMANDEUR présent
--   3) Inventaire des trois rôles cibles (+ DEMANDEUR restant)
--
-- Les RESPONSABLE_* n'ont pas de profil historique source : attribution manuelle
-- via administration. Permissions DP actuelles = lire + ecrire + soumettre
-- (validation hiérarchique interne = évolution ultérieure).
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dpm.PROFIL_UTILISATEUR', N'U') IS NULL
BEGIN
    RAISERROR(N'dpm.PROFIL_UTILISATEUR introuvable. Exécutez d''abord les scripts socle.', 16, 1);
    RETURN;
END;

BEGIN TRAN;

-- 1) Ajouter SERVICE_DEMANDEUR là où DEMANDEUR existe encore sans la cible
INSERT INTO dpm.PROFIL_UTILISATEUR (FK_Utilisateur, CodeProfil)
SELECT p.FK_Utilisateur, N'SERVICE_DEMANDEUR'
FROM dpm.PROFIL_UTILISATEUR p
WHERE p.CodeProfil = N'DEMANDEUR'
  AND NOT EXISTS (
        SELECT 1
        FROM dpm.PROFIL_UTILISATEUR x
        WHERE x.FK_Utilisateur = p.FK_Utilisateur
          AND x.CodeProfil = N'SERVICE_DEMANDEUR'
      );

-- 2) Retirer le profil historique une fois la cible en place
DELETE FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil = N'DEMANDEUR'
  AND EXISTS (
        SELECT 1
        FROM dpm.PROFIL_UTILISATEUR x
        WHERE x.FK_Utilisateur = dpm.PROFIL_UTILISATEUR.FK_Utilisateur
          AND x.CodeProfil = N'SERVICE_DEMANDEUR'
      );

COMMIT TRAN;

-- 3) Inventaire Entité Initiatrice
SELECT CodeProfil, COUNT(*) AS Nb
FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil IN (
    N'DEMANDEUR',
    N'SERVICE_DEMANDEUR',
    N'RESPONSABLE_SERVICE_DEMANDEUR',
    N'RESPONSABLE_ENTITE_INITIATRICE'
)
GROUP BY CodeProfil
ORDER BY CodeProfil;
