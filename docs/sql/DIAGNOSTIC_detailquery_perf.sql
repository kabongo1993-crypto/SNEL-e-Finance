-- Diagnostic DetailQuery — STATISTICS IO/TIME sur composantes FK
-- Usage: sqlcmd -S localhost\HEROS_SQL19 -d BD_SNEL -E -i docs/sql/DIAGNOSTIC_detailquery_perf.sql -v IdDemande=50

SET NOCOUNT ON;
DECLARE @id BIGINT = $(IdDemande);

PRINT '=== Volumes DPM #' + CAST(@id AS VARCHAR(20)) + ' ===';
SELECT d.IdDemandePaiement, d.Statut, d.Reference,
       (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_BENEFICIAIRE b WHERE b.FK_DemandePaiement = d.IdDemandePaiement) AS NbBenef,
       (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION i WHERE i.FK_DemandePaiement = d.IdDemandePaiement) AS NbImput,
       (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT s WHERE s.FK_DemandePaiement = d.IdDemandePaiement) AS NbSnap,
       (SELECT COUNT(*) FROM dpm.PIECE_JOINTE p WHERE p.FK_DemandePaiement = d.IdDemandePaiement) AS NbPieces,
       (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_VALIDATION v WHERE v.FK_DemandePaiement = d.IdDemandePaiement) AS NbVal
FROM dpm.DEMANDE_PAIEMENT d
WHERE d.IdDemandePaiement = @id;

PRINT '=== Produits cartésiens théoriques (collections) ===';
SELECT
    ISNULL(b.c,0) * ISNULL(i.c,0) * ISNULL(p.c,0) * ISNULL(v.c,0) AS ProduitCollections,
    ISNULL(b.c,0) AS benef, ISNULL(i.c,0) AS imput, ISNULL(p.c,0) AS pieces, ISNULL(v.c,0) AS val
FROM (SELECT COUNT(*) c FROM dpm.DEMANDE_PAIEMENT_BENEFICIAIRE WHERE FK_DemandePaiement = @id) b
CROSS JOIN (SELECT COUNT(*) c FROM dpm.DEMANDE_PAIEMENT_IMPUTATION WHERE FK_DemandePaiement = @id) i
CROSS JOIN (SELECT COUNT(*) c FROM dpm.PIECE_JOINTE WHERE FK_DemandePaiement = @id) p
CROSS JOIN (SELECT COUNT(*) c FROM dpm.DEMANDE_PAIEMENT_VALIDATION WHERE FK_DemandePaiement = @id) v;

PRINT '=== FK lookups FROID (clean buffers si droits) ===';
-- DBCC DROPCLEANBUFFERS; -- nécessite VIEW SERVER STATE
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT WHERE IdDemandePaiement = @id;
SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_BENEFICIAIRE WHERE FK_DemandePaiement = @id;
SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION WHERE FK_DemandePaiement = @id;
SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT WHERE FK_DemandePaiement = @id;
SELECT COUNT(*) FROM dpm.PIECE_JOINTE WHERE FK_DemandePaiement = @id;
SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_VALIDATION WHERE FK_DemandePaiement = @id;

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;

PRINT '=== FK lookups CHAUD (répétition immédiate) ===';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT WHERE IdDemandePaiement = @id;
SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_BENEFICIAIRE WHERE FK_DemandePaiement = @id;
SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION WHERE FK_DemandePaiement = @id;
SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT WHERE FK_DemandePaiement = @id;
SELECT COUNT(*) FROM dpm.PIECE_JOINTE WHERE FK_DemandePaiement = @id;
SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_VALIDATION WHERE FK_DemandePaiement = @id;

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;

PRINT '=== Index sur tables DetailQuery ===';
SELECT OBJECT_SCHEMA_NAME(i.object_id) + '.' + OBJECT_NAME(i.object_id) AS [Table],
       i.name, i.type_desc,
       STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyCols
FROM sys.indexes i
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE OBJECT_NAME(i.object_id) IN (
    'DEMANDE_PAIEMENT','DEMANDE_PAIEMENT_BENEFICIAIRE','DEMANDE_PAIEMENT_IMPUTATION',
    'DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT','PIECE_JOINTE','DEMANDE_PAIEMENT_VALIDATION',
    'BILLET_CONVERSION','PIECE_CAISSE','BON_PROVISOIRE','MINUTE_CHEQUE')
  AND ic.is_included_column = 0 AND i.type > 0
GROUP BY i.object_id, i.name, i.type_desc, i.index_id
ORDER BY 1, 2;
