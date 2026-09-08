using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class SuiviPrevisionRepository : ISuiviPrevisionRepository
{
    private readonly BudgetDbContext _context;

    public SuiviPrevisionRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<SuiviPrevisionListeDto> GetResumeParUbAsync(
        long? idUtilisateurCreation,
        long? idExercice,
        long? idVersion,
        long? idDepartement,
        string? statut,
        string? searchUb,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PrevisionsBudgetaires.AsNoTracking().AsQueryable();

        if (idUtilisateurCreation is > 0)
        {
            query = query.Where(p => p.FK_UtilisateurCreation == idUtilisateurCreation);
        }

        if (idExercice is > 0)
        {
            query = query.Where(p => p.VersionBudgetaire.FK_ExerciceBudgetaire == idExercice);
        }

        if (idVersion is > 0)
        {
            query = query.Where(p => p.FK_VersionBudgetaire == idVersion);
        }

        if (idDepartement is > 0)
        {
            query = query.Where(p => p.UniteBudgetaire.FK_Departement == idDepartement);
        }

        if (!string.IsNullOrWhiteSpace(statut))
        {
            var st = statut.Trim().ToUpperInvariant();
            query = query.Where(p =>
                _context.WorkflowsPrevisionUb.Any(w =>
                    w.FK_VersionBudgetaire == p.FK_VersionBudgetaire
                    && w.FK_UniteBudgetaire == p.FK_UniteBudgetaire
                    && w.Statut == st)
                || (!_context.WorkflowsPrevisionUb.Any(w =>
                        w.FK_VersionBudgetaire == p.FK_VersionBudgetaire
                        && w.FK_UniteBudgetaire == p.FK_UniteBudgetaire)
                    && p.VersionBudgetaire.Statut == st));
        }

        if (!string.IsNullOrWhiteSpace(searchUb))
        {
            var q = searchUb.Trim().ToLower();
            query = query.Where(p =>
                p.UniteBudgetaire.CodeUB.ToLower().Contains(q)
                || p.UniteBudgetaire.Libelle.ToLower().Contains(q));
        }

        // Agrégats Version×UB×Type — pivot en mémoire (évite Sum conditionnel non portable).
        var aggregates = await query
            .GroupBy(p => new
            {
                p.FK_VersionBudgetaire,
                p.FK_UniteBudgetaire,
                CodeType = p.TypeBudget.CodeType,
            })
            .Select(g => new
            {
                g.Key.FK_VersionBudgetaire,
                g.Key.FK_UniteBudgetaire,
                g.Key.CodeType,
                Montant = g.Sum(x => x.MontantAnnuel),
                DateMax = g.Max(x => x.DateModification ?? x.DateCreation),
            })
            .ToListAsync(cancellationToken);

        if (aggregates.Count == 0)
        {
            return new SuiviPrevisionListeDto(new SuiviPrevisionCompteursDto(0, 0, 0, 0, 0), []);
        }

        var versionIds = aggregates.Select(a => a.FK_VersionBudgetaire).Distinct().ToList();
        var ubIds = aggregates.Select(a => a.FK_UniteBudgetaire).Distinct().ToList();

        var versions = await _context.VersionsBudgetaires.AsNoTracking()
            .Where(v => versionIds.Contains(v.IdVersion))
            .Select(v => new
            {
                v.IdVersion,
                v.NumeroVersion,
                v.Libelle,
                v.Statut,
                v.FK_ExerciceBudgetaire,
                Annee = v.ExerciceBudgetaire.Annee,
                v.DateSoumission,
                NomSoumission = v.UtilisateurSoumission != null
                    ? (v.UtilisateurSoumission.Nom + " " + v.UtilisateurSoumission.Prenom).Trim()
                    : null,
                v.DateRejet,
                NomRejet = v.UtilisateurRejet != null
                    ? (v.UtilisateurRejet.Nom + " " + v.UtilisateurRejet.Prenom).Trim()
                    : null,
                v.MotifRejet,
            })
            .ToListAsync(cancellationToken);

        var ubs = await _context.UnitesBudgetaires.AsNoTracking()
            .Where(u => ubIds.Contains(u.IdUB))
            .Select(u => new
            {
                u.IdUB,
                u.CodeUB,
                u.Libelle,
                IdDepartement = u.FK_Departement,
                CodeDepartement = u.Departement.Code,
                LibelleDepartement = u.Departement.Libelle,
            })
            .ToListAsync(cancellationToken);

        var versionMap = versions.ToDictionary(v => v.IdVersion);
        var ubMap = ubs.ToDictionary(u => u.IdUB);

        var workflows = await _context.WorkflowsPrevisionUb.AsNoTracking()
            .Where(w => versionIds.Contains(w.FK_VersionBudgetaire) && ubIds.Contains(w.FK_UniteBudgetaire))
            .Select(w => new
            {
                w.FK_VersionBudgetaire,
                w.FK_UniteBudgetaire,
                w.Statut,
                w.DateSoumission,
                NomSoumission = w.UtilisateurSoumission != null
                    ? (w.UtilisateurSoumission.Nom + " " + w.UtilisateurSoumission.Prenom).Trim()
                    : null,
                w.DateRejet,
                NomRejet = w.UtilisateurRejet != null
                    ? (w.UtilisateurRejet.Nom + " " + w.UtilisateurRejet.Prenom).Trim()
                    : null,
                w.MotifRejet,
            })
            .ToListAsync(cancellationToken);
        var workflowMap = workflows.ToDictionary(w => (w.FK_VersionBudgetaire, w.FK_UniteBudgetaire));

        var lignes = aggregates
            .GroupBy(a => new { a.FK_VersionBudgetaire, a.FK_UniteBudgetaire })
            .Select(g =>
            {
                if (!versionMap.TryGetValue(g.Key.FK_VersionBudgetaire, out var v)
                    || !ubMap.TryGetValue(g.Key.FK_UniteBudgetaire, out var u))
                {
                    return null;
                }

                decimal dc = 0, ae = 0, bi = 0;
                var dateMax = DateTime.MinValue;
                foreach (var row in g)
                {
                    dateMax = row.DateMax > dateMax ? row.DateMax : dateMax;
                    if (row.CodeType == TypeBudgetCode.DepensesCourantes) dc = row.Montant;
                    else if (row.CodeType == TypeBudgetCode.ActionsExploitation) ae = row.Montant;
                    else if (row.CodeType == TypeBudgetCode.BudgetInvestissement) bi = row.Montant;
                }

                workflowMap.TryGetValue((g.Key.FK_VersionBudgetaire, g.Key.FK_UniteBudgetaire), out var wf);
                var statut = wf?.Statut ?? v.Statut;
                var dateSoumission = wf?.DateSoumission ?? v.DateSoumission;
                var nomSoumission = EmptyToNull(wf?.NomSoumission) ?? EmptyToNull(v.NomSoumission);
                var dateRejet = wf?.DateRejet ?? v.DateRejet;
                var nomRejet = EmptyToNull(wf?.NomRejet) ?? EmptyToNull(v.NomRejet);
                var motifRejet = wf?.MotifRejet ?? v.MotifRejet;

                return new SuiviPrevisionUbResumeDto(
                    v.IdVersion,
                    v.NumeroVersion,
                    v.Libelle,
                    statut,
                    v.FK_ExerciceBudgetaire,
                    v.Annee,
                    u.IdUB,
                    u.CodeUB,
                    u.Libelle,
                    u.IdDepartement,
                    u.CodeDepartement,
                    u.LibelleDepartement,
                    dc,
                    ae,
                    bi,
                    dc + ae + bi,
                    dateMax,
                    dateSoumission,
                    nomSoumission,
                    dateRejet,
                    nomRejet,
                    motifRejet);
            })
            .Where(x => x is not null)
            .Cast<SuiviPrevisionUbResumeDto>()
            .OrderBy(x => x.LibelleDepartement)
            .ThenBy(x => x.CodeUB)
            .ThenBy(x => x.NumeroVersion)
            .ToList();

        var compteurs = new SuiviPrevisionCompteursDto(
            lignes.Count(l => Norm(l.Statut) == StatutVersionBudgetaire.Brouillon),
            lignes.Count(l => Norm(l.Statut) == StatutVersionBudgetaire.Soumise),
            lignes.Count(l => Norm(l.Statut) == StatutVersionBudgetaire.Controlee),
            lignes.Count(l => Norm(l.Statut) == StatutVersionBudgetaire.Validee),
            lignes.Count(l => Norm(l.Statut) == StatutVersionBudgetaire.Rejetee));

        return new SuiviPrevisionListeDto(compteurs, lignes);
    }

    public async Task<SuiviUbDetailDto?> GetUbDetailAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        var version = await _context.VersionsBudgetaires.AsNoTracking()
            .Where(v => v.IdVersion == idVersion)
            .Select(v => new
            {
                v.IdVersion,
                v.NumeroVersion,
                v.Libelle,
                v.Statut,
                v.FK_ExerciceBudgetaire,
                Annee = v.ExerciceBudgetaire.Annee,
                v.DateSoumission,
                NomSoumission = v.UtilisateurSoumission != null
                    ? (v.UtilisateurSoumission.Nom + " " + v.UtilisateurSoumission.Prenom).Trim()
                    : null,
                v.DateControle,
                NomControle = v.UtilisateurControle != null
                    ? (v.UtilisateurControle.Nom + " " + v.UtilisateurControle.Prenom).Trim()
                    : null,
                v.DateValidation,
                NomValidation = v.UtilisateurValidation != null
                    ? (v.UtilisateurValidation.Nom + " " + v.UtilisateurValidation.Prenom).Trim()
                    : null,
                v.DateRejet,
                NomRejet = v.UtilisateurRejet != null
                    ? (v.UtilisateurRejet.Nom + " " + v.UtilisateurRejet.Prenom).Trim()
                    : null,
                v.MotifRejet,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null) return null;

        var ub = await _context.UnitesBudgetaires.AsNoTracking()
            .Where(u => u.IdUB == idUB)
            .Select(u => new
            {
                u.IdUB,
                u.CodeUB,
                u.Libelle,
                IdDepartement = u.FK_Departement,
                CodeDepartement = u.Departement.Code,
                LibelleDepartement = u.Departement.Libelle,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ub is null) return null;

        var hasLines = await _context.PrevisionsBudgetaires.AsNoTracking()
            .AnyAsync(p => p.FK_VersionBudgetaire == idVersion && p.FK_UniteBudgetaire == idUB, cancellationToken);
        if (!hasLines) return null;

        var wf = await _context.WorkflowsPrevisionUb.AsNoTracking()
            .Where(w => w.FK_VersionBudgetaire == idVersion && w.FK_UniteBudgetaire == idUB)
            .Select(w => new
            {
                w.Statut,
                w.DateSoumission,
                NomSoumission = w.UtilisateurSoumission != null
                    ? (w.UtilisateurSoumission.Nom + " " + w.UtilisateurSoumission.Prenom).Trim()
                    : null,
                w.DateControle,
                NomControle = w.UtilisateurControle != null
                    ? (w.UtilisateurControle.Nom + " " + w.UtilisateurControle.Prenom).Trim()
                    : null,
                w.DateValidation,
                NomValidation = w.UtilisateurValidation != null
                    ? (w.UtilisateurValidation.Nom + " " + w.UtilisateurValidation.Prenom).Trim()
                    : null,
                w.DateRejet,
                NomRejet = w.UtilisateurRejet != null
                    ? (w.UtilisateurRejet.Nom + " " + w.UtilisateurRejet.Prenom).Trim()
                    : null,
                w.MotifRejet,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var byType = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion && p.FK_UniteBudgetaire == idUB)
            .GroupBy(p => p.TypeBudget.CodeType)
            .Select(g => new { CodeType = g.Key, Montant = g.Sum(x => x.MontantAnnuel) })
            .ToListAsync(cancellationToken);

        decimal dc = byType.FirstOrDefault(x => x.CodeType == TypeBudgetCode.DepensesCourantes)?.Montant ?? 0;
        decimal ae = byType.FirstOrDefault(x => x.CodeType == TypeBudgetCode.ActionsExploitation)?.Montant ?? 0;
        decimal bi = byType.FirstOrDefault(x => x.CodeType == TypeBudgetCode.BudgetInvestissement)?.Montant ?? 0;

        return new SuiviUbDetailDto(
            version.IdVersion,
            version.NumeroVersion,
            version.Libelle,
            wf?.Statut ?? version.Statut,
            version.FK_ExerciceBudgetaire,
            version.Annee,
            ub.IdUB,
            ub.CodeUB,
            ub.Libelle,
            ub.IdDepartement,
            ub.CodeDepartement,
            ub.LibelleDepartement,
            dc,
            ae,
            bi,
            dc + ae + bi,
            wf?.DateSoumission ?? version.DateSoumission,
            EmptyToNull(wf?.NomSoumission) ?? EmptyToNull(version.NomSoumission),
            wf?.DateControle ?? version.DateControle,
            EmptyToNull(wf?.NomControle) ?? EmptyToNull(version.NomControle),
            wf?.DateValidation ?? version.DateValidation,
            EmptyToNull(wf?.NomValidation) ?? EmptyToNull(version.NomValidation),
            wf?.DateRejet ?? version.DateRejet,
            EmptyToNull(wf?.NomRejet) ?? EmptyToNull(version.NomRejet),
            wf?.MotifRejet ?? version.MotifRejet,
            PeutControler: false,
            PeutValider: false,
            PeutRejeter: false);
    }

    public async Task<SuiviUbDetailLignesDto> GetUbLignesAsync(
        long idVersion,
        long idUB,
        string codeType,
        CancellationToken cancellationToken = default)
    {
        var code = (codeType ?? string.Empty).Trim().ToUpperInvariant();

        return code switch
        {
            TypeBudgetCode.DepensesCourantes => new SuiviUbDetailLignesDto(
                TypeBudgetCode.DepensesCourantes,
                await BuildLignesDcAsync(idVersion, idUB, cancellationToken),
                null,
                null),
            TypeBudgetCode.ActionsExploitation => new SuiviUbDetailLignesDto(
                TypeBudgetCode.ActionsExploitation,
                null,
                await BuildLignesAeAsync(idVersion, idUB, cancellationToken),
                null),
            TypeBudgetCode.BudgetInvestissement => new SuiviUbDetailLignesDto(
                TypeBudgetCode.BudgetInvestissement,
                null,
                null,
                await BuildLignesBiAsync(idVersion, idUB, cancellationToken)),
            _ => throw new ArgumentException("codeType doit être DC, AE ou BI."),
        };
    }

    private async Task<IReadOnlyList<SuiviUbLigneDcDto>> BuildLignesDcAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken)
    {
        var previsions = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p =>
                p.FK_VersionBudgetaire == idVersion
                && p.FK_UniteBudgetaire == idUB
                && p.TypeBudget.CodeType == TypeBudgetCode.DepensesCourantes
                && p.FK_RubriqueBudgetaire != null)
            .Select(p => new
            {
                IdRB = p.FK_RubriqueBudgetaire!.Value,
                CodeRB = p.RubriqueBudgetaire!.CodeRB,
                Libelle = p.RubriqueBudgetaire.Libelle,
                IdGroupeRB = p.RubriqueBudgetaire.FK_GroupeRubriqueBudgetaire,
                CodeGroupe = p.RubriqueBudgetaire.GroupeRubriqueBudgetaire != null
                    ? p.RubriqueBudgetaire.GroupeRubriqueBudgetaire.CodeGroupe
                    : null,
                LibelleGroupe = p.RubriqueBudgetaire.GroupeRubriqueBudgetaire != null
                    ? p.RubriqueBudgetaire.GroupeRubriqueBudgetaire.Libelle
                    : null,
                Ordre = p.RubriqueBudgetaire.GroupeRubriqueBudgetaire != null
                    ? p.RubriqueBudgetaire.GroupeRubriqueBudgetaire.OrdreAffichage
                    : (int?)null,
                p.MontantAnnuel,
                Reps = p.RepartitionsMensuelles
                    .OrderBy(r => r.Mois)
                    .Select(r => r.Montant)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        // Une RB peut avoir plusieurs modes — agrège montant, conserve répartition si définie.
        var byRb = previsions
            .GroupBy(p => p.IdRB)
            .Select(g =>
            {
                var first = g.First();
                var montant = g.Sum(x => x.MontantAnnuel);
                var withRep = g.FirstOrDefault(x => x.Reps.Count > 0 && x.Reps.Any(m => m != 0));
                var reps = withRep?.Reps;
                var defined = reps is { Count: > 0 } && reps.Any(m => m != 0);
                return new
                {
                    first.IdRB,
                    first.CodeRB,
                    first.Libelle,
                    first.IdGroupeRB,
                    first.CodeGroupe,
                    first.LibelleGroupe,
                    first.Ordre,
                    MontantAnnuel = montant,
                    RepartitionDefinie = defined,
                    MontantsMensuels = defined ? Normalize12(reps!) : null,
                };
            })
            .ToList();

        var result = new List<SuiviUbLigneDcDto>();
        foreach (var grp in byRb
            .GroupBy(x => new { x.IdGroupeRB, x.CodeGroupe, x.LibelleGroupe, x.Ordre })
            .OrderBy(g => g.Key.Ordre ?? int.MaxValue)
            .ThenBy(g => g.Key.CodeGroupe))
        {
            var totalGroupe = grp.Sum(x => x.MontantAnnuel);
            result.Add(new SuiviUbLigneDcDto(
                true,
                grp.Key.IdGroupeRB,
                grp.Key.CodeGroupe,
                grp.Key.LibelleGroupe,
                grp.Key.Ordre,
                null,
                null,
                grp.Key.LibelleGroupe ?? grp.Key.CodeGroupe ?? "Groupe",
                totalGroupe,
                false,
                null));

            foreach (var leaf in grp.OrderBy(x => x.CodeRB))
            {
                result.Add(new SuiviUbLigneDcDto(
                    false,
                    leaf.IdGroupeRB,
                    leaf.CodeGroupe,
                    leaf.LibelleGroupe,
                    leaf.Ordre,
                    leaf.IdRB,
                    leaf.CodeRB,
                    leaf.Libelle,
                    leaf.MontantAnnuel,
                    leaf.RepartitionDefinie,
                    leaf.MontantsMensuels));
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<SuiviUbLigneAeDto>> BuildLignesAeAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken)
    {
        var rows = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p =>
                p.FK_VersionBudgetaire == idVersion
                && p.FK_UniteBudgetaire == idUB
                && p.TypeBudget.CodeType == TypeBudgetCode.ActionsExploitation)
            .Select(p => new
            {
                LibelleItemAE = p.LibelleItemAE ?? string.Empty,
                LibelleGroupeAE = p.GroupeItemAE != null ? p.GroupeItemAE.Libelle : null,
                IdRB = p.FK_RubriqueBudgetaire,
                CodeRB = p.RubriqueBudgetaire != null ? p.RubriqueBudgetaire.CodeRB : null,
                LibelleRB = p.RubriqueBudgetaire != null ? p.RubriqueBudgetaire.Libelle : null,
                p.MontantAnnuel,
                CodeMode = p.ModePrevision.CodeMode,
                Reps = p.RepartitionsMensuelles.OrderBy(r => r.Mois).Select(r => r.Montant).ToList(),
            })
            .OrderBy(x => x.LibelleItemAE)
            .ThenBy(x => x.CodeRB)
            .ToListAsync(cancellationToken);

        return rows.Select(r =>
        {
            var defined = r.Reps.Count > 0 && r.Reps.Any(m => m != 0);
            return new SuiviUbLigneAeDto(
                r.LibelleItemAE,
                r.LibelleGroupeAE,
                r.IdRB,
                r.CodeRB,
                r.LibelleRB,
                r.MontantAnnuel,
                r.CodeMode,
                defined,
                defined ? Normalize12(r.Reps) : null);
        }).ToList();
    }

    private async Task<IReadOnlyList<SuiviUbLigneBiDto>> BuildLignesBiAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken)
    {
        var rows = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p =>
                p.FK_VersionBudgetaire == idVersion
                && p.FK_UniteBudgetaire == idUB
                && p.TypeBudget.CodeType == TypeBudgetCode.BudgetInvestissement
                && p.FK_ItemBI != null)
            .Select(p => new
            {
                IdItemBI = p.FK_ItemBI!.Value,
                CodeItem = p.ItemBI!.CodeItem,
                LibelleItem = p.ItemBI.Libelle,
                p.DetailBI,
                p.MontantAnnuel,
                CodeMode = p.ModePrevision.CodeMode,
                Reps = p.RepartitionsMensuelles.OrderBy(r => r.Mois).Select(r => r.Montant).ToList(),
            })
            .OrderBy(x => x.CodeItem)
            .ThenBy(x => x.DetailBI)
            .ToListAsync(cancellationToken);

        return rows.Select(r =>
        {
            var defined = r.Reps.Count > 0 && r.Reps.Any(m => m != 0);
            return new SuiviUbLigneBiDto(
                r.IdItemBI,
                r.CodeItem,
                r.LibelleItem,
                r.DetailBI,
                r.MontantAnnuel,
                r.CodeMode,
                defined,
                defined ? Normalize12(r.Reps) : null);
        }).ToList();
    }

    private static IReadOnlyList<decimal> Normalize12(IReadOnlyList<decimal> reps)
    {
        var arr = new decimal[12];
        for (var i = 0; i < Math.Min(12, reps.Count); i++)
        {
            arr[i] = reps[i];
        }

        return arr;
    }

    private static string Norm(string statut) => (statut ?? string.Empty).Trim().ToUpperInvariant();

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
