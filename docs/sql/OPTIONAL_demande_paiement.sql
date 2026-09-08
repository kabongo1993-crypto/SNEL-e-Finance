-- =============================================================================
-- OPTIONAL — Module Demande de Paiement (DPM) — phase Visa budgétaire
-- Schéma : dpm
-- Base   : BD_SNEL (map-only EF)
-- Idempotent : schéma / tables / contraintes / index / seeds référentiels.
-- Ne modifie PAS les tables budgétaires dbo existantes.
-- Périmètre : BROUILLON → VISEE_BUDGETAIREMENT (hors Trésorerie/Comptabilité).
--
-- Complément référentiel Demandeur + assouplissement en-tête initiale :
--   docs/sql/OPTIONAL_demandeur_dpm.sql
--
-- -----------------------------------------------------------------------------
-- RÈGLES GARANTIES EN SQL (CHECK / FK / index)
-- -----------------------------------------------------------------------------
-- Imputation — structure de champs (CK_DPM_IMPUT_Discriminant) :
--   Forme DC : Mois + FK_RubriqueBudgetaire renseignés ;
--              LibelleItemAE, FK_GroupeItemAE, FK_ItemBI, DetailBI = NULL.
--   Forme AE : LibelleItemAE + FK_RubriqueBudgetaire renseignés ;
--              Mois, FK_ItemBI, DetailBI = NULL ;
--              FK_GroupeItemAE facultatif (NULL autorisé).
--   Forme BI : FK_ItemBI + DetailBI renseignés ;
--              FK_RubriqueBudgetaire, LibelleItemAE, FK_GroupeItemAE, Mois = NULL.
-- Montants / taux / devise / mois 1-12 / statuts phase 1 / mode CAISSE|BANQUE.
-- Cohérence MontantUsd ≈ MontantBrut / TauxConversion (tolérance 0.01) si taux renseigné.
-- FK_BudgetLigne NULLABLE → dbo.PREVISION_BUDGETAIRE (imputation sans prévision OK).
-- Unicité taux ACTIF (DeviseSource, DeviseCible, DateEffet).
-- Demandeur N→1 UB (dpm.DEMANDEUR) — voir OPTIONAL_demandeur_dpm.sql.
--
-- -----------------------------------------------------------------------------
-- RÈGLES LAISSÉES AU SERVICE APPLICATIF (pas de CHECK SQL Server)
-- -----------------------------------------------------------------------------
-- FK_TypeBudget.CodeType ∈ {DC,AE,BI} cohérent avec la forme de champs ci-dessus :
--   SQL Server CHECK ne peut pas joindre dbo.TYPE_BUDGET ; pas de trigger.
-- Clés métier d'agrégation (UB + année + discriminants DC/AE/BI).
-- Contrôle crédit disponible (DC mensuel+annuel ; AE/BI annuel).
-- Engagement budgétaire uniquement au statut VISEE_BUDGETAIREMENT.
-- Sélection de FK_VersionBudgetaire.
-- UB de la DPM = UB du Demandeur (cohérence imposée en Application).
--
-- -----------------------------------------------------------------------------
-- Engt_Encours (historique IMPUTATION.xlsx) — interprétation Budget Web
-- -----------------------------------------------------------------------------
-- Pas de colonne EncoursPipeline / EngagementEnCours persistée.
-- Historiquement Engt_Encours ≈ montant USD de la ligne en cours de saisie.
-- Dans Budget Web :
--   BROUILLON / SOUMISE / RECEPTIONNEE_BUDGETS / EN_CONTROLE_BUDGETAIRE / A_CORRIGER
--     → aucun engagement (0) ; le montant en contrôle = MontantUsd de l'imputation.
--   VISEE_BUDGETAIREMENT
--     → engagement ; le snapshot au visa fige budget, crédit engagé, disponible,
--       prévision, écart, montants/devise/taux pour reconstitution a posteriori.
-- Au contrôle, EngagementEnCours (calculé, non persisté) = MontantUsd de la ligne
--   (ou 0 hors contexte de saisie) ; CreditEngage* = sommes des imputations déjà
--   visées sur la même clé budgétaire.
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

-- ---------------------------------------------------------------------------
-- Schéma dpm
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'dpm')
    EXEC(N'CREATE SCHEMA dpm AUTHORIZATION dbo;');

-- ---------------------------------------------------------------------------
-- dpm.CAS_DOSSIER
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.CAS_DOSSIER', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.CAS_DOSSIER
    (
        IdCasDossier BIGINT IDENTITY(1,1) NOT NULL,
        Code         VARCHAR(40)  NOT NULL,
        Libelle      NVARCHAR(200) NOT NULL,
        Ordre        INT          NOT NULL,
        Actif        BIT          NOT NULL CONSTRAINT DF_DPM_CAS_DOSSIER_Actif DEFAULT (1),
        CONSTRAINT PK_DPM_CAS_DOSSIER PRIMARY KEY (IdCasDossier),
        CONSTRAINT CK_DPM_CAS_DOSSIER_Ordre CHECK (Ordre >= 1)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_CAS_DOSSIER_Code'
      AND object_id = OBJECT_ID(N'dpm.CAS_DOSSIER')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_CAS_DOSSIER_Code
        ON dpm.CAS_DOSSIER (Code);
END;

-- ---------------------------------------------------------------------------
-- dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE
    (
        IdPieceObligatoire BIGINT IDENTITY(1,1) NOT NULL,
        FK_CasDossier      BIGINT       NOT NULL,
        CodeTypePiece      VARCHAR(60)  NOT NULL,
        Libelle            NVARCHAR(300) NOT NULL,
        Ordre              INT          NOT NULL,
        Actif              BIT          NOT NULL CONSTRAINT DF_DPM_CAS_PIECE_Actif DEFAULT (1),
        Obligatoire        BIT          NOT NULL CONSTRAINT DF_DPM_CAS_PIECE_Obligatoire DEFAULT (1),
        CONSTRAINT PK_DPM_CAS_DOSSIER_PIECE_OBLIGATOIRE PRIMARY KEY (IdPieceObligatoire),
        CONSTRAINT CK_DPM_CAS_PIECE_Ordre CHECK (Ordre >= 1),
        CONSTRAINT CK_DPM_CAS_PIECE_Obligatoire_Implique_Actif CHECK (Obligatoire = 0 OR Actif = 1)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_CAS_PIECE_Cas_Code'
      AND object_id = OBJECT_ID(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_CAS_PIECE_Cas_Code
        ON dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE (FK_CasDossier, CodeTypePiece);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_CAS_PIECE_CAS_DOSSIER')
    ALTER TABLE dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE
        ADD CONSTRAINT FK_DPM_CAS_PIECE_CAS_DOSSIER
        FOREIGN KEY (FK_CasDossier) REFERENCES dpm.CAS_DOSSIER (IdCasDossier);

-- ---------------------------------------------------------------------------
-- dpm.PAIRE_TAUX_CHANGE (registre orientation canonique — non figé en code)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.PAIRE_TAUX_CHANGE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.PAIRE_TAUX_CHANGE
    (
        IdPaireTauxChange BIGINT IDENTITY(1,1) NOT NULL,
        DeviseBase        VARCHAR(3) NOT NULL,
        DeviseQuote       VARCHAR(3) NOT NULL,
        Actif             BIT        NOT NULL CONSTRAINT DF_DPM_PAIRE_TAUX_Actif DEFAULT (1),
        DateCreation      DATETIME2  NOT NULL CONSTRAINT DF_DPM_PAIRE_TAUX_DateCreation DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DPM_PAIRE_TAUX_CHANGE PRIMARY KEY (IdPaireTauxChange),
        CONSTRAINT CK_DPM_PAIRE_TAUX_Devise_Distinctes CHECK (DeviseBase <> DeviseQuote)
    );

    CREATE UNIQUE INDEX UX_DPM_PAIRE_TAUX_DeviseBase_Quote
        ON dpm.PAIRE_TAUX_CHANGE (DeviseBase, DeviseQuote);
END;

-- ---------------------------------------------------------------------------
-- dpm.TAUX_CHANGE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.TAUX_CHANGE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.TAUX_CHANGE
    (
        IdTauxChange             BIGINT IDENTITY(1,1) NOT NULL,
        DeviseSource             VARCHAR(3)  NOT NULL,
        DeviseCible              VARCHAR(3)  NOT NULL CONSTRAINT DF_DPM_TAUX_DeviseCible DEFAULT ('USD'),
        Taux                     DECIMAL(19,8) NOT NULL,
        DateEffet                DATE        NOT NULL,
        Statut                   VARCHAR(20) NOT NULL,
        FK_UtilisateurCreation   BIGINT      NOT NULL,
        DateCreation             DATETIME2   NOT NULL CONSTRAINT DF_DPM_TAUX_DateCreation DEFAULT (SYSUTCDATETIME()),
        FK_UtilisateurModification BIGINT    NULL,
        DateModification         DATETIME2   NULL,
        CONSTRAINT PK_DPM_TAUX_CHANGE PRIMARY KEY (IdTauxChange),
        CONSTRAINT CK_DPM_TAUX_TauxPositif CHECK (Taux > 0),
        CONSTRAINT CK_DPM_TAUX_Statut CHECK (Statut IN ('ACTIF', 'INACTIF')),
        CONSTRAINT CK_DPM_TAUX_DeviseSource CHECK (LEN(DeviseSource) = 3),
        CONSTRAINT CK_DPM_TAUX_DeviseCible CHECK (LEN(DeviseCible) = 3)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_TAUX_Devise_Date'
      AND object_id = OBJECT_ID(N'dpm.TAUX_CHANGE')
)
BEGIN
    CREATE INDEX IX_DPM_TAUX_Devise_Date
        ON dpm.TAUX_CHANGE (DeviseSource, DeviseCible, DateEffet DESC, Statut);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_TAUX_ACTIF_Devise_Date'
      AND object_id = OBJECT_ID(N'dpm.TAUX_CHANGE')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_TAUX_ACTIF_Devise_Date
        ON dpm.TAUX_CHANGE (DeviseSource, DeviseCible, DateEffet)
        WHERE Statut = 'ACTIF';
END;

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

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_TAUX_USER_CREATION')
    ALTER TABLE dpm.TAUX_CHANGE
        ADD CONSTRAINT FK_DPM_TAUX_USER_CREATION
        FOREIGN KEY (FK_UtilisateurCreation) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_TAUX_USER_MODIF')
    ALTER TABLE dpm.TAUX_CHANGE
        ADD CONSTRAINT FK_DPM_TAUX_USER_MODIF
        FOREIGN KEY (FK_UtilisateurModification) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

-- ---------------------------------------------------------------------------
-- dpm.DEMANDE_PAIEMENT
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.DEMANDE_PAIEMENT
    (
        IdDemandePaiement        BIGINT IDENTITY(1,1) NOT NULL,
        Reference                VARCHAR(40)   NOT NULL,
        DateEmission             DATE          NOT NULL,
        LieuEmission             NVARCHAR(100) NULL,
        FK_ExerciceBudgetaire    BIGINT        NOT NULL,
        FK_VersionBudgetaire     BIGINT        NULL,
        FK_UniteBudgetaire       BIGINT        NOT NULL,
        FK_Demandeur             BIGINT        NULL,
        FK_CasDossier            BIGINT        NOT NULL,
        FK_TypeBudget            BIGINT        NULL,
        Objet                    NVARCHAR(1000) NOT NULL,
        CompteSection            VARCHAR(20)   NULL,
        MontantBrut              DECIMAL(19,4) NOT NULL,
        Devise                   VARCHAR(3)    NOT NULL,
        TauxConversion           DECIMAL(19,8) NULL,
        MontantUsd               DECIMAL(19,4) NULL,
        FK_TauxChange            BIGINT        NULL,
        ModePaiementSollicite    VARCHAR(10)   NULL,
        Statut                   VARCHAR(30)   NOT NULL,
        MotifRetour              NVARCHAR(500) NULL,
        CommentaireRetour        NVARCHAR(2000) NULL,
        FK_UtilisateurCreation   BIGINT        NOT NULL,
        DateCreation             DATETIME2     NOT NULL CONSTRAINT DF_DPM_DP_DateCreation DEFAULT (SYSUTCDATETIME()),
        FK_UtilisateurModification BIGINT      NULL,
        DateModification         DATETIME2     NULL,
        FK_UtilisateurSoumission BIGINT        NULL,
        DateSoumission           DATETIME2     NULL,
        FK_UtilisateurReception  BIGINT        NULL,
        DateReception            DATETIME2     NULL,
        FK_UtilisateurControle   BIGINT        NULL,
        DateControle             DATETIME2     NULL,
        FK_UtilisateurVisa       BIGINT        NULL,
        DateVisa                 DATETIME2     NULL,
        FK_UtilisateurRetour     BIGINT        NULL,
        DateRetour               DATETIME2     NULL,
        CONSTRAINT PK_DPM_DEMANDE_PAIEMENT PRIMARY KEY (IdDemandePaiement),
        CONSTRAINT CK_DPM_DP_MontantBrut CHECK (MontantBrut >= 0),
        CONSTRAINT CK_DPM_DP_TauxPositif CHECK (TauxConversion IS NULL OR TauxConversion > 0),
        CONSTRAINT CK_DPM_DP_MontantUsd CHECK (MontantUsd IS NULL OR MontantUsd >= 0),
        CONSTRAINT CK_DPM_DP_MontantUsdCoherent CHECK (
            TauxConversion IS NULL
            OR MontantUsd IS NULL
            OR ABS(MontantUsd - (MontantBrut / TauxConversion)) < 0.01),
        CONSTRAINT CK_DPM_DP_Devise CHECK (LEN(Devise) = 3),
        CONSTRAINT CK_DPM_DP_ModePaiement CHECK (
            ModePaiementSollicite IS NULL OR ModePaiementSollicite IN ('CAISSE', 'BANQUE')),
        CONSTRAINT CK_DPM_DP_Statut CHECK (Statut IN (
            'BROUILLON', 'SOUMISE', 'RECEPTIONNEE_BUDGETS',
            'EN_CONTROLE_BUDGETAIRE', 'A_CORRIGER', 'VISEE_BUDGETAIREMENT'))
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_DP_Reference'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_DP_Reference
        ON dpm.DEMANDE_PAIEMENT (Reference);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_DP_Statut_DateSoumission'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
)
BEGIN
    CREATE INDEX IX_DPM_DP_Statut_DateSoumission
        ON dpm.DEMANDE_PAIEMENT (Statut, DateSoumission DESC);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_DP_UB_Statut'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
)
BEGIN
    CREATE INDEX IX_DPM_DP_UB_Statut
        ON dpm.DEMANDE_PAIEMENT (FK_UniteBudgetaire, Statut);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_DP_Exercice_CasDossier'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT')
)
BEGIN
    CREATE INDEX IX_DPM_DP_Exercice_CasDossier
        ON dpm.DEMANDE_PAIEMENT (FK_ExerciceBudgetaire, FK_CasDossier);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_EXERCICE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_EXERCICE
        FOREIGN KEY (FK_ExerciceBudgetaire) REFERENCES dbo.EXERCICE_BUDGETAIRE (IdExercice);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_VERSION')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_VERSION
        FOREIGN KEY (FK_VersionBudgetaire) REFERENCES dbo.VERSION_BUDGETAIRE (IdVersion);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_UB')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_UB
        FOREIGN KEY (FK_UniteBudgetaire) REFERENCES dbo.UNITE_BUDGETAIRE (IdUB);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_CAS_DOSSIER')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_CAS_DOSSIER
        FOREIGN KEY (FK_CasDossier) REFERENCES dpm.CAS_DOSSIER (IdCasDossier);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_TYPE_BUDGET')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_TYPE_BUDGET
        FOREIGN KEY (FK_TypeBudget) REFERENCES dbo.TYPE_BUDGET (IdTypeBudget);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_TAUX_CHANGE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_TAUX_CHANGE
        FOREIGN KEY (FK_TauxChange) REFERENCES dpm.TAUX_CHANGE (IdTauxChange);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_USER_CREATION')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_USER_CREATION
        FOREIGN KEY (FK_UtilisateurCreation) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_USER_MODIF')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_USER_MODIF
        FOREIGN KEY (FK_UtilisateurModification) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_USER_SOUMISSION')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_USER_SOUMISSION
        FOREIGN KEY (FK_UtilisateurSoumission) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_USER_RECEPTION')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_USER_RECEPTION
        FOREIGN KEY (FK_UtilisateurReception) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_USER_CONTROLE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_USER_CONTROLE
        FOREIGN KEY (FK_UtilisateurControle) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_USER_VISA')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_USER_VISA
        FOREIGN KEY (FK_UtilisateurVisa) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_DP_USER_RETOUR')
    ALTER TABLE dpm.DEMANDE_PAIEMENT
        ADD CONSTRAINT FK_DPM_DP_USER_RETOUR
        FOREIGN KEY (FK_UtilisateurRetour) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

-- ---------------------------------------------------------------------------
-- dpm.DEMANDE_PAIEMENT_BENEFICIAIRE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_BENEFICIAIRE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.DEMANDE_PAIEMENT_BENEFICIAIRE
    (
        IdBeneficiaire      BIGINT IDENTITY(1,1) NOT NULL,
        FK_DemandePaiement  BIGINT        NOT NULL,
        TypeBeneficiaire    VARCHAR(20)   NOT NULL,
        NomComplet          NVARCHAR(300) NOT NULL,
        Matricule           VARCHAR(30)   NULL,
        Fonction            NVARCHAR(200) NULL,
        RaisonSociale       NVARCHAR(300) NULL,
        Rccm                VARCHAR(50)   NULL,
        Adresse             NVARCHAR(500) NULL,
        Banque              NVARCHAR(200) NULL,
        NumeroCompte        VARCHAR(50)   NULL,
        EstPrincipal        BIT           NOT NULL CONSTRAINT DF_DPM_BENEF_EstPrincipal DEFAULT (1),
        Ordre               INT           NOT NULL CONSTRAINT DF_DPM_BENEF_Ordre DEFAULT (1),
        CONSTRAINT PK_DPM_DEMANDE_PAIEMENT_BENEFICIAIRE PRIMARY KEY (IdBeneficiaire),
        CONSTRAINT CK_DPM_BENEF_Type CHECK (TypeBeneficiaire IN ('AGENT', 'TIERS', 'DIVERS')),
        CONSTRAINT CK_DPM_BENEF_Ordre CHECK (Ordre >= 1)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_BENEF_DEMANDE'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_BENEFICIAIRE')
)
BEGIN
    CREATE INDEX IX_DPM_BENEF_DEMANDE
        ON dpm.DEMANDE_PAIEMENT_BENEFICIAIRE (FK_DemandePaiement, Ordre);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_BENEF_NomComplet'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_BENEFICIAIRE')
)
BEGIN
    CREATE INDEX IX_DPM_BENEF_NomComplet
        ON dpm.DEMANDE_PAIEMENT_BENEFICIAIRE (NomComplet);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_BENEF_DEMANDE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_BENEFICIAIRE
        ADD CONSTRAINT FK_DPM_BENEF_DEMANDE
        FOREIGN KEY (FK_DemandePaiement) REFERENCES dpm.DEMANDE_PAIEMENT (IdDemandePaiement);

-- ---------------------------------------------------------------------------
-- dpm.DEMANDE_PAIEMENT_IMPUTATION
-- ---------------------------------------------------------------------------
-- LibelleItemAE NVARCHAR(500) : identité de l'Item AE réellement imputé
-- (volontairement texte — aucun référentiel Item AE exploitable aujourd'hui).
-- Ce n'est PAS un libellé décoratif. Ne pas créer de table Item AE ici.
--
-- Structure champs (SQL) vs FK_TypeBudget.CodeType (applicatif) : voir en-tête.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
    (
        IdImputation              BIGINT IDENTITY(1,1) NOT NULL,
        FK_DemandePaiement        BIGINT        NOT NULL,
        Ordre                     INT           NOT NULL,
        FK_TypeBudget             BIGINT        NOT NULL,
        FK_UniteBudgetaire        BIGINT        NOT NULL,
        FK_ExerciceBudgetaire     BIGINT        NOT NULL,
        FK_RubriqueBudgetaire     BIGINT        NULL,
        Mois                      TINYINT       NULL,
        -- Item AE réel de l'imputation (clé métier AE avec UB + RB + année)
        LibelleItemAE             NVARCHAR(500) NULL,
        FK_GroupeItemAE           BIGINT        NULL,
        FK_ItemBI                 BIGINT        NULL,
        DetailBI                  NVARCHAR(1000) NULL,
        -- NULLABLE : l'absence de prévision n'empêche jamais l'enregistrement
        FK_BudgetLigne            BIGINT        NULL,
        MontantBrut               DECIMAL(19,4) NOT NULL,
        Devise                    VARCHAR(3)    NOT NULL,
        TauxConversion            DECIMAL(19,8) NOT NULL,
        MontantUsd                DECIMAL(19,4) NOT NULL,
        NumeroFicheSuivi          INT           NULL,
        FK_UtilisateurCreation    BIGINT        NOT NULL,
        DateImputation            DATETIME2     NOT NULL CONSTRAINT DF_DPM_IMPUT_DateImputation DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DPM_DEMANDE_PAIEMENT_IMPUTATION PRIMARY KEY (IdImputation),
        CONSTRAINT CK_DPM_IMPUT_Ordre CHECK (Ordre >= 1),
        CONSTRAINT CK_DPM_IMPUT_Mois CHECK (Mois IS NULL OR Mois BETWEEN 1 AND 12),
        CONSTRAINT CK_DPM_IMPUT_MontantBrut CHECK (MontantBrut >= 0),
        CONSTRAINT CK_DPM_IMPUT_TauxPositif CHECK (TauxConversion > 0),
        CONSTRAINT CK_DPM_IMPUT_MontantUsd CHECK (MontantUsd >= 0),
        CONSTRAINT CK_DPM_IMPUT_MontantUsdCoherent CHECK (ABS(MontantUsd - (MontantBrut / TauxConversion)) < 0.01),
        CONSTRAINT CK_DPM_IMPUT_Devise CHECK (LEN(Devise) = 3)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_IMPUT_DEMANDE_ORDRE'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION')
)
BEGIN
    CREATE INDEX IX_DPM_IMPUT_DEMANDE_ORDRE
        ON dpm.DEMANDE_PAIEMENT_IMPUTATION (FK_DemandePaiement, Ordre);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_IMPUT_DC_CLE'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION')
)
BEGIN
    CREATE INDEX IX_DPM_IMPUT_DC_CLE
        ON dpm.DEMANDE_PAIEMENT_IMPUTATION (FK_ExerciceBudgetaire, FK_UniteBudgetaire, FK_RubriqueBudgetaire, Mois)
        WHERE Mois IS NOT NULL AND FK_RubriqueBudgetaire IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_IMPUT_AE_CLE'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION')
)
BEGIN
    CREATE INDEX IX_DPM_IMPUT_AE_CLE
        ON dpm.DEMANDE_PAIEMENT_IMPUTATION (FK_ExerciceBudgetaire, FK_UniteBudgetaire, FK_RubriqueBudgetaire, LibelleItemAE, FK_GroupeItemAE)
        WHERE LibelleItemAE IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_IMPUT_BI_CLE'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION')
)
BEGIN
    CREATE INDEX IX_DPM_IMPUT_BI_CLE
        ON dpm.DEMANDE_PAIEMENT_IMPUTATION (FK_ExerciceBudgetaire, FK_UniteBudgetaire, FK_ItemBI, DetailBI)
        WHERE FK_ItemBI IS NOT NULL AND DetailBI IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_IMPUT_BUDGET_LIGNE'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION')
)
BEGIN
    CREATE INDEX IX_DPM_IMPUT_BUDGET_LIGNE
        ON dpm.DEMANDE_PAIEMENT_IMPUTATION (FK_BudgetLigne)
        WHERE FK_BudgetLigne IS NOT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_IMPUT_DEMANDE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT FK_DPM_IMPUT_DEMANDE
        FOREIGN KEY (FK_DemandePaiement) REFERENCES dpm.DEMANDE_PAIEMENT (IdDemandePaiement);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_IMPUT_TYPE_BUDGET')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT FK_DPM_IMPUT_TYPE_BUDGET
        FOREIGN KEY (FK_TypeBudget) REFERENCES dbo.TYPE_BUDGET (IdTypeBudget);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_IMPUT_UB')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT FK_DPM_IMPUT_UB
        FOREIGN KEY (FK_UniteBudgetaire) REFERENCES dbo.UNITE_BUDGETAIRE (IdUB);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_IMPUT_EXERCICE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT FK_DPM_IMPUT_EXERCICE
        FOREIGN KEY (FK_ExerciceBudgetaire) REFERENCES dbo.EXERCICE_BUDGETAIRE (IdExercice);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_IMPUT_RB')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT FK_DPM_IMPUT_RB
        FOREIGN KEY (FK_RubriqueBudgetaire) REFERENCES dbo.RUBRIQUE_BUDGETAIRE (IdRB);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_IMPUT_GROUPE_AE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT FK_DPM_IMPUT_GROUPE_AE
        FOREIGN KEY (FK_GroupeItemAE) REFERENCES dbo.GROUPE_ITEM_AE (IdGroupeItemAE);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_IMPUT_ITEM_BI')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT FK_DPM_IMPUT_ITEM_BI
        FOREIGN KEY (FK_ItemBI) REFERENCES dbo.ITEM_BI (IdItemBI);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_IMPUT_BUDGET_LIGNE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT FK_DPM_IMPUT_BUDGET_LIGNE
        FOREIGN KEY (FK_BudgetLigne) REFERENCES dbo.PREVISION_BUDGETAIRE (IdPrevision);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_IMPUT_USER_CREATION')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT FK_DPM_IMPUT_USER_CREATION
        FOREIGN KEY (FK_UtilisateurCreation) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

-- Discriminants structurels DC / AE / BI (champs incompatibles = NULL).
-- SQL garantit la forme des champs ; PAS l'égalité FK_TypeBudget ↔ CodeType DC/AE/BI
-- (référence externe dbo.TYPE_BUDGET — règle applicative ; pas de trigger).
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_DPM_IMPUT_Discriminant'
      AND parent_object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION')
)
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT CK_DPM_IMPUT_Discriminant CHECK (
            -- Forme DC : UB+RB+Mois (+ année via FK_ExerciceBudgetaire)
            (
                Mois IS NOT NULL
                AND FK_RubriqueBudgetaire IS NOT NULL
                AND LibelleItemAE IS NULL
                AND FK_GroupeItemAE IS NULL
                AND FK_ItemBI IS NULL
                AND DetailBI IS NULL
            )
            OR
            -- Forme AE : UB+RB+LibelleItemAE ; Groupe AE facultatif ; Mois NULL
            (
                LibelleItemAE IS NOT NULL
                AND FK_RubriqueBudgetaire IS NOT NULL
                AND Mois IS NULL
                AND FK_ItemBI IS NULL
                AND DetailBI IS NULL
            )
            OR
            -- Forme BI : UB+ItemBI+DetailBI ; RB/AE/Mois NULL
            (
                FK_ItemBI IS NOT NULL
                AND DetailBI IS NOT NULL
                AND FK_RubriqueBudgetaire IS NULL
                AND Mois IS NULL
                AND LibelleItemAE IS NULL
                AND FK_GroupeItemAE IS NULL
            )
        );

-- Renforce : FK_GroupeItemAE ne peut être renseigné que sur une forme AE.
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_DPM_IMPUT_GroupeAeAeUniquement'
      AND parent_object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION')
)
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION
        ADD CONSTRAINT CK_DPM_IMPUT_GroupeAeAeUniquement CHECK (
            FK_GroupeItemAE IS NULL
            OR (
                LibelleItemAE IS NOT NULL
                AND FK_RubriqueBudgetaire IS NOT NULL
                AND Mois IS NULL
                AND FK_ItemBI IS NULL
                AND DetailBI IS NULL
            )
        );

-- ---------------------------------------------------------------------------
-- dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT
-- ---------------------------------------------------------------------------
-- Créé au moment du visa budgétaire (statut VISEE_BUDGETAIREMENT).
-- Fige la situation budgétaire ayant permis le visa (reconstitution a posteriori).
--
-- Pas de colonne EncoursPipeline / EngagementEnCours :
--   - avant visa : aucun engagement ; au contrôle, « Engt_Encours » calculé =
--     MontantUsd de l'imputation en cours (non persisté) ;
--   - au visa : CreditEngage* = sommes déjà visées sur la clé ; disponibles =
--     budget − engagé − MontantUsd ligne ; champs mensuels NULL pour AE/BI.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT
    (
        IdSnapshot                        BIGINT IDENTITY(1,1) NOT NULL,
        FK_Imputation                     BIGINT        NOT NULL,
        FK_DemandePaiement                BIGINT        NOT NULL,
        DateSnapshot                      DATETIME2     NOT NULL CONSTRAINT DF_DPM_SNAP_DateSnapshot DEFAULT (SYSUTCDATETIME()),
        -- Mensuel : DC uniquement (NULL pour AE/BI)
        BudgetMensuel                     DECIMAL(19,4) NULL,
        CreditEngageMensuel               DECIMAL(19,4) NULL,
        CreditDisponibleMensuelAvantVisa  DECIMAL(19,4) NULL,
        -- Annuel : DC + AE + BI
        BudgetAnnuel                      DECIMAL(19,4) NOT NULL,
        CreditEngageAnnuel                DECIMAL(19,4) NOT NULL,
        CreditDisponibleAnnuelAvantVisa   DECIMAL(19,4) NOT NULL,
        MontantPrevision                  DECIMAL(19,4) NOT NULL,
        EcartPrevisionImputation          DECIMAL(19,4) NOT NULL,
        MontantBrut                       DECIMAL(19,4) NOT NULL,
        Devise                            VARCHAR(3)    NOT NULL,
        TauxConversion                    DECIMAL(19,8) NOT NULL,
        MontantUsd                        DECIMAL(19,4) NOT NULL,
        FK_BudgetLigne                    BIGINT        NULL,
        CONSTRAINT PK_DPM_IMPUTATION_SNAPSHOT PRIMARY KEY (IdSnapshot),
        CONSTRAINT CK_DPM_SNAP_Devise CHECK (LEN(Devise) = 3)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_SNAP_IMPUTATION'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT')
)
BEGIN
    CREATE INDEX IX_DPM_SNAP_IMPUTATION
        ON dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT (FK_Imputation, DateSnapshot DESC);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_SNAP_DEMANDE'
      AND object_id = OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT')
)
BEGIN
    CREATE INDEX IX_DPM_SNAP_DEMANDE
        ON dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT (FK_DemandePaiement, DateSnapshot DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_SNAP_IMPUTATION')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT
        ADD CONSTRAINT FK_DPM_SNAP_IMPUTATION
        FOREIGN KEY (FK_Imputation) REFERENCES dpm.DEMANDE_PAIEMENT_IMPUTATION (IdImputation);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_SNAP_DEMANDE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT
        ADD CONSTRAINT FK_DPM_SNAP_DEMANDE
        FOREIGN KEY (FK_DemandePaiement) REFERENCES dpm.DEMANDE_PAIEMENT (IdDemandePaiement);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_SNAP_BUDGET_LIGNE')
    ALTER TABLE dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT
        ADD CONSTRAINT FK_DPM_SNAP_BUDGET_LIGNE
        FOREIGN KEY (FK_BudgetLigne) REFERENCES dbo.PREVISION_BUDGETAIRE (IdPrevision);

-- ---------------------------------------------------------------------------
-- dpm.PIECE_JOINTE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.PIECE_JOINTE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.PIECE_JOINTE
    (
        IdPieceJointe       BIGINT IDENTITY(1,1) NOT NULL,
        FK_DemandePaiement  BIGINT        NOT NULL,
        FK_PieceObligatoire BIGINT        NULL,
        CodeTypePiece       VARCHAR(60)   NOT NULL,
        Libelle             NVARCHAR(300) NOT NULL,
        EstObligatoire      BIT           NOT NULL CONSTRAINT DF_DPM_PJ_EstObligatoire DEFAULT (0),
        NomFichierOriginal  NVARCHAR(260) NOT NULL,
        CheminRelatif       NVARCHAR(500) NOT NULL,
        HashSha256          CHAR(64)      NOT NULL,
        TailleOctets        BIGINT        NOT NULL,
        FK_Utilisateur      BIGINT        NOT NULL,
        DateUpload          DATETIME2     NOT NULL CONSTRAINT DF_DPM_PJ_DateUpload DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DPM_PIECE_JOINTE PRIMARY KEY (IdPieceJointe),
        CONSTRAINT CK_DPM_PJ_Taille CHECK (TailleOctets >= 0)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_PJ_DEMANDE'
      AND object_id = OBJECT_ID(N'dpm.PIECE_JOINTE')
)
BEGIN
    CREATE INDEX IX_DPM_PJ_DEMANDE
        ON dpm.PIECE_JOINTE (FK_DemandePaiement);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DPM_PJ_PIECE_OBLIGATOIRE'
      AND object_id = OBJECT_ID(N'dpm.PIECE_JOINTE')
)
BEGIN
    CREATE INDEX IX_DPM_PJ_PIECE_OBLIGATOIRE
        ON dpm.PIECE_JOINTE (FK_PieceObligatoire)
        WHERE FK_PieceObligatoire IS NOT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PJ_DEMANDE')
    ALTER TABLE dpm.PIECE_JOINTE
        ADD CONSTRAINT FK_DPM_PJ_DEMANDE
        FOREIGN KEY (FK_DemandePaiement) REFERENCES dpm.DEMANDE_PAIEMENT (IdDemandePaiement);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PJ_PIECE_OBLIGATOIRE')
    ALTER TABLE dpm.PIECE_JOINTE
        ADD CONSTRAINT FK_DPM_PJ_PIECE_OBLIGATOIRE
        FOREIGN KEY (FK_PieceObligatoire) REFERENCES dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE (IdPieceObligatoire);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PJ_UTILISATEUR')
    ALTER TABLE dpm.PIECE_JOINTE
        ADD CONSTRAINT FK_DPM_PJ_UTILISATEUR
        FOREIGN KEY (FK_Utilisateur) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

-- ---------------------------------------------------------------------------
-- Seeds idempotents — CAS_DOSSIER (Annexe 1 IGCF)
-- ---------------------------------------------------------------------------
;WITH CasSeed AS (
    SELECT * FROM (VALUES
        (1,  'FRAIS_MISSION',       N'Frais de mission',                                      1),
        (2,  'REMBOURSEMENTS_CAISSE', N'Remboursements frais divers par caisse',             1),
        (3,  'FORMATION',           N'Formation',                                              1),
        (4,  'COLLATION',           N'Collation',                                              1),
        (5,  'PRETS_SOCIAUX',       N'Pr' + NCHAR(234) + N'ts sociaux',                        1),
        (6,  'DECES',               N'D' + NCHAR(233) + N'c' + NCHAR(232) + N's',              1),
        (7,  'SALAIRES',            N'Salaires',                                               1),
        (8,  'SALAIRE_ATTENTE',     N'Salaire d''attente',                                     1),
        (9,  'CONGE_NON_PRIS',      N'Cong' + NCHAR(233) + N' non pris',                       1),
        (10, 'DECOMPTE_FINAL',      N'D' + NCHAR(233) + N'compte final',                       1),
        (11, 'IMPOTS_TAXES',        N'Imp' + NCHAR(244) + N'ts ou taxes',                      1),
        (12, 'TREIZIEME_MOIS',      N'Treizi' + NCHAR(232) + N'me mois',                       1),
        (13, 'VIVRES_FIN_ANNEE',    N'Vivres fin d''ann' + NCHAR(233) + N'e',                  1),
        (14, 'RETROCESSION',        N'R' + NCHAR(233) + N'trocession',                         1),
        (15, 'FONDS_A_JUSTIFIER',   N'Fonds ' + NCHAR(224) + N' justifier pour diverses interventions', 1)
    ) AS v(Ordre, Code, Libelle, Actif)
)
INSERT INTO dpm.CAS_DOSSIER (Code, Libelle, Ordre, Actif)
SELECT s.Code, s.Libelle, s.Ordre, s.Actif
FROM CasSeed s
WHERE NOT EXISTS (
    SELECT 1 FROM dpm.CAS_DOSSIER c WHERE c.Code = s.Code
);

-- ---------------------------------------------------------------------------
-- Seeds idempotents — PIÈCES OBLIGATOIRES (Annexe 1 IGCF)
-- ---------------------------------------------------------------------------
;WITH PieceSeed AS (
    SELECT * FROM (VALUES
        ('FRAIS_MISSION',       'ORDRE_MISSION',              N'Copie de l''ordre de mission',                              1),
        ('FRAIS_MISSION',       'FEUILLE_CALCUL_DRH',         N'Feuille de calcul DRH',                                     2),
        ('REMBOURSEMENTS_CAISSE', 'ORDRE_MISSION',            N'Copie de l''ordre de mission',                              1),
        ('REMBOURSEMENTS_CAISSE', 'FACTURE_APPROUVEE',        N'Facture approuvée',                                         2),
        ('REMBOURSEMENTS_CAISSE', 'DECLARATION_CREANCE',      N'Déclaration de créance approuvée',                          3),
        ('FORMATION',           'LISTE_PRESENCE',             N'Liste de présence signée',                                  1),
        ('FORMATION',           'FACTURE_APPROUVEE',          N'Facture approuvée',                                         2),
        ('COLLATION',           'LISTE_PRESENCE',             N'Liste de présence signée',                                  1),
        ('COLLATION',           'REQUISITION_MEMO',           N'Réquisition ou mémo',                                       2),
        ('COLLATION',           'FEUILLE_CALCUL_DRH',         N'Feuille de calcul DRH',                                     3),
        ('COLLATION',           'ORDRE_MISSION',              N'Copie de l''ordre de mission',                              4),
        ('PRETS_SOCIAUX',       'LETTRE_DEMANDE_PRET',        N'Lettre de demande de prêt de l''agent avec annexes requises', 1),
        ('DECES',               'CERTIFICAT_DECES',           N'Certificat de décès',                                       1),
        ('DECES',               'FICHE_INDIVIDUELLE_AGENT',   N'Fiche individuelle de l''agent',                            2),
        ('DECES',               'FEUILLE_CALCUL_DRH',         N'Feuille de calcul DRH',                                     3),
        ('SALAIRES',            'LISTING_PAIE',               N'Listing de paie',                                           1),
        ('SALAIRES',            'ETAT_PAIE',                  N'Etat de paie',                                              2),
        ('SALAIRE_ATTENTE',     'ETAT_PAIE',                  N'Etat de paie',                                              1),
        ('SALAIRE_ATTENTE',     'EXTRAIT_COMPTE_BANCAIRE',    N'Extrait de compte bancaire',                                2),
        ('CONGE_NON_PRIS',      'ETAT_PAIE_DRH',              N'Etat de paie DRH',                                          1),
        ('CONGE_NON_PRIS',      'LETTRE_CONGE',               N'Lettre de congé',                                           2),
        ('CONGE_NON_PRIS',      'JUSTIF_NON_PRISE_CONGE',     N'Justificatif de non prise de congé',                        3),
        ('DECOMPTE_FINAL',      'ETAT_PAIE_DRH',              N'Etat de paie DRH',                                          1),
        ('IMPOTS_TAXES',        'DECLARATION_FISCALE',        N'Déclaration fiscale',                                       1),
        ('IMPOTS_TAXES',        'NOTE_PERCEPTION_AMR',        N'Note de perception / Avis de Mise en Recouvrement AMR (A ou B)', 2),
        ('IMPOTS_TAXES',        'BON_A_PAYER',                N'Bon à payer',                                               3),
        ('TREIZIEME_MOIS',      'LISTING_PAIE',               N'Listing de paie',                                           1),
        ('TREIZIEME_MOIS',      'ETAT_PAIE',                  N'Etat de paie',                                              2),
        ('VIVRES_FIN_ANNEE',    'FACTURE_PROFORMA',           N'Facture pro-forma',                                         1),
        ('VIVRES_FIN_ANNEE',    'BON_COMMANDE',               N'Bon de commande',                                           2),
        ('VIVRES_FIN_ANNEE',    'ETAT_BESOINS_DRH',           N'Etat des besoins DRH',                                      3),
        ('RETROCESSION',        'MSG_AUTORISATION_PRELEVEMENT', N'Message de demande d''autorisation de prélèvement sur les recettes DG', 1),
        ('RETROCESSION',        'MSG_DEMANDE_RETROCESSION',   N'Message de demande de rétrocession approuvé',               2),
        ('RETROCESSION',        'ETAT_BESOINS_TECHNIQUE',     N'Etat des besoins (technique)',                              3),
        ('FONDS_A_JUSTIFIER',   'FACTURE_PROFORMA',           N'Facture pro-forma',                                         1),
        ('FONDS_A_JUSTIFIER',   'DEVIS',                      N'Devis',                                                     2),
        ('FONDS_A_JUSTIFIER',   'MEMO_APPROUVE',              N'Mémo approuvé',                                             3),
        ('FONDS_A_JUSTIFIER',   'ETAT_BESOINS',               N'Etat des besoins',                                          4)
    ) AS v(CasCode, CodeTypePiece, Libelle, Ordre)
)
INSERT INTO dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE (FK_CasDossier, CodeTypePiece, Libelle, Ordre)
SELECT c.IdCasDossier, p.CodeTypePiece, p.Libelle, p.Ordre
FROM PieceSeed p
INNER JOIN dpm.CAS_DOSSIER c ON c.Code = p.CasCode
WHERE NOT EXISTS (
    SELECT 1
    FROM dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE po
    WHERE po.FK_CasDossier = c.IdCasDossier
      AND po.CodeTypePiece = p.CodeTypePiece
);

COMMIT TRAN;
PRINT 'OPTIONAL_demande_paiement : OK';
