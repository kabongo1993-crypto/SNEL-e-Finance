using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface IAjustementBudgetaireService
{
    Task<IReadOnlyList<AjustementBudgetaireDto>> GetAllAsync(
        AjustementBudgetaireQuery query, CancellationToken cancellationToken = default);

    Task<AjustementBudgetaireDto?> GetByIdAsync(long idAjustement, CancellationToken cancellationToken = default);

    Task<AjustementHistoriqueLigneDto> GetHistoriqueLigneAsync(
        long idPrevision, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AjustementLigneCandidateDto>> GetLignesValideesAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toutes les lignes de prévision dont le workflow VERSION×UB est VALIDEE
    /// (budget annuel applicable), avec budget initial et actuel.
    /// </summary>
    Task<IReadOnlyList<AjustementLigneCandidateDto>> GetLignesDisponiblesAsync(
        AjustementBudgetaireQuery query, CancellationToken cancellationToken = default);

    Task<AjustementBudgetaireDto> CreateAsync(
        CreateAjustementBudgetaireRequest request, CancellationToken cancellationToken = default);

    Task<AjustementBudgetaireDto> UpdateAsync(
        long idAjustement, UpdateAjustementBudgetaireRequest request, CancellationToken cancellationToken = default);

    Task<AjustementBudgetaireDto> ValiderAsync(long idAjustement, CancellationToken cancellationToken = default);

    Task<AjustementBudgetaireDto> AnnulerAsync(long idAjustement, CancellationToken cancellationToken = default);
}

public interface IAjustementBudgetaireRepository
{
    Task<IReadOnlyList<AjustementBudgetaire>> ListAsync(
        AjustementBudgetaireQuery query, CancellationToken cancellationToken = default);

    Task<AjustementBudgetaire?> GetByIdAsync(long idAjustement, CancellationToken cancellationToken = default);

    Task<AjustementBudgetaire?> GetTrackedAsync(long idAjustement, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AjustementBudgetaire>> ListByPrevisionAsync(
        long idPrevision, CancellationToken cancellationToken = default);

    Task<PrevisionBudgetaire?> GetPrevisionAsync(long idPrevision, CancellationToken cancellationToken = default);

    Task<string?> GetWorkflowStatutAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<bool> ExistsBrouillonPourPrevisionAsync(
        long idPrevision, long? excludeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrevisionBudgetaire>> GetPrevisionsValideesUbAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrevisionBudgetaire>> GetPrevisionsValideesAsync(
        AjustementBudgetaireQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, (decimal? FirstAncien, int NbValides, bool HasBrouillon)>> GetAjustementStatsByPrevisionAsync(
        IReadOnlyList<long> idPrevisions, CancellationToken cancellationToken = default);

    Task<ExerciceBudgetaire?> GetExerciceCourantAsync(CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<AjustementBudgetaire> AddAsync(AjustementBudgetaire entity, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task ApplyMontantPrevisionAsync(
        long idPrevision,
        decimal montantNouveau,
        long idUtilisateur,
        CancellationToken cancellationToken = default);

    Task AddAuditAsync(
        long idUtilisateur,
        string operation,
        long idAjustement,
        object? anciennes,
        object? nouvelles,
        CancellationToken cancellationToken = default);
}
