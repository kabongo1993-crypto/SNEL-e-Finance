using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.DpmConsultation;

/// <summary>Projection lecture seule des destinataires de retour depuis DEMANDE_PAIEMENT_ROUTAGE (Lot 3.6.3).</summary>
public static class DemandePaiementRetourLecture
{
    private static readonly HashSet<string> ActionsRetour = new(StringComparer.OrdinalIgnoreCase)
    {
        DemandePaiementRoutageAction.RejetValidationEntite,
        DemandePaiementRoutageAction.RetourDemandeur,
        DemandePaiementRoutageAction.RetourInterEtapes,
        DemandePaiementRoutageAction.RetourVisa,
    };

    public static bool EstRoutageRetour(DemandePaiementRoutage routage)
        => ActionsRetour.Contains(routage.Action);

    public static string? ClassifierTypeRetour(string action, string statutSource, string statutCible)
    {
        var src = StatutDemandePaiement.Normaliser(statutSource);
        var dst = StatutDemandePaiement.Normaliser(statutCible);

        if (string.Equals(action, DemandePaiementRoutageAction.RejetValidationEntite, StringComparison.OrdinalIgnoreCase))
        {
            if (src == StatutDemandePaiement.EnValidationN2 && dst == StatutDemandePaiement.EnValidationN1)
                return DemandePaiementRetourType.N2VersN1;
            if (src == StatutDemandePaiement.EnValidationN1 && dst == StatutDemandePaiement.ACorriger)
                return DemandePaiementRetourType.N1VersDemandeur;
        }

        if (string.Equals(action, DemandePaiementRoutageAction.RetourDemandeur, StringComparison.OrdinalIgnoreCase)
            && src == StatutDemandePaiement.EnTraitementDpm
            && dst == StatutDemandePaiement.ACorriger)
        {
            return DemandePaiementRetourType.ChargeVersDemandeur;
        }

        if (string.Equals(action, DemandePaiementRoutageAction.RetourInterEtapes, StringComparison.OrdinalIgnoreCase)
            && src == StatutDemandePaiement.EnControleBudgetaire
            && dst == StatutDemandePaiement.EnTraitementDpm)
        {
            return DemandePaiementRetourType.JuniorVersCharge;
        }

        if (string.Equals(action, DemandePaiementRoutageAction.RetourVisa, StringComparison.OrdinalIgnoreCase)
            && src == StatutDemandePaiement.EnControleBudgetaire
            && dst == StatutDemandePaiement.EnControleBudgetaire)
        {
            return DemandePaiementRetourType.VisaVersControle;
        }

        return null;
    }

    public static DemandePaiementRetourDestinataireDto Map(DemandePaiementRoutage routage)
    {
        long? idDestinataire = routage.FK_UtilisateurCible > 0 ? routage.FK_UtilisateurCible : null;
        return new(
            routage.IdRoutage,
            ClassifierTypeRetour(routage.Action, routage.StatutSource, routage.StatutCible),
            routage.Action,
            routage.StatutSource,
            routage.StatutCible,
            idDestinataire,
            routage.UtilisateurCible?.Nom,
            routage.UtilisateurCible?.Prenom,
            routage.DateRoutage,
            routage.Motif);
    }

    public static IReadOnlyList<DemandePaiementRetourDestinataireDto> FromRoutages(
        IEnumerable<DemandePaiementRoutage> routages)
        => routages
            .Where(EstRoutageRetour)
            .OrderBy(r => r.DateRoutage)
            .ThenBy(r => r.IdRoutage)
            .Select(Map)
            .ToList();
}
