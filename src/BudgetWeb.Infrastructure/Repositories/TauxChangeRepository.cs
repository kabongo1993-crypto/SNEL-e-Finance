using BudgetWeb.Application.DTOs.Referentiels;
using BudgetWeb.Application.Interfaces.Referentiels;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class TauxChangeRepository : ITauxChangeRepository
{
    private readonly BudgetDbContext _context;

    public TauxChangeRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TauxChange>> ListAsync(
        TauxChangeListQuery query,
        CancellationToken cancellationToken = default)
    {
        var q = _context.TauxChanges.AsNoTracking()
            .Include(t => t.UtilisateurCreation)
            .Include(t => t.UtilisateurModification)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.DeviseBase))
        {
            var b = query.DeviseBase.Trim().ToUpperInvariant();
            q = q.Where(t => t.DeviseSource == b);
        }

        if (!string.IsNullOrWhiteSpace(query.DeviseQuote))
        {
            var quote = query.DeviseQuote.Trim().ToUpperInvariant();
            q = q.Where(t => t.DeviseCible == quote);
        }

        if (!string.IsNullOrWhiteSpace(query.Statut))
        {
            var statut = StatutTauxChange.Normaliser(query.Statut);
            q = q.Where(t => t.Statut == statut);
        }

        if (query.DateEffetMin is DateOnly min)
            q = q.Where(t => t.DateEffet >= min);

        if (query.DateEffetMax is DateOnly max)
            q = q.Where(t => t.DateEffet <= max);

        return await q
            .OrderByDescending(t => t.DateEffet)
            .ThenByDescending(t => t.IdTauxChange)
            .ToListAsync(cancellationToken);
    }

    public Task<TauxChange?> GetByIdAsync(long idTauxChange, CancellationToken cancellationToken = default)
        => _context.TauxChanges.AsNoTracking()
            .Include(t => t.UtilisateurCreation)
            .Include(t => t.UtilisateurModification)
            .FirstOrDefaultAsync(t => t.IdTauxChange == idTauxChange, cancellationToken);

    public Task<TauxChange?> GetByIdTrackedAsync(long idTauxChange, CancellationToken cancellationToken = default)
        => _context.TauxChanges
            .FirstOrDefaultAsync(t => t.IdTauxChange == idTauxChange, cancellationToken);

    public Task<TauxChange?> FindApplicableCanoniqueAsync(
        string deviseBase,
        string deviseQuote,
        DateOnly dateReference,
        CancellationToken cancellationToken = default)
    {
        var b = deviseBase.Trim().ToUpperInvariant();
        var q = deviseQuote.Trim().ToUpperInvariant();

        return _context.TauxChanges.AsNoTracking()
            .Where(t => t.DeviseSource == b
                        && t.DeviseCible == q
                        && t.DateEffet <= dateReference)
            .OrderByDescending(t => t.DateEffet)
            .ThenByDescending(t => t.IdTauxChange)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<TauxChange?> FindActifCourantCanoniqueAsync(
        string deviseBase,
        string deviseQuote,
        CancellationToken cancellationToken = default)
    {
        var b = deviseBase.Trim().ToUpperInvariant();
        var q = deviseQuote.Trim().ToUpperInvariant();

        return _context.TauxChanges.AsNoTracking()
            .Where(t => t.DeviseSource == b
                        && t.DeviseCible == q
                        && t.Statut == StatutTauxChange.Actif)
            .OrderByDescending(t => t.DateEffet)
            .ThenByDescending(t => t.IdTauxChange)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsForCanoniqueAndDateEffetAsync(
        string deviseBase,
        string deviseQuote,
        DateOnly dateEffet,
        long? excludeIdTauxChange = null,
        CancellationToken cancellationToken = default)
    {
        var b = deviseBase.Trim().ToUpperInvariant();
        var q = deviseQuote.Trim().ToUpperInvariant();

        return _context.TauxChanges.AsNoTracking()
            .AnyAsync(
                t => t.DeviseSource == b
                     && t.DeviseCible == q
                     && t.DateEffet == dateEffet
                     && (excludeIdTauxChange == null || t.IdTauxChange != excludeIdTauxChange),
                cancellationToken);
    }

    public Task<bool> EstReferenceParProcedureAsync(long idTauxChange, CancellationToken cancellationToken = default)
        => _context.DemandesPaiement.AsNoTracking()
            .AnyAsync(
                d => d.FK_TauxChange == idTauxChange || d.FK_TauxChangePaiement == idTauxChange,
                cancellationToken);

    public async Task<IReadOnlySet<long>> GetIdsReferenceParProcedureAsync(
        IEnumerable<long> idsTauxChange,
        CancellationToken cancellationToken = default)
    {
        var ids = idsTauxChange.Distinct().ToList();
        if (ids.Count == 0)
            return new HashSet<long>();

        var referenced = await _context.DemandesPaiement.AsNoTracking()
            .Where(d => (d.FK_TauxChange != null && ids.Contains(d.FK_TauxChange.Value))
                        || (d.FK_TauxChangePaiement != null && ids.Contains(d.FK_TauxChangePaiement.Value)))
            .Select(d => new { d.FK_TauxChange, d.FK_TauxChangePaiement })
            .ToListAsync(cancellationToken);

        var set = new HashSet<long>();
        foreach (var row in referenced)
        {
            if (row.FK_TauxChange is long fk && ids.Contains(fk))
                set.Add(fk);
            if (row.FK_TauxChangePaiement is long fkP && ids.Contains(fkP))
                set.Add(fkP);
        }

        return set;
    }

    public Task<bool> ExistsOrientationInverseAsync(
        string deviseBase,
        string deviseQuote,
        CancellationToken cancellationToken = default)
    {
        var b = deviseBase.Trim().ToUpperInvariant();
        var q = deviseQuote.Trim().ToUpperInvariant();

        return _context.TauxChanges.AsNoTracking()
            .AnyAsync(
                t => t.DeviseSource == q && t.DeviseCible == b,
                cancellationToken);
    }

    public async Task<TauxChange> CreateVersionReplacingActifAsync(
        TauxChange newVersion,
        long userIdModification,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var baseDevise = newVersion.DeviseSource;
        var quoteDevise = newVersion.DeviseCible;

        var actifsCourants = await _context.TauxChanges
            .Where(t => t.Statut == StatutTauxChange.Actif
                        && ((t.DeviseSource == baseDevise && t.DeviseCible == quoteDevise)
                            || (t.DeviseSource == quoteDevise && t.DeviseCible == baseDevise)))
            .ToListAsync(cancellationToken);

        foreach (var actif in actifsCourants)
        {
            actif.Statut = StatutTauxChange.Inactif;
            actif.FK_UtilisateurModification = userIdModification;
            actif.DateModification = DateTime.Now;
        }

        _context.TauxChanges.Add(newVersion);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return newVersion;
    }

    public async Task<TauxChange> AddAsync(TauxChange entity, CancellationToken cancellationToken = default)
    {
        _context.TauxChanges.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
