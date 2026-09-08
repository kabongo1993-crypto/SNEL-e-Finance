-- =============================================================================
-- OPTIONAL — Traçabilité workflow VERSION_BUDGETAIRE
-- Base : BD_SNEL (map-only EF, pas de migration EF)
-- Idempotent : colonnes ajoutées seulement si absentes.
-- Ne supprime aucune donnée.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

IF COL_LENGTH('dbo.VERSION_BUDGETAIRE', 'FK_UtilisateurSoumission') IS NULL
    ALTER TABLE dbo.VERSION_BUDGETAIRE ADD FK_UtilisateurSoumission BIGINT NULL;

IF COL_LENGTH('dbo.VERSION_BUDGETAIRE', 'DateSoumission') IS NULL
    ALTER TABLE dbo.VERSION_BUDGETAIRE ADD DateSoumission DATETIME2 NULL;

IF COL_LENGTH('dbo.VERSION_BUDGETAIRE', 'FK_UtilisateurControle') IS NULL
    ALTER TABLE dbo.VERSION_BUDGETAIRE ADD FK_UtilisateurControle BIGINT NULL;

IF COL_LENGTH('dbo.VERSION_BUDGETAIRE', 'DateControle') IS NULL
    ALTER TABLE dbo.VERSION_BUDGETAIRE ADD DateControle DATETIME2 NULL;

IF COL_LENGTH('dbo.VERSION_BUDGETAIRE', 'FK_UtilisateurRejet') IS NULL
    ALTER TABLE dbo.VERSION_BUDGETAIRE ADD FK_UtilisateurRejet BIGINT NULL;

IF COL_LENGTH('dbo.VERSION_BUDGETAIRE', 'DateRejet') IS NULL
    ALTER TABLE dbo.VERSION_BUDGETAIRE ADD DateRejet DATETIME2 NULL;

IF COL_LENGTH('dbo.VERSION_BUDGETAIRE', 'MotifRejet') IS NULL
    ALTER TABLE dbo.VERSION_BUDGETAIRE ADD MotifRejet NVARCHAR(1000) NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VERSION_UTILISATEUR_SOUMISSION'
)
    ALTER TABLE dbo.VERSION_BUDGETAIRE
        ADD CONSTRAINT FK_VERSION_UTILISATEUR_SOUMISSION
        FOREIGN KEY (FK_UtilisateurSoumission) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VERSION_UTILISATEUR_CONTROLE'
)
    ALTER TABLE dbo.VERSION_BUDGETAIRE
        ADD CONSTRAINT FK_VERSION_UTILISATEUR_CONTROLE
        FOREIGN KEY (FK_UtilisateurControle) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VERSION_UTILISATEUR_REJET'
)
    ALTER TABLE dbo.VERSION_BUDGETAIRE
        ADD CONSTRAINT FK_VERSION_UTILISATEUR_REJET
        FOREIGN KEY (FK_UtilisateurRejet) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

COMMIT TRAN;
GO
