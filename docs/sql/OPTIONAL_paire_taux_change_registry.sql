/*
  MIGRATION OPTIONNELLE — Registre PAIRE_TAUX_CHANGE (orientation libre)

  Objectif :
  - Stocker l'orientation canonique choisie par le métier (ex. EUR/USD ou USD/EUR).
  - Aucun pivot USD imposé : la paire est créée à la première saisie de taux.
  - L'inverse directionnel reste calculé par ITauxChangeService.

  Idempotent autant que possible.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dpm.PAIRE_TAUX_CHANGE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.PAIRE_TAUX_CHANGE (
        IdPaireTauxChange bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_DPM_PAIRE_TAUX_CHANGE PRIMARY KEY,
        DeviseBase  varchar(3)  NOT NULL,
        DeviseQuote varchar(3)  NOT NULL,
        Actif       bit         NOT NULL CONSTRAINT DF_DPM_PAIRE_TAUX_Actif DEFAULT (1),
        DateCreation datetime2  NOT NULL CONSTRAINT DF_DPM_PAIRE_TAUX_DateCreation DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_DPM_PAIRE_TAUX_Devise_Distinctes CHECK (DeviseBase <> DeviseQuote)
    );

    CREATE UNIQUE INDEX UX_DPM_PAIRE_TAUX_DeviseBase_Quote
        ON dpm.PAIRE_TAUX_CHANGE (DeviseBase, DeviseQuote);

    PRINT 'Table dpm.PAIRE_TAUX_CHANGE créée.';
END

IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_DPM_TAUX_ORIENTATION_CANONIQUE'
      AND parent_object_id = OBJECT_ID(N'dpm.TAUX_CHANGE')
)
BEGIN
    ALTER TABLE dpm.TAUX_CHANGE DROP CONSTRAINT CK_DPM_TAUX_ORIENTATION_CANONIQUE;
    PRINT 'Contrainte CK_DPM_TAUX_ORIENTATION_CANONIQUE supprimée.';
END

-- Alimenter le registre depuis les taux déjà en base (orientation réellement stockée)
INSERT INTO dpm.PAIRE_TAUX_CHANGE (DeviseBase, DeviseQuote, Actif)
SELECT DISTINCT t.DeviseSource, t.DeviseCible, 1
FROM dpm.TAUX_CHANGE t
WHERE NOT EXISTS (
    SELECT 1 FROM dpm.PAIRE_TAUX_CHANGE p
    WHERE (p.DeviseBase = t.DeviseSource AND p.DeviseQuote = t.DeviseCible)
       OR (p.DeviseBase = t.DeviseCible AND p.DeviseQuote = t.DeviseSource)
);

PRINT 'Paires dérivées des taux existants = ' + CAST(@@ROWCOUNT AS varchar(10));

COMMIT TRANSACTION;

PRINT '=== REGISTRE PAIRE_TAUX_CHANGE ===';
SELECT IdPaireTauxChange, DeviseBase, DeviseQuote, Actif
FROM dpm.PAIRE_TAUX_CHANGE
ORDER BY DeviseBase, DeviseQuote;

/*
  NOTE — Changer l'orientation métier (ex. passer de USD/EUR à EUR/USD) :
  1. Inactiver / migrer les lignes TAUX_CHANGE (taux_inversé = 1/ancien_taux)
  2. UPDATE PAIRE_TAUX_CHANGE SET DeviseBase='EUR', DeviseQuote='USD' WHERE ...
  À faire via script métier validé, jamais automatiquement.
*/
