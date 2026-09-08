using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    private static void ExigerMiseAJourReussie(int rowsAffected)
    {
        if (rowsAffected == 0)
            throw new DemandePaiementConcurrencyException();
    }

    private async Task AppliquerTransitionStatutAsync(
        long idDemande,
        DemandePaiementEntity demande,
        string nouveauStatut,
        DemandePaiementConditionalUpdatePatch patch,
        CancellationToken cancellationToken)
    {
        var statutAttendu = StatutDemandePaiement.Normaliser(demande.Statut);
        var statutCible = StatutDemandePaiement.Normaliser(nouveauStatut);

        if (!DemandePaiementWorkflow.PeutTransitionner(statutAttendu, statutCible))
        {
            if (StatutDemandePaiement.EstProgressionApres(statutAttendu, statutCible))
                throw new DemandePaiementConcurrencyException();

            throw new InvalidOperationException(
                $"Transition interdite : {statutAttendu} → {statutCible}.");
        }

        var rows = await _repository.UpdateIfStatutMatchesAsync(
            idDemande,
            statutAttendu,
            patch with { NouveauStatut = statutCible },
            cancellationToken);
        ExigerMiseAJourReussie(rows);

        demande.Statut = statutCible;
    }

    private async Task AppliquerMiseAJourSiStatutAsync(
        long idDemande,
        string statutAttendu,
        DemandePaiementConditionalUpdatePatch patch,
        CancellationToken cancellationToken)
    {
        var rows = await _repository.UpdateIfStatutMatchesAsync(
            idDemande,
            statutAttendu,
            patch,
            cancellationToken);
        ExigerMiseAJourReussie(rows);
    }
}
