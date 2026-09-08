-- =============================================================================
-- OPTIONAL — Workflow opérationnel VERSION × UB
-- Table : WORKFLOW_PREVISION_UB
-- Base : BD_SNEL (map-only EF)
-- Idempotent : table/contraintes/seed uniquement si absents.
-- Ne modifie PAS PREVISION_BUDGETAIRE (montants).
-- Ne supprime aucune donnée métier.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

IF OBJECT_ID(N'dbo.WORKFLOW_PREVISION_UB', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WORKFLOW_PREVISION_UB
    (
        IdWorkflowPrevisionUB     BIGINT IDENTITY(1,1) NOT NULL,
        FK_VersionBudgetaire      BIGINT NOT NULL,
        FK_UniteBudgetaire        BIGINT NOT NULL,
        Statut                    VARCHAR(30) NOT NULL,
        FK_UtilisateurSoumission  BIGINT NULL,
        DateSoumission            DATETIME2 NULL,
        FK_UtilisateurControle    BIGINT NULL,
        DateControle              DATETIME2 NULL,
        FK_UtilisateurValidation  BIGINT NULL,
        DateValidation            DATETIME2 NULL,
        FK_UtilisateurRejet       BIGINT NULL,
        DateRejet                 DATETIME2 NULL,
        MotifRejet                NVARCHAR(1000) NULL,
        FK_UtilisateurCreation    BIGINT NOT NULL,
        DateCreation              DATETIME2 NOT NULL CONSTRAINT DF_WF_UB_DateCreation DEFAULT (SYSUTCDATETIME()),
        FK_UtilisateurModification BIGINT NULL,
        DateModification          DATETIME2 NULL,
        CONSTRAINT PK_WORKFLOW_PREVISION_UB PRIMARY KEY (IdWorkflowPrevisionUB)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_WORKFLOW_PREVISION_UB_VERSION_UB'
      AND object_id = OBJECT_ID(N'dbo.WORKFLOW_PREVISION_UB')
)
BEGIN
    CREATE UNIQUE INDEX UX_WORKFLOW_PREVISION_UB_VERSION_UB
        ON dbo.WORKFLOW_PREVISION_UB (FK_VersionBudgetaire, FK_UniteBudgetaire);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WF_UB_VERSION')
    ALTER TABLE dbo.WORKFLOW_PREVISION_UB
        ADD CONSTRAINT FK_WF_UB_VERSION
        FOREIGN KEY (FK_VersionBudgetaire) REFERENCES dbo.VERSION_BUDGETAIRE (IdVersion);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WF_UB_UNITE')
    ALTER TABLE dbo.WORKFLOW_PREVISION_UB
        ADD CONSTRAINT FK_WF_UB_UNITE
        FOREIGN KEY (FK_UniteBudgetaire) REFERENCES dbo.UNITE_BUDGETAIRE (IdUB);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WF_UB_USER_CREATION')
    ALTER TABLE dbo.WORKFLOW_PREVISION_UB
        ADD CONSTRAINT FK_WF_UB_USER_CREATION
        FOREIGN KEY (FK_UtilisateurCreation) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WF_UB_USER_MODIF')
    ALTER TABLE dbo.WORKFLOW_PREVISION_UB
        ADD CONSTRAINT FK_WF_UB_USER_MODIF
        FOREIGN KEY (FK_UtilisateurModification) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WF_UB_USER_SOUMISSION')
    ALTER TABLE dbo.WORKFLOW_PREVISION_UB
        ADD CONSTRAINT FK_WF_UB_USER_SOUMISSION
        FOREIGN KEY (FK_UtilisateurSoumission) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WF_UB_USER_CONTROLE')
    ALTER TABLE dbo.WORKFLOW_PREVISION_UB
        ADD CONSTRAINT FK_WF_UB_USER_CONTROLE
        FOREIGN KEY (FK_UtilisateurControle) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WF_UB_USER_VALIDATION')
    ALTER TABLE dbo.WORKFLOW_PREVISION_UB
        ADD CONSTRAINT FK_WF_UB_USER_VALIDATION
        FOREIGN KEY (FK_UtilisateurValidation) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WF_UB_USER_REJET')
    ALTER TABLE dbo.WORKFLOW_PREVISION_UB
        ADD CONSTRAINT FK_WF_UB_USER_REJET
        FOREIGN KEY (FK_UtilisateurRejet) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

-- Seed : une ligne par (Version, UB) ayant au moins une prévision.
-- Statut initial = statut VERSION (pas d'invention de traces UB si ambiguës).
;WITH Paires AS (
    SELECT DISTINCT
        p.FK_VersionBudgetaire,
        p.FK_UniteBudgetaire,
        MIN(p.FK_UtilisateurCreation) AS FK_UtilisateurCreation
    FROM dbo.PREVISION_BUDGETAIRE p
    GROUP BY p.FK_VersionBudgetaire, p.FK_UniteBudgetaire
)
INSERT INTO dbo.WORKFLOW_PREVISION_UB
(
    FK_VersionBudgetaire,
    FK_UniteBudgetaire,
    Statut,
    FK_UtilisateurCreation,
    DateCreation
)
SELECT
    pa.FK_VersionBudgetaire,
    pa.FK_UniteBudgetaire,
    UPPER(LTRIM(RTRIM(v.Statut))),
    pa.FK_UtilisateurCreation,
    SYSUTCDATETIME()
FROM Paires pa
INNER JOIN dbo.VERSION_BUDGETAIRE v ON v.IdVersion = pa.FK_VersionBudgetaire
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.WORKFLOW_PREVISION_UB w
    WHERE w.FK_VersionBudgetaire = pa.FK_VersionBudgetaire
      AND w.FK_UniteBudgetaire = pa.FK_UniteBudgetaire
);

COMMIT TRAN;
GO

SELECT COUNT(*) AS NbWorkflowPrevisionUb FROM dbo.WORKFLOW_PREVISION_UB;
GO
