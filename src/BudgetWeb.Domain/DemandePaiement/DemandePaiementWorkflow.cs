using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Transitions d'état DPM phase 1. Ne contrôle pas le crédit budgétaire.</summary>
public static class DemandePaiementWorkflow
{
    public static void Transitionner(Entities.DemandePaiement demande, string nouveauStatut)
    {
        ArgumentNullException.ThrowIfNull(demande);

        if (!StatutDemandePaiement.IsValid(nouveauStatut))
            throw new ArgumentException($"Statut DPM invalide : {nouveauStatut}.", nameof(nouveauStatut));

        var statutActuel = StatutDemandePaiement.Normaliser(demande.Statut);
        var statutCible = StatutDemandePaiement.Normaliser(nouveauStatut);

        if (!StatutDemandePaiement.EstTransitionAutorisee(statutActuel, statutCible))
            throw new InvalidOperationException(
                $"Transition interdite : {statutActuel} → {statutCible}.");

        demande.Statut = statutCible;
    }

    public static bool PeutTransitionner(string? statutActuel, string? nouveauStatut)
        => StatutDemandePaiement.EstTransitionAutorisee(statutActuel, nouveauStatut);
}
