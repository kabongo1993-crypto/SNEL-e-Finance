-- =============================================================================
-- DIAGNOSTIC Lot 3.6.7 — Audit fallbacks routage / retours (LECTURE SEULE)
-- Base : BD_SNEL — schéma dpm
-- NE PAS modifier les données. Idempotent, sans effet de bord.
-- =============================================================================

SET NOCOUNT ON;

PRINT '=== Lot 3.6.7 — Prérequis migration routage ===';
SELECT
    CASE WHEN OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_ROUTAGE', N'U') IS NOT NULL
         THEN N'PRESENT' ELSE N'ABSENT' END AS Table_DEMANDE_PAIEMENT_ROUTAGE,
    CASE WHEN COL_LENGTH(N'dpm.DEMANDE_PAIEMENT', N'FK_UtilisateurAssigne') IS NOT NULL
         THEN N'PRESENT' ELSE N'ABSENT' END AS Colonne_FK_UtilisateurAssigne;

PRINT '=== Synthèse DPM ===';
SELECT COUNT(*) AS NbDpm FROM dpm.DEMANDE_PAIEMENT;

SELECT Statut, COUNT(*) AS Nb
FROM dpm.DEMANDE_PAIEMENT
GROUP BY Statut
ORDER BY Nb DESC;

PRINT '=== Retours legacy (colonnes DPM, hors routage) ===';
SELECT
    COUNT(*) AS NbAvecMotifRetour
FROM dpm.DEMANDE_PAIEMENT
WHERE MotifRetour IS NOT NULL AND LTRIM(RTRIM(MotifRetour)) <> N'';

SELECT
    COUNT(*) AS NbAvecDateRetour
FROM dpm.DEMANDE_PAIEMENT
WHERE DateRetour IS NOT NULL;

SELECT
    COUNT(*) AS NbAvecFkUtilisateurRetour
FROM dpm.DEMANDE_PAIEMENT
WHERE FK_UtilisateurRetour IS NOT NULL;

SELECT
    IdDemandePaiement,
    Reference,
    Statut,
    FK_UtilisateurCreation,
    FK_UtilisateurRetour,
    DateRetour,
    MotifRetour,
    LEFT(CommentaireRetour, 120) AS CommentaireRetour
FROM dpm.DEMANDE_PAIEMENT
WHERE FK_UtilisateurRetour IS NOT NULL
   OR DateRetour IS NOT NULL
   OR (MotifRetour IS NOT NULL AND LTRIM(RTRIM(MotifRetour)) <> N'');

PRINT '=== Utilisateurs référencés par DPM (orphelins) ===';
;WITH Refs AS (
    SELECT FK_UtilisateurCreation AS IdUser FROM dpm.DEMANDE_PAIEMENT
    UNION SELECT FK_UtilisateurRetour FROM dpm.DEMANDE_PAIEMENT WHERE FK_UtilisateurRetour IS NOT NULL
    UNION SELECT FK_UtilisateurReception FROM dpm.DEMANDE_PAIEMENT WHERE FK_UtilisateurReception IS NOT NULL
    UNION SELECT FK_UtilisateurControle FROM dpm.DEMANDE_PAIEMENT WHERE FK_UtilisateurControle IS NOT NULL
    UNION SELECT FK_UtilisateurSoumission FROM dpm.DEMANDE_PAIEMENT WHERE FK_UtilisateurSoumission IS NOT NULL
    UNION SELECT FK_UtilisateurVisa FROM dpm.DEMANDE_PAIEMENT WHERE FK_UtilisateurVisa IS NOT NULL
)
SELECT r.IdUser
FROM Refs r
LEFT JOIN dbo.UTILISATEUR u ON u.IdUtilisateur = r.IdUser
WHERE u.IdUtilisateur IS NULL;

PRINT '=== DPM sans routage (si table présente) ===';
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_ROUTAGE', N'U') IS NOT NULL
BEGIN
    SELECT d.IdDemandePaiement, d.Reference, d.Statut
    FROM dpm.DEMANDE_PAIEMENT d
    WHERE NOT EXISTS (
        SELECT 1 FROM dpm.DEMANDE_PAIEMENT_ROUTAGE r
        WHERE r.FK_DemandePaiement = d.IdDemandePaiement
    )
    ORDER BY d.IdDemandePaiement;
END
ELSE
    PRINT 'Table dpm.DEMANDE_PAIEMENT_ROUTAGE absente — toutes les DPM sont sans historique routage.';

PRINT '=== Routages actifs multiples (si table présente) ===';
IF OBJECT_ID(N'dpm.DEMANDE_PAIEMENT_ROUTAGE', N'U') IS NOT NULL
BEGIN
    SELECT FK_DemandePaiement, COUNT(*) AS NbActifs
    FROM dpm.DEMANDE_PAIEMENT_ROUTAGE
    WHERE EstActif = 1
    GROUP BY FK_DemandePaiement
    HAVING COUNT(*) > 1;

    PRINT '=== Retours par type (action + statuts) ===';
    SELECT
        Action,
        StatutSource,
        StatutCible,
        COUNT(*) AS Nb
    FROM dpm.DEMANDE_PAIEMENT_ROUTAGE
    WHERE Action IN (
        N'REJET_VALIDATION_ENTITE',
        N'RETOUR_DEMANDEUR',
        N'RETOUR_INTER_ETAPES',
        N'RETOUR_VISA'
    )
    GROUP BY Action, StatutSource, StatutCible
    ORDER BY Action, StatutSource, StatutCible;

    PRINT '=== Routages sans utilisateur cible valide ===';
    SELECT r.IdRoutage, r.FK_DemandePaiement, r.Action, r.FK_UtilisateurCible
    FROM dpm.DEMANDE_PAIEMENT_ROUTAGE r
    LEFT JOIN dbo.UTILISATEUR u ON u.IdUtilisateur = r.FK_UtilisateurCible
    WHERE r.FK_UtilisateurCible IS NULL
       OR r.FK_UtilisateurCible <= 0
       OR u.IdUtilisateur IS NULL;

    PRINT '=== Incohérence statut source/cible vs statut DPM courant ===';
    SELECT
        d.IdDemandePaiement,
        d.Reference,
        d.Statut AS StatutCourant,
        r.IdRoutage,
        r.Action,
        r.StatutCible AS DernierStatutCibleRoutage,
        r.EstActif,
        r.DateRoutage
    FROM dpm.DEMANDE_PAIEMENT d
    CROSS APPLY (
        SELECT TOP 1 *
        FROM dpm.DEMANDE_PAIEMENT_ROUTAGE rx
        WHERE rx.FK_DemandePaiement = d.IdDemandePaiement
        ORDER BY rx.DateRoutage DESC, rx.IdRoutage DESC
    ) r
    WHERE d.Statut <> r.StatutCible;
END;

PRINT '=== Validation entité (N1/N2) pour DPM en circuit entité ===';
SELECT
    d.IdDemandePaiement,
    d.Reference,
    d.Statut,
    v.Niveau,
    v.Statut AS StatutValidation,
    v.FK_UtilisateurValidateur,
    v.FK_UtilisateurDeclarant
FROM dpm.DEMANDE_PAIEMENT d
JOIN dpm.DEMANDE_PAIEMENT_VALIDATION v ON v.FK_DemandePaiement = d.IdDemandePaiement
WHERE d.Statut IN (N'EN_VALIDATION_N1', N'EN_VALIDATION_N2', N'A_CORRIGER')
ORDER BY d.IdDemandePaiement, v.Niveau;
