using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class GroupeTypeCompteRepository : IGroupeTypeCompteRepository
{
    private readonly BudgetDbContext _context;

    public GroupeTypeCompteRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GroupeTypeCompte>> ListAsync(
        bool? actifsSeulement,
        CancellationToken cancellationToken = default)
    {
        var q = _context.GroupesTypeCompte.AsNoTracking().AsQueryable();
        if (actifsSeulement == true)
            q = q.Where(g => g.Actif);
        return await q.OrderBy(g => g.IdGroupeTypeCompte).ToListAsync(cancellationToken);
    }

    public Task<GroupeTypeCompte?> GetByIdAsync(long idGroupeTypeCompte, CancellationToken cancellationToken = default)
        => _context.GroupesTypeCompte.FirstOrDefaultAsync(
            g => g.IdGroupeTypeCompte == idGroupeTypeCompte,
            cancellationToken);

    public Task<bool> LibelleExistsAsync(
        string libelle,
        long? excludeId,
        CancellationToken cancellationToken = default)
        => _context.GroupesTypeCompte.AsNoTracking()
            .AnyAsync(
                g => g.Libelle == libelle && (excludeId == null || g.IdGroupeTypeCompte != excludeId),
                cancellationToken);

    public async Task<GroupeTypeCompte> AddAsync(GroupeTypeCompte entity, CancellationToken cancellationToken = default)
    {
        _context.GroupesTypeCompte.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
