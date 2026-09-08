using BudgetWeb.Application.SnelComptes.Import.Interfaces;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Import;

public class SnelComptesImportTransaction : ISnelComptesImportTransaction
{
    private readonly BudgetDbContext _context;

    public SnelComptesImportTransaction(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
