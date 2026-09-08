-- =============================================================================
-- OPTIONAL — Migration progressive CONTROLE_BUDGET
--              → GESTIONNAIRE_SENIOR + CHEF_DIVISION
-- Idempotent. Prérequis : OPTIONAL_socle_utilisateurs_roles_perimetres.sql
--
-- Constat métier : CONTROLE_BUDGET couvre aujourd'hui DEUX niveaux :
--   - contrôle budgétaire  (paiements.controler_budget)
--   - visa budgétaire      (paiements.viser_budget)
--
-- Stratégie de conservation des droits effectifs :
--   1) Ajouter GESTIONNAIRE_SENIOR  (lire + controler)
--   2) Ajouter CHEF_DIVISION         (lire + viser)
--   3) Retirer CONTROLE_BUDGET
--
-- Équivalence :
--   CONTROLE_BUDGET ≡ Senior ∪ Chef Division
--
-- Après migration, on pourra retirer SENIOR ou CHEF_DIVISION au cas par cas
-- selon la fonction réelle de chaque personne (scission organisationnelle).
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dpm.PROFIL_UTILISATEUR', N'U') IS NULL
BEGIN
    RAISERROR(N'dpm.PROFIL_UTILISATEUR introuvable. Exécutez d''abord les scripts socle.', 16, 1);
    RETURN;
END;

BEGIN TRAN;

DECLARE @Users TABLE (FK_Utilisateur BIGINT PRIMARY KEY);
INSERT INTO @Users (FK_Utilisateur)
SELECT DISTINCT FK_Utilisateur
FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil = N'CONTROLE_BUDGET';

-- 1) Gestionnaire Senior
INSERT INTO dpm.PROFIL_UTILISATEUR (FK_Utilisateur, CodeProfil)
SELECT u.FK_Utilisateur, N'GESTIONNAIRE_SENIOR'
FROM @Users u
WHERE NOT EXISTS (
    SELECT 1 FROM dpm.PROFIL_UTILISATEUR p
    WHERE p.FK_Utilisateur = u.FK_Utilisateur AND p.CodeProfil = N'GESTIONNAIRE_SENIOR'
);

-- 2) Chef de Division
INSERT INTO dpm.PROFIL_UTILISATEUR (FK_Utilisateur, CodeProfil)
SELECT u.FK_Utilisateur, N'CHEF_DIVISION'
FROM @Users u
WHERE NOT EXISTS (
    SELECT 1 FROM dpm.PROFIL_UTILISATEUR p
    WHERE p.FK_Utilisateur = u.FK_Utilisateur AND p.CodeProfil = N'CHEF_DIVISION'
);

-- 3) Retirer le profil historique une fois les deux cibles présentes
DELETE FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil = N'CONTROLE_BUDGET'
  AND EXISTS (
        SELECT 1 FROM dpm.PROFIL_UTILISATEUR s
        WHERE s.FK_Utilisateur = dpm.PROFIL_UTILISATEUR.FK_Utilisateur
          AND s.CodeProfil = N'GESTIONNAIRE_SENIOR'
  )
  AND EXISTS (
        SELECT 1 FROM dpm.PROFIL_UTILISATEUR c
        WHERE c.FK_Utilisateur = dpm.PROFIL_UTILISATEUR.FK_Utilisateur
          AND c.CodeProfil = N'CHEF_DIVISION'
  );

COMMIT TRAN;

-- Contrôle
SELECT CodeProfil, COUNT(*) AS Nb
FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil IN (N'CONTROLE_BUDGET', N'GESTIONNAIRE_SENIOR', N'CHEF_DIVISION')
GROUP BY CodeProfil
ORDER BY CodeProfil;
