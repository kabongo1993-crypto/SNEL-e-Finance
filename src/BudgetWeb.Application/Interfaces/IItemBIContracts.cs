using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IItemBIRepository
{
    Task<IReadOnlyList<ItemBIDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ItemBIDto?> GetByIdAsync(long idItemBI, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string codeItem, long? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsByIdAsync(long idItemBI, CancellationToken cancellationToken = default);

    Task<bool> WouldCreateCycleAsync(long idItemBI, long parentId, CancellationToken cancellationToken = default);

    Task<int> CountEnfantsAsync(long idItemBI, CancellationToken cancellationToken = default);

    Task<int> CountPrevisionsAsync(long idItemBI, CancellationToken cancellationToken = default);

    Task<ItemBIDto> CreateAsync(
        string codeItem,
        string libelle,
        long? parentId,
        int niveau,
        string? categorie,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<ItemBIDto?> UpdateAsync(
        long idItemBI,
        string codeItem,
        string libelle,
        long? parentId,
        int niveau,
        string? categorie,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<ItemBIDto?> SetActifAsync(long idItemBI, bool actif, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idItemBI, CancellationToken cancellationToken = default);
}

public interface IItemBIService
{
    Task<IReadOnlyList<ItemBIDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemBINoeudDto>> GetArbreAsync(CancellationToken cancellationToken = default);

    Task<ItemBIDto?> GetByIdAsync(long idItemBI, CancellationToken cancellationToken = default);

    Task<ItemBIDto> CreateAsync(CreateItemBIRequest request, CancellationToken cancellationToken = default);

    Task<ItemBIDto?> UpdateAsync(
        long idItemBI,
        UpdateItemBIRequest request,
        CancellationToken cancellationToken = default);

    Task<ItemBIDto?> SetActifAsync(
        long idItemBI,
        SetActifRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idItemBI, CancellationToken cancellationToken = default);
}
