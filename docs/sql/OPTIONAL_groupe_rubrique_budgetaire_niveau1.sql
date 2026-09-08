/*
================================================================================
SCRIPT — GROUPE_RUBRIQUE_BUDGETAIRE + rattachement des RB feuilles
Base     : BD_SNEL
Source   : Tableau_Comptes_SYSCOHADA_Sections_Colore.xlsx (colonnes C/D/E)
Règle    : Section technique (parent SQL) → Groupe niveau 1 (C/D Excel)
           JAMAIS LEFT(CodeRB, 2)
Lots séparés par GO (ALTER COLUMN puis usage).
================================================================================
*/

SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

------------------------------------------------------------
-- 1) Table GROUPE_RUBRIQUE_BUDGETAIRE
------------------------------------------------------------
IF OBJECT_ID(N'dbo.GROUPE_RUBRIQUE_BUDGETAIRE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GROUPE_RUBRIQUE_BUDGETAIRE
    (
        IdGroupeRB     bigint IDENTITY(1,1) NOT NULL,
        CodeGroupe     varchar(10)  NOT NULL,
        Libelle        nvarchar(200) NOT NULL,
        OrdreAffichage int NOT NULL CONSTRAINT DF_GRB_Ordre DEFAULT (0),
        Actif          bit NOT NULL CONSTRAINT DF_GRB_Actif DEFAULT (1),
        DateCreation   datetime2 NOT NULL CONSTRAINT DF_GRB_DateCreation DEFAULT (sysdatetime()),
        CONSTRAINT PK_GROUPE_RUBRIQUE_BUDGETAIRE PRIMARY KEY CLUSTERED (IdGroupeRB)
    );

    CREATE NONCLUSTERED INDEX IX_GRB_CodeGroupe
        ON dbo.GROUPE_RUBRIQUE_BUDGETAIRE (CodeGroupe);

    CREATE UNIQUE NONCLUSTERED INDEX UQ_GRB_Code_Libelle
        ON dbo.GROUPE_RUBRIQUE_BUDGETAIRE (CodeGroupe, Libelle);
END;
GO

------------------------------------------------------------
-- 2) Colonne FK nullable sur RUBRIQUE_BUDGETAIRE
------------------------------------------------------------
IF COL_LENGTH(N'dbo.RUBRIQUE_BUDGETAIRE', N'FK_GroupeRubriqueBudgetaire') IS NULL
BEGIN
    ALTER TABLE dbo.RUBRIQUE_BUDGETAIRE
        ADD FK_GroupeRubriqueBudgetaire bigint NULL;
END;
GO

------------------------------------------------------------
-- 3) Index + contrainte FK
------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RB_FK_Groupe' AND object_id = OBJECT_ID(N'dbo.RUBRIQUE_BUDGETAIRE')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_RB_FK_Groupe
        ON dbo.RUBRIQUE_BUDGETAIRE (FK_GroupeRubriqueBudgetaire);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RB_GROUPE'
)
BEGIN
    ALTER TABLE dbo.RUBRIQUE_BUDGETAIRE
        ADD CONSTRAINT FK_RB_GROUPE
        FOREIGN KEY (FK_GroupeRubriqueBudgetaire)
        REFERENCES dbo.GROUPE_RUBRIQUE_BUDGETAIRE (IdGroupeRB);
END;
GO

------------------------------------------------------------
-- 4) Groupes + rattachement feuilles (transaction)
------------------------------------------------------------
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

MERGE dbo.GROUPE_RUBRIQUE_BUDGETAIRE AS t
USING (VALUES
    ('00', N'ACHAT ET VARIATIONS DE STOCKS', 1),
    ('01', N'TRANSPORTS', 2),
    ('02', N'SERVICES EXTERIEURS A', 3),
    ('02', N'SERVICES EXTERIEURS B', 4),
    ('04', N'IMPOTS ET TAXES', 5),
    ('05', N'AUTRES CHARGES', 6),
    ('06', N'CHARGES DU PERSONNEL', 7),
    ('07', N'FRAIS FINANCIERS ET CHARGES ASSIMILEES', 8),
    ('08', N'DOTATIONS AUX AMORTISSEMENTS', 9),
    ('09', N'DOTATIONS AUX PROVISIONS', 10)
) AS s (CodeGroupe, Libelle, OrdreAffichage)
ON t.CodeGroupe = s.CodeGroupe AND t.Libelle = s.Libelle
WHEN NOT MATCHED THEN
    INSERT (CodeGroupe, Libelle, OrdreAffichage, Actif)
    VALUES (s.CodeGroupe, s.Libelle, s.OrdreAffichage, 1)
WHEN MATCHED AND (t.OrdreAffichage <> s.OrdreAffichage OR t.Actif <> 1) THEN
    UPDATE SET OrdreAffichage = s.OrdreAffichage, Actif = 1;

;WITH MapSection AS (
    SELECT * FROM (VALUES
        ('0010',  '00', N'ACHAT ET VARIATIONS DE STOCKS'),
        ('0011',  '00', N'ACHAT ET VARIATIONS DE STOCKS'),
        ('0012',  '00', N'ACHAT ET VARIATIONS DE STOCKS'),
        ('0120',  '01', N'TRANSPORTS'),
        ('0121',  '01', N'TRANSPORTS'),
        ('0230',  '02', N'SERVICES EXTERIEURS A'),
        ('0330',  '02', N'SERVICES EXTERIEURS B'),
        ('0450',  '04', N'IMPOTS ET TAXES'),
        ('0540',  '05', N'AUTRES CHARGES'),
        ('0650',  '06', N'CHARGES DU PERSONNEL'),
        ('0770',  '07', N'FRAIS FINANCIERS ET CHARGES ASSIMILEES'),
        ('08800', '08', N'DOTATIONS AUX AMORTISSEMENTS'),
        ('09810', '09', N'DOTATIONS AUX PROVISIONS')
    ) AS v (CodeSection, CodeGroupe, LibelleGroupe)
),
Cible AS (
    SELECT
        rb.IdRB,
        g.IdGroupeRB
    FROM dbo.RUBRIQUE_BUDGETAIRE rb
    INNER JOIN dbo.RUBRIQUE_BUDGETAIRE parent
        ON parent.IdRB = rb.FK_RubriqueBudgetaireParent
    INNER JOIN MapSection m
        ON m.CodeSection = parent.CodeRB
    INNER JOIN dbo.GROUPE_RUBRIQUE_BUDGETAIRE g
        ON g.CodeGroupe = m.CodeGroupe
       AND g.Libelle = m.LibelleGroupe
    WHERE rb.FK_RubriqueBudgetaireParent IS NOT NULL
)
UPDATE rb
SET rb.FK_GroupeRubriqueBudgetaire = c.IdGroupeRB
FROM dbo.RUBRIQUE_BUDGETAIRE rb
INNER JOIN Cible c ON c.IdRB = rb.IdRB;

DECLARE @NbGroupes int =
    (SELECT COUNT(*) FROM dbo.GROUPE_RUBRIQUE_BUDGETAIRE);

DECLARE @NbFeuilles int =
    (SELECT COUNT(*) FROM dbo.RUBRIQUE_BUDGETAIRE WHERE FK_RubriqueBudgetaireParent IS NOT NULL);

DECLARE @NbFeuillesAvecGroupe int =
    (SELECT COUNT(*) FROM dbo.RUBRIQUE_BUDGETAIRE
     WHERE FK_RubriqueBudgetaireParent IS NOT NULL
       AND FK_GroupeRubriqueBudgetaire IS NOT NULL);

DECLARE @NbFeuillesSansGroupe int =
    (SELECT COUNT(*) FROM dbo.RUBRIQUE_BUDGETAIRE
     WHERE FK_RubriqueBudgetaireParent IS NOT NULL
       AND FK_GroupeRubriqueBudgetaire IS NULL);

DECLARE @NbNiveau1 int =
    (SELECT COUNT(*) FROM dbo.RUBRIQUE_BUDGETAIRE WHERE Niveau = 1);

DECLARE @NbNiveau1SansGroupe int =
    (SELECT COUNT(*) FROM dbo.RUBRIQUE_BUDGETAIRE
     WHERE Niveau = 1 AND FK_GroupeRubriqueBudgetaire IS NULL);

PRINT '=== CONTROLES ===';
PRINT CONCAT('NbGroupes=', @NbGroupes, ' (attendu 10)');
PRINT CONCAT('NbFeuilles=', @NbFeuilles, ' (attendu 51)');
PRINT CONCAT('NbFeuillesAvecGroupe=', @NbFeuillesAvecGroupe, ' (attendu 51)');
PRINT CONCAT('NbFeuillesSansGroupe=', @NbFeuillesSansGroupe, ' (attendu 0)');
PRINT CONCAT('NbNiveau1=', @NbNiveau1, ' (attendu 51)');
PRINT CONCAT('NbNiveau1SansGroupe=', @NbNiveau1SansGroupe, ' (attendu 0)');

SELECT
    rb.CodeRB,
    rb.Libelle AS LibelleRB,
    parent.CodeRB AS SectionParent,
    g.CodeGroupe,
    g.Libelle AS LibelleGroupe
FROM dbo.RUBRIQUE_BUDGETAIRE rb
INNER JOIN dbo.RUBRIQUE_BUDGETAIRE parent
    ON parent.IdRB = rb.FK_RubriqueBudgetaireParent
LEFT JOIN dbo.GROUPE_RUBRIQUE_BUDGETAIRE g
    ON g.IdGroupeRB = rb.FK_GroupeRubriqueBudgetaire
WHERE rb.FK_RubriqueBudgetaireParent IS NOT NULL
ORDER BY g.OrdreAffichage, rb.CodeRB;

SELECT
    rb.CodeRB,
    g.CodeGroupe,
    g.Libelle AS LibelleGroupe
FROM dbo.RUBRIQUE_BUDGETAIRE rb
LEFT JOIN dbo.GROUPE_RUBRIQUE_BUDGETAIRE g
    ON g.IdGroupeRB = rb.FK_GroupeRubriqueBudgetaire
WHERE rb.CodeRB IN (
    '00100','00110','00113','00124',
    '01201','01205','01210',
    '02300','03316','04511','06510','07700'
)
ORDER BY rb.CodeRB;

IF @NbGroupes <> 10
   OR @NbFeuilles <> 51
   OR @NbFeuillesAvecGroupe <> 51
   OR @NbFeuillesSansGroupe <> 0
   OR @NbNiveau1 <> 51
   OR @NbNiveau1SansGroupe <> 0
BEGIN
    PRINT 'ECHEC DES CONTROLES — ROLLBACK';
    ROLLBACK TRANSACTION;
    THROW 50001, 'Controles GROUPE_RUBRIQUE_BUDGETAIRE echoues — transaction annulee.', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.RUBRIQUE_BUDGETAIRE rb
    JOIN dbo.GROUPE_RUBRIQUE_BUDGETAIRE g ON g.IdGroupeRB = rb.FK_GroupeRubriqueBudgetaire
    WHERE rb.CodeRB = '03316' AND g.CodeGroupe = '02' AND g.Libelle = N'SERVICES EXTERIEURS B'
)
BEGIN
    PRINT 'ECHEC controle 03316';
    ROLLBACK TRANSACTION;
    THROW 50002, 'Controle cible 03316 echoue.', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.RUBRIQUE_BUDGETAIRE rb
    JOIN dbo.GROUPE_RUBRIQUE_BUDGETAIRE g ON g.IdGroupeRB = rb.FK_GroupeRubriqueBudgetaire
    WHERE rb.CodeRB = '02300' AND g.CodeGroupe = '02' AND g.Libelle = N'SERVICES EXTERIEURS A'
)
BEGIN
    PRINT 'ECHEC controle 02300';
    ROLLBACK TRANSACTION;
    THROW 50003, 'Controle cible 02300 echoue.', 1;
END;

PRINT 'CONTROLES OK — COMMIT';
COMMIT TRANSACTION;
GO
