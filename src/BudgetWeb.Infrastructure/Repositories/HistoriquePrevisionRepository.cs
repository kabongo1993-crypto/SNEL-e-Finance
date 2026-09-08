using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class HistoriquePrevisionRepository : IHistoriquePrevisionRepository
{
    private readonly BudgetDbContext _context;

    public HistoriquePrevisionRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<HistoriquePrevisionPageDto> QueryAsync(
        HistoriquePrevisionQuery query,
        long currentUserId,
        bool peutVoirToutes,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 50 : query.PageSize;
        var monHistorique = query.MonHistorique || !peutVoirToutes;

        var operations = HistoriquePrevisionMapper.OperationsForActionFilter(query.Action);
        var journalQuery = _context.JournalAudits.AsNoTracking()
            .Where(j => j.Entite == "WORKFLOW_PREVISION_UB" && operations.Contains(j.Operation));

        if (query.DateDebut is not null)
        {
            journalQuery = journalQuery.Where(j => j.DateHeure >= query.DateDebut.Value);
        }

        if (query.DateFin is not null)
        {
            // Inclusif fin de journée si date sans heure
            var fin = query.DateFin.Value;
            if (fin.TimeOfDay == TimeSpan.Zero)
            {
                fin = fin.Date.AddDays(1).AddTicks(-1);
            }

            journalQuery = journalQuery.Where(j => j.DateHeure <= fin);
        }

        // Périmètre « mon historique » : acteur = JWT OU Version×UB où l'utilisateur a créé des prévisions.
        HashSet<(long Version, long Ub)>? mesPaires = null;
        if (monHistorique)
        {
            mesPaires = await ChargerPairesUtilisateurAsync(currentUserId, query, cancellationToken);
            journalQuery = journalQuery.Where(j =>
                j.FK_Utilisateur == currentUserId
                || j.NouvellesValeurs != null); // filtrage fin après expand
        }

        // Préfiltre version / exercice via jointure Versions si possible
        if (query.IdVersion is > 0 || query.IdExercice is > 0)
        {
            var versionIds = await _context.VersionsBudgetaires.AsNoTracking()
                .Where(v =>
                    (query.IdVersion == null || query.IdVersion <= 0 || v.IdVersion == query.IdVersion)
                    && (query.IdExercice == null || query.IdExercice <= 0 || v.FK_ExerciceBudgetaire == query.IdExercice))
                .Select(v => v.IdVersion)
                .ToListAsync(cancellationToken);

            if (versionIds.Count == 0)
            {
                return new HistoriquePrevisionPageDto([], page, pageSize, 0, peutVoirToutes);
            }

            // IdEntite = IdWorkflow pour UB, IdVersion pour département — filtre via JSON côté expand.
            // On charge les journaux puis filtre IdVersion.
            _ = versionIds;
        }

        // Ne pas charger tout : plafonner la fenêtre brute (filtres SQL déjà appliqués sur dates/ops).
        // Ensuite expand + filtres métier + pagination.
        const int maxJournalRows = 5000;
        var journals = await journalQuery
            .OrderByDescending(j => j.DateHeure)
            .ThenByDescending(j => j.IdAudit)
            .Take(maxJournalRows)
            .Select(j => new
            {
                j.IdAudit,
                j.DateHeure,
                j.Operation,
                j.FK_Utilisateur,
                j.AnciennesValeurs,
                j.NouvellesValeurs,
            })
            .ToListAsync(cancellationToken);

        var expanded = new List<HistoriqueAuditExpanded>();
        foreach (var j in journals)
        {
            expanded.AddRange(HistoriquePrevisionMapper.ExpandJournalRow(
                j.IdAudit,
                j.DateHeure,
                j.Operation,
                j.FK_Utilisateur,
                j.AnciennesValeurs,
                j.NouvellesValeurs));
        }

        if (query.IdVersion is > 0)
        {
            expanded = expanded.Where(e => e.IdVersion == query.IdVersion).ToList();
        }

        if (query.IdUB is > 0)
        {
            expanded = expanded.Where(e => e.IdUB == query.IdUB).ToList();
        }

        if (query.IdDepartement is > 0)
        {
            expanded = expanded.Where(e => e.IdDepartement == query.IdDepartement).ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.Statut))
        {
            var st = query.Statut.Trim().ToUpperInvariant();
            expanded = expanded.Where(e =>
                string.Equals(e.AncienStatut, st, StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.NouveauStatut, st, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (monHistorique && mesPaires is not null)
        {
            expanded = expanded.Where(e =>
                e.IdUtilisateur == currentUserId
                || mesPaires.Contains((e.IdVersion, e.IdUB))).ToList();
        }

        if (query.IdExercice is > 0)
        {
            var versionIdsEx = await _context.VersionsBudgetaires.AsNoTracking()
                .Where(v => v.FK_ExerciceBudgetaire == query.IdExercice)
                .Select(v => v.IdVersion)
                .ToListAsync(cancellationToken);
            var set = versionIdsEx.ToHashSet();
            expanded = expanded.Where(e => set.Contains(e.IdVersion)).ToList();
        }

        var enriched = await EnrichirAsync(expanded, cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var q = query.Search.Trim().ToLowerInvariant();
            enriched = enriched.Where(e =>
                e.CodeUB.ToLowerInvariant().Contains(q)
                || e.LibelleUB.ToLowerInvariant().Contains(q)
                || e.LibelleDepartement.ToLowerInvariant().Contains(q)
                || e.CodeDepartement.ToLowerInvariant().Contains(q)
                || (e.LibelleVersion?.ToLowerInvariant().Contains(q) ?? false)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim().ToUpperInvariant();
            enriched = enriched.Where(e => type switch
            {
                "DC" => e.MontantDC > 0,
                "AE" => e.MontantAE > 0,
                "BI" => e.MontantBI > 0,
                _ => true,
            }).Select(e => e with { TypePrevision = type }).ToList();
        }

        var total = enriched.Count;
        var items = enriched
            .OrderByDescending(e => e.DateHeure)
            .ThenByDescending(e => e.IdAudit)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new HistoriquePrevisionPageDto(items, page, pageSize, total, peutVoirToutes);
    }

    public async Task<HistoriquePrevisionTimelineDto?> GetTimelineAsync(
        long idVersion,
        long idUB,
        long currentUserId,
        bool peutVoirToutes,
        CancellationToken cancellationToken = default)
    {
        if (!peutVoirToutes)
        {
            var lie = await _context.PrevisionsBudgetaires.AsNoTracking()
                .AnyAsync(p =>
                    p.FK_VersionBudgetaire == idVersion
                    && p.FK_UniteBudgetaire == idUB
                    && p.FK_UtilisateurCreation == currentUserId, cancellationToken);
            var aAgir = await _context.JournalAudits.AsNoTracking()
                .AnyAsync(j =>
                    j.Entite == "WORKFLOW_PREVISION_UB"
                    && j.FK_Utilisateur == currentUserId
                    && j.NouvellesValeurs != null
                    && j.NouvellesValeurs.Contains($"\"idUB\":{idUB}"), cancellationToken);
            if (!lie && !aAgir)
            {
                throw new UnauthorizedAccessException(
                    "Vous n'avez pas accès à l'historique de cette unité budgétaire.");
            }
        }

        // Timeline complète pour la paire — sans filtre « mon historique ».
        var page = await QueryAsync(
            new HistoriquePrevisionQuery(
                null, idVersion, null, idUB, null, null, null, null, null, null,
                MonHistorique: false,
                Page: 1,
                PageSize: 500),
            currentUserId,
            peutVoirToutes: true,
            cancellationToken);

        var evenements = page.Items
            .OrderByDescending(e => e.DateHeure)
            .ThenByDescending(e => e.IdAudit)
            .ToList();

        if (evenements.Count == 0)
        {
            return await ChargerEnteteAsync(idVersion, idUB, cancellationToken);
        }

        var first = evenements[0];
        return new HistoriquePrevisionTimelineDto(
            first.IdVersion,
            first.NumeroVersion,
            first.LibelleVersion,
            first.AnneeExercice,
            first.IdDepartement,
            first.CodeDepartement,
            first.LibelleDepartement,
            first.IdUB,
            first.CodeUB,
            first.LibelleUB,
            first.StatutActuel ?? "—",
            first.MontantDC,
            first.MontantAE,
            first.MontantBI,
            first.MontantTotal,
            evenements);
    }

    private async Task<HashSet<(long Version, long Ub)>> ChargerPairesUtilisateurAsync(
        long userId,
        HistoriquePrevisionQuery query,
        CancellationToken cancellationToken)
    {
        var q = _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_UtilisateurCreation == userId);

        if (query.IdVersion is > 0)
            q = q.Where(p => p.FK_VersionBudgetaire == query.IdVersion);
        if (query.IdUB is > 0)
            q = q.Where(p => p.FK_UniteBudgetaire == query.IdUB);
        if (query.IdDepartement is > 0)
            q = q.Where(p => p.UniteBudgetaire.FK_Departement == query.IdDepartement);
        if (query.IdExercice is > 0)
            q = q.Where(p => p.VersionBudgetaire.FK_ExerciceBudgetaire == query.IdExercice);

        var pairs = await q
            .Select(p => new { p.FK_VersionBudgetaire, p.FK_UniteBudgetaire })
            .Distinct()
            .ToListAsync(cancellationToken);

        return pairs.Select(p => (p.FK_VersionBudgetaire, p.FK_UniteBudgetaire)).ToHashSet();
    }

    private async Task<List<HistoriquePrevisionEvenementDto>> EnrichirAsync(
        List<HistoriqueAuditExpanded> expanded,
        CancellationToken cancellationToken)
    {
        if (expanded.Count == 0) return [];

        var versionIds = expanded.Select(e => e.IdVersion).Distinct().ToList();
        var ubIds = expanded.Select(e => e.IdUB).Distinct().ToList();
        var userIds = expanded.Select(e => e.IdUtilisateur).Distinct().ToList();

        var versions = await _context.VersionsBudgetaires.AsNoTracking()
            .Where(v => versionIds.Contains(v.IdVersion))
            .Select(v => new
            {
                v.IdVersion,
                v.NumeroVersion,
                v.Libelle,
                v.FK_ExerciceBudgetaire,
                Annee = (int)v.ExerciceBudgetaire.Annee,
            })
            .ToDictionaryAsync(v => v.IdVersion, cancellationToken);

        var ubs = await _context.UnitesBudgetaires.AsNoTracking()
            .Where(u => ubIds.Contains(u.IdUB))
            .Select(u => new
            {
                u.IdUB,
                u.CodeUB,
                u.Libelle,
                u.FK_Departement,
                CodeDept = u.Departement.Code,
                LibelleDept = u.Departement.Libelle,
            })
            .ToDictionaryAsync(u => u.IdUB, cancellationToken);

        var usersRaw = await _context.Utilisateurs.AsNoTracking()
            .Where(u => userIds.Contains(u.IdUtilisateur))
            .Select(u => new { u.IdUtilisateur, u.Prenom, u.Nom, u.NomUtilisateur })
            .ToListAsync(cancellationToken);
        var users = usersRaw.ToDictionary(
            u => u.IdUtilisateur,
            u =>
            {
                var display = $"{u.Prenom} {u.Nom}".Trim();
                return string.IsNullOrWhiteSpace(display) ? u.NomUtilisateur : display;
            });

        // Montants actuels (snapshot) — pas d'historique de montants dans JOURNAL_AUDIT.
        var montants = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => versionIds.Contains(p.FK_VersionBudgetaire) && ubIds.Contains(p.FK_UniteBudgetaire))
            .GroupBy(p => new { p.FK_VersionBudgetaire, p.FK_UniteBudgetaire, Code = p.TypeBudget.CodeType })
            .Select(g => new
            {
                g.Key.FK_VersionBudgetaire,
                g.Key.FK_UniteBudgetaire,
                g.Key.Code,
                Montant = g.Sum(x => x.MontantAnnuel),
            })
            .ToListAsync(cancellationToken);

        var montantMap = montants
            .GroupBy(m => (m.FK_VersionBudgetaire, m.FK_UniteBudgetaire))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    decimal dc = 0, ae = 0, bi = 0;
                    foreach (var x in g)
                    {
                        var c = (x.Code ?? "").Trim().ToUpperInvariant();
                        if (c == "DC") dc = x.Montant;
                        else if (c == "AE") ae = x.Montant;
                        else if (c == "BI") bi = x.Montant;
                    }

                    return (dc, ae, bi, dc + ae + bi);
                });

        var statutsActuels = await _context.WorkflowsPrevisionUb.AsNoTracking()
            .Where(w => versionIds.Contains(w.FK_VersionBudgetaire) && ubIds.Contains(w.FK_UniteBudgetaire))
            .Select(w => new { w.FK_VersionBudgetaire, w.FK_UniteBudgetaire, w.Statut })
            .ToListAsync(cancellationToken);

        var statutMap = statutsActuels.ToDictionary(
            w => (w.FK_VersionBudgetaire, w.FK_UniteBudgetaire),
            w => w.Statut);

        var result = new List<HistoriquePrevisionEvenementDto>(expanded.Count);
        foreach (var e in expanded)
        {
            if (!versions.TryGetValue(e.IdVersion, out var v)) continue;
            if (!ubs.TryGetValue(e.IdUB, out var u)) continue;

            users.TryGetValue(e.IdUtilisateur, out var userNom);
            montantMap.TryGetValue((e.IdVersion, e.IdUB), out var m);
            statutMap.TryGetValue((e.IdVersion, e.IdUB), out var statutActuel);

            var idDept = e.IdDepartement ?? u.FK_Departement;
            result.Add(new HistoriquePrevisionEvenementDto(
                e.IdAudit,
                e.DateHeure,
                v.FK_ExerciceBudgetaire,
                v.Annee,
                e.IdVersion,
                v.NumeroVersion,
                v.Libelle,
                idDept,
                u.CodeDept,
                u.LibelleDept,
                e.IdUB,
                u.CodeUB,
                u.Libelle,
                null,
                e.Action,
                HistoriquePrevisionMapper.MapActionLibelle(e.Action, e.Portee),
                e.Portee,
                e.AncienStatut,
                e.NouveauStatut,
                e.IdUtilisateur,
                userNom ?? $"Utilisateur #{e.IdUtilisateur}",
                e.Motif,
                m.dc,
                m.ae,
                m.bi,
                m.Item4,
                statutActuel));
        }

        return result;
    }

    private async Task<HistoriquePrevisionTimelineDto?> ChargerEnteteAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken)
    {
        var ub = await _context.UnitesBudgetaires.AsNoTracking()
            .Where(u => u.IdUB == idUB)
            .Select(u => new
            {
                u.IdUB,
                u.CodeUB,
                u.Libelle,
                u.FK_Departement,
                CodeDept = u.Departement.Code,
                LibelleDept = u.Departement.Libelle,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (ub is null) return null;

        var v = await _context.VersionsBudgetaires.AsNoTracking()
            .Where(x => x.IdVersion == idVersion)
            .Select(x => new
            {
                x.IdVersion,
                x.NumeroVersion,
                x.Libelle,
                Annee = (int)x.ExerciceBudgetaire.Annee,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (v is null) return null;

        var montants = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion && p.FK_UniteBudgetaire == idUB)
            .GroupBy(p => p.TypeBudget.CodeType)
            .Select(g => new { Code = g.Key, Montant = g.Sum(x => x.MontantAnnuel) })
            .ToListAsync(cancellationToken);

        decimal dc = 0, ae = 0, bi = 0;
        foreach (var x in montants)
        {
            var c = (x.Code ?? "").Trim().ToUpperInvariant();
            if (c == "DC") dc = x.Montant;
            else if (c == "AE") ae = x.Montant;
            else if (c == "BI") bi = x.Montant;
        }

        var statut = await _context.WorkflowsPrevisionUb.AsNoTracking()
            .Where(w => w.FK_VersionBudgetaire == idVersion && w.FK_UniteBudgetaire == idUB)
            .Select(w => w.Statut)
            .FirstOrDefaultAsync(cancellationToken) ?? "—";

        return new HistoriquePrevisionTimelineDto(
            v.IdVersion,
            v.NumeroVersion,
            v.Libelle,
            v.Annee,
            ub.FK_Departement,
            ub.CodeDept,
            ub.LibelleDept,
            ub.IdUB,
            ub.CodeUB,
            ub.Libelle,
            statut,
            dc,
            ae,
            bi,
            dc + ae + bi,
            []);
    }
}
