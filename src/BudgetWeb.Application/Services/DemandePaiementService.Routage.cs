using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    private async Task EnregistrerTransmissionAsync(
        long idDemande,
        long source,
        long cible,
        string statutSource,
        string statutCible,
        string action,
        DateTime now,
        string? motif,
        CancellationToken cancellationToken)
    {
        await _repository.DesactiverRoutagesActifsAsync(idDemande, cancellationToken);
        await _repository.AddRoutageAsync(
            DemandePaiementRoutageRules.NouvelleTransmission(
                idDemande,
                source,
                cible,
                statutSource,
                statutCible,
                action,
                now,
                motif),
            cancellationToken);
    }

    private Task<IReadOnlyList<DemandePaiementRoutage>> ChargerRoutagesAsync(
        long idDemande,
        CancellationToken cancellationToken)
        => _repository.GetRoutagesAsync(idDemande, cancellationToken);

    private static DemandePaiementConditionalUpdatePatch PatchAssignation(long? assigne)
        => new()
        {
            FK_UtilisateurAssigne = assigne,
            MettreAJourAssigne = true,
        };

    private async Task<DemandePaiementDetailDto> MapperDetailApresMutationAsync(
        long idDemande,
        CancellationToken cancellationToken)
    {
        var row = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable après opération.");
        return MapDetail(row);
    }

    private static DemandePaiementDetailDto MapperDetailApresMutation(DemandePaiementEntity demande)
        => MapDetail(demande);

    private static void SynchroniserCollectionsDepuisTracked(
        DemandePaiementEntity cible,
        DemandePaiementEntity source)
    {
        cible.Beneficiaires = source.Beneficiaires.ToList();
        cible.ValidationsEntite = source.ValidationsEntite.ToList();
        cible.PiecesJointes = source.PiecesJointes.ToList();
        cible.Imputations = source.Imputations.ToList();
        cible.BilletConversion = source.BilletConversion;
        cible.PieceCaisse = source.PieceCaisse;
        cible.BonProvisoire = source.BonProvisoire;
        cible.MinuteCheque = source.MinuteCheque;
    }

    private static void AppliquerPatchBrouillonSurDemande(
        DemandePaiementEntity demande,
        DemandePaiementBrouillonHeaderPatch patch)
    {
        demande.DateEmission = patch.DateEmission;
        demande.LieuEmission = patch.LieuEmission;
        demande.Objet = patch.Objet;
        demande.CompteSection = patch.CompteSection;
        demande.MontantBrut = patch.MontantBrut;
        demande.FK_Devise = patch.FK_Devise;
        demande.Devise = patch.Devise;
        demande.TypeBudgetSollicite = patch.TypeBudgetSollicite;
        demande.ItemSollicite = patch.ItemSollicite;
        demande.ModePaiementSollicite = patch.ModePaiementSollicite;
        demande.FK_UtilisateurModification = patch.FK_UtilisateurModification;
        demande.DateModification = patch.DateModification;
    }

    private static void AppliquerSoumissionSurDemande(
        DemandePaiementEntity demande,
        long userId,
        DateTime now)
    {
        demande.FK_UtilisateurSoumission = userId;
        demande.DateSoumission = now;
        demande.FK_UtilisateurModification = userId;
        demande.DateModification = now;
    }

    private static void CopierValidationEntiteMutee(
        DemandePaiementValidation source,
        DemandePaiementValidation cible)
    {
        cible.Statut = source.Statut;
        cible.ModeValidation = source.ModeValidation;
        cible.FK_UtilisateurValidateur = source.FK_UtilisateurValidateur;
        cible.FK_UtilisateurDeclarant = source.FK_UtilisateurDeclarant;
        cible.NomSignatairePhysique = source.NomSignatairePhysique;
        cible.FonctionSignatairePhysique = source.FonctionSignatairePhysique;
        cible.DateSignaturePhysique = source.DateSignaturePhysique;
        cible.DateValidation = source.DateValidation;
        cible.Commentaire = source.Commentaire;
        cible.EmpreinteDonnees = source.EmpreinteDonnees;
    }

    private static void SynchroniserValidationEntite(
        DemandePaiementEntity demande,
        DemandePaiementEntity tracked,
        byte niveau)
    {
        var source = ValidationEntiteRules.ObtenirNiveau(tracked, niveau);
        var cible = ValidationEntiteRules.ObtenirNiveau(demande, niveau);
        CopierValidationEntiteMutee(source, cible);
    }

    private async Task<DemandePaiementDetailDto> MapperDetailApresCreationAsync(
        long idDemande,
        CancellationToken cancellationToken)
    {
        var header = await _repository.GetByIdAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable après opération.");
        var tracked = await _repository.GetTrackedAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable après opération.");
        SynchroniserCollectionsDepuisTracked(header, tracked);
        return MapperDetailApresMutation(header);
    }
}
