-- =============================================================================
-- OPTIONAL — Module Trésorerie / PCT — paramétrage des comptes financiers
-- Schéma : pct
-- Base   : BD_SNEL (map-only EF — aucune migration EF Core)
-- Idempotent : schéma / tables / contraintes / index / seeds référentiels minimaux.
--
-- Périmètre :
--   Référentiels BANQUE, DIRECTION, PROVINCE, GROUPE_TYPE_COMPTE, TYPE_COMPTE,
--   CATEGORIE_COMPTE, COMPTE, COMPTE_CATEGORIE.
--   Seeds structurels uniquement — AUCUN compte financier réel inséré.
--
-- Hors périmètre :
--   Circuit DPM, lien DEMANDE_PAIEMENT → COMPTE, encours, migration comptes ancien système.
--
-- Prérequis :
--   dbo.UTILISATEUR (socle existant)
--   dpm.DEVISE (docs/sql/OPTIONAL_devise_dpm.sql)
--
-- -----------------------------------------------------------------------------
-- RÈGLES GARANTIES EN SQL (CHECK / FK / index)
-- -----------------------------------------------------------------------------
-- COMPTE : unicité (FK_Banque, NumeroCompte).
-- COMPTE : cohérence clôture (Actif / DateCloture).
-- BANQUE.IdBanque : référence métier/historique SNEL (VARCHAR, fournie, pas IDENTITY).
-- COMPTE_CATEGORIE : une seule catégorie active par compte (index filtré).
-- COMPTE_CATEGORIE : DateFin >= DateDebut.
-- FK_Utilisateur sur COMPTE = responsable PCT suivi (≠ autorisation applicative).
--
-- -----------------------------------------------------------------------------
-- RÈGLES LAISSÉES AU SERVICE APPLICATIF (pas de trigger SQL v1)
-- -----------------------------------------------------------------------------
-- Absence de chevauchement des périodes COMPTE_CATEGORIE.
-- Normalisation trim NumeroCompte avant insert/update.
-- Référentiels inactifs interdits sur création de compte.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;

IF OBJECT_ID(N'dpm.DEVISE', N'U') IS NULL
BEGIN
    RAISERROR(N'Prérequis manquant : dpm.DEVISE. Exécutez docs/sql/OPTIONAL_devise_dpm.sql.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.UTILISATEUR', N'U') IS NULL
BEGIN
    RAISERROR(N'Prérequis manquant : dbo.UTILISATEUR.', 16, 1);
    RETURN;
END;

BEGIN TRAN;

-- ---------------------------------------------------------------------------
-- Schéma pct
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'pct')
    EXEC(N'CREATE SCHEMA pct AUTHORIZATION dbo;');

-- ---------------------------------------------------------------------------
-- pct.BANQUE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'pct.BANQUE', N'U') IS NULL
BEGIN
    CREATE TABLE pct.BANQUE
    (
        IdBanque         VARCHAR(50)   NOT NULL,
        LibelleBanque    NVARCHAR(200) NOT NULL,
        Pays             NVARCHAR(100) NULL,
        Actif            BIT           NOT NULL CONSTRAINT DF_PCT_BANQUE_Actif DEFAULT (1),
        DateCreation     DATETIME2     NOT NULL CONSTRAINT DF_PCT_BANQUE_DateCreation DEFAULT (SYSUTCDATETIME()),
        DateModification DATETIME2     NULL,
        CONSTRAINT PK_PCT_BANQUE PRIMARY KEY (IdBanque),
        CONSTRAINT CK_PCT_BANQUE_IdBanque CHECK (LEN(LTRIM(RTRIM(IdBanque))) > 0),
        CONSTRAINT CK_PCT_BANQUE_Libelle CHECK (LEN(LTRIM(RTRIM(LibelleBanque))) > 0)
    );
END;

IF OBJECT_ID(N'pct.BANQUE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_BANQUE_IdBanque')
BEGIN
    ALTER TABLE pct.BANQUE
        ADD CONSTRAINT CK_PCT_BANQUE_IdBanque
        CHECK (LEN(LTRIM(RTRIM(IdBanque))) > 0);
END;

IF OBJECT_ID(N'pct.BANQUE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_BANQUE_Libelle')
BEGIN
    ALTER TABLE pct.BANQUE
        ADD CONSTRAINT CK_PCT_BANQUE_Libelle
        CHECK (LEN(LTRIM(RTRIM(LibelleBanque))) > 0);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_BANQUE_Actif'
      AND object_id = OBJECT_ID(N'pct.BANQUE')
)
BEGIN
    CREATE INDEX IX_PCT_BANQUE_Actif
        ON pct.BANQUE (Actif);
END;

-- ---------------------------------------------------------------------------
-- pct.DIRECTION
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'pct.DIRECTION', N'U') IS NULL
BEGIN
    CREATE TABLE pct.DIRECTION
    (
        IdDirection      BIGINT IDENTITY(1,1) NOT NULL,
        Libelle          NVARCHAR(200) NOT NULL,
        Actif            BIT           NOT NULL CONSTRAINT DF_PCT_DIRECTION_Actif DEFAULT (1),
        DateCreation     DATETIME2     NOT NULL CONSTRAINT DF_PCT_DIRECTION_DateCreation DEFAULT (SYSUTCDATETIME()),
        DateModification DATETIME2     NULL,
        CONSTRAINT PK_PCT_DIRECTION PRIMARY KEY (IdDirection),
        CONSTRAINT CK_PCT_DIRECTION_Libelle CHECK (LEN(LTRIM(RTRIM(Libelle))) > 0)
    );
END;

IF OBJECT_ID(N'pct.DIRECTION', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_DIRECTION_Libelle')
BEGIN
    ALTER TABLE pct.DIRECTION
        ADD CONSTRAINT CK_PCT_DIRECTION_Libelle
        CHECK (LEN(LTRIM(RTRIM(Libelle))) > 0);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PCT_DIRECTION_Libelle'
      AND object_id = OBJECT_ID(N'pct.DIRECTION')
)
BEGIN
    CREATE UNIQUE INDEX UX_PCT_DIRECTION_Libelle
        ON pct.DIRECTION (Libelle);
END;

-- ---------------------------------------------------------------------------
-- pct.PROVINCE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'pct.PROVINCE', N'U') IS NULL
BEGIN
    CREATE TABLE pct.PROVINCE
    (
        IdProvince       VARCHAR(20)   NOT NULL,
        Libelle          NVARCHAR(200) NOT NULL,
        Actif            BIT           NOT NULL CONSTRAINT DF_PCT_PROVINCE_Actif DEFAULT (1),
        DateCreation     DATETIME2     NOT NULL CONSTRAINT DF_PCT_PROVINCE_DateCreation DEFAULT (SYSUTCDATETIME()),
        DateModification DATETIME2     NULL,
        CONSTRAINT PK_PCT_PROVINCE PRIMARY KEY (IdProvince),
        CONSTRAINT CK_PCT_PROVINCE_IdProvince CHECK (LEN(LTRIM(RTRIM(IdProvince))) > 0),
        CONSTRAINT CK_PCT_PROVINCE_Libelle CHECK (LEN(LTRIM(RTRIM(Libelle))) > 0)
    );
END;

IF OBJECT_ID(N'pct.PROVINCE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_PROVINCE_IdProvince')
BEGIN
    ALTER TABLE pct.PROVINCE
        ADD CONSTRAINT CK_PCT_PROVINCE_IdProvince
        CHECK (LEN(LTRIM(RTRIM(IdProvince))) > 0);
END;

IF OBJECT_ID(N'pct.PROVINCE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_PROVINCE_Libelle')
BEGIN
    ALTER TABLE pct.PROVINCE
        ADD CONSTRAINT CK_PCT_PROVINCE_Libelle
        CHECK (LEN(LTRIM(RTRIM(Libelle))) > 0);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PCT_PROVINCE_Libelle'
      AND object_id = OBJECT_ID(N'pct.PROVINCE')
)
BEGIN
    CREATE UNIQUE INDEX UX_PCT_PROVINCE_Libelle
        ON pct.PROVINCE (Libelle);
END;

-- ---------------------------------------------------------------------------
-- pct.GROUPE_TYPE_COMPTE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'pct.GROUPE_TYPE_COMPTE', N'U') IS NULL
BEGIN
    CREATE TABLE pct.GROUPE_TYPE_COMPTE
    (
        IdGroupeTypeCompte BIGINT IDENTITY(1,1) NOT NULL,
        Libelle            NVARCHAR(200) NOT NULL,
        Actif              BIT           NOT NULL CONSTRAINT DF_PCT_GROUPE_TYPE_COMPTE_Actif DEFAULT (1),
        DateCreation       DATETIME2     NOT NULL CONSTRAINT DF_PCT_GROUPE_TYPE_COMPTE_DateCreation DEFAULT (SYSUTCDATETIME()),
        DateModification   DATETIME2     NULL,
        CONSTRAINT PK_PCT_GROUPE_TYPE_COMPTE PRIMARY KEY (IdGroupeTypeCompte),
        CONSTRAINT CK_PCT_GROUPE_TYPE_COMPTE_Libelle CHECK (LEN(LTRIM(RTRIM(Libelle))) > 0)
    );
END;

IF OBJECT_ID(N'pct.GROUPE_TYPE_COMPTE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_GROUPE_TYPE_COMPTE_Libelle')
BEGIN
    ALTER TABLE pct.GROUPE_TYPE_COMPTE
        ADD CONSTRAINT CK_PCT_GROUPE_TYPE_COMPTE_Libelle
        CHECK (LEN(LTRIM(RTRIM(Libelle))) > 0);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PCT_GROUPE_TYPE_COMPTE_Libelle'
      AND object_id = OBJECT_ID(N'pct.GROUPE_TYPE_COMPTE')
)
BEGIN
    CREATE UNIQUE INDEX UX_PCT_GROUPE_TYPE_COMPTE_Libelle
        ON pct.GROUPE_TYPE_COMPTE (Libelle);
END;

-- ---------------------------------------------------------------------------
-- pct.CATEGORIE_COMPTE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'pct.CATEGORIE_COMPTE', N'U') IS NULL
BEGIN
    CREATE TABLE pct.CATEGORIE_COMPTE
    (
        IdCategorieCompte BIGINT IDENTITY(1,1) NOT NULL,
        Libelle           NVARCHAR(200) NOT NULL,
        Orientation       NVARCHAR(200) NULL,
        Actif             BIT           NOT NULL CONSTRAINT DF_PCT_CATEGORIE_COMPTE_Actif DEFAULT (1),
        DateCreation      DATETIME2     NOT NULL CONSTRAINT DF_PCT_CATEGORIE_COMPTE_DateCreation DEFAULT (SYSUTCDATETIME()),
        DateModification  DATETIME2     NULL,
        CONSTRAINT PK_PCT_CATEGORIE_COMPTE PRIMARY KEY (IdCategorieCompte),
        CONSTRAINT CK_PCT_CATEGORIE_COMPTE_Libelle CHECK (LEN(LTRIM(RTRIM(Libelle))) > 0)
    );
END;

IF OBJECT_ID(N'pct.CATEGORIE_COMPTE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_CATEGORIE_COMPTE_Libelle')
BEGIN
    ALTER TABLE pct.CATEGORIE_COMPTE
        ADD CONSTRAINT CK_PCT_CATEGORIE_COMPTE_Libelle
        CHECK (LEN(LTRIM(RTRIM(Libelle))) > 0);
END;

IF OBJECT_ID(N'pct.CATEGORIE_COMPTE', N'U') IS NOT NULL
   AND EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'pct.CATEGORIE_COMPTE')
          AND name = N'Orientation'
          AND is_nullable = 0
   )
BEGIN
    ALTER TABLE pct.CATEGORIE_COMPTE ALTER COLUMN Orientation NVARCHAR(200) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PCT_CATEGORIE_COMPTE_Libelle'
      AND object_id = OBJECT_ID(N'pct.CATEGORIE_COMPTE')
)
BEGIN
    CREATE UNIQUE INDEX UX_PCT_CATEGORIE_COMPTE_Libelle
        ON pct.CATEGORIE_COMPTE (Libelle);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_CATEGORIE_COMPTE_Orientation'
      AND object_id = OBJECT_ID(N'pct.CATEGORIE_COMPTE')
)
BEGIN
    CREATE INDEX IX_PCT_CATEGORIE_COMPTE_Orientation
        ON pct.CATEGORIE_COMPTE (Orientation);
END;

-- ---------------------------------------------------------------------------
-- pct.TYPE_COMPTE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'pct.TYPE_COMPTE', N'U') IS NULL
BEGIN
    CREATE TABLE pct.TYPE_COMPTE
    (
        Code                 VARCHAR(20)   NOT NULL,
        Libelle              NVARCHAR(200) NOT NULL,
        FK_GroupeTypeCompte  BIGINT        NOT NULL,
        Actif                BIT           NOT NULL CONSTRAINT DF_PCT_TYPE_COMPTE_Actif DEFAULT (1),
        DateCreation         DATETIME2     NOT NULL CONSTRAINT DF_PCT_TYPE_COMPTE_DateCreation DEFAULT (SYSUTCDATETIME()),
        DateModification     DATETIME2     NULL,
        CONSTRAINT PK_PCT_TYPE_COMPTE PRIMARY KEY (Code),
        CONSTRAINT CK_PCT_TYPE_COMPTE_Code CHECK (LEN(LTRIM(RTRIM(Code))) > 0),
        CONSTRAINT CK_PCT_TYPE_COMPTE_Libelle CHECK (LEN(LTRIM(RTRIM(Libelle))) > 0)
    );
END;

IF OBJECT_ID(N'pct.TYPE_COMPTE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_TYPE_COMPTE_Code')
BEGIN
    ALTER TABLE pct.TYPE_COMPTE
        ADD CONSTRAINT CK_PCT_TYPE_COMPTE_Code
        CHECK (LEN(LTRIM(RTRIM(Code))) > 0);
END;

IF OBJECT_ID(N'pct.TYPE_COMPTE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_TYPE_COMPTE_Libelle')
BEGIN
    ALTER TABLE pct.TYPE_COMPTE
        ADD CONSTRAINT CK_PCT_TYPE_COMPTE_Libelle
        CHECK (LEN(LTRIM(RTRIM(Libelle))) > 0);
END;

-- Unicité de Code = PK. L'ancien index UX_PCT_TYPE_COMPTE_Code est retiré
-- après conversion (voir OPTIONAL_pct_type_compte_code_pk.sql).
-- Sur un schéma non encore converti (IdTypeCompte encore présent), on conserve
-- l'unicité métier via cet index jusqu'à l'exécution du script de conversion.
IF OBJECT_ID(N'pct.TYPE_COMPTE', N'U') IS NOT NULL
   AND COL_LENGTH(N'pct.TYPE_COMPTE', N'IdTypeCompte') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_PCT_TYPE_COMPTE_Code'
          AND object_id = OBJECT_ID(N'pct.TYPE_COMPTE')
   )
BEGIN
    CREATE UNIQUE INDEX UX_PCT_TYPE_COMPTE_Code
        ON pct.TYPE_COMPTE (Code);
END;

IF OBJECT_ID(N'pct.TYPE_COMPTE', N'U') IS NOT NULL
   AND COL_LENGTH(N'pct.TYPE_COMPTE', N'IdTypeCompte') IS NULL
   AND EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_PCT_TYPE_COMPTE_Code'
          AND object_id = OBJECT_ID(N'pct.TYPE_COMPTE')
   )
BEGIN
    DROP INDEX UX_PCT_TYPE_COMPTE_Code ON pct.TYPE_COMPTE;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_TYPE_COMPTE_FK_GroupeTypeCompte'
      AND object_id = OBJECT_ID(N'pct.TYPE_COMPTE')
)
BEGIN
    CREATE INDEX IX_PCT_TYPE_COMPTE_FK_GroupeTypeCompte
        ON pct.TYPE_COMPTE (FK_GroupeTypeCompte);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_TYPE_COMPTE_Groupe')
    ALTER TABLE pct.TYPE_COMPTE
        ADD CONSTRAINT FK_PCT_TYPE_COMPTE_Groupe
        FOREIGN KEY (FK_GroupeTypeCompte) REFERENCES pct.GROUPE_TYPE_COMPTE (IdGroupeTypeCompte);

-- ---------------------------------------------------------------------------
-- pct.COMPTE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'pct.COMPTE', N'U') IS NULL
BEGIN
    CREATE TABLE pct.COMPTE
    (
        IdCompte         BIGINT IDENTITY(1,1) NOT NULL,
        NumeroCompte     NVARCHAR(50)  NOT NULL,
        LibelleCompte    NVARCHAR(200) NOT NULL,
        FK_Banque        VARCHAR(50)   NOT NULL,
        FK_Direction     BIGINT        NOT NULL,
        FK_TypeCompte    VARCHAR(20)   NOT NULL,
        FK_Devise        BIGINT        NOT NULL,
        FK_Province      VARCHAR(20)   NULL,
        FK_Utilisateur   BIGINT        NULL,
        DateCreation     DATETIME2     NOT NULL CONSTRAINT DF_PCT_COMPTE_DateCreation DEFAULT (SYSUTCDATETIME()),
        DateCloture      DATE          NULL,
        DateModification DATETIME2     NULL,
        Actif            BIT           NOT NULL CONSTRAINT DF_PCT_COMPTE_Actif DEFAULT (1),
        CONSTRAINT PK_PCT_COMPTE PRIMARY KEY (IdCompte),
        CONSTRAINT CK_PCT_COMPTE_NumeroCompte CHECK (LEN(LTRIM(RTRIM(NumeroCompte))) > 0),
        CONSTRAINT CK_PCT_COMPTE_LibelleCompte CHECK (LEN(LTRIM(RTRIM(LibelleCompte))) > 0),
        CONSTRAINT CK_PCT_COMPTE_Cloture CHECK (
            (Actif = 1 AND DateCloture IS NULL)
            OR (Actif = 0 AND DateCloture IS NOT NULL)
        )
    );
END;

IF OBJECT_ID(N'pct.COMPTE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_COMPTE_NumeroCompte')
BEGIN
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT CK_PCT_COMPTE_NumeroCompte
        CHECK (LEN(LTRIM(RTRIM(NumeroCompte))) > 0);
END;

IF OBJECT_ID(N'pct.COMPTE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_COMPTE_LibelleCompte')
BEGIN
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT CK_PCT_COMPTE_LibelleCompte
        CHECK (LEN(LTRIM(RTRIM(LibelleCompte))) > 0);
END;

IF OBJECT_ID(N'pct.COMPTE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_COMPTE_Cloture')
BEGIN
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT CK_PCT_COMPTE_Cloture
        CHECK (
            (Actif = 1 AND DateCloture IS NULL)
            OR (Actif = 0 AND DateCloture IS NOT NULL)
        );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PCT_COMPTE_Banque_NumeroCompte'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    CREATE UNIQUE INDEX UX_PCT_COMPTE_Banque_NumeroCompte
        ON pct.COMPTE (FK_Banque, NumeroCompte);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_FK_Direction'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    CREATE INDEX IX_PCT_COMPTE_FK_Direction
        ON pct.COMPTE (FK_Direction);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_FK_TypeCompte'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    CREATE INDEX IX_PCT_COMPTE_FK_TypeCompte
        ON pct.COMPTE (FK_TypeCompte);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_FK_Devise'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    CREATE INDEX IX_PCT_COMPTE_FK_Devise
        ON pct.COMPTE (FK_Devise);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_FK_Province'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    CREATE INDEX IX_PCT_COMPTE_FK_Province
        ON pct.COMPTE (FK_Province);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_FK_Utilisateur'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    CREATE INDEX IX_PCT_COMPTE_FK_Utilisateur
        ON pct.COMPTE (FK_Utilisateur);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_Actif'
      AND object_id = OBJECT_ID(N'pct.COMPTE')
)
BEGIN
    CREATE INDEX IX_PCT_COMPTE_Actif
        ON pct.COMPTE (Actif);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_Banque')
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT FK_PCT_COMPTE_Banque
        FOREIGN KEY (FK_Banque) REFERENCES pct.BANQUE (IdBanque);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_Direction')
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT FK_PCT_COMPTE_Direction
        FOREIGN KEY (FK_Direction) REFERENCES pct.DIRECTION (IdDirection)
        ON DELETE NO ACTION
        ON UPDATE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_TypeCompte')
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT FK_PCT_COMPTE_TypeCompte
        FOREIGN KEY (FK_TypeCompte) REFERENCES pct.TYPE_COMPTE (Code)
        ON DELETE NO ACTION
        ON UPDATE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_Devise')
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT FK_PCT_COMPTE_Devise
        FOREIGN KEY (FK_Devise) REFERENCES dpm.DEVISE (IdDevise);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_Province')
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT FK_PCT_COMPTE_Province
        FOREIGN KEY (FK_Province) REFERENCES pct.PROVINCE (IdProvince)
        ON DELETE NO ACTION
        ON UPDATE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_Utilisateur')
    ALTER TABLE pct.COMPTE
        ADD CONSTRAINT FK_PCT_COMPTE_Utilisateur
        FOREIGN KEY (FK_Utilisateur) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

-- ---------------------------------------------------------------------------
-- pct.COMPTE_CATEGORIE
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'pct.COMPTE_CATEGORIE', N'U') IS NULL
BEGIN
    CREATE TABLE pct.COMPTE_CATEGORIE
    (
        IdCompteCategorie  BIGINT IDENTITY(1,1) NOT NULL,
        FK_Compte          BIGINT NOT NULL,
        FK_CategorieCompte BIGINT NOT NULL,
        DateDebut          DATE   NOT NULL,
        DateFin            DATE   NULL,
        CONSTRAINT PK_PCT_COMPTE_CATEGORIE PRIMARY KEY (IdCompteCategorie),
        CONSTRAINT CK_PCT_COMPTE_CATEGORIE_Dates CHECK (DateFin IS NULL OR DateFin >= DateDebut)
    );
END;

IF OBJECT_ID(N'pct.COMPTE_CATEGORIE', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCT_COMPTE_CATEGORIE_Dates')
BEGIN
    ALTER TABLE pct.COMPTE_CATEGORIE
        ADD CONSTRAINT CK_PCT_COMPTE_CATEGORIE_Dates
        CHECK (DateFin IS NULL OR DateFin >= DateDebut);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PCT_COMPTE_CATEGORIE_Compte_Actif'
      AND object_id = OBJECT_ID(N'pct.COMPTE_CATEGORIE')
)
BEGIN
    CREATE UNIQUE INDEX UX_PCT_COMPTE_CATEGORIE_Compte_Actif
        ON pct.COMPTE_CATEGORIE (FK_Compte)
        WHERE DateFin IS NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PCT_COMPTE_CATEGORIE_Compte_Dates'
      AND object_id = OBJECT_ID(N'pct.COMPTE_CATEGORIE')
)
BEGIN
    CREATE INDEX IX_PCT_COMPTE_CATEGORIE_Compte_Dates
        ON pct.COMPTE_CATEGORIE (FK_Compte, DateDebut, DateFin);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_CATEGORIE_Compte')
    ALTER TABLE pct.COMPTE_CATEGORIE
        ADD CONSTRAINT FK_PCT_COMPTE_CATEGORIE_Compte
        FOREIGN KEY (FK_Compte) REFERENCES pct.COMPTE (IdCompte);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PCT_COMPTE_CATEGORIE_Categorie')
    ALTER TABLE pct.COMPTE_CATEGORIE
        ADD CONSTRAINT FK_PCT_COMPTE_CATEGORIE_Categorie
        FOREIGN KEY (FK_CategorieCompte) REFERENCES pct.CATEGORIE_COMPTE (IdCategorieCompte)
        ON DELETE NO ACTION
        ON UPDATE NO ACTION;

-- ---------------------------------------------------------------------------
-- Seeds référentiels minimaux (aucun compte réel)
-- ---------------------------------------------------------------------------

MERGE pct.GROUPE_TYPE_COMPTE AS t
USING (VALUES
    (N'Comptes courants',                    1),
    (N'Comptes dédiés',                      1),
    (N'Provisions Consignées à Eximbank',    1)
) AS s (Libelle, Actif)
ON t.Libelle = s.Libelle
WHEN NOT MATCHED THEN
    INSERT (Libelle, Actif)
    VALUES (s.Libelle, s.Actif);

MERGE pct.TYPE_COMPTE AS t
USING (
    SELECT
        g.IdGroupeTypeCompte,
        s.Code,
        s.Libelle,
        s.Actif
    FROM (VALUES
        ('FCT',    N'FCT',    N'Comptes courants', 1),
        ('CAISSE', N'Caisse', N'Comptes courants', 1)
    ) AS s (Code, Libelle, GroupeLibelle, Actif)
    INNER JOIN pct.GROUPE_TYPE_COMPTE g
        ON g.Libelle = s.GroupeLibelle
) AS src
ON t.Code = src.Code
WHEN NOT MATCHED THEN
    INSERT (Code, Libelle, FK_GroupeTypeCompte, Actif)
    VALUES (src.Code, src.Libelle, src.IdGroupeTypeCompte, src.Actif);

MERGE pct.CATEGORIE_COMPTE AS t
USING (VALUES
    (N'Kinshasa',                          N'Kinshasa',                          1),
    (N'Province',                          N'Province',                          1),
    (N'TVA',                               N'TVA',                               1),
    (N'Comptes dédiés',                    N'Comptes spécifique',                1),
    (N'Comptes spécifique',                N'Comptes spécifique',                1),
    (N'Dépôt à terme',                     N'Comptes spécifique',                1),
    (N'Provisions Consignées à Eximbank',  N'Provisions Consignées à Eximbank', 1),
    (N'Kinshasa CNX',                      N'Kinshasa B',                        1),
    (N'Caisse',                            N'Caisse DG',                         1)
) AS s (Libelle, Orientation, Actif)
ON t.Libelle = s.Libelle
WHEN NOT MATCHED THEN
    INSERT (Libelle, Orientation, Actif)
    VALUES (s.Libelle, s.Orientation, s.Actif);

MERGE pct.DIRECTION AS t
USING (VALUES
    (N'Kinshasa',    1),
    (N'Lubumbashi',  1)
) AS s (Libelle, Actif)
ON t.Libelle = s.Libelle
WHEN NOT MATCHED THEN
    INSERT (Libelle, Actif)
    VALUES (s.Libelle, s.Actif);

-- Banques : volontairement AUCUN seed.
-- Le cas métier historique « SNEL DG » (caisse) ne sera inséré qu'avec la migration
-- ou la saisie paramétrique ultérieure — aucun compte n'étant créé ici, SNEL DG
-- n'est pas requis pour l'initialisation structurelle.

COMMIT TRAN;
PRINT N'OPTIONAL_pct_parametrage_comptes : OK';
GO

-- =============================================================================
-- Contrôles post-script (lecture seule — hors transaction)
-- =============================================================================
PRINT N'=== Contrôles OPTIONAL_pct_parametrage_comptes ===';

SELECT
    OBJECT_ID(N'pct.BANQUE', N'U')             AS TableBanque,
    OBJECT_ID(N'pct.DIRECTION', N'U')          AS TableDirection,
    OBJECT_ID(N'pct.PROVINCE', N'U')           AS TableProvince,
    OBJECT_ID(N'pct.GROUPE_TYPE_COMPTE', N'U') AS TableGroupeTypeCompte,
    OBJECT_ID(N'pct.TYPE_COMPTE', N'U')        AS TableTypeCompte,
    OBJECT_ID(N'pct.CATEGORIE_COMPTE', N'U')   AS TableCategorieCompte,
    OBJECT_ID(N'pct.COMPTE', N'U')             AS TableCompte,
    OBJECT_ID(N'pct.COMPTE_CATEGORIE', N'U')   AS TableCompteCategorie;

SELECT N'GROUPE_TYPE_COMPTE' AS Referentiel, COUNT(*) AS NbLignes
FROM pct.GROUPE_TYPE_COMPTE
UNION ALL
SELECT N'TYPE_COMPTE', COUNT(*) FROM pct.TYPE_COMPTE
UNION ALL
SELECT N'CATEGORIE_COMPTE', COUNT(*) FROM pct.CATEGORIE_COMPTE
UNION ALL
SELECT N'DIRECTION', COUNT(*) FROM pct.DIRECTION
UNION ALL
SELECT N'BANQUE', COUNT(*) FROM pct.BANQUE
UNION ALL
SELECT N'COMPTE', COUNT(*) FROM pct.COMPTE
UNION ALL
SELECT N'COMPTE_CATEGORIE', COUNT(*) FROM pct.COMPTE_CATEGORIE;

SELECT
    fk.name AS NomFk,
    OBJECT_SCHEMA_NAME(fk.parent_object_id) + N'.' + OBJECT_NAME(fk.parent_object_id) AS TableSource,
    OBJECT_SCHEMA_NAME(fk.referenced_object_id) + N'.' + OBJECT_NAME(fk.referenced_object_id) AS TableCible
FROM sys.foreign_keys fk
WHERE fk.parent_object_id IN (
    OBJECT_ID(N'pct.TYPE_COMPTE'),
    OBJECT_ID(N'pct.COMPTE'),
    OBJECT_ID(N'pct.COMPTE_CATEGORIE')
)
ORDER BY fk.name;
GO
