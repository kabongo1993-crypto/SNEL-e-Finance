using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>
/// Règles pures de backfill pour <c>FK_UtilisateurAssigne</c> (Lot 3.1).
/// Miroir documentaire du script SQL OPTIONAL_dpm_routage_assignation.sql — sans effet runtime workflow.
/// </summary>
public static class DemandePaiementAssignationBackfillRules
{
    public sealed record ControleAuditContext(
        long? DernierControleurAudit,
        bool PossedeAuditOrienter);

    /// <summary>
    /// Résout l'assignation courante à partir du statut et des colonnes snapshot (hors audit).
    /// Pour <see cref="StatutDemandePaiement.EnControleBudgetaire"/>, préférer
    /// <see cref="ResoudreAssignationControleBudgetaire"/>.
    /// </summary>
    public static long? ResoudreAssignation(
        string? statut,
        long fkUtilisateurCreation,
        long? fkUtilisateurReception)
    {
        var normalise = StatutDemandePaiement.Normaliser(statut);

        return normalise switch
        {
            StatutDemandePaiement.Brouillon or StatutDemandePaiement.ACorriger => fkUtilisateurCreation,
            StatutDemandePaiement.EnValidationN1 or StatutDemandePaiement.EnValidationN2 => null,
            StatutDemandePaiement.ValideeEntite or StatutDemandePaiement.Soumise => null,
            StatutDemandePaiement.EnTraitementDpm => fkUtilisateurReception,
            StatutDemandePaiement.EnControleBudgetaire => null,
            StatutDemandePaiement.ViseeBudgetairement => null,
            _ => null,
        };
    }

    /// <summary>
    /// Règle EN_CONTROLE_BUDGETAIRE : audit CONTROLER prioritaire, fallback FK_UtilisateurControle
    /// uniquement sans audit ORIENTER (interprétation PrendreEnControle directe).
    /// </summary>
    public static long? ResoudreAssignationControleBudgetaire(
        long? fkUtilisateurControle,
        ControleAuditContext audit)
    {
        if (audit.DernierControleurAudit is long controleur)
            return controleur;

        if (!audit.PossedeAuditOrienter && fkUtilisateurControle is long controle)
            return controle;

        return null;
    }
}
