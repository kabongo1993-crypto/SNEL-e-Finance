using System.Text.Json;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class AjustementBudgetaireRepository : IAjustementBudgetaireRepository
{
    private readonly BudgetDbContext _context;

    public AjustementBudgetaireRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AjustementBudgetaire>> ListAsync(
        AjustementBudgetaireQuery query, CancellationToken cancellationToken = default)
    {
        var q = BaseQuery();
        if (query.IdExercice is long idEx)
            q = q.Where(a => a.FK_ExerciceBudgetaire == idEx);
        if (query.IdVersion is long idV)
            q = q.Where(a => a.FK_VersionBudgetaire == idV);
        if (query.IdUB is long idUb)
            q = q.Where(a => a.FK_UniteBudgetaire == idUb);
        if (!string.IsNullOrWhiteSpace(query.Statut))
        {
            var st = StatutAjustementBudgetaire.Normaliser(query.Statut);
            q = q.Where(a => a.Statut == st);
        }

        return await q.OrderByDescending(a => a.DateCreation).ToListAsync(cancellationToken);
    }

    public Task<AjustementBudgetaire?> GetByIdAsync(long idAjustement, CancellationToken cancellationToken = default)
        => BaseQuery().FirstOrDefaultAsync(a => a.IdAjustement == idAjustement, cancellationToken);

    public Task<AjustementBudgetaire?> GetTrackedAsync(long idAjustement, CancellationToken cancellationToken = default)
        => _context.AjustementsBudgetaires
            .FirstOrDefaultAsync(a => a.IdAjustement == idAjustement, cancellationToken);

    public async Task<IReadOnlyList<AjustementBudgetaire>> ListByPrevisionAsync(
        long idPrevision, CancellationToken cancellationToken = default)
        => await BaseQuery()
            .Where(a => a.FK_PrevisionBudgetaire == idPrevision)
            .OrderBy(a => a.DateCreation)
            .ToListAsync(cancellationToken);

    public Task<PrevisionBudgetaire?> GetPrevisionAsync(long idPrevision, CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires.AsNoTracking()
            .Include(p => p.VersionBudgetaire).ThenInclude(v => v.ExerciceBudgetaire)
            .Include(p => p.UniteBudgetaire).ThenInclude(u => u.Departement)
            .Include(p => p.TypeBudget)
            .Include(p => p.RubriqueBudgetaire)
            .Include(p => p.ItemBI)
            .Include(p => p.ModePrevision)
            .FirstOrDefaultAsync(p => p.IdPrevision == idPrevision, cancellationToken);

    public async Task<string?> GetWorkflowStatutAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default)
        => await _context.WorkflowsPrevisionUb.AsNoTracking()
            .Where(w => w.FK_VersionBudgetaire == idVersion && w.FK_UniteBudgetaire == idUB)
            .Select(w => w.Statut)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsBrouillonPourPrevisionAsync(
        long idPrevision, long? excludeId, CancellationToken cancellationToken = default)
    {
        var q = _context.AjustementsBudgetaires.AsNoTracking()
            .Where(a => a.FK_PrevisionBudgetaire == idPrevision
                        && a.Statut == StatutAjustementBudgetaire.Brouillon);
        if (excludeId is long id)
            q = q.Where(a => a.IdAjustement != id);
        return q.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrevisionBudgetaire>> GetPrevisionsValideesUbAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default)
    {
        var statut = await GetWorkflowStatutAsync(idVersion, idUB, cancellationToken);
        if (!string.Equals(statut, StatutVersionBudgetaire.Validee, StringComparison.OrdinalIgnoreCase))
            return [];

        return await _context.PrevisionsBudgetaires.AsNoTracking()
            .Include(p => p.TypeBudget)
            .Include(p => p.RubriqueBudgetaire)
            .Include(p => p.ItemBI)
            .Include(p => p.ModePrevision)
            .Include(p => p.UniteBudgetaire).ThenInclude(u => u.Departement)
            .Include(p => p.VersionBudgetaire).ThenInclude(v => v.ExerciceBudgetaire)
            .Where(p => p.FK_VersionBudgetaire == idVersion && p.FK_UniteBudgetaire == idUB)
            .OrderBy(p => p.IdPrevision)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrevisionBudgetaire>> GetPrevisionsValideesAsync(
        AjustementBudgetaireQuery query, CancellationToken cancellationToken = default)
    {
        // Même définition métier que le workflow prévisions : WORKFLOW_PREVISION_UB.Statut = VALIDEE.
        var q = _context.PrevisionsBudgetaires.AsNoTracking()
            .Include(p => p.TypeBudget)
            .Include(p => p.RubriqueBudgetaire)
            .Include(p => p.ItemBI)
            .Include(p => p.ModePrevision)
            .Include(p => p.UniteBudgetaire).ThenInclude(u => u.Departement)
            .Include(p => p.VersionBudgetaire).ThenInclude(v => v.ExerciceBudgetaire)
            .Where(p => _context.WorkflowsPrevisionUb.Any(w =>
                w.FK_VersionBudgetaire == p.FK_VersionBudgetaire
                && w.FK_UniteBudgetaire == p.FK_UniteBudgetaire
                && w.Statut == StatutVersionBudgetaire.Validee));

        if (query.IdExercice is long idEx)
            q = q.Where(p => p.VersionBudgetaire.FK_ExerciceBudgetaire == idEx);
        if (query.IdVersion is long idV)
            q = q.Where(p => p.FK_VersionBudgetaire == idV);
        if (query.IdUB is long idUb)
            q = q.Where(p => p.FK_UniteBudgetaire == idUb);

        return await q
            .OrderBy(p => p.VersionBudgetaire.ExerciceBudgetaire.Annee)
            .ThenBy(p => p.UniteBudgetaire.CodeUB)
            .ThenBy(p => p.TypeBudget.CodeType)
            .ThenBy(p => p.IdPrevision)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, (decimal? FirstAncien, int NbValides, bool HasBrouillon)>> GetAjustementStatsByPrevisionAsync(
        IReadOnlyList<long> idPrevisions, CancellationToken cancellationToken = default)
    {
        if (idPrevisions.Count == 0)
            return new Dictionary<long, (decimal?, int, bool)>();

        var rows = await _context.AjustementsBudgetaires.AsNoTracking()
            .Where(a => idPrevisions.Contains(a.FK_PrevisionBudgetaire))
            .Select(a => new
            {
                a.FK_PrevisionBudgetaire,
                a.Statut,
                a.MontantAncien,
                a.DateValidation,
                a.DateCreation,
            })
            .ToListAsync(cancellationToken);

        return idPrevisions.ToDictionary(
            id => id,
            id =>
            {
                var mine = rows.Where(r => r.FK_PrevisionBudgetaire == id).ToList();
                var valides = mine
                    .Where(r => r.Statut == StatutAjustementBudgetaire.Valide)
                    .OrderBy(r => r.DateValidation ?? r.DateCreation)
                    .ToList();
                decimal? first = valides.Count == 0 ? null : valides[0].MontantAncien;
                var hasBrouillon = mine.Any(r => r.Statut == StatutAjustementBudgetaire.Brouillon);
                return (first, valides.Count, hasBrouillon);
            });
    }

    public Task<ExerciceBudgetaire?> GetExerciceCourantAsync(CancellationToken cancellationToken = default)
        => _context.ExercicesBudgetaires.AsNoTracking()
            .Where(e => e.Statut == StatutExerciceBudgetaire.Ouvert)
            .OrderByDescending(e => e.Annee)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => _context.AjustementsBudgetaires.CountAsync(cancellationToken);

    public async Task<AjustementBudgetaire> AddAsync(
        AjustementBudgetaire entity, CancellationToken cancellationToken = default)
    {
        _context.AjustementsBudgetaires.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(entity.IdAjustement, cancellationToken))!;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task ApplyMontantPrevisionAsync(
        long idPrevision,
        decimal montantNouveau,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        var prevision = await _context.PrevisionsBudgetaires
            .Include(p => p.RepartitionsMensuelles)
            .Include(p => p.ModePrevision)
            .FirstOrDefaultAsync(p => p.IdPrevision == idPrevision, cancellationToken)
            ?? throw new InvalidOperationException("Prévision introuvable.");

        var ancien = prevision.MontantAnnuel;
        prevision.MontantAnnuel = montantNouveau;
        prevision.DateModification = DateTime.Now;
        prevision.FK_UtilisateurModification = idUtilisateur;

        if (string.Equals(prevision.ModePrevision.CodeMode, "MENSUEL", StringComparison.OrdinalIgnoreCase)
            && prevision.RepartitionsMensuelles.Count > 0
            && ancien != 0)
        {
            var ratio = montantNouveau / ancien;
            foreach (var r in prevision.RepartitionsMensuelles)
            {
                r.Montant = Math.Round(r.Montant * ratio, 4, MidpointRounding.AwayFromZero);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAuditAsync(
        long idUtilisateur,
        string operation,
        long idAjustement,
        object? anciennes,
        object? nouvelles,
        CancellationToken cancellationToken = default)
    {
        _context.JournalAudits.Add(new JournalAudit
        {
            FK_Utilisateur = idUtilisateur,
            DateHeure = DateTime.Now,
            Operation = operation,
            Entite = "AJUSTEMENT_BUDGETAIRE",
            IdEntite = idAjustement,
            AnciennesValeurs = anciennes is null ? null : JsonSerializer.Serialize(anciennes),
            NouvellesValeurs = nouvelles is null ? null : JsonSerializer.Serialize(nouvelles),
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<AjustementBudgetaire> BaseQuery()
        => _context.AjustementsBudgetaires.AsNoTracking()
            .Include(a => a.VersionBudgetaire).ThenInclude(v => v.ExerciceBudgetaire)
            .Include(a => a.UniteBudgetaire)
            .Include(a => a.PrevisionBudgetaire).ThenInclude(p => p.TypeBudget)
            .Include(a => a.PrevisionBudgetaire).ThenInclude(p => p.RubriqueBudgetaire)
            .Include(a => a.PrevisionBudgetaire).ThenInclude(p => p.ItemBI)
            .Include(a => a.UtilisateurCreation)
            .Include(a => a.UtilisateurValidation);
}
