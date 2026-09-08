using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class CompteFinancierRepository : ICompteFinancierRepository
{
    private readonly BudgetDbContext _context;

    public CompteFinancierRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CompteFinancier>> ListAsync(
        bool? actifsSeulement,
        CancellationToken cancellationToken = default)
    {
        var q = _context.ComptesFinanciers.AsNoTracking();
        if (actifsSeulement == true)
            q = q.Where(c => c.Actif);
        return await q
            .OrderBy(c => c.FK_Banque)
            .ThenBy(c => c.NumeroCompte)
            .Select(c => new CompteFinancier
            {
                IdCompte = c.IdCompte,
                NumeroCompte = c.NumeroCompte,
                LibelleCompte = c.LibelleCompte,
                FK_Banque = c.FK_Banque,
                FK_Direction = c.FK_Direction,
                FK_TypeCompte = c.FK_TypeCompte,
                FK_Devise = c.FK_Devise,
                FK_Province = c.FK_Province,
                FK_Utilisateur = c.FK_Utilisateur,
                DateCreation = c.DateCreation,
                DateCloture = c.DateCloture,
                DateModification = c.DateModification,
                Actif = c.Actif,
                Banque = new Banque { IdBanque = c.FK_Banque, LibelleBanque = c.Banque.LibelleBanque },
                Direction = new DirectionTresorerie { IdDirection = c.FK_Direction, Libelle = c.Direction.Libelle },
                TypeCompte = new TypeCompte { Code = c.FK_TypeCompte, Libelle = c.TypeCompte.Libelle },
                Devise = new Devise { IdDevise = c.FK_Devise, Code = c.Devise.Code, Libelle = c.Devise.Libelle },
                Province = c.FK_Province == null
                    ? null
                    : new Province { IdProvince = c.FK_Province, Libelle = c.Province!.Libelle },
                Utilisateur = c.FK_Utilisateur == null
                    ? null
                    : new Utilisateur
                    {
                        IdUtilisateur = c.FK_Utilisateur.Value,
                        Nom = c.Utilisateur!.Nom,
                        Prenom = c.Utilisateur.Prenom,
                        NomUtilisateur = c.Utilisateur.NomUtilisateur,
                    },
            })
            .ToListAsync(cancellationToken);
    }

    public Task<CompteFinancier?> GetByIdAsync(long idCompte, CancellationToken cancellationToken = default)
        => QueryWithRefs().FirstOrDefaultAsync(c => c.IdCompte == idCompte, cancellationToken);

    public Task<CompteFinancier?> GetTrackedByIdAsync(long idCompte, CancellationToken cancellationToken = default)
        => _context.ComptesFinanciers
            .Include(c => c.Banque)
            .Include(c => c.Direction)
            .Include(c => c.TypeCompte)
            .Include(c => c.Devise)
            .Include(c => c.Province)
            .Include(c => c.Utilisateur)
            .FirstOrDefaultAsync(c => c.IdCompte == idCompte, cancellationToken);

    public Task<bool> ExistsNumeroAsync(
        string fkBanque,
        string numeroCompte,
        long? excludeId,
        CancellationToken cancellationToken = default)
        => _context.ComptesFinanciers.AsNoTracking().AnyAsync(
            c => c.FK_Banque == fkBanque
                && c.NumeroCompte == numeroCompte
                && (excludeId == null || c.IdCompte != excludeId),
            cancellationToken);

    public Task<bool> ExistsIdAsync(long idCompte, CancellationToken cancellationToken = default)
        => _context.ComptesFinanciers.AsNoTracking().AnyAsync(c => c.IdCompte == idCompte, cancellationToken);

    public async Task<IReadOnlyList<long>> ListIdsAsync(CancellationToken cancellationToken = default)
        => await _context.ComptesFinanciers.AsNoTracking().Select(c => c.IdCompte).ToListAsync(cancellationToken);

    public async Task<CompteFinancier> AddAsync(CompteFinancier entity, CancellationToken cancellationToken = default)
    {
        _context.ComptesFinanciers.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task AddRangeWithExplicitIdsAsync(
        IReadOnlyList<CompteFinancier> entities,
        CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
            return;

        await _context.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [pct].[COMPTE] ON;", cancellationToken);
            foreach (var entity in entities)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO [pct].[COMPTE]
                        ([IdCompte], [NumeroCompte], [LibelleCompte], [FK_Banque], [FK_Direction],
                         [FK_TypeCompte], [FK_Devise], [FK_Province], [FK_Utilisateur],
                         [DateCreation], [DateCloture], [DateModification], [Actif])
                    VALUES
                        ({entity.IdCompte}, {entity.NumeroCompte}, {entity.LibelleCompte}, {entity.FK_Banque},
                         {entity.FK_Direction}, {entity.FK_TypeCompte}, {entity.FK_Devise}, {entity.FK_Province},
                         {entity.FK_Utilisateur}, {entity.DateCreation},
                         {(entity.DateCloture is null
                             ? (DateTime?)null
                             : entity.DateCloture.Value.ToDateTime(TimeOnly.MinValue))},
                         NULL, {entity.Actif});
                    """,
                    cancellationToken);
            }

            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [pct].[COMPTE] OFF;", cancellationToken);
            var maxId = entities.Max(e => e.IdCompte);
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DBCC CHECKIDENT ('pct.COMPTE', RESEED, {maxId});",
                cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [pct].[COMPTE] OFF;", cancellationToken);
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

    private IQueryable<CompteFinancier> QueryWithRefs()
        => _context.ComptesFinanciers
            .AsNoTracking()
            .Include(c => c.Banque)
            .Include(c => c.Direction)
            .Include(c => c.TypeCompte)
            .Include(c => c.Devise)
            .Include(c => c.Province)
            .Include(c => c.Utilisateur);
}
