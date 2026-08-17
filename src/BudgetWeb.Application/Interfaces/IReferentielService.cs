using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IReferentielService
{
    Task<IReadOnlyList<TypeBudgetDto>> GetTypesBudgetAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModePrevisionDto>> GetModesPrevisionAsync(CancellationToken cancellationToken = default);
}
