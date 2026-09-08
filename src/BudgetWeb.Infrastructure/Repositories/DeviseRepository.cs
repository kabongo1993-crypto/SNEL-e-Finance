using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class DeviseRepository : IDeviseRepository
{
    private readonly BudgetDbContext _context;

    public DeviseRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Devise>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
    {
        var q = _context.Devises.AsNoTracking().AsQueryable();
        if (actifsSeulement == true)
            q = q.Where(d => d.Actif);
        return await q.OrderBy(d => d.Code).ToListAsync(cancellationToken);
    }

    public Task<Devise?> GetByIdAsync(long idDevise, CancellationToken cancellationToken = default)
        => _context.Devises.FirstOrDefaultAsync(d => d.IdDevise == idDevise, cancellationToken);

    public Task<Devise?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => _context.Devises.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Code == code, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, long? excludeId, CancellationToken cancellationToken = default)
        => _context.Devises.AsNoTracking()
            .AnyAsync(d => d.Code == code && (excludeId == null || d.IdDevise != excludeId), cancellationToken);

    public Task<bool> EstUtiliseeAsync(long idDevise, CancellationToken cancellationToken = default)
        => _context.DemandesPaiement.AsNoTracking()
            .AnyAsync(d => d.FK_Devise == idDevise, cancellationToken);

    public async Task<Devise> AddAsync(Devise entity, CancellationToken cancellationToken = default)
    {
        _context.Devises.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
