-- =============================================================================
-- OPTIONAL — Seed périmètres Direction des Budgets (Tous*)
-- Idempotent. Prérequis : OPTIONAL_socle_utilisateurs_roles_perimetres.sql
--
-- Politique (alignée sur le branchement DPM + PeutVoirToutesUb) :
--   Les profils Direction des Budgets reçoivent un périmètre CONFIGURÉ :
--     TousDepartements = 1
--     ToutesUnitesBudgetaires = 1
--   → PerimetreAccess.EstConfigure = true (plus de fallback proxy).
--
-- Profils ciblés (cibles + historiques encore reconnus) :
--   CHARGE_DP, CHARGE_DPM
--   GESTIONNAIRE_JUNIOR, GESTIONNAIRE_JUNIOR_DC/AE/BI
--   GESTIONNAIRE_SENIOR, CHEF_DIVISION, CONTROLE_BUDGET
--   DIRECTEUR_BUDGETS
--
-- Hors scope (ne pas toucher) :
--   SERVICE_DEMANDEUR / RESPONSABLE_*  → périmètre ciblé (manuel)
--   ADMIN / ADMINISTRATEUR_SYSTEME     → technique
--   Matricule DG                       → hors Direction Budgets DP
--
-- Règles d'écriture :
--   1) Utilisateur Direction SANS en-tête → créer Tous*
--   2) En-tête non configuré (0/0 + aucune ligne) → passer en Tous*
--   3) Déjà Tous* → no-op
--   4) Périmètre déjà restreint (lignes ou flags partiels) → NE PAS écraser
--      (personnalisation admin conservée ; inventaire en sortie)
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF OBJECT_ID(N'dpm.PERIMETRE_UTILISATEUR', N'U') IS NULL
   OR OBJECT_ID(N'dpm.PROFIL_UTILISATEUR', N'U') IS NULL
BEGIN
    RAISERROR(N'Tables périmètre / profils introuvables. Exécutez d''abord le script socle.', 16, 1);
    RETURN;
END;

DECLARE @ProfilsDirection TABLE (CodeProfil NVARCHAR(40) PRIMARY KEY);
INSERT INTO @ProfilsDirection (CodeProfil) VALUES
    (N'CHARGE_DP'),
    (N'CHARGE_DPM'),
    (N'GESTIONNAIRE_JUNIOR'),
    (N'GESTIONNAIRE_JUNIOR_DC'),
    (N'GESTIONNAIRE_JUNIOR_AE'),
    (N'GESTIONNAIRE_JUNIOR_BI'),
    (N'GESTIONNAIRE_SENIOR'),
    (N'CHEF_DIVISION'),
    (N'CONTROLE_BUDGET'),
    (N'DIRECTEUR_BUDGETS');

DECLARE @Cibles TABLE (FK_Utilisateur BIGINT PRIMARY KEY);
INSERT INTO @Cibles (FK_Utilisateur)
SELECT DISTINCT p.FK_Utilisateur
FROM dpm.PROFIL_UTILISATEUR p
INNER JOIN @ProfilsDirection d ON d.CodeProfil = p.CodeProfil;

BEGIN TRAN;

-- 1) Créer l'en-tête Tous* s'il manque
INSERT INTO dpm.PERIMETRE_UTILISATEUR (
    FK_Utilisateur,
    TousDepartements,
    ToutesUnitesBudgetaires,
    DateModification
)
SELECT
    c.FK_Utilisateur,
    1,
    1,
    SYSUTCDATETIME()
FROM @Cibles c
WHERE NOT EXISTS (
    SELECT 1
    FROM dpm.PERIMETRE_UTILISATEUR x
    WHERE x.FK_Utilisateur = c.FK_Utilisateur
);

-- 2) Upgrader uniquement les périmètres « non configurés »
--    (false/false et aucune ligne dept/UB) → Tous*
UPDATE p
SET
    p.TousDepartements = 1,
    p.ToutesUnitesBudgetaires = 1,
    p.DateModification = SYSUTCDATETIME()
FROM dpm.PERIMETRE_UTILISATEUR p
INNER JOIN @Cibles c ON c.FK_Utilisateur = p.FK_Utilisateur
WHERE p.TousDepartements = 0
  AND p.ToutesUnitesBudgetaires = 0
  AND NOT EXISTS (
        SELECT 1 FROM dpm.PERIMETRE_DEPARTEMENT d
        WHERE d.FK_PerimetreUtilisateur = p.IdPerimetreUtilisateur
      )
  AND NOT EXISTS (
        SELECT 1 FROM dpm.PERIMETRE_UB u
        WHERE u.FK_PerimetreUtilisateur = p.IdPerimetreUtilisateur
      );

-- 3) Nettoyer les lignes détail inutiles quand les deux flags « tous » sont actifs
DELETE d
FROM dpm.PERIMETRE_DEPARTEMENT d
INNER JOIN dpm.PERIMETRE_UTILISATEUR p
    ON p.IdPerimetreUtilisateur = d.FK_PerimetreUtilisateur
INNER JOIN @Cibles c ON c.FK_Utilisateur = p.FK_Utilisateur
WHERE p.TousDepartements = 1
  AND p.ToutesUnitesBudgetaires = 1;

DELETE u
FROM dpm.PERIMETRE_UB u
INNER JOIN dpm.PERIMETRE_UTILISATEUR p
    ON p.IdPerimetreUtilisateur = u.FK_PerimetreUtilisateur
INNER JOIN @Cibles c ON c.FK_Utilisateur = p.FK_Utilisateur
WHERE p.TousDepartements = 1
  AND p.ToutesUnitesBudgetaires = 1;

COMMIT TRAN;

-- -----------------------------------------------------------------------------
-- Contrôles / inventaire
-- -----------------------------------------------------------------------------

-- A) Utilisateurs Direction des Budgets et état de périmètre
SELECT
    u.IdUtilisateur,
    u.Matricule,
    u.NomUtilisateur,
    u.Actif,
    STUFF((
        SELECT N', ' + pr.CodeProfil
        FROM dpm.PROFIL_UTILISATEUR pr
        INNER JOIN @ProfilsDirection d ON d.CodeProfil = pr.CodeProfil
        WHERE pr.FK_Utilisateur = u.IdUtilisateur
        ORDER BY pr.CodeProfil
        FOR XML PATH(N''), TYPE
    ).value(N'.', N'NVARCHAR(MAX)'), 1, 2, N'') AS ProfilsDirection,
    CASE
        WHEN p.IdPerimetreUtilisateur IS NULL THEN N'ABSENT'
        WHEN p.TousDepartements = 1 AND p.ToutesUnitesBudgetaires = 1 THEN N'TOUS'
        WHEN p.TousDepartements = 0
         AND p.ToutesUnitesBudgetaires = 0
         AND NOT EXISTS (
                SELECT 1 FROM dpm.PERIMETRE_DEPARTEMENT d
                WHERE d.FK_PerimetreUtilisateur = p.IdPerimetreUtilisateur)
         AND NOT EXISTS (
                SELECT 1 FROM dpm.PERIMETRE_UB ub
                WHERE ub.FK_PerimetreUtilisateur = p.IdPerimetreUtilisateur)
            THEN N'VIDE'
        ELSE N'RESTREINT'
    END AS EtatPerimetre,
    ISNULL(p.TousDepartements, 0) AS TousDepartements,
    ISNULL(p.ToutesUnitesBudgetaires, 0) AS ToutesUnitesBudgetaires
FROM @Cibles c
INNER JOIN dbo.UTILISATEUR u ON u.IdUtilisateur = c.FK_Utilisateur
LEFT JOIN dpm.PERIMETRE_UTILISATEUR p ON p.FK_Utilisateur = u.IdUtilisateur
ORDER BY u.NomUtilisateur;

-- B) Synthèse
;WITH Etat AS (
    SELECT
        c.FK_Utilisateur,
        CASE
            WHEN p.IdPerimetreUtilisateur IS NULL THEN N'ABSENT'
            WHEN p.TousDepartements = 1 AND p.ToutesUnitesBudgetaires = 1 THEN N'TOUS'
            WHEN p.TousDepartements = 0
             AND p.ToutesUnitesBudgetaires = 0
             AND NOT EXISTS (
                    SELECT 1 FROM dpm.PERIMETRE_DEPARTEMENT d
                    WHERE d.FK_PerimetreUtilisateur = p.IdPerimetreUtilisateur)
             AND NOT EXISTS (
                    SELECT 1 FROM dpm.PERIMETRE_UB ub
                    WHERE ub.FK_PerimetreUtilisateur = p.IdPerimetreUtilisateur)
                THEN N'VIDE'
            ELSE N'RESTREINT'
        END AS EtatPerimetre
    FROM @Cibles c
    LEFT JOIN dpm.PERIMETRE_UTILISATEUR p ON p.FK_Utilisateur = c.FK_Utilisateur
)
SELECT EtatPerimetre, COUNT(*) AS Nb
FROM Etat
GROUP BY EtatPerimetre
ORDER BY EtatPerimetre;
