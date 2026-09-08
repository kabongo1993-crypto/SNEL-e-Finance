using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IDepartementRepository
{
    Task<IReadOnlyList<DepartementDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DepartementDto?> GetByIdAsync(long idDepartement, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, long? excludeId = null, CancellationToken cancellationToken = default);

    Task<int> CountUnitesBudgetairesAsync(long idDepartement, CancellationToken cancellationToken = default);

    Task<DepartementDto> CreateAsync(
        string code,
        string libelle,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<DepartementDto?> UpdateAsync(
        long idDepartement,
        string code,
        string libelle,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idDepartement, CancellationToken cancellationToken = default);
}

public interface IDepartementService
{
    Task<IReadOnlyList<DepartementDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DepartementDto?> GetByIdAsync(long idDepartement, CancellationToken cancellationToken = default);

    Task<DepartementDto> CreateAsync(
        CreateDepartementRequest request,
        CancellationToken cancellationToken = default);

    Task<DepartementDto?> UpdateAsync(
        long idDepartement,
        UpdateDepartementRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idDepartement, CancellationToken cancellationToken = default);
}
