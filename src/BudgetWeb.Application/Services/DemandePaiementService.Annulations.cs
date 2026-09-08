using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    public Task<DemandePaiementDetailDto> AnnulerSoumissionAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerSoumettre();
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Lire, ct);
            ExigerStatut(demande, StatutDemandePaiement.Soumise,
                "Seule une demande soumise peut faire l'objet d'une annulation de soumission.");

            if (demande.DateReception is not null)
                throw new InvalidOperationException(
                    "Impossible d'annuler la soumission : la demande a déjà été réceptionnée par le Budget.");

            if (demande.FK_UtilisateurSoumission is not long soumetteur || soumetteur != userId)
                throw new UnauthorizedAccessException(
                    "Seul l'utilisateur ayant soumis la demande peut annuler la soumission.");

            var statutAvant = demande.Statut;
            var now = DateTime.Now;
            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                StatutDemandePaiement.ValideeEntite,
                new DemandePaiementConditionalUpdatePatch
                {
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                    EffacerSoumission = true,
                    FK_UtilisateurAssigne = null,
                    MettreAJourAssigne = true,
                },
                ct);

            await _repository.AddAuditAsync(userId, "ANNULER_SOUMISSION", idDemande,
                new { statut = statutAvant },
                new { statut = StatutDemandePaiement.ValideeEntite },
                ct);

            return await MapperDetailApresMutationAsync(idDemande, ct);
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> AnnulerValidationN2Async(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await AssurerValidationsEntiteAsync(demande, ct);
            var n2 = ValidationEntiteRules.ObtenirNiveau(demande, ValidationEntiteNiveau.N2);
            ExigerPermissionAnnulationValidationN2(n2);
            ExigerActeurValidation(n2, userId);

            ExigerStatut(demande, StatutDemandePaiement.ValideeEntite,
                "La demande doit être validée entité pour annuler la validation N2.");
            ValidationEntiteRules.ExigerValidationValidee(n2, ValidationEntiteNiveau.N2);

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Lire, ct);

            var statutAvant = demande.Statut;
            var nouveauStatut = ValidationEntiteRules.ResoudreStatutApresAnnulationN2(demande);
            var now = DateTime.Now;
            var cascadeBrouillon = nouveauStatut == StatutDemandePaiement.Brouillon;

            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                nouveauStatut,
                new DemandePaiementConditionalUpdatePatch
                {
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                    FK_UtilisateurAssigne = cascadeBrouillon ? null : demande.FK_UtilisateurAssigne,
                    MettreAJourAssigne = cascadeBrouillon,
                },
                ct);

            var tracked = await _repository.GetTrackedAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            if (cascadeBrouillon)
                ValidationEntiteRules.ReinitialiserValidations(tracked);
            else
                ValidationEntiteRules.ReinitialiserValidationN2(tracked);

            await _repository.SaveChangesAsync(ct);

            await _repository.AddAuditAsync(userId, "ANNULER_VALIDATION_N2", idDemande,
                new { statut = statutAvant },
                new { statut = nouveauStatut, cascade = cascadeBrouillon },
                ct);

            return await MapperDetailApresMutationAsync(idDemande, ct);
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> AnnulerValidationN1Async(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await AssurerValidationsEntiteAsync(demande, ct);
            var n1 = ValidationEntiteRules.ObtenirNiveau(demande, ValidationEntiteNiveau.N1);
            var n2 = ValidationEntiteRules.ObtenirNiveau(demande, ValidationEntiteNiveau.N2);
            ExigerPermissionAnnulationValidationN1(n1);
            ExigerActeurValidation(n1, userId);

            var statut = StatutDemandePaiement.Normaliser(demande.Statut);
            ValidationEntiteRules.ExigerValidationValidee(n1, ValidationEntiteNiveau.N1);

            if (!ValidationEntiteRules.EstEnAttente(n2))
                throw new InvalidOperationException(
                    "La validation N2 doit être en attente pour annuler la validation N1.");

            string nouveauStatut;
            if (statut == StatutDemandePaiement.EnValidationN1)
            {
                nouveauStatut = StatutDemandePaiement.Brouillon;
            }
            else if (statut == StatutDemandePaiement.EnValidationN2)
            {
                nouveauStatut = StatutDemandePaiement.EnValidationN1;
            }
            else
            {
                throw new InvalidOperationException(
                    "La demande doit être en validation entité pour annuler la validation N1.");
            }

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Lire, ct);

            var statutAvant = demande.Statut;
            var now = DateTime.Now;
            var retourBrouillon = nouveauStatut == StatutDemandePaiement.Brouillon;

            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                nouveauStatut,
                new DemandePaiementConditionalUpdatePatch
                {
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                    FK_UtilisateurAssigne = retourBrouillon ? null : demande.FK_UtilisateurAssigne,
                    MettreAJourAssigne = retourBrouillon,
                },
                ct);

            var tracked = await _repository.GetTrackedAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            if (retourBrouillon)
                ValidationEntiteRules.ReinitialiserValidations(tracked);
            else
                ValidationEntiteRules.ReinitialiserValidationN1(tracked);

            await _repository.SaveChangesAsync(ct);

            await _repository.AddAuditAsync(userId, "ANNULER_VALIDATION_N1", idDemande,
                new { statut = statutAvant },
                new { statut = nouveauStatut },
                ct);

            return await MapperDetailApresMutationAsync(idDemande, ct);
        }, cancellationToken);

    private async Task AssurerValidationsEntiteAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken)
    {
        if (demande.ValidationsEntite.Count >= 2)
            return;

        var tracked = await _repository.GetTrackedAsync(demande.IdDemandePaiement, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");
        await AssurerValidationsInitialesAsync(tracked, cancellationToken);
        demande.ValidationsEntite = tracked.ValidationsEntite.ToList();
    }

    private static void ExigerActeurValidation(
        Domain.Entities.DemandePaiementValidation validation,
        long userId)
    {
        if (ValidationEntiteRules.EstActeurValidation(validation, userId))
            return;

        throw new UnauthorizedAccessException(
            "Seul l'acteur ayant enregistré cette validation peut l'annuler.");
    }

    private void ExigerPermissionAnnulationValidationN2(
        Domain.Entities.DemandePaiementValidation n2)
    {
        if (ValidationEntiteRules.EstValideePhysique(n2))
            ExigerDeclarerValidationPhysique();
        else
            ExigerValiderElectronique(ValidationEntiteNiveau.N2);
    }

    private void ExigerPermissionAnnulationValidationN1(
        Domain.Entities.DemandePaiementValidation n1)
    {
        if (ValidationEntiteRules.EstValideePhysique(n1))
            ExigerDeclarerValidationPhysique();
        else
            ExigerValiderElectronique(ValidationEntiteNiveau.N1);
    }
}
