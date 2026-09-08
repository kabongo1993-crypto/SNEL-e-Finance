using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class ProvinceRepository : IProvinceRepository
{
    private readonly BudgetDbContext _context;

    public ProvinceRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Province>> ListAsync(
        bool? actifsSeulement,
        CancellationToken cancellationToken = default)
    {
        var q = _context.Provinces.AsNoTracking().AsQueryable();
        if (actifsSeulement == true)
            q = q.Where(p => p.Actif);
        return await q.OrderBy(p => p.IdProvince).ToListAsync(cancellationToken);
    }

    public Task<Province?> GetByIdAsync(string idProvince, CancellationToken cancellationToken = default)
        => _context.Provinces.FirstOrDefaultAsync(p => p.IdProvince == idProvince, cancellationToken);

    public Task<bool> IdExistsAsync(string idProvince, string? excludeId, CancellationToken cancellationToken = default)
        => _context.Provinces.AsNoTracking()
            .AnyAsync(
                p => p.IdProvince == idProvince && (excludeId == null || p.IdProvince != excludeId),
                cancellationToken);

    public Task<bool> LibelleExistsAsync(string libelle, string? excludeId, CancellationToken cancellationToken = default)
        => _context.Provinces.AsNoTracking()
            .AnyAsync(
                p => p.Libelle == libelle && (excludeId == null || p.IdProvince != excludeId),
                cancellationToken);

    public Task<int> CountComptesByIdAsync(string idProvince, CancellationToken cancellationToken = default)
        => _context.ComptesFinanciers.AsNoTracking()
            .CountAsync(c => c.FK_Province == idProvince, cancellationToken);

    public async Task<Province> AddAsync(Province entity, CancellationToken cancellationToken = default)
    {
        _context.Provinces.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task RenameIdAsync(
        string currentId,
        string newId,
        Province updated,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var n = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE [pct].[PROVINCE]
                SET [IdProvince] = {newId},
                    [Libelle] = {updated.Libelle},
                    [Actif] = {updated.Actif},
                    [DateModification] = {updated.DateModification}
                WHERE [IdProvince] = {currentId}
                """,
                cancellationToken);
            if (n != 1)
                throw new InvalidOperationException("La province à modifier est introuvable.");

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }

        _context.ChangeTracker.Clear();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
