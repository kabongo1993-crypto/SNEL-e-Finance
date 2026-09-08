using BudgetWeb.Application.DpmBatch;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BudgetWeb.Application.Services;

public sealed class DemandePaiementBatchService : IDemandePaiementBatchService
{
    /// <summary>Message client sûr — le détail technique reste uniquement dans les logs.</summary>
    public const string InternalErrorClientMessage =
        "Une erreur interne est survenue lors du traitement de cette demande.";

    private readonly IDemandePaiementService _demandePaiementService;
    private readonly IDemandePaiementRepository _repository;
    private readonly ILogger<DemandePaiementBatchService> _logger;

    public DemandePaiementBatchService(
        IDemandePaiementService demandePaiementService,
        IDemandePaiementRepository repository,
        ILogger<DemandePaiementBatchService> logger)
    {
        _demandePaiementService = demandePaiementService;
        _repository = repository;
        _logger = logger;
    }

    public async Task<DemandePaiementBatchResultDto> ExecuterAsync(
        DemandePaiementBatchOperation operation,
        DemandePaiementBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        DemandePaiementBatchOperationRules.ValidateRequest(operation, request);

        var correlationId = Guid.NewGuid();
        var statutFiltre = StatutDemandePaiement.Normaliser(request.StatutFiltre);
        var operationLabel = DemandePaiementBatchOperationRules.ToRouteSegment(operation);

        _logger.LogInformation(
            "Batch DPM {Operation} démarré (CorrelationId={CorrelationId}, StatutFiltre={StatutFiltre}, Count={Count})",
            operationLabel,
            correlationId,
            statutFiltre,
            request.Items.Count);

        var details = new List<DemandePaiementBatchItemResultDto>(request.Items.Count);

        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            details.Add(await TraiterItemAsync(operation, statutFiltre, item, operationLabel, correlationId, cancellationToken));
        }

        var reussies = details.Count(d => d.Outcome == DemandePaiementBatchOutcome.Success);
        var ignorees = details.Count(d => d.Outcome == DemandePaiementBatchOutcome.Ignored);
        var erreurs = details.Count(d => d.Outcome == DemandePaiementBatchOutcome.Error);
        var traitees = reussies + erreurs;

        var result = new DemandePaiementBatchResultDto(
            operationLabel,
            statutFiltre,
            correlationId,
            details.Count,
            traitees,
            reussies,
            ignorees,
            erreurs,
            details);

        _logger.LogInformation(
            "Batch DPM {Operation} terminé (CorrelationId={CorrelationId}, Reussies={Reussies}, Ignorees={Ignorees}, Erreurs={Erreurs})",
            operationLabel,
            correlationId,
            reussies,
            ignorees,
            erreurs);

        return result;
    }

    private async Task<DemandePaiementBatchItemResultDto> TraiterItemAsync(
        DemandePaiementBatchOperation operation,
        string statutFiltre,
        DemandePaiementBatchItemRequest item,
        string operationLabel,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = item.IdDemandePaiement;
            DemandePaiementDetailDto? avant;
            try
            {
                avant = await _demandePaiementService.GetByIdAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                var mapped = MapException(ex);
                LogItemFailure(ex, mapped.Code, operationLabel, id, correlationId, phase: "lecture");
                return BuildError(id, null, string.Empty, null, mapped);
            }

            if (avant is null)
            {
                return new DemandePaiementBatchItemResultDto(
                    id,
                    null,
                    DemandePaiementBatchOutcome.Error,
                    DemandePaiementBatchErrorCode.NotFound,
                    "Demande de paiement introuvable.",
                    string.Empty,
                    null);
            }

            var statutAvant = StatutDemandePaiement.Normaliser(avant.Statut);
            if (!string.Equals(statutAvant, statutFiltre, StringComparison.Ordinal))
            {
                return new DemandePaiementBatchItemResultDto(
                    id,
                    avant.Reference,
                    DemandePaiementBatchOutcome.Ignored,
                    DemandePaiementBatchErrorCode.StatutMismatch,
                    $"Statut actuel « {statutAvant} » différent du filtre « {statutFiltre} ».",
                    statutAvant,
                    null);
            }

            try
            {
                if (operation == DemandePaiementBatchOperation.SupprimerBrouillon)
                {
                    var deleted = await _demandePaiementService.DeleteBrouillonAsync(id, cancellationToken);
                    if (!deleted)
                    {
                        return new DemandePaiementBatchItemResultDto(
                            id,
                            avant.Reference,
                            DemandePaiementBatchOutcome.Error,
                            DemandePaiementBatchErrorCode.NotFound,
                            "Demande de paiement introuvable.",
                            statutAvant,
                            null);
                    }

                    return new DemandePaiementBatchItemResultDto(
                        id,
                        avant.Reference,
                        DemandePaiementBatchOutcome.Success,
                        null,
                        null,
                        statutAvant,
                        null);
                }

                var apres = await ExecuterUniteAsync(operation, id, item, cancellationToken);
                return new DemandePaiementBatchItemResultDto(
                    id,
                    apres.Reference,
                    DemandePaiementBatchOutcome.Success,
                    null,
                    null,
                    statutAvant,
                    StatutDemandePaiement.Normaliser(apres.Statut));
            }
            catch (Exception ex)
            {
                var mapped = MapException(ex);
                LogItemFailure(ex, mapped.Code, operationLabel, id, correlationId, phase: "exécution");
                return BuildError(id, avant.Reference, statutAvant, null, mapped);
            }
        }
        finally
        {
            // Isolation EF : une DPM (succès / ignorée / erreur) ne doit jamais contaminer la suivante.
            _repository.ClearChangeTracker();
        }
    }

    private void LogItemFailure(
        Exception ex,
        string codeErreur,
        string operationLabel,
        long idDemande,
        Guid correlationId,
        string phase)
    {
        if (string.Equals(codeErreur, DemandePaiementBatchErrorCode.InternalError, StringComparison.Ordinal))
        {
            _logger.LogError(
                ex,
                "Batch DPM {Operation} erreur interne ({Phase}) demande {IdDemande} (CorrelationId={CorrelationId})",
                operationLabel,
                phase,
                idDemande,
                correlationId);
            return;
        }

        _logger.LogWarning(
            ex,
            "Batch DPM {Operation} échec ({Phase}) demande {IdDemande} Code={CodeErreur} (CorrelationId={CorrelationId})",
            operationLabel,
            phase,
            idDemande,
            codeErreur,
            correlationId);
    }

    private Task<DemandePaiementDetailDto> ExecuterUniteAsync(
        DemandePaiementBatchOperation operation,
        long idDemande,
        DemandePaiementBatchItemRequest item,
        CancellationToken cancellationToken)
        => operation switch
        {
            DemandePaiementBatchOperation.EnvoyerValidation
                => _demandePaiementService.EnvoyerEnValidationAsync(idDemande, cancellationToken),
            DemandePaiementBatchOperation.ValiderN1
                => _demandePaiementService.ValiderN1ElectroniqueAsync(idDemande, item.Validation, cancellationToken),
            DemandePaiementBatchOperation.ValiderN2
                => _demandePaiementService.ValiderN2ElectroniqueAsync(idDemande, item.Validation, cancellationToken),
            DemandePaiementBatchOperation.SoumettreBudget
                => _demandePaiementService.SoumettreAsync(idDemande, cancellationToken),
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1
                => _demandePaiementService.DeclarerValidationPhysiqueN1Async(
                    idDemande,
                    item.Declaration
                        ?? throw new ArgumentException(
                            "La déclaration de validation physique est obligatoire.",
                            nameof(item)),
                    cancellationToken),
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2
                => _demandePaiementService.DeclarerValidationPhysiqueN2Async(
                    idDemande,
                    item.Declaration
                        ?? throw new ArgumentException(
                            "La déclaration de validation physique est obligatoire.",
                            nameof(item)),
                    cancellationToken),
            DemandePaiementBatchOperation.Receptionner
                => _demandePaiementService.ReceptionnerAsync(idDemande, cancellationToken),
            DemandePaiementBatchOperation.TraiterCharge
                => _demandePaiementService.TraiterChargeAsync(
                    idDemande,
                    item.Traitement
                        ?? throw new ArgumentException(
                            "Le traitement de charge est obligatoire.",
                            nameof(item)),
                    cancellationToken),
            DemandePaiementBatchOperation.EtablirDocuments
                => EtablirDocumentsAsync(
                    idDemande,
                    item.Documents
                        ?? throw new ArgumentException(
                            "Le payload documents est obligatoire.",
                            nameof(item)),
                    cancellationToken),
            // Orientation null = pool (même sémantique qu'OrienterAsync(request: null)).
            DemandePaiementBatchOperation.Orienter
                => _demandePaiementService.OrienterAsync(idDemande, item.Orientation, cancellationToken),
            DemandePaiementBatchOperation.RejeterValidationN1
                or DemandePaiementBatchOperation.RejeterValidationN2
                => _demandePaiementService.RejeterValidationEntiteAsync(
                    idDemande,
                    item.Retour
                        ?? throw new ArgumentException(
                            "Le motif de rejet est obligatoire.",
                            nameof(item)),
                    cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

    /// <summary>
    /// Orchestration pure : billet si requis (règle domaine) puis instrument unitaire (TX séparées).
    /// </summary>
    private async Task<DemandePaiementDetailDto> EtablirDocumentsAsync(
        long idDemande,
        DemandePaiementBatchDocumentsRequest documents,
        CancellationToken cancellationToken)
    {
        var detail = await _demandePaiementService.GetByIdAsync(idDemande, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de paiement introuvable.");

        var billetRequis = BilletConversionRules.NecessiteBillet(
            detail.ModePaiementSollicite,
            detail.Devise);

        // Payload Billet optionnel : si la règle domaine exige le billet, l'établir même si Billet == null.
        // Si Billet est fourni alors qu'il n'est pas requis, déléguer à l'unitaire (refus métier inchangé).
        if (billetRequis || documents.Billet is not null)
        {
            await _demandePaiementService.EtablirBilletConversionAsync(
                idDemande,
                documents.Billet ?? new EtablirBilletConversionRequest(),
                cancellationToken);
        }

        if (documents.PieceCaisse is not null)
        {
            await _demandePaiementService.EtablirPieceCaisseAsync(
                idDemande,
                documents.PieceCaisse,
                cancellationToken);
        }
        else if (documents.BonProvisoire is not null)
        {
            await _demandePaiementService.EtablirBonProvisoireAsync(
                idDemande,
                documents.BonProvisoire,
                cancellationToken);
        }
        else if (documents.MinuteCheque is not null)
        {
            await _demandePaiementService.EtablirMinuteChequeAsync(
                idDemande,
                documents.MinuteCheque,
                cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(documents.TypeInstrumentForce))
        {
            var type = TypeInstrumentPaiement.Normaliser(documents.TypeInstrumentForce);
            switch (type)
            {
                case TypeInstrumentPaiement.PieceCaisse:
                    await _demandePaiementService.EtablirPieceCaisseAsync(
                        idDemande,
                        new EtablirPieceCaisseRequest(),
                        cancellationToken);
                    break;
                case TypeInstrumentPaiement.BonProvisoire:
                    await _demandePaiementService.EtablirBonProvisoireAsync(
                        idDemande,
                        new EtablirBonProvisoireRequest(),
                        cancellationToken);
                    break;
                case TypeInstrumentPaiement.MinuteCheque:
                    await _demandePaiementService.EtablirMinuteChequeAsync(
                        idDemande,
                        new EtablirMinuteChequeRequest(),
                        cancellationToken);
                    break;
                default:
                    throw new ArgumentException(
                        $"TypeInstrumentForce invalide : « {documents.TypeInstrumentForce} ».",
                        nameof(documents));
            }
        }

        return await _demandePaiementService.GetByIdAsync(idDemande, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de paiement introuvable.");
    }

    private static DemandePaiementBatchItemResultDto BuildError(
        long id,
        string? reference,
        string statutAvant,
        string? statutApres,
        (string Code, string Message) mapped)
        => new(
            id,
            reference,
            DemandePaiementBatchOutcome.Error,
            mapped.Code,
            mapped.Message,
            statutAvant,
            statutApres);

    private static (string Code, string Message) MapException(Exception ex)
    {
        if (ex is DemandePaiementConcurrencyException concurrency)
        {
            return (DemandePaiementBatchErrorCode.Concurrency, concurrency.Message);
        }

        if (ex is UnauthorizedAccessException unauthorized)
        {
            return (DemandePaiementBatchErrorCode.AccessDenied, unauthorized.Message);
        }

        if (ex is KeyNotFoundException notFound)
        {
            return (DemandePaiementBatchErrorCode.NotFound, notFound.Message);
        }

        if (ex is ArgumentException argument)
        {
            return (DemandePaiementBatchErrorCode.Validation, argument.Message);
        }

        if (ex is InvalidOperationException invalid)
        {
            return (DemandePaiementBatchErrorCode.BusinessRule, invalid.Message);
        }

        return (DemandePaiementBatchErrorCode.InternalError, InternalErrorClientMessage);
    }
}