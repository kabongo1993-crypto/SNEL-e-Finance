using BudgetWeb.Application.Interfaces;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class HealthRepository : IHealthRepository
{
    private readonly BudgetDbContext _context;

    public HealthRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public Task<bool> CanConnectToDatabaseAsync(CancellationToken cancellationToken = default)
        => _context.Database.CanConnectAsync(cancellationToken);
}
