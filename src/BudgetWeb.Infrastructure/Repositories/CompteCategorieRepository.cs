using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class CompteCategorieRepository : ICompteCategorieRepository
{
    private readonly BudgetDbContext _context;

    public CompteCategorieRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CompteCategorie>> ListByCompteAsync(
        long idCompte,
        CancellationToken cancellationToken = default)
        => await QueryWithRefs()
            .Where(c => c.FK_Compte == idCompte)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CompteCategorie>> ListAllAsync(CancellationToken cancellationToken = default)
        => await QueryWithRefs().ToListAsync(cancellationToken);

    public Task<CompteCategorie?> GetByIdAsync(long idCompteCategorie, CancellationToken cancellationToken = default)
        => QueryWithRefs().FirstOrDefaultAsync(c => c.IdCompteCategorie == idCompteCategorie, cancellationToken);

    public Task<CompteCategorie?> GetTrackedByIdAsync(long idCompteCategorie, CancellationToken cancellationToken = default)
        => _context.ComptesCategories
            .Include(c => c.Compte)
            .Include(c => c.CategorieCompte)
            .FirstOrDefaultAsync(c => c.IdCompteCategorie == idCompteCategorie, cancellationToken);

    public Task<bool> ExistsIdAsync(long idCompteCategorie, CancellationToken cancellationToken = default)
        => _context.ComptesCategories.AsNoTracking()
            .AnyAsync(c => c.IdCompteCategorie == idCompteCategorie, cancellationToken);

    public async Task<IReadOnlyList<long>> ListIdsAsync(CancellationToken cancellationToken = default)
        => await _context.ComptesCategories.AsNoTracking()
            .Select(c => c.IdCompteCategorie)
            .ToListAsync(cancellationToken);

    public async Task<CompteCategorie> AddAsync(CompteCategorie entity, CancellationToken cancellationToken = default)
    {
        _context.ComptesCategories.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task AddRangeWithExplicitIdsAsync(
        IReadOnlyList<CompteCategorie> entities,
        CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
            return;

        await _context.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SET IDENTITY_INSERT [pct].[COMPTE_CATEGORIE] ON;",
                cancellationToken);
            foreach (var entity in entities)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO [pct].[COMPTE_CATEGORIE]
                        ([IdCompteCategorie], [FK_Compte], [FK_CategorieCompte], [DateDebut], [DateFin])
                    VALUES
                        ({entity.IdCompteCategorie}, {entity.FK_Compte}, {entity.FK_CategorieCompte},
                         {entity.DateDebut.ToDateTime(TimeOnly.MinValue)},
                         {(entity.DateFin is null
                             ? (DateTime?)null
                             : entity.DateFin.Value.ToDateTime(TimeOnly.MinValue))});
                    """,
                    cancellationToken);
            }

            await _context.Database.ExecuteSqlRawAsync(
                "SET IDENTITY_INSERT [pct].[COMPTE_CATEGORIE] OFF;",
                cancellationToken);
            var maxId = entities.Max(e => e.IdCompteCategorie);
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DBCC CHECKIDENT ('pct.COMPTE_CATEGORIE', RESEED, {maxId});",
                cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "SET IDENTITY_INSERT [pct].[COMPTE_CATEGORIE] OFF;",
                    cancellationToken);
            }
            catch
            {
                // La session peut déjà être fermée.
            }

            throw;
        }
        finally
        {
            _context.ChangeTracker.Clear();
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is not null)
        {
            await action();
            return;
        }

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action();
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private IQueryable<CompteCategorie> QueryWithRefs()
        => _context.ComptesCategories
            .AsNoTracking()
            .Include(c => c.Compte)
            .Include(c => c.CategorieCompte);
}
