using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IDepartementRepository
{
    Task<IReadOnlyList<DepartementDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<DepartementDto> CreateAsync(
        string code,
        string libelle,
        bool actif,
        CancellationToken cancellationToken = default);
}

public interface IDepartementService
{
    Task<IReadOnlyList<DepartementDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DepartementDto> CreateAsync(
        CreateDepartementRequest request,
        CancellationToken cancellationToken = default);
}
