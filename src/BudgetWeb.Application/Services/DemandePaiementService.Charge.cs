using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    public Task<DemandePaiementDetailDto> EntrerTraitementAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerChargeDpm();
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(demande, ct);
            ExigerStatut(demande, StatutDemandePaiement.Brouillon, "La demande doit être en brouillon.");

            var statutAvant = demande.Statut;
            var now = DateTime.Now;
            var source = demande.FK_UtilisateurCreation;
            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                StatutDemandePaiement.EnTraitementDpm,
                new DemandePaiementConditionalUpdatePatch
                {
                    FK_UtilisateurReception = userId,
                    DateReception = now,
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                    FK_UtilisateurAssigne = userId,
                    MettreAJourAssigne = true,
                },
                ct);

            await EnregistrerTransmissionAsync(
                idDemande,
                source,
                userId,
                statutAvant,
                StatutDemandePaiement.EnTraitementDpm,
                DemandePaiementRoutageAction.EntrerTraitement,
                now,
                null,
                ct);

            demande.FK_UtilisateurAssigne = userId;

            await _repository.AddAuditAsync(userId, "ENTRER_TRAITEMENT", idDemande,
                new { statut = statutAvant },
                new { statut = StatutDemandePaiement.EnTraitementDpm },
                ct);

            return await MapperDetailApresMutationAsync(idDemande, ct);
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> TraiterChargeAsync(
        long idDemande,
        TraitementChargeDpmRequest request,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerChargeDpm();
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Traiter, ct);
            ExigerStatut(
                demande,
                StatutDemandePaiement.EnTraitementDpm,
                "La demande doit être en traitement DPM.");

            if (!string.IsNullOrWhiteSpace(request.ModePaiementSollicite))
            {
                ModePaiementVerrouillageRules.ExigerModificationModeAutorisee(
                    demande,
                    request.ModePaiementSollicite);
            }

            var sollicitationAvant = new
            {
                demande.ModePaiementSollicite,
                demande.TypeBudgetSollicite,
                demande.ItemSollicite,
            };

            var tracked = await _repository.GetTrackedAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");
            AppliquerSollicitationCharge(tracked, request);

            var montantSollicite = tracked.MontantBrut;
            var deviseSollicitee = tracked.Devise;

            if (string.IsNullOrWhiteSpace(tracked.ModePaiementSollicite))
            {
                throw new InvalidOperationException(
                    "Le mode de paiement sollicité (CAISSE/BANQUE) doit être renseigné par le demandeur.");
            }

            var mode = ModePaiementDpm.Normaliser(tracked.ModePaiementSollicite);
            var instrument = TypeInstrumentPaiement.Normaliser(request.TypeInstrument);
            TypeInstrumentPaiement.ExigerCoherence(mode, instrument);

            var devisePaiement = string.IsNullOrWhiteSpace(request.DevisePaiement)
                ? (mode == ModePaiementDpm.Caisse ? "CDF" : deviseSollicitee)
                : request.DevisePaiement.Trim().ToUpperInvariant();
            DemandePaiementMontants.ValiderDevise(devisePaiement);

            if (mode == ModePaiementDpm.Caisse && devisePaiement != "CDF")
                throw new InvalidOperationException("Le paiement caisse est en CDF.");

            var necessiteBillet = BilletConversionRules.NecessiteBillet(mode, deviseSollicitee);
            if (necessiteBillet)
            {
                var billet = await _repository.GetBilletConversionByDemandeAsync(idDemande, ct);
                if (billet is null
                    || !string.Equals(
                        StatutBilletConversion.Normaliser(billet.Statut),
                        StatutBilletConversion.Etabli,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Le billet de conversion doit être établi avant de poursuivre le traitement.");
                }
            }

            await ExigerDocumentInstrumentEtabliAsync(idDemande, instrument, tracked, ct);

            decimal tauxPaiement;
            long? idTauxPaiement;
            decimal montantPaiement;

            (tauxPaiement, idTauxPaiement, montantPaiement) = await CalculerPaiementAsync(
                montantSollicite,
                deviseSollicitee,
                devisePaiement,
                request.TauxPaiement,
                request.IdTauxChangePaiement,
                DateTraitementDpm(),
                ct);

            if (!necessiteBillet || tracked.TauxConversion is null || tracked.MontantUsd is null)
            {
                await AppliquerTauxDemandeEtImputationsAsync(tracked, ct);
            }

            if (tracked.MontantBrut != montantSollicite
                || !string.Equals(tracked.Devise, deviseSollicitee, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Le montant et la devise sollicités ne peuvent pas être écrasés.");
            }

            var now = DateTime.Now;
            await AppliquerMiseAJourSiStatutAsync(
                idDemande,
                StatutDemandePaiement.EnTraitementDpm,
                new DemandePaiementConditionalUpdatePatch
                {
                    TypeInstrumentPaiement = instrument,
                    DevisePaiement = devisePaiement,
                    MontantPaiement = montantPaiement,
                    TauxPaiement = tauxPaiement,
                    FK_TauxChangePaiement = idTauxPaiement,
                    TauxConversion = tracked.TauxConversion,
                    MontantUsd = tracked.MontantUsd,
                    FK_TauxChange = tracked.FK_TauxChange,
                    ModePaiementSollicite = tracked.ModePaiementSollicite,
                    TypeBudgetSollicite = tracked.TypeBudgetSollicite,
                    ItemSollicite = tracked.ItemSollicite,
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                },
                ct);

            await _repository.AddAuditAsync(userId, "TRAITER", idDemande,
                new { montantSollicite, deviseSollicitee, sollicitation = sollicitationAvant },
                new
                {
                    mode,
                    instrument,
                    devisePaiement,
                    montantPaiement,
                    tauxPaiement,
                    tracked.TauxConversion,
                    tracked.MontantUsd,
                    sollicitation = new
                    {
                        tracked.ModePaiementSollicite,
                        tracked.TypeBudgetSollicite,
                        tracked.ItemSollicite,
                    },
                },
                ct);

            return (await RequireDetailAsync(idDemande, ct))!;
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> RetenirSollicitationChargeAsync(
        long idDemande,
        RetenirSollicitationChargeRequest request,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerChargeDpm();
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Traiter, ct);
            ExigerStatut(
                demande,
                StatutDemandePaiement.EnTraitementDpm,
                "La demande doit être en traitement DPM pour retenir le mode de paiement.");

            ModePaiementVerrouillageRules.ExigerModificationModeAutorisee(
                demande,
                request.ModePaiementSollicite);

            var typeBudget = DestinationBudgetaireSolliciteeRules.NormaliserType(request.TypeBudgetSollicite);
            var item = DestinationBudgetaireSolliciteeRules.NormaliserItem(request.ItemSollicite);
            var mode = ModePaiementDpm.Normaliser(request.ModePaiementSollicite);
            var now = DateTime.Now;

            await AppliquerMiseAJourSiStatutAsync(
                idDemande,
                StatutDemandePaiement.EnTraitementDpm,
                new DemandePaiementConditionalUpdatePatch
                {
                    ModePaiementSollicite = mode,
                    TypeBudgetSollicite = typeBudget,
                    ItemSollicite = item,
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                },
                ct);

            await _repository.AddAuditAsync(userId, "RETENIR_SOLlicitation_CHARGE", idDemande,
                null,
                new
                {
                    ModePaiementSollicite = mode,
                    TypeBudgetSollicite = typeBudget,
                    ItemSollicite = item,
                },
                ct);

            return (await RequireDetailAsync(idDemande, ct))!;
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> OrienterAsync(
        long idDemande,
        OrienterDemandePaiementRequest? request,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerChargeDpm();
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Orienter, ct);
            ExigerStatut(
                demande,
                StatutDemandePaiement.EnTraitementDpm,
                "La demande doit être en traitement DPM.");

            if (string.IsNullOrWhiteSpace(demande.ModePaiementSollicite)
                || string.IsNullOrWhiteSpace(demande.TypeInstrumentPaiement)
                || string.IsNullOrWhiteSpace(demande.DevisePaiement)
                || demande.MontantPaiement is null)
            {
                throw new InvalidOperationException(
                    "Le traitement Chargé DPM (instrument, paiement) est requis avant l'orientation.");
            }

            if (string.IsNullOrWhiteSpace(demande.TypeBudgetSollicite))
            {
                throw new InvalidOperationException(
                    "La destination budgétaire sollicitée est absente — compléter la demande avant orientation.");
            }

            var codeSollicite = DestinationBudgetaireSolliciteeRules.NormaliserType(demande.TypeBudgetSollicite);
            var typeBudget = await _repository.GetTypeBudgetByCodeAsync(codeSollicite, ct)
                ?? throw new InvalidOperationException(
                    $"Type budget « {codeSollicite} » introuvable pour l'orientation.");

            var code = typeBudget.CodeType.Trim().ToUpperInvariant();
            if (code is not (
                TypeBudgetCode.DepensesCourantes
                or TypeBudgetCode.ActionsExploitation
                or TypeBudgetCode.BudgetInvestissement))
            {
                throw new InvalidOperationException("L'orientation doit être DC, AE ou BI.");
            }

            var instrument = TypeInstrumentPaiement.Normaliser(demande.TypeInstrumentPaiement);
            await ExigerDocumentInstrumentEtabliAsync(idDemande, instrument, demande, ct);

            var statutAvant = demande.Statut;
            var now = DateTime.Now;
            var patch = new DemandePaiementConditionalUpdatePatch
            {
                FK_TypeBudget = typeBudget.IdTypeBudget,
                FK_UtilisateurControle = userId,
                DateControle = now,
                FK_UtilisateurModification = userId,
                DateModification = now,
            };

            if (request?.IdUtilisateurCible is long cible)
            {
                patch = patch with
                {
                    FK_UtilisateurAssigne = cible,
                    MettreAJourAssigne = true,
                };
            }
            else
            {
                patch = patch with
                {
                    FK_UtilisateurAssigne = null,
                    MettreAJourAssigne = true,
                };
            }

            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                StatutDemandePaiement.EnControleBudgetaire,
                patch,
                ct);

            if (request?.IdUtilisateurCible is long idCible)
            {
                await EnregistrerTransmissionAsync(
                    idDemande,
                    userId,
                    idCible,
                    statutAvant,
                    StatutDemandePaiement.EnControleBudgetaire,
                    DemandePaiementRoutageAction.Orienter,
                    now,
                    null,
                    ct);
                demande.FK_UtilisateurAssigne = idCible;
            }
            else
            {
                await _repository.DesactiverRoutagesActifsAsync(idDemande, ct);
                demande.FK_UtilisateurAssigne = null;
            }

            await _repository.AddAuditAsync(userId, "ORIENTER", idDemande,
                new { statut = statutAvant },
                new { statut = StatutDemandePaiement.EnControleBudgetaire, codeType = code, idTypeBudget = typeBudget.IdTypeBudget },
                ct);

            return await MapperDetailApresMutationAsync(idDemande, ct);
        }, cancellationToken);

    private static void AppliquerSollicitationCharge(
        DemandePaiementEntity demande,
        TraitementChargeDpmRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ModePaiementSollicite)
            && string.IsNullOrWhiteSpace(request.TypeBudgetSollicite))
        {
            return;
        }

        var typeBudget = !string.IsNullOrWhiteSpace(request.TypeBudgetSollicite)
            ? request.TypeBudgetSollicite
            : demande.TypeBudgetSollicite
                ?? throw new InvalidOperationException(
                    "La destination budgétaire sollicitée est absente — compléter la demande.");

        var modeSollicite = !string.IsNullOrWhiteSpace(request.ModePaiementSollicite)
            ? request.ModePaiementSollicite
            : demande.ModePaiementSollicite
                ?? throw new InvalidOperationException(
                    "Le mode de paiement sollicité est absent — compléter la demande.");

        string? item = !string.IsNullOrWhiteSpace(request.TypeBudgetSollicite)
            ? request.ItemSollicite
            : request.ItemSollicite ?? demande.ItemSollicite;

        AppliquerSollicitationEntete(demande, typeBudget, item, modeSollicite);
    }

    private async Task<(decimal Taux, long? IdTaux, decimal Montant)> CalculerPaiementAsync(
        decimal montantSollicite,
        string deviseSollicitee,
        string devisePaiement,
        decimal? tauxSaisi,
        long? idTauxSaisi,
        DateOnly dateReference,
        CancellationToken cancellationToken)
    {
        if (string.Equals(deviseSollicitee, devisePaiement, StringComparison.Ordinal))
            return (1m, null, montantSollicite);

        if (tauxSaisi is decimal tauxManuel && tauxManuel > 0m)
        {
            throw new InvalidOperationException(
                "La saisie manuelle du taux de paiement n'est pas autorisée : le taux référentiel doit être appliqué.");
        }

        if (idTauxSaisi is long)
        {
            throw new InvalidOperationException(
                "La saisie manuelle du taux de paiement n'est pas autorisée : le taux référentiel doit être appliqué.");
        }

        var conversion = await _tauxChange.ConvertirAsync(
            montantSollicite,
            deviseSollicitee,
            devisePaiement,
            dateReference,
            cancellationToken);

        return (conversion.TauxApplique, conversion.IdTauxChange, conversion.MontantCible);
    }
}
