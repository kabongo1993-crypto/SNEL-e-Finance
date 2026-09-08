using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.DpmBatch;

public sealed class DemandePaiementBatchRequestException : ArgumentException
{
    public string? ErrorCode { get; }

    public DemandePaiementBatchRequestException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

public static class DemandePaiementBatchOperationRules
{
    public const int MaxItems = 100;

    public static string GetRequiredStatut(DemandePaiementBatchOperation operation)
        => operation switch
        {
            DemandePaiementBatchOperation.EnvoyerValidation => StatutDemandePaiement.Brouillon,
            DemandePaiementBatchOperation.ValiderN1 => StatutDemandePaiement.EnValidationN1,
            DemandePaiementBatchOperation.ValiderN2 => StatutDemandePaiement.EnValidationN2,
            DemandePaiementBatchOperation.SoumettreBudget => StatutDemandePaiement.ValideeEntite,
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1 => StatutDemandePaiement.EnValidationN1,
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2 => StatutDemandePaiement.EnValidationN2,
            DemandePaiementBatchOperation.Receptionner => StatutDemandePaiement.Soumise,
            DemandePaiementBatchOperation.TraiterCharge => StatutDemandePaiement.EnTraitementDpm,
            DemandePaiementBatchOperation.EtablirDocuments => StatutDemandePaiement.EnTraitementDpm,
            DemandePaiementBatchOperation.Orienter => StatutDemandePaiement.EnTraitementDpm,
            DemandePaiementBatchOperation.SupprimerBrouillon => StatutDemandePaiement.Brouillon,
            DemandePaiementBatchOperation.RejeterValidationN1 => StatutDemandePaiement.EnValidationN1,
            DemandePaiementBatchOperation.RejeterValidationN2 => StatutDemandePaiement.EnValidationN2,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

    public static string ToRouteSegment(DemandePaiementBatchOperation operation)
        => operation switch
        {
            DemandePaiementBatchOperation.EnvoyerValidation => "envoyer-validation",
            DemandePaiementBatchOperation.ValiderN1 => "valider-n1",
            DemandePaiementBatchOperation.ValiderN2 => "valider-n2",
            DemandePaiementBatchOperation.SoumettreBudget => "soumettre-budget",
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1 => "declarer-validation-physique-n1",
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2 => "declarer-validation-physique-n2",
            DemandePaiementBatchOperation.Receptionner => "receptionner",
            DemandePaiementBatchOperation.TraiterCharge => "traiter-charge",
            DemandePaiementBatchOperation.EtablirDocuments => "etablir-documents",
            DemandePaiementBatchOperation.Orienter => "orienter",
            DemandePaiementBatchOperation.SupprimerBrouillon => "supprimer",
            DemandePaiementBatchOperation.RejeterValidationN1 => "rejeter-validation-n1",
            DemandePaiementBatchOperation.RejeterValidationN2 => "rejeter-validation-n2",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

    public static bool RequiresDeclaration(DemandePaiementBatchOperation operation)
        => operation is DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1
            or DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2;

    public static bool RequiresTraitement(DemandePaiementBatchOperation operation)
        => operation is DemandePaiementBatchOperation.TraiterCharge;

    public static bool RequiresDocuments(DemandePaiementBatchOperation operation)
        => operation is DemandePaiementBatchOperation.EtablirDocuments;

    public static bool RequiresRetour(DemandePaiementBatchOperation operation)
        => operation is DemandePaiementBatchOperation.RejeterValidationN1
            or DemandePaiementBatchOperation.RejeterValidationN2;

    public static void ValidateRequest(DemandePaiementBatchOperation operation, DemandePaiementBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var statutFiltre = StatutDemandePaiement.Normaliser(request.StatutFiltre);
        if (string.IsNullOrEmpty(statutFiltre))
        {
            var emptyMessage = operation switch
            {
                DemandePaiementBatchOperation.Receptionner
                    => "La réception batch nécessite le filtre SOUMISE.",
                DemandePaiementBatchOperation.TraiterCharge
                    => "Le traitement de charge batch nécessite le filtre EN_TRAITEMENT_DPM.",
                DemandePaiementBatchOperation.EtablirDocuments
                    => "L'établissement des documents batch nécessite le filtre EN_TRAITEMENT_DPM.",
                DemandePaiementBatchOperation.Orienter
                    => "L'orientation batch nécessite le filtre EN_TRAITEMENT_DPM.",
                _ => "Le filtre statut est obligatoire pour un traitement par lot.",
            };
            throw new DemandePaiementBatchRequestException(emptyMessage, "INVALID_STATUS_FILTER");
        }

        if (!StatutDemandePaiement.IsValid(statutFiltre))
        {
            throw new DemandePaiementBatchRequestException(
                $"Statut filtre invalide : « {request.StatutFiltre} ».",
                "INVALID_STATUS_FILTER");
        }

        var statutAttendu = GetRequiredStatut(operation);
        if (!string.Equals(statutFiltre, statutAttendu, StringComparison.Ordinal))
        {
            var message = operation switch
            {
                DemandePaiementBatchOperation.Receptionner
                    => "La réception batch nécessite le filtre SOUMISE.",
                DemandePaiementBatchOperation.TraiterCharge
                    => "Le traitement de charge batch nécessite le filtre EN_TRAITEMENT_DPM.",
                DemandePaiementBatchOperation.EtablirDocuments
                    => "L'établissement des documents batch nécessite le filtre EN_TRAITEMENT_DPM.",
                DemandePaiementBatchOperation.Orienter
                    => "L'orientation batch nécessite le filtre EN_TRAITEMENT_DPM.",
                _ => $"Le filtre statut « {statutFiltre} » est incompatible avec l'opération « {ToRouteSegment(operation)} » (attendu : {statutAttendu}).",
            };
            throw new DemandePaiementBatchRequestException(message, "INVALID_STATUS_FILTER");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new DemandePaiementBatchRequestException(
                "La sélection doit contenir au moins une demande de paiement.");
        }

        if (request.Items.Count > MaxItems)
        {
            throw new DemandePaiementBatchRequestException(
                $"La sélection ne peut pas dépasser {MaxItems} demandes de paiement.");
        }

        var requiresDeclaration = RequiresDeclaration(operation);
        var requiresTraitement = RequiresTraitement(operation);
        var requiresDocuments = RequiresDocuments(operation);
        var requiresRetour = RequiresRetour(operation);
        var ids = new HashSet<long>();
        foreach (var item in request.Items)
        {
            if (item.IdDemandePaiement <= 0)
            {
                throw new DemandePaiementBatchRequestException(
                    "Chaque élément doit avoir un identifiant de demande valide.");
            }

            if (!ids.Add(item.IdDemandePaiement))
            {
                throw new DemandePaiementBatchRequestException(
                    $"La demande {item.IdDemandePaiement} est présente plusieurs fois dans la sélection.");
            }

            if (requiresDeclaration && item.Declaration is null)
            {
                throw new DemandePaiementBatchRequestException(
                    $"La déclaration de validation physique est obligatoire pour la demande {item.IdDemandePaiement}.");
            }

            if (requiresTraitement && item.Traitement is null)
            {
                throw new DemandePaiementBatchRequestException(
                    $"Le traitement de charge est obligatoire pour la demande {item.IdDemandePaiement}.");
            }

            if (requiresRetour && item.Retour is null)
            {
                throw new DemandePaiementBatchRequestException(
                    $"Le motif de rejet est obligatoire pour la demande {item.IdDemandePaiement}.");
            }

            if (requiresDocuments)
            {
                if (item.Documents is null)
                {
                    throw new DemandePaiementBatchRequestException(
                        $"Le payload documents est obligatoire pour la demande {item.IdDemandePaiement}.");
                }

                ValidateDocumentsPayload(item.IdDemandePaiement, item.Documents);
            }
        }
    }

    /// <summary>Validation structurelle uniquement — aucune règle métier billet/instrument.</summary>
    private static void ValidateDocumentsPayload(long idDemande, DemandePaiementBatchDocumentsRequest documents)
    {
        var instrumentCount =
            (documents.PieceCaisse is not null ? 1 : 0)
            + (documents.BonProvisoire is not null ? 1 : 0)
            + (documents.MinuteCheque is not null ? 1 : 0);

        if (instrumentCount > 1)
        {
            throw new DemandePaiementBatchRequestException(
                $"La demande {idDemande} ne peut indiquer qu'un seul instrument à établir.");
        }

        var hasForce = !string.IsNullOrWhiteSpace(documents.TypeInstrumentForce);
        if (hasForce)
        {
            if (!TypeInstrumentPaiement.IsValid(documents.TypeInstrumentForce))
            {
                throw new DemandePaiementBatchRequestException(
                    $"TypeInstrumentForce invalide pour la demande {idDemande}.");
            }

            if (instrumentCount > 0)
            {
                throw new DemandePaiementBatchRequestException(
                    $"La demande {idDemande} ne peut pas combiner TypeInstrumentForce et un DTO instrument.");
            }
        }

        if (documents.Billet is null && instrumentCount == 0 && !hasForce)
        {
            throw new DemandePaiementBatchRequestException(
                $"Aucun document à établir pour la demande {idDemande}.");
        }
    }
}