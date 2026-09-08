-- =============================================================================
-- OPTIONAL — Classement AE (ordre Version × UB)
-- Table : CLASSEMENT_AE
-- Base : BD_SNEL (map-only EF — pas de migration EF Core dans ce dépôt)
-- Idempotent : table / contraintes / index uniquement si absents.
-- Ne modifie PAS PREVISION_BUDGETAIRE (montants).
-- L'ordre initial n'est PAS celui du fichier Excel historique (inexistant en données).
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Requis pour CREATE INDEX filtré (SQL Server).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;

BEGIN TRAN;

IF OBJECT_ID(N'dbo.CLASSEMENT_AE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CLASSEMENT_AE
    (
        IdClassementAE       BIGINT IDENTITY(1,1) NOT NULL,
        FK_VersionBudgetaire BIGINT NOT NULL,
        FK_UniteBudgetaire   BIGINT NOT NULL,
        TypeLigne            VARCHAR(10) NOT NULL,
        FK_GroupeItemAE      BIGINT NULL,
        LibelleItemAE        NVARCHAR(500) NULL,
        OrdreAffichage       INT NOT NULL,
        DateCreation         DATETIME2 NOT NULL
            CONSTRAINT DF_CLASSEMENT_AE_DateCreation DEFAULT (SYSUTCDATETIME()),
        DateModification     DATETIME2 NULL,
        CONSTRAINT PK_CLASSEMENT_AE PRIMARY KEY (IdClassementAE),
        CONSTRAINT CK_CLASSEMENT_AE_TypeLigne
            CHECK (TypeLigne IN ('GROUPE', 'ITEM')),
        CONSTRAINT CK_CLASSEMENT_AE_Discriminant
            CHECK (
                (TypeLigne = 'GROUPE' AND FK_GroupeItemAE IS NOT NULL AND LibelleItemAE IS NULL)
             OR (TypeLigne = 'ITEM' AND LibelleItemAE IS NOT NULL AND FK_GroupeItemAE IS NULL)
            ),
        CONSTRAINT CK_CLASSEMENT_AE_Ordre
            CHECK (OrdreAffichage >= 1)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_CLASSEMENT_AE_VERSION_UB_GROUPE'
      AND object_id = OBJECT_ID(N'dbo.CLASSEMENT_AE')
)
BEGIN
    CREATE UNIQUE INDEX UX_CLASSEMENT_AE_VERSION_UB_GROUPE
        ON dbo.CLASSEMENT_AE (FK_VersionBudgetaire, FK_UniteBudgetaire, FK_GroupeItemAE)
        WHERE TypeLigne = 'GROUPE' AND FK_GroupeItemAE IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_CLASSEMENT_AE_VERSION_UB_ITEM'
      AND object_id = OBJECT_ID(N'dbo.CLASSEMENT_AE')
)
BEGIN
    CREATE UNIQUE INDEX UX_CLASSEMENT_AE_VERSION_UB_ITEM
        ON dbo.CLASSEMENT_AE (FK_VersionBudgetaire, FK_UniteBudgetaire, LibelleItemAE)
        WHERE TypeLigne = 'ITEM' AND LibelleItemAE IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_CLASSEMENT_AE_VERSION_UB_ORDRE'
      AND object_id = OBJECT_ID(N'dbo.CLASSEMENT_AE')
)
BEGIN
    CREATE UNIQUE INDEX UX_CLASSEMENT_AE_VERSION_UB_ORDRE
        ON dbo.CLASSEMENT_AE (FK_VersionBudgetaire, FK_UniteBudgetaire, OrdreAffichage);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CLASSEMENT_AE_VERSION')
    ALTER TABLE dbo.CLASSEMENT_AE
        ADD CONSTRAINT FK_CLASSEMENT_AE_VERSION
        FOREIGN KEY (FK_VersionBudgetaire) REFERENCES dbo.VERSION_BUDGETAIRE (IdVersion);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CLASSEMENT_AE_UB')
    ALTER TABLE dbo.CLASSEMENT_AE
        ADD CONSTRAINT FK_CLASSEMENT_AE_UB
        FOREIGN KEY (FK_UniteBudgetaire) REFERENCES dbo.UNITE_BUDGETAIRE (IdUB);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CLASSEMENT_AE_GROUPE')
    ALTER TABLE dbo.CLASSEMENT_AE
        ADD CONSTRAINT FK_CLASSEMENT_AE_GROUPE
        FOREIGN KEY (FK_GroupeItemAE) REFERENCES dbo.GROUPE_ITEM_AE (IdGroupeItemAE);

COMMIT;
GO
