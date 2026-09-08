using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface ITypeBudgetRepository
{
    Task<IReadOnlyList<TypeBudgetDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TypeBudgetDto?> GetByIdAsync(long idTypeBudget, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string codeType, long? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsByOrdreAsync(int ordreAffichage, long? excludeId = null, CancellationToken cancellationToken = default);

    Task<int> CountPrevisionsAsync(long idTypeBudget, CancellationToken cancellationToken = default);

    Task<TypeBudgetDto> CreateAsync(
        string codeType,
        string libelle,
        int ordreAffichage,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<TypeBudgetDto?> UpdateAsync(
        long idTypeBudget,
        string codeType,
        string libelle,
        int ordreAffichage,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idTypeBudget, CancellationToken cancellationToken = default);
}

public interface ITypeBudgetService
{
    Task<IReadOnlyList<TypeBudgetDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TypeBudgetDto?> GetByIdAsync(long idTypeBudget, CancellationToken cancellationToken = default);

    Task<TypeBudgetDto> CreateAsync(
        CreateTypeBudgetRequest request,
        CancellationToken cancellationToken = default);

    Task<TypeBudgetDto?> UpdateAsync(
        long idTypeBudget,
        UpdateTypeBudgetRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idTypeBudget, CancellationToken cancellationToken = default);
}
