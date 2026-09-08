-- =============================================================================
-- OPTIONAL — Correction des libellés DPM double-encodés (UTF-8 lu en ANSI)
-- Symptôme : "Dollar amÃ©ricain", "DÃ©cÃ¨s", "CongÃ©", etc.
-- Cause typique : sqlcmd sans -f 65001 sur un fichier UTF-8.
-- Idempotent. Ne touche ni codes, ni FK, ni workflow.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

-- Helper inline : remplace les séquences UTF-8 mal interprétées
-- C3 A9 → é, C3 A8 → è, C3 AA → ê, C3 A0 → à, C3 B4 → ô, etc.

DECLARE @Tables TABLE (SchemaName SYSNAME, TableName SYSNAME, ColName SYSNAME);
INSERT INTO @Tables (SchemaName, TableName, ColName) VALUES
    (N'dpm', N'DEVISE', N'Libelle'),
    (N'dpm', N'DEVISE', N'Symbole'),
    (N'dpm', N'CAS_DOSSIER', N'Libelle'),
    (N'dpm', N'CAS_DOSSIER_PIECE_OBLIGATOIRE', N'Libelle');

DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql = @sql + N'
IF OBJECT_ID(N''' + SchemaName + N'.' + TableName + N''', N''U'') IS NOT NULL
   AND COL_LENGTH(N''' + SchemaName + N'.' + TableName + N''', N''' + ColName + N''') IS NOT NULL
BEGIN
    UPDATE ' + QUOTENAME(SchemaName) + N'.' + QUOTENAME(TableName) + N'
    SET ' + QUOTENAME(ColName) + N' = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            ' + QUOTENAME(ColName) + N',
            NCHAR(0x00C3)+NCHAR(0x00A9), NCHAR(233)),  -- é
            NCHAR(0x00C3)+NCHAR(0x00A8), NCHAR(232)),  -- è
            NCHAR(0x00C3)+NCHAR(0x00AA), NCHAR(234)),  -- ê
            NCHAR(0x00C3)+NCHAR(0x00A0), NCHAR(224)),  -- à
            NCHAR(0x00C3)+NCHAR(0x00B4), NCHAR(244)),  -- ô
            NCHAR(0x00C3)+NCHAR(0x00AE), NCHAR(238)),  -- î
            NCHAR(0x00C3)+NCHAR(0x00A7), NCHAR(231)),  -- ç
            NCHAR(0x00C3)+NCHAR(0x00B9), NCHAR(249))   -- ù
    WHERE ' + QUOTENAME(ColName) + N' LIKE N''%'' + NCHAR(0x00C3) + N''%'';
END;
'
FROM @Tables;

EXEC sp_executesql @sql;

-- Réaligner explicitement les seeds Devise (source de vérité)
IF OBJECT_ID(N'dpm.DEVISE', N'U') IS NOT NULL
BEGIN
    UPDATE dpm.DEVISE SET Libelle = N'Franc congolais', Symbole = N'FC' WHERE Code = 'CDF';
    UPDATE dpm.DEVISE SET Libelle = N'Dollar am' + NCHAR(233) + N'ricain', Symbole = N'$' WHERE Code = 'USD';
    UPDATE dpm.DEVISE SET Libelle = N'Euro', Symbole = NCHAR(8364) WHERE Code = 'EUR';
END;

-- Réaligner les cas de dossier avec accents (Annexe IGCF)
IF OBJECT_ID(N'dpm.CAS_DOSSIER', N'U') IS NOT NULL
BEGIN
    UPDATE dpm.CAS_DOSSIER SET Libelle = N'Pr' + NCHAR(234) + N'ts sociaux' WHERE Code = 'PRETS_SOCIAUX';
    UPDATE dpm.CAS_DOSSIER SET Libelle = N'D' + NCHAR(233) + N'c' + NCHAR(232) + N's' WHERE Code = 'DECES';
    UPDATE dpm.CAS_DOSSIER SET Libelle = N'Cong' + NCHAR(233) + N' non pris' WHERE Code = 'CONGE_NON_PRIS';
    UPDATE dpm.CAS_DOSSIER SET Libelle = N'D' + NCHAR(233) + N'compte final' WHERE Code = 'DECOMPTE_FINAL';
    UPDATE dpm.CAS_DOSSIER SET Libelle = N'Imp' + NCHAR(244) + N'ts ou taxes' WHERE Code = 'IMPOTS_TAXES';
    UPDATE dpm.CAS_DOSSIER SET Libelle = N'Treizi' + NCHAR(232) + N'me mois' WHERE Code = 'TREIZIEME_MOIS';
    UPDATE dpm.CAS_DOSSIER SET Libelle = N'Vivres fin d''' + N'ann' + NCHAR(233) + N'e' WHERE Code = 'VIVRES_FIN_ANNEE';
    UPDATE dpm.CAS_DOSSIER SET Libelle = N'R' + NCHAR(233) + N'trocession' WHERE Code = 'RETROCESSION';
    UPDATE dpm.CAS_DOSSIER SET Libelle = N'Fonds ' + NCHAR(224) + N' justifier pour diverses interventions' WHERE Code = 'FONDS_A_JUSTIFIER';
END;

COMMIT TRAN;

PRINT N'=== Contrôle encoding libellés DPM ===';
SELECT Code, Libelle, Symbole FROM dpm.DEVISE ORDER BY Code;
SELECT Code, Libelle FROM dpm.CAS_DOSSIER ORDER BY Ordre, Code;
GO
