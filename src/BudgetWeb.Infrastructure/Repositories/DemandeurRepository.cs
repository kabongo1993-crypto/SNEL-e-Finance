using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class DemandeurRepository : IDemandeurRepository
{
    private readonly BudgetDbContext _context;

    public DemandeurRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Demandeur>> ListAsync(
        bool? actifOnly = true,
        CancellationToken cancellationToken = default)
    {
        var q = Query();
        if (actifOnly == true)
            q = q.Where(d => d.Actif);
        return await q
            .OrderBy(d => d.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<Demandeur?> GetByIdAsync(long idDemandeur, CancellationToken cancellationToken = default)
        => Query().FirstOrDefaultAsync(d => d.IdDemandeur == idDemandeur, cancellationToken);

    public Task<Demandeur?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var c = code.Trim().ToUpperInvariant();
        return Query().FirstOrDefaultAsync(d => d.Code == c, cancellationToken);
    }

    public Task<Demandeur?> GetTrackedAsync(long idDemandeur, CancellationToken cancellationToken = default)
        => _context.Demandeurs.FirstOrDefaultAsync(d => d.IdDemandeur == idDemandeur, cancellationToken);

    public Task<bool> CodeExistsAsync(
        string code,
        long? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var c = code.Trim().ToUpperInvariant();
        var q = _context.Demandeurs.AsNoTracking().Where(d => d.Code == c);
        if (excludeId is long id)
            q = q.Where(d => d.IdDemandeur != id);
        return q.AnyAsync(cancellationToken);
    }

    public Task<UniteBudgetaire?> GetUniteBudgetaireAsync(long idUB, CancellationToken cancellationToken = default)
        => _context.UnitesBudgetaires.AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUB == idUB, cancellationToken);

    public async Task<Demandeur> AddAsync(Demandeur entity, CancellationToken cancellationToken = default)
    {
        // Ne jamais persister le graphe UB/Département : FK seule.
        entity.UniteBudgetaire = default!;
        _context.Demandeurs.Add(entity);
        await Task.CompletedTask;
        return entity;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    private IQueryable<Demandeur> Query()
        => _context.Demandeurs.AsNoTracking()
            .Include(d => d.UniteBudgetaire)
            .ThenInclude(u => u.Departement);
}
