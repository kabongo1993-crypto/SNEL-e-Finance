-- =============================================================================
-- OPTIONAL — Documents instrument de paiement (pièce caisse, bon provisoire, minute chèque)
-- + paramétrage comptable associé.
-- Idempotent. Ne supprime aucune donnée.
-- Prérequis : OPTIONAL_demande_paiement.sql, OPTIONAL_billet_conversion.sql
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

IF OBJECT_ID(N'dpm.PARAMETRE_INSTRUMENT_PAIEMENT', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.PARAMETRE_INSTRUMENT_PAIEMENT
    (
        IdParametreInstrumentPaiement BIGINT IDENTITY(1,1) NOT NULL,
        TypeInstrument               VARCHAR(20)     NOT NULL,
        Sr                           NVARCHAR(50)    NULL,
        ComptabiliteGenerale         NVARCHAR(100)   NULL,
        Cp                           NVARCHAR(50)    NULL,
        Cpa                          NVARCHAR(50)    NULL,
        CompteGeneral                NVARCHAR(100)   NULL,
        CompteParticulier            NVARCHAR(100)   NULL,
        CpCa                         NVARCHAR(50)    NULL,
        Ls                           NVARCHAR(50)    NULL,
        SuiviExtraComptable          NVARCHAR(100)   NULL,
        MontantSuiviExtraComptable   DECIMAL(19,4)   NULL,
        NumeroAppariement            NVARCHAR(50)    NULL,
        RecuInstitutionnel           NVARCHAR(200)   NULL,
        Actif                        BIT             NOT NULL CONSTRAINT DF_DPM_PARAM_INST_Actif DEFAULT (1),
        DateCreation                 DATETIME2       NOT NULL CONSTRAINT DF_DPM_PARAM_INST_Crea DEFAULT (SYSUTCDATETIME()),
        DateModification             DATETIME2       NULL,
        FK_UtilisateurCreation       BIGINT          NOT NULL,
        FK_UtilisateurModification   BIGINT          NULL,
        CONSTRAINT PK_DPM_PARAM_INSTRUMENT PRIMARY KEY (IdParametreInstrumentPaiement),
        CONSTRAINT CK_DPM_PARAM_INST_Type CHECK (TypeInstrument IN ('PIECE_CAISSE', 'BON_PROVISOIRE', 'MINUTE_CHEQUE'))
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_PARAM_INST_Type'
      AND object_id = OBJECT_ID(N'dpm.PARAMETRE_INSTRUMENT_PAIEMENT')
)
BEGIN
    CREATE UNIQUE INDEX UX_DPM_PARAM_INST_Type
        ON dpm.PARAMETRE_INSTRUMENT_PAIEMENT (TypeInstrument);
END;

IF OBJECT_ID(N'dpm.PIECE_CAISSE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.PIECE_CAISSE
    (
        IdPieceCaisse              BIGINT IDENTITY(1,1) NOT NULL,
        FK_DemandePaiement         BIGINT          NOT NULL,
        NumeroPiece                VARCHAR(20)     NOT NULL,
        DatePiece                  DATE            NOT NULL,
        Statut                     VARCHAR(20)     NOT NULL,
        MontantFc                  DECIMAL(19,4)   NOT NULL,
        MontantEnLettres           NVARCHAR(500)   NOT NULL,
        ReferenceDemande           VARCHAR(30)     NOT NULL,
        Motif                      NVARCHAR(500)   NOT NULL,
        PieceJustificative         NVARCHAR(500)   NULL,
        BeneficiaireAffichage      NVARCHAR(300)   NOT NULL,
        BeneficiaireMatricule      NVARCHAR(50)    NULL,
        BeneficiaireIdentite       NVARCHAR(300)   NULL,
        RecuSnel                   NVARCHAR(200)   NULL,
        Sr                         NVARCHAR(50)    NULL,
        ComptabiliteGenerale       NVARCHAR(100)   NULL,
        Cp                         NVARCHAR(50)    NULL,
        Cpa                        NVARCHAR(50)    NULL,
        NumeroAppariement          NVARCHAR(50)    NULL,
        IdentifiantVerification    NVARCHAR(120)   NOT NULL,
        FK_UtilisateurEtabli       BIGINT          NOT NULL,
        DateEtabli                 DATETIME2       NOT NULL,
        FK_UtilisateurModification BIGINT          NULL,
        DateModification           DATETIME2       NULL,
        CONSTRAINT PK_DPM_PIECE_CAISSE PRIMARY KEY (IdPieceCaisse),
        CONSTRAINT CK_DPM_PC_Statut CHECK (Statut IN ('ETABLI')),
        CONSTRAINT CK_DPM_PC_MontantPositif CHECK (MontantFc > 0)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = N'UX_DPM_PC_Demande' AND object_id = OBJECT_ID(N'dpm.PIECE_CAISSE')
)
    CREATE UNIQUE INDEX UX_DPM_PC_Demande ON dpm.PIECE_CAISSE (FK_DemandePaiement);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = N'UX_DPM_PC_Numero' AND object_id = OBJECT_ID(N'dpm.PIECE_CAISSE')
)
    CREATE UNIQUE INDEX UX_DPM_PC_Numero ON dpm.PIECE_CAISSE (NumeroPiece);

IF OBJECT_ID(N'dpm.BON_PROVISOIRE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.BON_PROVISOIRE
    (
        IdBonProvisoire            BIGINT IDENTITY(1,1) NOT NULL,
        FK_DemandePaiement         BIGINT          NOT NULL,
        NumeroBon                  VARCHAR(20)     NOT NULL,
        DateBon                    DATE            NOT NULL,
        Statut                     VARCHAR(20)     NOT NULL,
        MontantFc                  DECIMAL(19,4)   NOT NULL,
        MontantEnLettres           NVARCHAR(500)   NOT NULL,
        ReferenceDemande           VARCHAR(30)     NOT NULL,
        Motif                      NVARCHAR(500)   NOT NULL,
        MentionJustificationRetrait NVARCHAR(500)  NULL,
        BeneficiaireAffichage      NVARCHAR(300)   NOT NULL,
        BeneficiaireMatricule      NVARCHAR(50)    NULL,
        BeneficiaireIdentite       NVARCHAR(300)   NULL,
        DirectionBeneficiaire        NVARCHAR(200)   NULL,
        RecuCaisseCentrale         NVARCHAR(200)   NULL,
        CompteGeneral              NVARCHAR(100)   NULL,
        CompteParticulier          NVARCHAR(100)   NULL,
        NumeroAppariement          NVARCHAR(50)    NULL,
        IdentifiantVerification    NVARCHAR(120)   NOT NULL,
        FK_UtilisateurEtabli       BIGINT          NOT NULL,
        DateEtabli                 DATETIME2       NOT NULL,
        FK_UtilisateurModification BIGINT          NULL,
        DateModification           DATETIME2       NULL,
        CONSTRAINT PK_DPM_BON_PROVISOIRE PRIMARY KEY (IdBonProvisoire),
        CONSTRAINT CK_DPM_BP_Statut CHECK (Statut IN ('ETABLI')),
        CONSTRAINT CK_DPM_BP_MontantPositif CHECK (MontantFc > 0)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = N'UX_DPM_BP_Demande' AND object_id = OBJECT_ID(N'dpm.BON_PROVISOIRE')
)
    CREATE UNIQUE INDEX UX_DPM_BP_Demande ON dpm.BON_PROVISOIRE (FK_DemandePaiement);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = N'UX_DPM_BP_Numero' AND object_id = OBJECT_ID(N'dpm.BON_PROVISOIRE')
)
    CREATE UNIQUE INDEX UX_DPM_BP_Numero ON dpm.BON_PROVISOIRE (NumeroBon);

IF OBJECT_ID(N'dpm.MINUTE_CHEQUE', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.MINUTE_CHEQUE
    (
        IdMinuteCheque             BIGINT IDENTITY(1,1) NOT NULL,
        FK_DemandePaiement         BIGINT          NOT NULL,
        NumeroOp                   VARCHAR(20)     NOT NULL,
        DateDocument               DATE            NOT NULL,
        Statut                     VARCHAR(20)     NOT NULL,
        MontantPaiement            DECIMAL(19,4)   NOT NULL,
        DevisePaiement             VARCHAR(3)      NOT NULL,
        MontantEnLettres           NVARCHAR(500)   NOT NULL,
        ReferenceDemande           VARCHAR(30)     NOT NULL,
        Motif                      NVARCHAR(500)   NOT NULL,
        BeneficiaireAffichage      NVARCHAR(300)   NOT NULL,
        BeneficiaireAdresse        NVARCHAR(300)   NULL,
        BeneficiaireBanque         NVARCHAR(200)   NULL,
        BeneficiaireNumeroCompte    NVARCHAR(80)    NULL,
        CompteGeneral              NVARCHAR(100)   NULL,
        CpCa                       NVARCHAR(50)    NULL,
        Ls                         NVARCHAR(50)    NULL,
        SuiviExtraComptable        NVARCHAR(100)   NULL,
        NumeroAppariement          NVARCHAR(50)    NULL,
        MontantSuiviExtraComptable DECIMAL(19,4)   NULL,
        IdentifiantVerification    NVARCHAR(120)   NOT NULL,
        FK_UtilisateurEtabli       BIGINT          NOT NULL,
        DateEtabli                 DATETIME2       NOT NULL,
        FK_UtilisateurModification BIGINT          NULL,
        DateModification           DATETIME2       NULL,
        CONSTRAINT PK_DPM_MINUTE_CHEQUE PRIMARY KEY (IdMinuteCheque),
        CONSTRAINT CK_DPM_MC_Statut CHECK (Statut IN ('ETABLI')),
        CONSTRAINT CK_DPM_MC_MontantPositif CHECK (MontantPaiement > 0)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = N'UX_DPM_MC_Demande' AND object_id = OBJECT_ID(N'dpm.MINUTE_CHEQUE')
)
    CREATE UNIQUE INDEX UX_DPM_MC_Demande ON dpm.MINUTE_CHEQUE (FK_DemandePaiement);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = N'UX_DPM_MC_Numero' AND object_id = OBJECT_ID(N'dpm.MINUTE_CHEQUE')
)
    CREATE UNIQUE INDEX UX_DPM_MC_Numero ON dpm.MINUTE_CHEQUE (NumeroOp);

-- FK
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PC_DEMANDE')
    ALTER TABLE dpm.PIECE_CAISSE ADD CONSTRAINT FK_DPM_PC_DEMANDE
        FOREIGN KEY (FK_DemandePaiement) REFERENCES dpm.DEMANDE_PAIEMENT (IdDemandePaiement);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_BP_DEMANDE')
    ALTER TABLE dpm.BON_PROVISOIRE ADD CONSTRAINT FK_DPM_BP_DEMANDE
        FOREIGN KEY (FK_DemandePaiement) REFERENCES dpm.DEMANDE_PAIEMENT (IdDemandePaiement);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_MC_DEMANDE')
    ALTER TABLE dpm.MINUTE_CHEQUE ADD CONSTRAINT FK_DPM_MC_DEMANDE
        FOREIGN KEY (FK_DemandePaiement) REFERENCES dpm.DEMANDE_PAIEMENT (IdDemandePaiement);

COMMIT;
