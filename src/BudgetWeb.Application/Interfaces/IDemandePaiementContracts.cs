using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface IDemandePaiementRepository
{
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DemandePaiement>> ListAsync(DemandePaiementQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandePaiementScopeRow>> ListScopeRowsAsync(
        DemandePaiementQuery query,
        CancellationToken cancellationToken = default);
    Task<DemandePaiement?> GetByIdAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiement?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);
    Task<DemandePaiement?> GetTrackedAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiement?> GetForPieceUploadAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<bool> HasValidatedEntiteValidationsAsync(long idDemande, CancellationToken cancellationToken = default);
    Task AttachEmpreinteCollectionsForUploadAsync(
        DemandePaiement demande,
        long idDemande,
        CancellationToken cancellationToken = default);
    Task<DemandePaiement?> GetDetailAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiement?> GetFicheImputationContextAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiement?> GetDetailConsultationAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalAudit>> GetHistoriqueConsultationAsync(
        long idDemande,
        CancellationToken cancellationToken = default);

    Task<string> GenererReferenceAsync(short anneeExercice, CancellationToken cancellationToken = default);
    Task<long?> ResolveVersionBudgetaireValideeAsync(long idExercice, long idUB, CancellationToken cancellationToken = default);
    Task<string?> GetWorkflowStatutUbAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);
    Task<TypeBudget?> GetTypeBudgetAsync(long idTypeBudget, CancellationToken cancellationToken = default);
    Task<TypeBudget?> GetTypeBudgetByCodeAsync(string codeType, CancellationToken cancellationToken = default);
    Task<PrevisionBudgetaire?> GetPrevisionAsync(long idPrevision, CancellationToken cancellationToken = default);
    Task<long?> FindPrevisionIdAsync(DemandePaiementImputation imputation, long idVersion, CancellationToken cancellationToken = default);

    Task<decimal> SumEngageDcMensuelAsync(long idExercice, long idUB, long idRB, byte mois, long? excludeDemandeId, CancellationToken cancellationToken = default);
    Task<decimal> SumEngageDcAnnuelAsync(long idExercice, long idUB, long idRB, long? excludeDemandeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RubriqueBudgetaire>> ListRubriquesDcActivesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<long, PrevisionBudgetaire>> GetPrevisionsDcParRubriqueAsync(
        long idVersion,
        long idUB,
        long idTypeBudgetDc,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyDictionary<EngageDcMensuelCle, decimal> Mensuel, IReadOnlyDictionary<long, decimal> Annuel)>
        SumEngageDcMapsAsync(
            long idExercice,
            long idUB,
            long? excludeDemandeId,
            CancellationToken cancellationToken = default);
    Task ReplaceImputationsDcForMoisAsync(
        long idDemande,
        byte mois,
        IReadOnlyList<DemandePaiementImputation> nouvelles,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<long, PrevisionBudgetaire>> GetPrevisionsAeParRubriqueAsync(
        long idVersion,
        long idUB,
        long idTypeBudgetAe,
        string libelleItemAE,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<EngageAeAnnuelCle, decimal>> SumEngageAeMapsAsync(
        long idExercice,
        long idUB,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default);
    Task ReplaceImputationsAeForItemMoisAsync(
        long idDemande,
        string libelleItemAE,
        byte? mois,
        IReadOnlyList<DemandePaiementImputation> nouvelles,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, PrevisionBudgetaire>> GetPrevisionsBiParDetailAsync(
        long idVersion,
        long idUB,
        long idTypeBudgetBi,
        long idItemBI,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<EngageBiAnnuelCle, decimal>> SumEngageBiMapsAsync(
        long idExercice,
        long idUB,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default);
    Task ReplaceImputationsBiForItemMoisAsync(
        long idDemande,
        long idItemBI,
        byte? mois,
        IReadOnlyList<DemandePaiementImputation> nouvelles,
        CancellationToken cancellationToken = default);
    Task<decimal> SumEngageAeAnnuelAsync(long idExercice, long idUB, long idRB, string libelleItemAE, long? excludeDemandeId, CancellationToken cancellationToken = default);
    Task<decimal> SumEngageBiAnnuelAsync(long idExercice, long idUB, long idItemBI, string detailBI, long? excludeDemandeId, CancellationToken cancellationToken = default);

    Task<DemandePaiement> AddAsync(DemandePaiement entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Vide le ChangeTracker EF du DbContext courant.
    /// Utilisé par le batch pour isoler chaque DPM (évite la contamination inter-items).
    /// </summary>
    void ClearChangeTracker();

    Task<DemandePaiementImputation?> GetImputationTrackedAsync(long idImputation, CancellationToken cancellationToken = default);
    Task AddImputationAsync(DemandePaiementImputation imputation, CancellationToken cancellationToken = default);
    Task RemoveImputationAsync(DemandePaiementImputation imputation, CancellationToken cancellationToken = default);

    Task AddPieceAsync(PieceJointe piece, CancellationToken cancellationToken = default);
    Task<PieceJointe?> GetPieceTrackedAsync(long idPiece, CancellationToken cancellationToken = default);
    Task RemovePieceAsync(PieceJointe piece, CancellationToken cancellationToken = default);

    Task ReplaceBeneficiairesAsync(long idDemande, IReadOnlyList<DemandePaiementBeneficiaire> beneficiaires, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CasDossierPieceObligatoire>> GetPiecesActivesAsync(long idCasDossier, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CasDossierPieceObligatoire>> GetPiecesObligatoiresAsync(long idCasDossier, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalAudit>> GetHistoriqueAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<bool> UtilisateurPeutAccederUbAsync(long idUtilisateur, long idUB, CancellationToken cancellationToken = default);

    Task AddAuditAsync(long idUtilisateur, string operation, long idDemande, object? anciennes, object? nouvelles, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentEtabliListItemDto>> ListDocumentsEtablisAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default);

    Task<BilletConversion?> GetBilletConversionByDemandeAsync(long idDemande, CancellationToken cancellationToken = default);
    Task AddBilletConversionAsync(BilletConversion billet, CancellationToken cancellationToken = default);

    Task<PieceCaisse?> GetPieceCaisseByDemandeAsync(long idDemande, CancellationToken cancellationToken = default);
    Task AddPieceCaisseAsync(PieceCaisse piece, CancellationToken cancellationToken = default);

    Task<DemandePaiementAccesContext?> GetDemandeAccesContextAsync(
        long idDemande,
        CancellationToken cancellationToken = default);

    Task<DemandePaiementMutationHeaderReadModel?> GetMutationHeaderAsync(
        long idDemande,
        CancellationToken cancellationToken = default);

    Task<DemandePaiement?> GetDetailDtoApresMutationAsync(
        long idDemande,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validations entité minimales (Niveau, Statut, Ordre) — mutations Vague 2b-3 N1.
    /// </summary>
    Task<IReadOnlyList<DemandePaiementValidation>> GetValidationsEntiteMinimalAsync(
        long idDemande,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validations entité pour N2 physique (Niveau, Statut, Ordre, ModeValidation) — Vague 2b-4.
    /// </summary>
    Task<IReadOnlyList<DemandePaiementValidation>> GetValidationsEntiteN2Async(
        long idDemande,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validations N1/N2 pour Soumettre (empreinte, statut, mode, champs MapValidation) — Vague 2b-5.
    /// </summary>
    Task<IReadOnlyList<DemandePaiementValidation>> GetValidationsEntiteSoumettreAsync(
        long idDemande,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Données contractuelles pour <see cref="DemandePaiementEmpreinte.Calculer"/> (Vague 2b-3 N1).
    /// </summary>
    Task<DemandePaiement?> GetEmpreinteReadAsync(
        long idDemande,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enrichit une entité in-memory pour <see cref="MapDetail"/> sans DetailQuery (Vague 2b-3 N1).
    /// </summary>
    Task EnrichDemandeMapDetailShellAsync(
        DemandePaiement demande,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Statuts instruments minimaux pour <see cref="ModePaiementVerrouillageRules"/> (Vague 2b-5 Soumettre).
    /// </summary>
    Task EnrichStatutsInstrumentsMapDetailAsync(
        DemandePaiement demande,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enrichit l'entité tracked post-Envoyer pour <see cref="MapDetail"/> (Vague 2b-2).
    /// </summary>
    Task<DemandePaiement> GetDetailDtoApresEnvoyerAsync(
        DemandePaiement tracked,
        CancellationToken cancellationToken = default);
    Task<PieceCaissePdfData?> GetPieceCaissePdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default);
    Task<BonProvisoirePdfData?> GetBonProvisoirePdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default);
    Task<MinuteChequePdfData?> GetMinuteChequePdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default);
    Task<BilletConversionPdfData?> GetBilletConversionPdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default);
    Task<DemandePaiementPdfData?> GetDemandePaiementPdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default);
    Task<BonProvisoire?> GetBonProvisoireByDemandeAsync(long idDemande, CancellationToken cancellationToken = default);
    Task AddBonProvisoireAsync(BonProvisoire bon, CancellationToken cancellationToken = default);
    Task<MinuteCheque?> GetMinuteChequeByDemandeAsync(long idDemande, CancellationToken cancellationToken = default);
    Task AddMinuteChequeAsync(MinuteCheque minute, CancellationToken cancellationToken = default);

    Task<ParametreInstrumentPaiement?> GetParametreInstrumentAsync(
        string typeInstrument,
        CancellationToken cancellationToken = default);
    Task<ParametreInstrumentPaiement?> GetParametreInstrumentByTypeAsync(
        string typeInstrument,
        CancellationToken cancellationToken = default);
    Task UpsertParametreInstrumentAsync(
        ParametreInstrumentPaiement parametre,
        CancellationToken cancellationToken = default);

    Task<string> GenererNumeroPieceCaisseAsync(short annee, CancellationToken cancellationToken = default);
    Task<string> GenererNumeroBonProvisoireAsync(short annee, CancellationToken cancellationToken = default);
    Task<string> GenererNumeroMinuteChequeAsync(short annee, CancellationToken cancellationToken = default);

    Task AddValidationsEntiteAsync(
        IReadOnlyList<DemandePaiementValidation> validations,
        CancellationToken cancellationToken = default);

    Task<int> UpdateBrouillonHeaderIfModifiableAsync(
        long idDemande,
        DemandePaiementBrouillonHeaderPatch patch,
        CancellationToken cancellationToken = default);

    Task<int> UpdateIfStatutMatchesAsync(
        long idDemande,
        string statutAttendu,
        DemandePaiementConditionalUpdatePatch patch,
        CancellationToken cancellationToken = default);

    Task DesactiverRoutagesActifsAsync(long idDemande, CancellationToken cancellationToken = default);
    Task AddRoutageAsync(DemandePaiementRoutage routage, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandePaiementRoutage>> GetRoutagesAsync(
        long idDemande,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandePaiementRoutage>> GetRoutagesLectureAsync(
        long idDemande,
        CancellationToken cancellationToken = default);

    Task DeleteDemandeGraphAsync(DemandePaiement demande, CancellationToken cancellationToken = default);
}

public interface IDemandePaiementService
{
    Task<IReadOnlyList<DemandePaiementListDto>> ListAsync(DemandePaiementQuery query, CancellationToken cancellationToken = default);
    Task<DemandePaiementCompteursDto> GetCompteursAsync(
        DemandePaiementCompteursQuery query,
        CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto?> GetByIdAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailCompletDto?> GetDetailCompletAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailCompletDto?> GetDetailConsultationAsync(
        long idDemande,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandePaiementPieceDto>> GetPiecesAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<LigneBudgetaireDisponibleDto> GetLigneBudgetaireDisponibleAsync(
        LigneBudgetaireDisponibleQuery query,
        CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> CreateBrouillonAsync(CreateDemandePaiementRequest request, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> UpdateBrouillonAsync(long idDemande, UpdateDemandePaiementRequest request, CancellationToken cancellationToken = default);
    Task<DemandePaiementImputationDto> AddImputationAsync(long idDemande, CreateImputationRequest request, CancellationToken cancellationToken = default);
    Task<DemandePaiementImputationDto> UpdateImputationAsync(long idDemande, long idImputation, UpdateImputationRequest request, CancellationToken cancellationToken = default);
    Task DeleteImputationAsync(long idDemande, long idImputation, CancellationToken cancellationToken = default);
    Task<GrilleImputationDcDto> GetGrilleImputationDcAsync(
        long idDemande,
        byte? mois = null,
        CancellationToken cancellationToken = default);
    Task<GrilleImputationDcDto> EnregistrerImputationsDcAsync(
        long idDemande,
        EnregistrerImputationsDcRequest request,
        CancellationToken cancellationToken = default);
    Task<GrilleImputationAeDto> GetGrilleImputationAeAsync(
        long idDemande,
        string libelleItemAE,
        long? idGroupeItemAE = null,
        byte? mois = null,
        CancellationToken cancellationToken = default);
    Task<GrilleImputationAeDto> EnregistrerImputationsAeAsync(
        long idDemande,
        EnregistrerImputationsAeRequest request,
        CancellationToken cancellationToken = default);
    Task<GrilleImputationBiDto> GetGrilleImputationBiAsync(
        long idDemande,
        long idItemBI,
        byte? mois = null,
        CancellationToken cancellationToken = default);
    Task<GrilleImputationBiDto> EnregistrerImputationsBiAsync(
        long idDemande,
        EnregistrerImputationsBiRequest request,
        CancellationToken cancellationToken = default);
    Task<DemandePaiementPieceDto> AddPieceAsync(
        long idDemande,
        UploadDemandePaiementPieceMetadata metadata,
        Stream content,
        string originalFileName,
        string? contentType,
        CancellationToken cancellationToken = default);
    Task DeletePieceAsync(long idDemande, long idPiece, CancellationToken cancellationToken = default);
    Task<bool> DeleteBrouillonAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementPieceContentDto?> GetPieceContentAsync(
        long idDemande,
        long idPiece,
        CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> SoumettreAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> ReceptionnerAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> EntrerTraitementAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> TraiterChargeAsync(long idDemande, TraitementChargeDpmRequest request, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> RetenirSollicitationChargeAsync(
        long idDemande,
        RetenirSollicitationChargeRequest request,
        CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> OrienterAsync(long idDemande, OrienterDemandePaiementRequest? request = null, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> PrendreEnControleAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<ControleBudgetaireDto> ControlerBudgetaireAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> RetournerAsync(long idDemande, RetourDemandePaiementRequest request, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> RemettreEnBrouillonAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> ViserAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PieceManquanteDto>> GetPiecesManquantesAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<HistoriqueDemandePaiementDto?> GetHistoriqueAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandePaiementRoutageDto>?> GetRoutageAsync(
        long idDemande,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandePaiementRetourDestinataireDto>?> GetRetoursDestinatairesAsync(
        long idDemande,
        CancellationToken cancellationToken = default);

    Task<DemandePaiementDetailDto> EnvoyerEnValidationAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> ValiderN1ElectroniqueAsync(long idDemande, ValidationEntiteRequest? request = null, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> ValiderN2ElectroniqueAsync(long idDemande, ValidationEntiteRequest? request = null, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> DeclarerValidationPhysiqueN1Async(long idDemande, DeclarationValidationPhysiqueRequest request, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> DeclarerValidationPhysiqueN2Async(long idDemande, DeclarationValidationPhysiqueRequest request, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> RejeterValidationEntiteAsync(long idDemande, RetourDemandePaiementRequest request, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> AnnulerSoumissionAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> AnnulerValidationN2Async(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDetailDto> AnnulerValidationN1Async(long idDemande, CancellationToken cancellationToken = default);
    Task<DemandePaiementDocumentDto?> GetDocumentImpressionAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<byte[]> GenererDocumentPdfAsync(long idDemande, CancellationToken cancellationToken = default);

    Task<FicheImputationBudgetaireDto> GetFicheImputationAsync(
        long idDemande,
        FicheImputationMode mode,
        CancellationToken cancellationToken = default);
    Task<byte[]> GenererFicheImputationPdfAsync(
        long idDemande,
        FicheImputationMode mode,
        CancellationToken cancellationToken = default);

    Task<BilletConversionDto?> GetBilletConversionAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<BilletConversionDto> EtablirBilletConversionAsync(
        long idDemande,
        EtablirBilletConversionRequest request,
        CancellationToken cancellationToken = default);
    Task<byte[]> GenererBilletConversionPdfAsync(long idDemande, CancellationToken cancellationToken = default);

    Task<PieceCaisseDto?> GetPieceCaisseAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<PieceCaisseDto> EtablirPieceCaisseAsync(
        long idDemande,
        EtablirPieceCaisseRequest request,
        CancellationToken cancellationToken = default);
    Task<byte[]> GenererPieceCaissePdfAsync(long idDemande, CancellationToken cancellationToken = default);

    Task<BonProvisoireDto?> GetBonProvisoireAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<BonProvisoireDto> EtablirBonProvisoireAsync(
        long idDemande,
        EtablirBonProvisoireRequest request,
        CancellationToken cancellationToken = default);
    Task<byte[]> GenererBonProvisoirePdfAsync(long idDemande, CancellationToken cancellationToken = default);

    Task<MinuteChequeDto?> GetMinuteChequeAsync(long idDemande, CancellationToken cancellationToken = default);
    Task<MinuteChequeDto> EtablirMinuteChequeAsync(
        long idDemande,
        EtablirMinuteChequeRequest request,
        CancellationToken cancellationToken = default);
    Task<byte[]> GenererMinuteChequePdfAsync(long idDemande, CancellationToken cancellationToken = default);
}

public interface ICasDossierRepository
{
    Task<IReadOnlyList<CasDossier>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default);
    Task<CasDossier?> GetByIdAsync(long idCasDossier, CancellationToken cancellationToken = default);
    Task<CasDossier?> GetWithPiecesAsync(long idCasDossier, CancellationToken cancellationToken = default);
    Task<CasDossier?> GetTrackedWithPiecesAsync(long idCasDossier, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, long? excludeId, CancellationToken cancellationToken = default);
    Task<CasDossier> AddAsync(CasDossier entity, CancellationToken cancellationToken = default);
    Task<CasDossierPieceObligatoire?> GetPieceByIdAsync(long idPieceObligatoire, CancellationToken cancellationToken = default);
    Task<CasDossierPieceObligatoire?> GetPieceTrackedAsync(long idPieceObligatoire, CancellationToken cancellationToken = default);
    Task<bool> PieceCodeExistsAsync(long idCasDossier, string codeTypePiece, long? excludeId, CancellationToken cancellationToken = default);
    Task<bool> PieceEstReferenceeAsync(long idPieceObligatoire, CancellationToken cancellationToken = default);
    Task AddPieceAsync(CasDossierPieceObligatoire piece, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICasDossierService
{
    Task<IReadOnlyList<CasDossierDto>> ListAsync(bool actifsSeulement = true, CancellationToken cancellationToken = default);
    Task<CasDossierDto?> GetByIdAsync(long idCasDossier, bool piecesActivesSeulement = true, CancellationToken cancellationToken = default);
    Task<CasDossierDto> CreateAsync(CreateCasDossierRequest request, CancellationToken cancellationToken = default);
    Task<CasDossierDto?> UpdateAsync(long idCasDossier, UpdateCasDossierRequest request, CancellationToken cancellationToken = default);
    Task<CasDossierPieceObligatoireDto> AddPieceAsync(long idCasDossier, CreateCasDossierPieceRequest request, CancellationToken cancellationToken = default);
    Task<CasDossierPieceObligatoireDto?> UpdatePieceAsync(long idCasDossier, long idPiece, UpdateCasDossierPieceRequest request, CancellationToken cancellationToken = default);
}

public interface IDeviseRepository
{
    Task<IReadOnlyList<Devise>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default);
    Task<Devise?> GetByIdAsync(long idDevise, CancellationToken cancellationToken = default);
    Task<Devise?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, long? excludeId, CancellationToken cancellationToken = default);
    Task<bool> EstUtiliseeAsync(long idDevise, CancellationToken cancellationToken = default);
    Task<Devise> AddAsync(Devise entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IDeviseService
{
    Task<IReadOnlyList<DeviseDto>> ListAsync(bool actifsSeulement = true, CancellationToken cancellationToken = default);
    Task<DeviseDto?> GetByIdAsync(long idDevise, CancellationToken cancellationToken = default);
    Task<DeviseDto> CreateAsync(CreateDeviseRequest request, CancellationToken cancellationToken = default);
    Task<DeviseDto?> UpdateAsync(long idDevise, UpdateDeviseRequest request, CancellationToken cancellationToken = default);
}
