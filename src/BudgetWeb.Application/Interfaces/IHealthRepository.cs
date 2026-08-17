namespace BudgetWeb.Application.Interfaces;

public interface IHealthRepository
{
    Task<bool> CanConnectToDatabaseAsync(CancellationToken cancellationToken = default);
}
