namespace BudgetWeb.Application.DTOs;

public enum DemandePaiementBatchOperation
{
    EnvoyerValidation,
    ValiderN1,
    ValiderN2,
    SoumettreBudget,
    DeclarerValidationPhysiqueN1,
    DeclarerValidationPhysiqueN2,
    Receptionner,
    TraiterCharge,
    EtablirDocuments,
    Orienter,
    SupprimerBrouillon,
    RejeterValidationN1,
    RejeterValidationN2,
}

public static class DemandePaiementBatchOutcome
{
    public const string Success = "SUCCESS";
    public const string Ignored = "IGNORED";
    public const string Error = "ERROR";
}

public static class DemandePaiementBatchErrorCode
{
    public const string StatutMismatch = "STATUT_MISMATCH";
    public const string AccessDenied = "ACCESS_DENIED";
    public const string Concurrency = "CONCURRENCY";
    public const string BusinessRule = "BUSINESS_RULE";
    public const string Validation = "VALIDATION";
    public const string NotFound = "NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";
}

public record DemandePaiementBatchRequest(
    string StatutFiltre,
    IReadOnlyList<DemandePaiementBatchItemRequest> Items);

public record DemandePaiementBatchItemRequest(
    long IdDemandePaiement,
    ValidationEntiteRequest? Validation = null,
    DeclarationValidationPhysiqueRequest? Declaration = null,
    TraitementChargeDpmRequest? Traitement = null,
    DemandePaiementBatchDocumentsRequest? Documents = null,
    /// <summary>
    /// Phase 3D — orientation par DPM (DTO unitaire).
    /// Null = orientation pool (<see cref="OrienterDemandePaiementRequest"/> avec IdUtilisateurCible null).
    /// </summary>
    OrienterDemandePaiementRequest? Orientation = null,
    /// <summary>Rejet validation entité N1/N2 — DTO unitaire <see cref="RetourDemandePaiementRequest"/>.</summary>
    RetourDemandePaiementRequest? Retour = null);

/// <summary>
/// Payload Phase 3C — indique quels établissements unitaires appeler (pas de règles métier ici).
/// Les DTO unitaires existants sont réutilisés tels quels.
/// </summary>
public record DemandePaiementBatchDocumentsRequest(
    EtablirBilletConversionRequest? Billet = null,
    EtablirPieceCaisseRequest? PieceCaisse = null,
    EtablirBonProvisoireRequest? BonProvisoire = null,
    EtablirMinuteChequeRequest? MinuteCheque = null,
    /// <summary>
    /// Optionnel : route vers l'instrument unitaire si aucun des trois DTO instrument n'est fourni.
    /// Ne contourne pas TraiterCharge ni les règles unitaires.
    /// </summary>
    string? TypeInstrumentForce = null);

public record DemandePaiementBatchResultDto(
    string Operation,
    string StatutFiltre,
    Guid CorrelationId,
    int TotalSelectionne,
    int Traitees,
    int Reussies,
    int Ignorees,
    int Erreurs,
    IReadOnlyList<DemandePaiementBatchItemResultDto> Details);

public record DemandePaiementBatchItemResultDto(
    long IdDemandePaiement,
    string? Reference,
    string Outcome,
    string? CodeErreur,
    string? Message,
    string StatutAvant,
    string? StatutApres);