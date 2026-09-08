using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IRubriqueBudgetaireRepository
{
    Task<IReadOnlyList<RubriqueBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<RubriqueBudgetaireDto?> GetByIdAsync(long idRB, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string codeRB, long? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsByIdAsync(long idRB, CancellationToken cancellationToken = default);

    Task<bool> WouldCreateCycleAsync(long idRB, long parentId, CancellationToken cancellationToken = default);

    Task<int> CountEnfantsAsync(long idRB, CancellationToken cancellationToken = default);

    Task<int> CountPrevisionsAsync(long idRB, CancellationToken cancellationToken = default);

    Task<RubriqueBudgetaireDto> CreateAsync(
        string codeRB,
        string libelle,
        long? parentId,
        int niveau,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<RubriqueBudgetaireDto?> UpdateAsync(
        long idRB,
        string codeRB,
        string libelle,
        long? parentId,
        int niveau,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<RubriqueBudgetaireDto?> SetActifAsync(long idRB, bool actif, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idRB, CancellationToken cancellationToken = default);
}

public interface IRubriqueBudgetaireService
{
    Task<IReadOnlyList<RubriqueBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RubriqueBudgetaireNoeudDto>> GetArbreAsync(CancellationToken cancellationToken = default);

    Task<RubriqueBudgetaireDto?> GetByIdAsync(long idRB, CancellationToken cancellationToken = default);

    Task<RubriqueBudgetaireDto> CreateAsync(
        CreateRubriqueBudgetaireRequest request,
        CancellationToken cancellationToken = default);

    Task<RubriqueBudgetaireDto?> UpdateAsync(
        long idRB,
        UpdateRubriqueBudgetaireRequest request,
        CancellationToken cancellationToken = default);

    Task<RubriqueBudgetaireDto?> SetActifAsync(
        long idRB,
        SetActifRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idRB, CancellationToken cancellationToken = default);
}
