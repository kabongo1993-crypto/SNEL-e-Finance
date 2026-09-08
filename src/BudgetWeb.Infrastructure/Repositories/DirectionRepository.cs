using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class DirectionRepository : IDirectionRepository
{
    private readonly BudgetDbContext _context;

    public DirectionRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DirectionTresorerie>> ListAsync(
        bool? actifsSeulement,
        CancellationToken cancellationToken = default)
    {
        var q = _context.DirectionsTresorerie.AsNoTracking().AsQueryable();
        if (actifsSeulement == true)
            q = q.Where(d => d.Actif);
        return await q.OrderBy(d => d.IdDirection).ToListAsync(cancellationToken);
    }

    public Task<DirectionTresorerie?> GetByIdAsync(long idDirection, CancellationToken cancellationToken = default)
        => _context.DirectionsTresorerie.FirstOrDefaultAsync(
            d => d.IdDirection == idDirection,
            cancellationToken);

    public Task<bool> LibelleExistsAsync(
        string libelle,
        long? excludeId,
        CancellationToken cancellationToken = default)
        => _context.DirectionsTresorerie.AsNoTracking()
            .AnyAsync(
                d => d.Libelle == libelle && (excludeId == null || d.IdDirection != excludeId),
                cancellationToken);

    public async Task<DirectionTresorerie> AddAsync(DirectionTresorerie entity, CancellationToken cancellationToken = default)
    {
        _context.DirectionsTresorerie.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
