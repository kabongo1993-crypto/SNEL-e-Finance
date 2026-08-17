using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IHealthService
{
    Task<HealthStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
}
