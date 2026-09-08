-- =============================================================================
-- OPTIONAL — Ajustements budgétaires DG (post-validation)
-- Table : AJUSTEMENT_BUDGETAIRE
-- Base : BD_SNEL (map-only EF)
-- Idempotent : crée uniquement si absente.
-- Ne recrée pas les montants existants.
-- Ne modifie pas le workflow de validation des prévisions.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

IF OBJECT_ID(N'dbo.AJUSTEMENT_BUDGETAIRE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AJUSTEMENT_BUDGETAIRE
    (
        IdAjustement              BIGINT IDENTITY(1,1) NOT NULL,
        Reference                 VARCHAR(40) NOT NULL,
        FK_PrevisionBudgetaire    BIGINT NOT NULL,
        FK_VersionBudgetaire      BIGINT NOT NULL,
        FK_UniteBudgetaire        BIGINT NOT NULL,
        FK_ExerciceBudgetaire     BIGINT NOT NULL,
        MontantAncien             DECIMAL(19,4) NOT NULL,
        MontantNouveau            DECIMAL(19,4) NOT NULL,
        Variation                 DECIMAL(19,4) NOT NULL,
        Motif                     NVARCHAR(1000) NOT NULL,
        Statut                    VARCHAR(30) NOT NULL,
        FK_UtilisateurCreation    BIGINT NOT NULL,
        DateCreation              DATETIME2 NOT NULL
            CONSTRAINT DF_AJUST_DateCreation DEFAULT (SYSUTCDATETIME()),
        FK_UtilisateurValidation  BIGINT NULL,
        DateValidation            DATETIME2 NULL,
        FK_UtilisateurModification BIGINT NULL,
        DateModification          DATETIME2 NULL,
        CONSTRAINT PK_AJUSTEMENT_BUDGETAIRE PRIMARY KEY (IdAjustement)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AJUSTEMENT_PREVISION'
      AND object_id = OBJECT_ID(N'dbo.AJUSTEMENT_BUDGETAIRE')
)
BEGIN
    CREATE INDEX IX_AJUSTEMENT_PREVISION
        ON dbo.AJUSTEMENT_BUDGETAIRE (FK_PrevisionBudgetaire, DateCreation);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AJUSTEMENT_VERSION_UB'
      AND object_id = OBJECT_ID(N'dbo.AJUSTEMENT_BUDGETAIRE')
)
BEGIN
    CREATE INDEX IX_AJUSTEMENT_VERSION_UB
        ON dbo.AJUSTEMENT_BUDGETAIRE (FK_VersionBudgetaire, FK_UniteBudgetaire, Statut);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_AJUSTEMENT_REFERENCE'
      AND object_id = OBJECT_ID(N'dbo.AJUSTEMENT_BUDGETAIRE')
)
BEGIN
    CREATE UNIQUE INDEX UX_AJUSTEMENT_REFERENCE
        ON dbo.AJUSTEMENT_BUDGETAIRE (Reference);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AJUST_PREVISION')
    ALTER TABLE dbo.AJUSTEMENT_BUDGETAIRE
        ADD CONSTRAINT FK_AJUST_PREVISION
        FOREIGN KEY (FK_PrevisionBudgetaire) REFERENCES dbo.PREVISION_BUDGETAIRE (IdPrevision);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AJUST_VERSION')
    ALTER TABLE dbo.AJUSTEMENT_BUDGETAIRE
        ADD CONSTRAINT FK_AJUST_VERSION
        FOREIGN KEY (FK_VersionBudgetaire) REFERENCES dbo.VERSION_BUDGETAIRE (IdVersion);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AJUST_UB')
    ALTER TABLE dbo.AJUSTEMENT_BUDGETAIRE
        ADD CONSTRAINT FK_AJUST_UB
        FOREIGN KEY (FK_UniteBudgetaire) REFERENCES dbo.UNITE_BUDGETAIRE (IdUB);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AJUST_EXERCICE')
    ALTER TABLE dbo.AJUSTEMENT_BUDGETAIRE
        ADD CONSTRAINT FK_AJUST_EXERCICE
        FOREIGN KEY (FK_ExerciceBudgetaire) REFERENCES dbo.EXERCICE_BUDGETAIRE (IdExercice);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AJUST_USER_CREATION')
    ALTER TABLE dbo.AJUSTEMENT_BUDGETAIRE
        ADD CONSTRAINT FK_AJUST_USER_CREATION
        FOREIGN KEY (FK_UtilisateurCreation) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AJUST_USER_VALIDATION')
    ALTER TABLE dbo.AJUSTEMENT_BUDGETAIRE
        ADD CONSTRAINT FK_AJUST_USER_VALIDATION
        FOREIGN KEY (FK_UtilisateurValidation) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AJUST_USER_MODIF')
    ALTER TABLE dbo.AJUSTEMENT_BUDGETAIRE
        ADD CONSTRAINT FK_AJUST_USER_MODIF
        FOREIGN KEY (FK_UtilisateurModification) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_AJUSTEMENT_STATUT'
      AND parent_object_id = OBJECT_ID(N'dbo.AJUSTEMENT_BUDGETAIRE')
)
BEGIN
    ALTER TABLE dbo.AJUSTEMENT_BUDGETAIRE
        ADD CONSTRAINT CK_AJUSTEMENT_STATUT
        CHECK (Statut IN ('BROUILLON', 'VALIDE', 'ANNULE'));
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_AJUSTEMENT_VARIATION'
      AND parent_object_id = OBJECT_ID(N'dbo.AJUSTEMENT_BUDGETAIRE')
)
BEGIN
    ALTER TABLE dbo.AJUSTEMENT_BUDGETAIRE
        ADD CONSTRAINT CK_AJUSTEMENT_VARIATION
        CHECK (Variation = MontantNouveau - MontantAncien);
END;

COMMIT TRAN;
PRINT 'OPTIONAL_ajustement_budgetaire : OK';
