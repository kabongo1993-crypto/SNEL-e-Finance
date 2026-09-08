-- =============================================================================
-- OPTIONAL — Socle Utilisateurs / Rôles / Permissions individuelles / Périmètres
-- Idempotent. Ne supprime aucune donnée ni aucun profil historique.
-- Prérequis : dbo.UTILISATEUR, DEPARTEMENT, UNITE_BUDGETAIRE, STRUCTURE_ORGANISATIONNELLE
--             (et idéalement dpm.PROFIL_UTILISATEUR via OPTIONAL_circuit_dpm_charge_junior.sql)
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

-- ---------------------------------------------------------------------------
-- Affectation administrative sur UTILISATEUR (≠ périmètre sécurité)
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.UTILISATEUR', N'FK_StructureOrganisationnelle') IS NULL
    ALTER TABLE dbo.UTILISATEUR ADD FK_StructureOrganisationnelle BIGINT NULL;

IF COL_LENGTH(N'dbo.UTILISATEUR', N'FK_DepartementPrincipal') IS NULL
    ALTER TABLE dbo.UTILISATEUR ADD FK_DepartementPrincipal BIGINT NULL;

IF COL_LENGTH(N'dbo.UTILISATEUR', N'FK_StructureService') IS NULL
    ALTER TABLE dbo.UTILISATEUR ADD FK_StructureService BIGINT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_UTILISATEUR_STRUCTURE')
    AND OBJECT_ID(N'dbo.STRUCTURE_ORGANISATIONNELLE', N'U') IS NOT NULL
    ALTER TABLE dbo.UTILISATEUR
        ADD CONSTRAINT FK_UTILISATEUR_STRUCTURE
        FOREIGN KEY (FK_StructureOrganisationnelle) REFERENCES dbo.STRUCTURE_ORGANISATIONNELLE (IdStructure);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_UTILISATEUR_DEPT_PRINCIPAL')
    AND OBJECT_ID(N'dbo.DEPARTEMENT', N'U') IS NOT NULL
    ALTER TABLE dbo.UTILISATEUR
        ADD CONSTRAINT FK_UTILISATEUR_DEPT_PRINCIPAL
        FOREIGN KEY (FK_DepartementPrincipal) REFERENCES dbo.DEPARTEMENT (IdDepartement);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_UTILISATEUR_STRUCTURE_SERVICE')
    AND OBJECT_ID(N'dbo.STRUCTURE_ORGANISATIONNELLE', N'U') IS NOT NULL
    ALTER TABLE dbo.UTILISATEUR
        ADD CONSTRAINT FK_UTILISATEUR_STRUCTURE_SERVICE
        FOREIGN KEY (FK_StructureService) REFERENCES dbo.STRUCTURE_ORGANISATIONNELLE (IdStructure);

-- ---------------------------------------------------------------------------
-- Élargir CHECK CodeProfil (historiques + cibles) — sans supprimer les anciens
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.PROFIL_UTILISATEUR', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DPM_PROFIL_Code'
               AND parent_object_id = OBJECT_ID(N'dpm.PROFIL_UTILISATEUR'))
        ALTER TABLE dpm.PROFIL_UTILISATEUR DROP CONSTRAINT CK_DPM_PROFIL_Code;

    EXEC(N'ALTER TABLE dpm.PROFIL_UTILISATEUR ADD CONSTRAINT CK_DPM_PROFIL_Code CHECK (CodeProfil IN (
        ''DEMANDEUR'', ''CHARGE_DPM'',
        ''GESTIONNAIRE_JUNIOR_DC'', ''GESTIONNAIRE_JUNIOR_AE'', ''GESTIONNAIRE_JUNIOR_BI'',
        ''CONTROLE_BUDGET'', ''ADMIN'',
        ''SERVICE_DEMANDEUR'', ''RESPONSABLE_SERVICE_DEMANDEUR'', ''RESPONSABLE_ENTITE_INITIATRICE'',
        ''CHARGE_DP'', ''GESTIONNAIRE_JUNIOR'', ''GESTIONNAIRE_SENIOR'', ''CHEF_DIVISION'',
        ''DIRECTEUR_BUDGETS'', ''ADMINISTRATEUR_SYSTEME''))');
END;

-- ---------------------------------------------------------------------------
-- Permissions individuelles
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.PERMISSION_UTILISATEUR', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.PERMISSION_UTILISATEUR
    (
        IdPermissionUtilisateur BIGINT IDENTITY(1,1) NOT NULL,
        FK_Utilisateur          BIGINT       NOT NULL,
        CodePermission          VARCHAR(80)  NOT NULL,
        DateAttribution         DATETIME2    NOT NULL CONSTRAINT DF_DPM_PERM_Date DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DPM_PERMISSION_UTILISATEUR PRIMARY KEY (IdPermissionUtilisateur)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_PERM_USER_CODE' AND object_id = OBJECT_ID(N'dpm.PERMISSION_UTILISATEUR')
)
    CREATE UNIQUE INDEX UX_DPM_PERM_USER_CODE
        ON dpm.PERMISSION_UTILISATEUR (FK_Utilisateur, CodePermission);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PERM_UTILISATEUR')
    ALTER TABLE dpm.PERMISSION_UTILISATEUR
        ADD CONSTRAINT FK_DPM_PERM_UTILISATEUR
        FOREIGN KEY (FK_Utilisateur) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

-- ---------------------------------------------------------------------------
-- Périmètre (en-tête + détails) — flags « tous » sans explosion de lignes
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dpm.PERIMETRE_UTILISATEUR', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.PERIMETRE_UTILISATEUR
    (
        IdPerimetreUtilisateur   BIGINT IDENTITY(1,1) NOT NULL,
        FK_Utilisateur           BIGINT    NOT NULL,
        TousDepartements         BIT       NOT NULL CONSTRAINT DF_DPM_PERIM_TousDept DEFAULT (0),
        ToutesUnitesBudgetaires  BIT       NOT NULL CONSTRAINT DF_DPM_PERIM_ToutesUb DEFAULT (0),
        DateModification         DATETIME2 NOT NULL CONSTRAINT DF_DPM_PERIM_Date DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DPM_PERIMETRE_UTILISATEUR PRIMARY KEY (IdPerimetreUtilisateur)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_PERIMETRE_USER' AND object_id = OBJECT_ID(N'dpm.PERIMETRE_UTILISATEUR')
)
    CREATE UNIQUE INDEX UX_DPM_PERIMETRE_USER ON dpm.PERIMETRE_UTILISATEUR (FK_Utilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PERIMETRE_UTILISATEUR')
    ALTER TABLE dpm.PERIMETRE_UTILISATEUR
        ADD CONSTRAINT FK_DPM_PERIMETRE_UTILISATEUR
        FOREIGN KEY (FK_Utilisateur) REFERENCES dbo.UTILISATEUR (IdUtilisateur);

IF OBJECT_ID(N'dpm.PERIMETRE_DEPARTEMENT', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.PERIMETRE_DEPARTEMENT
    (
        IdPerimetreDepartement  BIGINT IDENTITY(1,1) NOT NULL,
        FK_PerimetreUtilisateur BIGINT NOT NULL,
        FK_Departement          BIGINT NOT NULL,
        CONSTRAINT PK_DPM_PERIMETRE_DEPARTEMENT PRIMARY KEY (IdPerimetreDepartement)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_PERIMETRE_DEPT' AND object_id = OBJECT_ID(N'dpm.PERIMETRE_DEPARTEMENT')
)
    CREATE UNIQUE INDEX UX_DPM_PERIMETRE_DEPT
        ON dpm.PERIMETRE_DEPARTEMENT (FK_PerimetreUtilisateur, FK_Departement);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PERIMETRE_DEPT_HDR')
    ALTER TABLE dpm.PERIMETRE_DEPARTEMENT
        ADD CONSTRAINT FK_DPM_PERIMETRE_DEPT_HDR
        FOREIGN KEY (FK_PerimetreUtilisateur) REFERENCES dpm.PERIMETRE_UTILISATEUR (IdPerimetreUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PERIMETRE_DEPT_REF')
    AND OBJECT_ID(N'dbo.DEPARTEMENT', N'U') IS NOT NULL
    ALTER TABLE dpm.PERIMETRE_DEPARTEMENT
        ADD CONSTRAINT FK_DPM_PERIMETRE_DEPT_REF
        FOREIGN KEY (FK_Departement) REFERENCES dbo.DEPARTEMENT (IdDepartement);

IF OBJECT_ID(N'dpm.PERIMETRE_UB', N'U') IS NULL
BEGIN
    CREATE TABLE dpm.PERIMETRE_UB
    (
        IdPerimetreUniteBudgetaire BIGINT IDENTITY(1,1) NOT NULL,
        FK_PerimetreUtilisateur    BIGINT NOT NULL,
        FK_UniteBudgetaire         BIGINT NOT NULL,
        CONSTRAINT PK_DPM_PERIMETRE_UB PRIMARY KEY (IdPerimetreUniteBudgetaire)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DPM_PERIMETRE_UB' AND object_id = OBJECT_ID(N'dpm.PERIMETRE_UB')
)
    CREATE UNIQUE INDEX UX_DPM_PERIMETRE_UB
        ON dpm.PERIMETRE_UB (FK_PerimetreUtilisateur, FK_UniteBudgetaire);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PERIMETRE_UB_HDR')
    ALTER TABLE dpm.PERIMETRE_UB
        ADD CONSTRAINT FK_DPM_PERIMETRE_UB_HDR
        FOREIGN KEY (FK_PerimetreUtilisateur) REFERENCES dpm.PERIMETRE_UTILISATEUR (IdPerimetreUtilisateur);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DPM_PERIMETRE_UB_REF')
    AND OBJECT_ID(N'dbo.UNITE_BUDGETAIRE', N'U') IS NOT NULL
    ALTER TABLE dpm.PERIMETRE_UB
        ADD CONSTRAINT FK_DPM_PERIMETRE_UB_REF
        FOREIGN KEY (FK_UniteBudgetaire) REFERENCES dbo.UNITE_BUDGETAIRE (IdUB);

COMMIT TRAN;
