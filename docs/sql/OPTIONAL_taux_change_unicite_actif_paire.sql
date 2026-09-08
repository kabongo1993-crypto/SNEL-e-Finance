/*
  MIGRATION OPTIONNELLE — règle unicité ACTIF par paire (taux de change)

  Contexte :
  - Une seule ligne ACTIF par couple (DeviseSource, DeviseCible).
  - Les versions antérieures passent automatiquement à INACTIF lors d'une nouvelle version.
  - La recherche historique (FindApplicable) utilise DateEffet, pas Statut.

  NE PAS EXÉCUTER AUTOMATIQUEMENT — vérifier l'environnement et sauvegarder avant application.

  1) Corriger les données existantes si plusieurs ACTIF par paire
  2) Ajouter l'index unique filtré UX_DPM_TAUX_ACTIF_Devise_Paire
*/

SET NOCOUNT ON;

-- ---------------------------------------------------------------------------
-- 1. Correction données : conserver un seul ACTIF par paire (DateEffet max)
-- ---------------------------------------------------------------------------
;WITH RangActif AS (
    SELECT
        IdTauxChange,
        ROW_NUMBER() OVER (
            PARTITION BY DeviseSource, DeviseCible
            ORDER BY DateEffet DESC, IdTauxChange DESC
        ) AS Rang
    FROM dpm.TAUX_CHANGE
    WHERE Statut = 'ACTIF'
)
UPDATE t
SET
    Statut = 'INACTIF',
    DateModification = SYSUTCDATETIME()
FROM dpm.TAUX_CHANGE t
INNER JOIN RangActif r ON r.IdTauxChange = t.IdTauxChange
WHERE r.Rang > 1;

-- ---------------------------------------------------------------------------
-- 2. Index unique : une seule ligne ACTIF par paire
--    (UX_DPM_TAUX_ACTIF_Devise_Date reste : unicité ACTIF par paire+date)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_TAUX_ACTIF_Devise_Paire'
      AND object_id = OBJECT_ID(N'dpm.TAUX_CHANGE')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_TAUX_ACTIF_Devise_Paire
        ON dpm.TAUX_CHANGE (DeviseSource, DeviseCible)
        WHERE Statut = 'ACTIF';
END;

PRINT 'Migration taux change — unicité ACTIF par paire terminée.';
