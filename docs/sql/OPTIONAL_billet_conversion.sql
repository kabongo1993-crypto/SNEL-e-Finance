-- =============================================================================
-- OPTIONAL — Billet de conversion (justification montant payé en FC)
-- Idempotent. Ne supprime aucune donnée.
-- Prérequis : OPTIONAL_demande_paiement.sql
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

IF OBJECT_ID(N'dpm.BILLET_CONVERSION', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.BILLET_CONVERSION
    (
        IdBilletConversion        BIGINT IDENTITY(1,1) NOT NULL,
        FK_DemandePaiement        BIGINT          NOT NULL,
        DateConversion            DATE            NOT NULL,
        DeviseOrigine             VARCHAR(3)      NOT NULL,
        MontantDeviseOrigine      DECIMAL(19,4)   NOT NULL,
        TauxApplique              DECIMAL(19,8)   NOT NULL,
        MontantCdf                DECIMAL(19,4)   NOT NULL,
        FK_TauxChange             BIGINT          NULL,
        DemandeChequeNumero       NVARCHAR(80)    NULL,
        CoursEchangeBanque        NVARCHAR(200)   NULL,
        SoldeAPayerDevise         DECIMAL(19,4)   NULL,
        Statut                    VARCHAR(20)     NOT NULL,
        FK_UtilisateurEtabli      BIGINT          NOT NULL,
        DateEtabli                DATETIME2       NOT NULL,
        FK_UtilisateurApprouve    BIGINT          NULL,
        FK_UtilisateurVisa        BIGINT          NULL,
        FK_UtilisateurModification BIGINT         NULL,
        DateModification          DATETIME2       NULL,
        CONSTRAINT PK_DPM_BILLET_CONVERSION PRIMARY KEY (IdBilletConversion),
        CONSTRAINT CK_DPM_BILLET_Statut CHECK (Statut IN ('ETABLI')),
        CONSTRAINT CK_DPM_BILLET_DeviseOrigine CHECK (LEN(DeviseOrigine) = 3),
        CONSTRAINT CK_DPM_BILLET_MontantPositif CHECK (MontantDeviseOrigine > 0 AND MontantCdf >= 0),
        CONSTRAINT CK_DPM_BILLET_TauxPositif CHECK (TauxApplique > 0)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_BILLET_DEMande'
      AND object_id = OBJECT_ID(N'dpm.BILLET_CONVERSION')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_BILLET_DEMande
        ON dpm.BILLET_CONVERSION (FK_DemandePaiement);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_BILLET_DEMANDE')
    ALTER TABLE dpm.BILLET_CONVERSION
        ADD CONSTRAINT FK_DPM_BILLET_DEMANDE
        FOREIGN KEY (FK_DemandePaiement) REFERENCES dpm.DEMANDE_PAIEMENT (IdDemandePaiement);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_BILLET_TAUX')
    ALTER TABLE dpm.BILLET_CONVERSION
        ADD CONSTRAINT FK_DPM_BILLET_TAUX
        FOREIGN KEY (FK_TauxChange) REFERENCES dpm.TAUX_CHANGE (IdTauxChange);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_BILLET_ETABLI')
    ALTER TABLE dpm.BILLET_CONVERSION
        ADD CONSTRAINT FK_DPM_BILLET_ETABLI
        FOREIGN KEY (FK_UtilisateurEtabli) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_BILLET_APPROUVE')
    ALTER TABLE dpm.BILLET_CONVERSION
        ADD CONSTRAINT FK_DPM_BILLET_APPROUVE
        FOREIGN KEY (FK_UtilisateurApprouve) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_BILLET_VISA')
    ALTER TABLE dpm.BILLET_CONVERSION
        ADD CONSTRAINT FK_DPM_BILLET_VISA
        FOREIGN KEY (FK_UtilisateurVisa) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_BILLET_MODIF')
    ALTER TABLE dpm.BILLET_CONVERSION
        ADD CONSTRAINT FK_DPM_BILLET_MODIF
        FOREIGN KEY (FK_UtilisateurModification) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

COMMIT;
