-- =============================================================================
-- OPTIONAL — Migration progressive GESTIONNAIRE_JUNIOR_DC|AE|BI → GESTIONNAIRE_JUNIOR
-- Idempotent. Conserve la filière via permissions individuelles imputer_*.
-- Prérequis : OPTIONAL_socle_utilisateurs_roles_perimetres.sql
--             (CHECK profils + table dpm.PERMISSION_UTILISATEUR)
--
-- Stratégie par utilisateur ayant un profil junior historique :
--   1) Ajouter GESTIONNAIRE_JUNIOR (une seule fois)
--   2) Ajouter paiements.imputer_dc / _ae / _bi selon le(s) profil(s) historique(s)
--   3) Retirer GESTIONNAIRE_JUNIOR_DC / _AE / _BI
--
-- Permissions effectives après migration :
--   Junior unifié (lire + controler) + imputer_* individuelles
--   ≡ anciens JuniorDc / JuniorAe / JuniorBi
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dpm.PROFIL_UTILISATEUR', N'U') IS NULL
   OR OBJECT_ID(N'dpm.PERMISSION_UTILISATEUR', N'U') IS NULL
BEGIN
    RAISERROR(N'Prérequis manquant : dpm.PROFIL_UTILISATEUR et/ou dpm.PERMISSION_UTILISATEUR. Exécutez OPTIONAL_socle_utilisateurs_roles_perimetres.sql.', 16, 1);
    RETURN;
END;

BEGIN TRAN;

-- Utilisateurs concernés (au moins un profil junior historique)
DECLARE @Users TABLE (FK_Utilisateur BIGINT PRIMARY KEY);
INSERT INTO @Users (FK_Utilisateur)
SELECT DISTINCT FK_Utilisateur
FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil IN (N'GESTIONNAIRE_JUNIOR_DC', N'GESTIONNAIRE_JUNIOR_AE', N'GESTIONNAIRE_JUNIOR_BI');

-- 1) Ajouter le profil unifié
INSERT INTO dpm.PROFIL_UTILISATEUR (FK_Utilisateur, CodeProfil)
SELECT u.FK_Utilisateur, N'GESTIONNAIRE_JUNIOR'
FROM @Users u
WHERE NOT EXISTS (
    SELECT 1 FROM dpm.PROFIL_UTILISATEUR p
    WHERE p.FK_Utilisateur = u.FK_Utilisateur AND p.CodeProfil = N'GESTIONNAIRE_JUNIOR'
);

-- 2) Permissions individuelles de filière
INSERT INTO dpm.PERMISSION_UTILISATEUR (FK_Utilisateur, CodePermission, DateAttribution)
SELECT p.FK_Utilisateur, N'paiements.imputer_dc', SYSUTCDATETIME()
FROM dpm.PROFIL_UTILISATEUR p
WHERE p.CodeProfil = N'GESTIONNAIRE_JUNIOR_DC'
  AND NOT EXISTS (
      SELECT 1 FROM dpm.PERMISSION_UTILISATEUR x
      WHERE x.FK_Utilisateur = p.FK_Utilisateur AND x.CodePermission = N'paiements.imputer_dc'
  );

INSERT INTO dpm.PERMISSION_UTILISATEUR (FK_Utilisateur, CodePermission, DateAttribution)
SELECT p.FK_Utilisateur, N'paiements.imputer_ae', SYSUTCDATETIME()
FROM dpm.PROFIL_UTILISATEUR p
WHERE p.CodeProfil = N'GESTIONNAIRE_JUNIOR_AE'
  AND NOT EXISTS (
      SELECT 1 FROM dpm.PERMISSION_UTILISATEUR x
      WHERE x.FK_Utilisateur = p.FK_Utilisateur AND x.CodePermission = N'paiements.imputer_ae'
  );

INSERT INTO dpm.PERMISSION_UTILISATEUR (FK_Utilisateur, CodePermission, DateAttribution)
SELECT p.FK_Utilisateur, N'paiements.imputer_bi', SYSUTCDATETIME()
FROM dpm.PROFIL_UTILISATEUR p
WHERE p.CodeProfil = N'GESTIONNAIRE_JUNIOR_BI'
  AND NOT EXISTS (
      SELECT 1 FROM dpm.PERMISSION_UTILISATEUR x
      WHERE x.FK_Utilisateur = p.FK_Utilisateur AND x.CodePermission = N'paiements.imputer_bi'
  );

-- 3) Retirer les profils historiques une fois le profil cible présent
DELETE FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil IN (N'GESTIONNAIRE_JUNIOR_DC', N'GESTIONNAIRE_JUNIOR_AE', N'GESTIONNAIRE_JUNIOR_BI')
  AND EXISTS (
      SELECT 1 FROM dpm.PROFIL_UTILISATEUR x
      WHERE x.FK_Utilisateur = dpm.PROFIL_UTILISATEUR.FK_Utilisateur
        AND x.CodeProfil = N'GESTIONNAIRE_JUNIOR'
  );

COMMIT TRAN;

-- Contrôle
SELECT N'Profils junior' AS Scope, CodeProfil, COUNT(*) AS Nb
FROM dpm.PROFIL_UTILISATEUR
WHERE CodeProfil IN (
    N'GESTIONNAIRE_JUNIOR',
    N'GESTIONNAIRE_JUNIOR_DC', N'GESTIONNAIRE_JUNIOR_AE', N'GESTIONNAIRE_JUNIOR_BI')
GROUP BY CodeProfil
UNION ALL
SELECT N'Perm. imputer', CodePermission, COUNT(*)
FROM dpm.PERMISSION_UTILISATEUR
WHERE CodePermission IN (N'paiements.imputer_dc', N'paiements.imputer_ae', N'paiements.imputer_bi')
GROUP BY CodePermission
ORDER BY Scope, CodeProfil;
