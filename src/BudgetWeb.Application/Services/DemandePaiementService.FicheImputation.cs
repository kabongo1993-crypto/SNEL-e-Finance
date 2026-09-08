using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    public async Task<FicheImputationBudgetaireDto> GetFicheImputationAsync(
        long idDemande,
        FicheImputationMode mode,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();

        var demande = await _repository.GetFicheImputationContextAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesFicheImputationAsync(demande, mode, cancellationToken);

        return mode switch
        {
            FicheImputationMode.Travail => await FicheImputationBudgetaireBuilder.BuildTravailAsync(
                demande,
                _repository,
                cancellationToken),
            FicheImputationMode.Definitive => await FicheImputationBudgetaireBuilder.BuildDefinitiveAsync(
                demande,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode de fiche inconnu."),
        };
    }

    public async Task<byte[]> GenererFicheImputationPdfAsync(
        long idDemande,
        FicheImputationMode mode,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetFicheImputationAsync(idDemande, mode, cancellationToken);

        var userId = _currentUser.RequireUserId();
        await _repository.AddAuditAsync(
            userId,
            mode == FicheImputationMode.Definitive ? "IMPRIMER_FIB_DEF" : "IMPRIMER_FIB_TRAVAIL",
            idDemande,
            null,
            new { payload.Reference, mode = payload.Mode.ToString() },
            cancellationToken);

        return _ficheImputationRenderer.Render(payload);
    }

    private async Task GarantirAccesFicheImputationAsync(
        Domain.Entities.DemandePaiement demande,
        FicheImputationMode mode,
        CancellationToken cancellationToken)
    {
        await GarantirAccesDemandeAsync(demande, cancellationToken);

        if (mode == FicheImputationMode.Travail)
        {
            ExigerStatut(
                demande,
                StatutDemandePaiement.EnControleBudgetaire,
                "La fiche de travail n'est disponible qu'en contrôle budgétaire.");

            if (demande.Imputations.Count == 0)
            {
                throw new InvalidOperationException(
                    "Enregistrez au moins une imputation avant d'imprimer la fiche de travail.");
            }

            var peutImputerOuControler = _currentUser.HasPermission(AppPermissions.PaiementsControlerBudget)
                || _currentUser.HasPermission(AppPermissions.PaiementsImputerDc)
                || _currentUser.HasPermission(AppPermissions.PaiementsImputerAe)
                || _currentUser.HasPermission(AppPermissions.PaiementsImputerBi);

            if (!peutImputerOuControler)
            {
                throw new UnauthorizedAccessException(
                    "Vous n'avez pas accès à la fiche d'imputation de travail.");
            }

            return;
        }

        ExigerStatut(
            demande,
            StatutDemandePaiement.ViseeBudgetairement,
            "La fiche définitive n'est disponible qu'après visa budgétaire.");
    }
}
