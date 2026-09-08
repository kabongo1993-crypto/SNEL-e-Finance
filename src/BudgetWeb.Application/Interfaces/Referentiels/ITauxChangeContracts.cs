using BudgetWeb.Application.DTOs.Referentiels;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Referentiels;



namespace BudgetWeb.Application.Interfaces.Referentiels;



public interface ITauxChangeRepository

{

    Task<IReadOnlyList<TauxChange>> ListAsync(TauxChangeListQuery query, CancellationToken cancellationToken = default);

    Task<TauxChange?> GetByIdAsync(long idTauxChange, CancellationToken cancellationToken = default);

    Task<TauxChange?> GetByIdTrackedAsync(long idTauxChange, CancellationToken cancellationToken = default);



    /// <summary>Recherche historique sur la paire canonique (orientation stockée).</summary>

    Task<TauxChange?> FindApplicableCanoniqueAsync(

        string deviseBase,

        string deviseQuote,

        DateOnly dateReference,

        CancellationToken cancellationToken = default);



    Task<TauxChange?> FindActifCourantCanoniqueAsync(

        string deviseBase,

        string deviseQuote,

        CancellationToken cancellationToken = default);



    Task<bool> ExistsForCanoniqueAndDateEffetAsync(

        string deviseBase,

        string deviseQuote,

        DateOnly dateEffet,

        long? excludeIdTauxChange = null,

        CancellationToken cancellationToken = default);



    Task<bool> EstReferenceParProcedureAsync(long idTauxChange, CancellationToken cancellationToken = default);



    Task<IReadOnlySet<long>> GetIdsReferenceParProcedureAsync(

        IEnumerable<long> idsTauxChange,

        CancellationToken cancellationToken = default);



    /// <summary>Existence d'une orientation inverse (legacy) — doit être refusée à la création.</summary>

    Task<bool> ExistsOrientationInverseAsync(

        string deviseBase,

        string deviseQuote,

        CancellationToken cancellationToken = default);



    /// <summary>Passe tous les ACTIF de la paire (canonique + inverse legacy) à INACTIF, insère la nouvelle version.</summary>

    Task<TauxChange> CreateVersionReplacingActifAsync(

        TauxChange newVersion,

        long userIdModification,

        CancellationToken cancellationToken = default);



    Task<TauxChange> AddAsync(TauxChange entity, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

}



public interface ITauxChangeService

{

    IReadOnlyList<PaireTauxChangeDto> ListerPairesSupportees();

    Task<IReadOnlyList<PaireTauxChangeDto>> ListerPairesSupporteesAsync(
        CancellationToken cancellationToken = default);



    Task<IReadOnlyList<TauxChangeDto>> ListAsync(

        TauxChangeListQuery? query = null,

        CancellationToken cancellationToken = default);



    Task<TauxChangeDto?> GetByIdAsync(long idTauxChange, CancellationToken cancellationToken = default);



    Task<TauxChangeApplicableDto?> GetApplicableAsync(

        string deviseSource,

        string deviseCible,

        DateOnly dateReference,

        CancellationToken cancellationToken = default);



    Task<TauxChangeApplicableDto?> GetApplicableVersUsdAsync(

        string deviseSource,

        DateOnly dateReference,

        CancellationToken cancellationToken = default);



    ConversionUsdResultDto ConvertirVersUsd(

        decimal montantBrut,

        string deviseSource,

        decimal tauxReference,

        decimal tauxDirectionnel,

        long? idTauxChange,

        bool estIdentite,

        TauxChangeConventions.PaireCanonique paire);



    Task<ConversionUsdResultDto> ConvertirVersUsdAsync(

        decimal montantBrut,

        string deviseSource,

        DateOnly dateReference,

        CancellationToken cancellationToken = default);



    ConversionResultDto Convertir(

        decimal montantSource,

        string deviseSource,

        string deviseCible,

        decimal tauxReference,

        decimal tauxDirectionnel,

        long? idTauxChange,

        bool estIdentite,

        TauxChangeConventions.PaireCanonique paire);



    Task<ConversionResultDto> ConvertirAsync(

        decimal montantSource,

        string deviseSource,

        string deviseCible,

        DateOnly dateReference,

        CancellationToken cancellationToken = default);



    Task<TauxChangeDto> CreateVersionAsync(

        CreateTauxChangeRequest request,

        CancellationToken cancellationToken = default);



    Task<TauxChangeDto> InactivateAsync(

        long idTauxChange,

        InactivateTauxChangeRequest request,

        CancellationToken cancellationToken = default);



    Task<TauxChangeDto> UpdateVersionAsync(

        long idTauxChange,

        UpdateTauxChangeRequest request,

        CancellationToken cancellationToken = default);

}


