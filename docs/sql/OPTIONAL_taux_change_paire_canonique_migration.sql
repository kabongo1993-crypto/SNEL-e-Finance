/*
  MIGRATION OPTIONNELLE — Paire canonique USD/CDF (taux unique + inverse calculé)

  Contexte :
  - Une seule orientation stockée : USD → CDF (1 USD = Taux CDF).
  - L'inverse CDF → USD est calculé par ITauxChangeService, jamais stocké.
  - Purge contrôlée des données de test existantes (Phase vérification).

  PRÉREQUIS : sauvegarde BD_SNEL. Idempotent autant que possible.

  ÉTAT DES LIEUX (2026-08-27) :
  - 6 lignes test TAUX_CHANGE (orientations mixtes CDF→USD et USD→CDF)
  - 2 DPM test (#31 DP-2026-00027, #32 DP-2026-00028) avec FK_TauxChangePaiement → ids 1, 2
  - Aucune FK_TauxChange (conversion USD) sur ces DPM
  - Aucune autre table ne référence TAUX_CHANGE
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

PRINT '=== RAPPORT PRÉ-MIGRATION ===';

SELECT 'TAUX_CHANGE' AS Section, IdTauxChange, DeviseSource, DeviseCible, Taux,
       CONVERT(varchar(10), DateEffet, 23) AS DateEffet, Statut
FROM dpm.TAUX_CHANGE
ORDER BY IdTauxChange;

SELECT 'DPM_FK' AS Section, IdDemandePaiement, Reference, Statut,
       FK_TauxChange, FK_TauxChangePaiement, TauxConversion, TauxPaiement
FROM dpm.DEMANDE_PAIEMENT
WHERE FK_TauxChange IS NOT NULL OR FK_TauxChangePaiement IS NOT NULL;

BEGIN TRANSACTION;

-- 1) Remettre les FK DPM de test dans un état cohérent (DPM #31, #32 = test Charge DP)
UPDATE dpm.DEMANDE_PAIEMENT
SET FK_TauxChangePaiement = NULL,
    TauxPaiement = NULL,
    MontantPaiement = NULL,
    DateModification = SYSUTCDATETIME()
WHERE IdDemandePaiement IN (31, 32)
  AND Reference IN (N'DP-2026-00027', N'DP-2026-00028');

PRINT 'DPM test : FK_TauxChangePaiement remises à NULL = ' + CAST(@@ROWCOUNT AS varchar(10));

-- 2) Purge des taux de test (données vérification, pas historique métier)
DELETE FROM dpm.TAUX_CHANGE
WHERE IdTauxChange IN (1, 2, 3, 4, 5, 6)
   OR (DeviseSource = 'CDF' AND DeviseCible = 'USD')
   OR (DeviseSource = 'USD' AND DeviseCible = 'CDF' AND DateEffet <= '2027-12-31');

PRINT 'TAUX_CHANGE supprimés = ' + CAST(@@ROWCOUNT AS varchar(10));

-- 3) Contrainte orientation canonique (Phase 1 : USD/CDF uniquement)
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_DPM_TAUX_ORIENTATION_CANONIQUE'
      AND parent_object_id = OBJECT_ID(N'dpm.TAUX_CHANGE')
)
BEGIN
    ALTER TABLE dpm.TAUX_CHANGE
        ADD CONSTRAINT CK_DPM_TAUX_ORIENTATION_CANONIQUE
        CHECK (DeviseSource = N'USD' AND DeviseCible = N'CDF');
    PRINT 'Contrainte CK_DPM_TAUX_ORIENTATION_CANONIQUE créée.';
END
ELSE
    PRINT 'Contrainte CK_DPM_TAUX_ORIENTATION_CANONIQUE déjà présente.';

-- 4) Unicité ACTIF par paire canonique (remplace l'index orientation libre si absent)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_TAUX_ACTIF_Devise_Paire'
      AND object_id = OBJECT_ID(N'dpm.TAUX_CHANGE')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_TAUX_ACTIF_Devise_Paire
        ON dpm.TAUX_CHANGE (DeviseSource, DeviseCible)
        WHERE Statut = 'ACTIF';
    PRINT 'Index UX_DPM_TAUX_ACTIF_Devise_Paire créé.';
END
ELSE
    PRINT 'Index UX_DPM_TAUX_ACTIF_Devise_Paire déjà présent.';

COMMIT TRANSACTION;

PRINT '=== POST-MIGRATION ===';
SELECT COUNT(*) AS NbTauxRestants FROM dpm.TAUX_CHANGE;
SELECT COUNT(*) AS DpmAvecFkTaux FROM dpm.DEMANDE_PAIEMENT
WHERE FK_TauxChange IS NOT NULL OR FK_TauxChangePaiement IS NOT NULL;

PRINT 'Migration paire canonique terminée.';
