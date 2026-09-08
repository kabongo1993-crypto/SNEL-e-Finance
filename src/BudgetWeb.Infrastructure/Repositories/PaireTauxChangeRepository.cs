using BudgetWeb.Application.Interfaces.Referentiels;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class PaireTauxChangeRepository : IPaireTauxChangeRepository
{
    private readonly BudgetDbContext _context;

    public PaireTauxChangeRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PaireTauxChange>> ListActivesAsync(CancellationToken cancellationToken = default)
        => await _context.PairesTauxChange.AsNoTracking()
            .Where(p => p.Actif)
            .OrderBy(p => p.DeviseBase)
            .ThenBy(p => p.DeviseQuote)
            .ToListAsync(cancellationToken);

    public Task<PaireTauxChange?> FindCanoniqueAsync(
        string deviseBase,
        string deviseQuote,
        CancellationToken cancellationToken = default)
    {
        var b = Normaliser(deviseBase);
        var q = Normaliser(deviseQuote);
        return _context.PairesTauxChange.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Actif && p.DeviseBase == b && p.DeviseQuote == q, cancellationToken);
    }

    public Task<PaireTauxChange?> ResolveForDevisesAsync(
        string deviseA,
        string deviseB,
        CancellationToken cancellationToken = default)
    {
        var a = Normaliser(deviseA);
        var b = Normaliser(deviseB);
        return _context.PairesTauxChange.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Actif
                     && ((p.DeviseBase == a && p.DeviseQuote == b)
                         || (p.DeviseBase == b && p.DeviseQuote == a)),
                cancellationToken);
    }

    public async Task<bool> DevisesActivesExistentAsync(
        string deviseBase,
        string deviseQuote,
        CancellationToken cancellationToken = default)
    {
        var b = Normaliser(deviseBase);
        var q = Normaliser(deviseQuote);
        var codes = await _context.Devises.AsNoTracking()
            .Where(d => d.Actif && (d.Code == b || d.Code == q))
            .Select(d => d.Code)
            .ToListAsync(cancellationToken);
        return codes.Contains(b) && codes.Contains(q);
    }

    public async Task<PaireTauxChange> CreerOuObtenirPaireCanoniqueAsync(
        string deviseBase,
        string deviseQuote,
        CancellationToken cancellationToken = default)
    {
        var b = Normaliser(deviseBase);
        var q = Normaliser(deviseQuote);

        if (b == q)
            throw new ArgumentException("Les deux devises de la paire doivent être différentes.");

        var existing = await _context.PairesTauxChange
            .FirstOrDefaultAsync(
                p => p.Actif
                     && ((p.DeviseBase == b && p.DeviseQuote == q)
                         || (p.DeviseBase == q && p.DeviseQuote == b)),
                cancellationToken);

        // Paire déjà connue (même sens ou inverse) : on conserve l'orientation canonique figée.
        if (existing is not null)
            return existing;

        if (!await DevisesActivesExistentAsync(b, q, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Les devises {b} et/ou {q} sont absentes ou inactives dans le référentiel.");
        }

        var paire = new PaireTauxChange
        {
            DeviseBase = b,
            DeviseQuote = q,
            Actif = true,
            DateCreation = DateTime.UtcNow,
        };
        _context.PairesTauxChange.Add(paire);
        await _context.SaveChangesAsync(cancellationToken);
        return paire;
    }

    public async Task<PaireTauxChange> AddAsync(PaireTauxChange entity, CancellationToken cancellationToken = default)
    {
        _context.PairesTauxChange.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    private static string Normaliser(string code) => code.Trim().ToUpperInvariant();
}
