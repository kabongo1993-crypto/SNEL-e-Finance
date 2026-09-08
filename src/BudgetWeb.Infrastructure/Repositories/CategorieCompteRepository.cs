using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class CategorieCompteRepository : ICategorieCompteRepository
{
    private readonly BudgetDbContext _context;

    public CategorieCompteRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CategorieCompte>> ListAsync(
        bool? actifsSeulement,
        CancellationToken cancellationToken = default)
    {
        var q = _context.CategoriesCompte.AsNoTracking().AsQueryable();
        if (actifsSeulement == true)
            q = q.Where(c => c.Actif);
        return await q.OrderBy(c => c.IdCategorieCompte).ToListAsync(cancellationToken);
    }

    public Task<CategorieCompte?> GetByIdAsync(long idCategorieCompte, CancellationToken cancellationToken = default)
        => _context.CategoriesCompte.FirstOrDefaultAsync(
            c => c.IdCategorieCompte == idCategorieCompte,
            cancellationToken);

    public Task<bool> LibelleExistsAsync(
        string libelle,
        long? excludeId,
        CancellationToken cancellationToken = default)
        => _context.CategoriesCompte.AsNoTracking()
            .AnyAsync(
                c => c.Libelle == libelle && (excludeId == null || c.IdCategorieCompte != excludeId),
                cancellationToken);

    public async Task<CategorieCompte> AddAsync(CategorieCompte entity, CancellationToken cancellationToken = default)
    {
        _context.CategoriesCompte.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
