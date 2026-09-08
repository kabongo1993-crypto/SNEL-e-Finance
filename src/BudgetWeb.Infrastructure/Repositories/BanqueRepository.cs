using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class BanqueRepository : IBanqueRepository
{
    private readonly BudgetDbContext _context;

    public BanqueRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Banque>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
    {
        var q = _context.Banques.AsNoTracking().AsQueryable();
        if (actifsSeulement == true)
            q = q.Where(b => b.Actif);
        return await q.OrderBy(b => b.IdBanque).ToListAsync(cancellationToken);
    }

    public Task<Banque?> GetByIdAsync(string idBanque, CancellationToken cancellationToken = default)
        => _context.Banques.FirstOrDefaultAsync(b => b.IdBanque == idBanque, cancellationToken);

    public Task<bool> ExistsAsync(string idBanque, CancellationToken cancellationToken = default)
        => _context.Banques.AsNoTracking().AnyAsync(b => b.IdBanque == idBanque, cancellationToken);

    public async Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken = default)
        => await _context.Banques.AsNoTracking().Select(b => b.IdBanque).ToListAsync(cancellationToken);

    public async Task<Banque> AddAsync(Banque entity, CancellationToken cancellationToken = default)
    {
        _context.Banques.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task AddRangeInTransactionAsync(
        IReadOnlyList<Banque> entities,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Banques.AddRange(entities);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
