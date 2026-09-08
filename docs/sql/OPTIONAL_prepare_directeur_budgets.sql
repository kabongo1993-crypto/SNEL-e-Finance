-- =============================================================================
-- OPTIONAL — Préparation / inventaire DIRECTEUR_BUDGETS
-- Idempotent. Lecture seule (aucune migration de profils).
-- Prérequis : OPTIONAL_socle_utilisateurs_roles_perimetres.sql
--
-- Constat :
--   DIRECTEUR_BUDGETS n'est PAS issu d'un profil historique à renommer.
--   C'est un rôle métier cible distinct de :
--     - ADMIN / ADMINISTRATEUR_SYSTEME  (admin.all, technique)
--     - matricule DG                     (ajustements écrire/valider)
--     - GESTIONNAIRE_SENIOR              (contrôle opérationnel)
--     - CHEF_DIVISION                    (visa opérationnel seul)
--
-- Bundle applicatif attendu (AppPermissions.DirecteurBudgets) :
--   paiements.lire
--   paiements.viser_budget
--   versions.controler / versions.valider / versions.rejeter
--   ajustements.lire
--
-- Ce script ne crée / ne modifie aucune attribution utilisateur.
-- =============================================================================

SET NOCOUNT ON;

IF OBJECT_ID(N'dpm.PROFIL_UTILISATEUR', N'U') IS NULL
BEGIN
    RAISERROR(N'dpm.PROFIL_UTILISATEUR introuvable. Exécutez d''abord les scripts socle.', 16, 1);
    RETURN;
END;

-- 1) Inventaire des profils Direction Budgets + admin
SELECT CodeProfil, COUNT(*) AS Nb
FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil IN (
    N'DIRECTEUR_BUDGETS',
    N'CHEF_DIVISION',
    N'GESTIONNAIRE_SENIOR',
    N'GESTIONNAIRE_JUNIOR',
    N'CHARGE_DP',
    N'CHARGE_DPM',
    N'ADMIN',
    N'ADMINISTRATEUR_SYSTEME'
)
GROUP BY CodeProfil
ORDER BY CodeProfil;

-- 2) Utilisateurs déjà en DIRECTEUR_BUDGETS (détail)
SELECT
    u.IdUtilisateur,
    u.Matricule,
    u.NomUtilisateur,
    u.Actif
FROM dpm.PROFIL_UTILISATEUR p
INNER JOIN dbo.UTILISATEUR u ON u.IdUtilisateur = p.FK_Utilisateur
WHERE p.CodeProfil = N'DIRECTEUR_BUDGETS'
ORDER BY u.NomUtilisateur;

-- 3) Alerte : comptes techniques ADMIN qui ne sont PAS Directeur
--    (ne pas confondre administration technique et direction métier)
SELECT
    u.IdUtilisateur,
    u.Matricule,
    u.NomUtilisateur,
    p.CodeProfil
FROM dpm.PROFIL_UTILISATEUR p
INNER JOIN dbo.UTILISATEUR u ON u.IdUtilisateur = p.FK_Utilisateur
WHERE p.CodeProfil IN (N'ADMIN', N'ADMINISTRATEUR_SYSTEME')
  AND NOT EXISTS (
        SELECT 1
        FROM dpm.PROFIL_UTILISATEUR d
        WHERE d.FK_Utilisateur = p.FK_Utilisateur
          AND d.CodeProfil = N'DIRECTEUR_BUDGETS'
      )
ORDER BY u.NomUtilisateur;
