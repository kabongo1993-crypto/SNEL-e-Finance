-- =============================================================================
-- OPTIONAL — Référentiel DEMANDEUR (DPM) + rattachement à DEMANDE_PAIEMENT
-- Schéma : dpm
-- Idempotent. Ne supprime aucune donnée.
-- Prérequis : docs/sql/OPTIONAL_demande_paiement.sql déjà appliqué.
--
-- Règle métier SNEL :
--   Un demandeur est rattaché à UNE SEULE UB (N Demandeur → 1 UB).
--   À la création DPM : sélection Demandeur → UB automatique.
--   Montant sollicité + Devise à la création ;
--   Type budget / Mode paiement / Taux / MontantUsd différés (Chargé DPM).
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;

BEGIN TRAN;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'dpm')
    EXEC(N'CREATE SCHEMA dpm AUTHORIZATION dbo;');

-- ---------------------------------------------------------------------------
-- dpm.DEMANDEUR
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDEUR', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.DEMANDEUR
    (
        IdDemandeur                BIGINT IDENTITY(1,1) NOT NULL,
        Code                       VARCHAR(60)   NOT NULL,
        Libelle                    NVARCHAR(200) NOT NULL,
        FK_UniteBudgetaire         BIGINT        NOT NULL,
        Actif                      BIT           NOT NULL CONSTRAINT DF_DPM_DEMANDEUR_Actif DEFAULT (1),
        DateCreation               DATETIME2     NOT NULL CONSTRAINT DF_DPM_DEMANDEUR_DateCreation DEFAULT (SYSUTCDATETIME()),
        DateModification           DATETIME2     NULL,
        FK_UtilisateurCreation     BIGINT        NOT NULL,
        FK_UtilisateurModification BIGINT        NULL,
        CONSTRAINT PK_DPM_DEMANDEUR PRIMARY KEY (IdDemandeur)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_DEMANDEUR_Code'
      AND object_id = OBJECT_ID(N'dpm.DEMANDEUR')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_DEMANDEUR_Code
        ON dpm.DEMANDEUR (Code);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_DEMANDEUR_UB'
      AND object_id = OBJECT_ID(N'dpm.DEMANDEUR')
)
BEGIN
    CREATE INDEX IX_DPM_DEMANDEUR_UB
        ON dpm.DEMANDEUR (FK_UniteBudgetaire, Actif);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DEMANDEUR_UB')
    ALTER TABLE dpm.DEMANDEUR
        ADD CONSTRAINT FK_DPM_DEMANDEUR_UB
        FOREIGN KEY (FK_UniteBudgetaire) REFERENCES dbo.UNITE_BUDGETAIRE (IdUB);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DEMANDEUR_USER_CREATION')
    ALTER TABLE dpm.DEMANDEUR
        ADD CONSTRAINT FK_DPM_DEMANDEUR_USER_CREATION
        FOREIGN KEY (FK_UtilisateurCreation) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DEMANDEUR_USER_MODIF')
    ALTER TABLE dpm.DEMANDEUR
        ADD CONSTRAINT FK_DPM_DEMANDEUR_USER_MODIF
        FOREIGN KEY (FK_UtilisateurModification) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

-- ---------------------------------------------------------------------------
-- dpm.DEMANDE_PAIEMENT — FK_Demandeur (nullable tant que données non migrées)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT', N'U') IS NOT NULL
   AND COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'FK_Demandeur') IS NULL
BEGIN
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD FK_Demandeur BIGINT NULL;
END;

IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_DPM_DP_Demandeur'
          AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
   )
BEGIN
    CREATE INDEX IX_DPM_DP_Demandeur
        ON dpm.DEMANDE_PAIEMENT (FK_Demandeur);
END;

IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT', N'U') IS NOT NULL
   AND OBJECT_ID(N'dpm.DEMANDEUR', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_DEMANDEUR')
BEGIN
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_DEMANDEUR
        FOREIGN KEY (FK_Demandeur) REFERENCES dpm.DEMANDEUR (IdDemandeur);
END;

-- ---------------------------------------------------------------------------
-- Assouplir en-tête DPM initiale :
-- Type budget / Mode / Taux / MontantUsd différés (Chargé DPM).
-- MontantBrut + Devise (montant sollicité) restent obligatoires.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT', N'U') IS NOT NULL
BEGIN
    -- FK_TypeBudget → NULL
    IF EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
          AND name = N'FK_TypeBudget'
          AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE dpm.DEMANDE_PAIEMENT ALTER COLUMN FK_TypeBudget BIGINT NULL;
    END;

    -- ModePaiementSollicite → NULL (+ CHECK tolère NULL)
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_ModePaiement')
        ALTER TABLE dpm.DEMANDE_PAIEMENT DROP CONSTRAINT CK_DPM_DP_ModePaiement;

    IF EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
          AND name = N'ModePaiementSollicite'
          AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE dpm.DEMANDE_PAIEMENT ALTER COLUMN ModePaiementSollicite VARCHAR(10) NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_ModePaiement')
        ALTER TABLE dpm.DEMANDE_PAIEMENT
            ADD CONSTRAINT CK_DPM_DP_ModePaiement
            CHECK (ModePaiementSollicite IS NULL OR ModePaiementSollicite IN ('CAISSE', 'BANQUE'));

    -- TauxConversion / MontantUsd → NULL (+ CHECKS conditionnels)
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_TauxPositif')
        ALTER TABLE dpm.DEMANDE_PAIEMENT DROP CONSTRAINT CK_DPM_DP_TauxPositif;
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_MontantUsd')
        ALTER TABLE dpm.DEMANDE_PAIEMENT DROP CONSTRAINT CK_DPM_DP_MontantUsd;
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_MontantUsdCoherent')
        ALTER TABLE dpm.DEMANDE_PAIEMENT DROP CONSTRAINT CK_DPM_DP_MontantUsdCoherent;

    IF EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
          AND name = N'TauxConversion'
          AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE dpm.DEMANDE_PAIEMENT ALTER COLUMN TauxConversion DECIMAL(19,8) NULL;
    END;

    IF EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
          AND name = N'MontantUsd'
          AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE dpm.DEMANDE_PAIEMENT ALTER COLUMN MontantUsd DECIMAL(19,4) NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_TauxPositif')
        ALTER TABLE dpm.DEMANDE_PAIEMENT
            ADD CONSTRAINT CK_DPM_DP_TauxPositif
            CHECK (TauxConversion IS NULL OR TauxConversion > 0);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_MontantUsd')
        ALTER TABLE dpm.DEMANDE_PAIEMENT
            ADD CONSTRAINT CK_DPM_DP_MontantUsd
            CHECK (MontantUsd IS NULL OR MontantUsd >= 0);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_DP_MontantUsdCoherent')
        ALTER TABLE dpm.DEMANDE_PAIEMENT
            ADD CONSTRAINT CK_DPM_DP_MontantUsdCoherent
            CHECK (
                TauxConversion IS NULL
                OR MontantUsd IS NULL
                OR ABS(MontantUsd - (MontantBrut / TauxConversion)) < 0.01
            );
END;

COMMIT TRAN;
PRINT N'OPTIONAL_demandeur_dpm.sql : OK';
