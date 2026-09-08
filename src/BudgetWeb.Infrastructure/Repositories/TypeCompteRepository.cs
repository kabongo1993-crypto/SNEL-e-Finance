using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class TypeCompteRepository : ITypeCompteRepository
{
    private readonly BudgetDbContext _context;

    public TypeCompteRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TypeCompte>> ListAsync(
        bool? actifsSeulement,
        CancellationToken cancellationToken = default)
    {
        var q = _context.TypesCompte.AsNoTracking().Include(t => t.GroupeTypeCompte).AsQueryable();
        if (actifsSeulement == true)
            q = q.Where(t => t.Actif);
        return await q.OrderBy(t => t.Code).ToListAsync(cancellationToken);
    }

    public Task<TypeCompte?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => _context.TypesCompte
            .Include(t => t.GroupeTypeCompte)
            .FirstOrDefaultAsync(t => t.Code == code, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, string? excludeCode, CancellationToken cancellationToken = default)
        => _context.TypesCompte.AsNoTracking()
            .AnyAsync(
                t => t.Code == code && (excludeCode == null || t.Code != excludeCode),
                cancellationToken);

    public Task<int> CountComptesByCodeAsync(string code, CancellationToken cancellationToken = default)
        => _context.ComptesFinanciers.AsNoTracking()
            .CountAsync(c => c.FK_TypeCompte == code, cancellationToken);

    public async Task<TypeCompte> AddAsync(TypeCompte entity, CancellationToken cancellationToken = default)
    {
        _context.TypesCompte.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task RenameCodeAsync(string currentCode, string newCode, TypeCompte updated, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var n = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE [pct].[TYPE_COMPTE]
                SET [Code] = {newCode},
                    [Libelle] = {updated.Libelle},
                    [FK_GroupeTypeCompte] = {updated.FK_GroupeTypeCompte},
                    [Actif] = {updated.Actif},
                    [DateModification] = {updated.DateModification}
                WHERE [Code] = {currentCode}
                """,
                cancellationToken);
            if (n != 1)
                throw new InvalidOperationException("Le type de compte à modifier est introuvable.");

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
