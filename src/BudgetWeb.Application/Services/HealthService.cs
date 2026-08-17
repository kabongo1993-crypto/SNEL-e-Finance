using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public class HealthService : IHealthService
{
    private readonly IHealthRepository _healthRepository;

    public HealthService(IHealthRepository healthRepository)
    {
        _healthRepository = healthRepository;
    }

    public async Task<HealthStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var canConnect = await _healthRepository.CanConnectToDatabaseAsync(cancellationToken);
        return new HealthStatusDto(canConnect ? "healthy" : "unhealthy", canConnect);
    }
}
