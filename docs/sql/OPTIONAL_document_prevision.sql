-- =============================================================================
-- OPTIONAL — Documents workflow prévisions
-- Tables : DOCUMENT_PREVISION, DOCUMENT_PREVISION_SEQUENCE
-- Base : BD_SNEL (map-only EF)
-- Idempotent. Ne modifie PAS PREVISION_BUDGETAIRE / WORKFLOW_PREVISION_UB.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

IF OBJECT_ID(N'dbo.DOCUMENT_PREVISION_SEQUENCE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DOCUMENT_PREVISION_SEQUENCE
    (
        IdSequence      BIGINT IDENTITY(1,1) NOT NULL,
        Annee           SMALLINT NOT NULL,
        IdDepartement   BIGINT NOT NULL,
        NumeroVersion   INT NOT NULL,
        TypeDocument    VARCHAR(10) NOT NULL,
        DernierNumero   INT NOT NULL CONSTRAINT DF_DOC_PREV_SEQ_NUM DEFAULT (0),
        CONSTRAINT PK_DOCUMENT_PREVISION_SEQUENCE PRIMARY KEY (IdSequence)
    );

    CREATE UNIQUE INDEX UX_DOCUMENT_PREVISION_SEQUENCE
        ON dbo.DOCUMENT_PREVISION_SEQUENCE (Annee, IdDepartement, NumeroVersion, TypeDocument);
END;

IF OBJECT_ID(N'dbo.DOCUMENT_PREVISION', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DOCUMENT_PREVISION
    (
        IdDocument              BIGINT IDENTITY(1,1) NOT NULL,
        Reference               VARCHAR(120) NOT NULL,
        TypeDocument            VARCHAR(10) NOT NULL,
        IdAudit                 BIGINT NULL,
        IdVersion               BIGINT NOT NULL,
        AnneeExercice           SMALLINT NOT NULL,
        NumeroVersion           INT NOT NULL,
        IdDepartement           BIGINT NOT NULL,
        CodeDepartement         VARCHAR(30) NOT NULL,
        LibelleDepartement      NVARCHAR(200) NOT NULL,
        IdUB                    BIGINT NULL,
        CodeUB                  VARCHAR(50) NULL,
        LibelleUB               NVARCHAR(250) NULL,
        Portee                  VARCHAR(20) NOT NULL,
        NbUbConcernees          INT NOT NULL,
        IdUtilisateurAuteur     BIGINT NOT NULL,
        NomUtilisateurAuteur    NVARCHAR(200) NOT NULL,
        DateEvenement           DATETIME2 NOT NULL,
        StatutAvant             VARCHAR(30) NULL,
        StatutApres             VARCHAR(30) NULL,
        Motif                   NVARCHAR(1000) NULL,
        MontantDC               DECIMAL(18,4) NOT NULL CONSTRAINT DF_DOC_PREV_DC DEFAULT (0),
        MontantAE               DECIMAL(18,4) NOT NULL CONSTRAINT DF_DOC_PREV_AE DEFAULT (0),
        MontantBI               DECIMAL(18,4) NOT NULL CONSTRAINT DF_DOC_PREV_BI DEFAULT (0),
        MontantTotal            DECIMAL(18,4) NOT NULL CONSTRAINT DF_DOC_PREV_TOT DEFAULT (0),
        PayloadJson             NVARCHAR(MAX) NOT NULL,
        CheminFichier           NVARCHAR(500) NOT NULL,
        HashSha256              VARCHAR(64) NOT NULL,
        TailleOctets            BIGINT NOT NULL,
        DateGeneration          DATETIME2 NOT NULL CONSTRAINT DF_DOC_PREV_DateGen DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DOCUMENT_PREVISION PRIMARY KEY (IdDocument)
    );

    CREATE UNIQUE INDEX UX_DOCUMENT_PREVISION_REFERENCE ON dbo.DOCUMENT_PREVISION (Reference);
    CREATE INDEX IX_DOCUMENT_PREVISION_AUDIT ON dbo.DOCUMENT_PREVISION (IdAudit);
END;

COMMIT TRAN;
PRINT 'OPTIONAL_document_prevision : OK';
