using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.Services;

public sealed class RapportPrevisionDcConsolideService : IRapportPrevisionDcConsolideService
{
    private readonly IRapportPrevisionDcRepository _repository;
    private readonly IRapportOrganisationResolver _orgResolver;
    private readonly IRapportPrevisionDcConsolidePdfRenderer _pdfRenderer;

    public RapportPrevisionDcConsolideService(
        IRapportPrevisionDcRepository repository,
        IRapportOrganisationResolver orgResolver,
        IRapportPrevisionDcConsolidePdfRenderer pdfRenderer)
    {
        _repository = repository;
        _orgResolver = orgResolver;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<RapportDcConsolideDto> GetAsync(
        RapportDcConsolideQuery query,
        CancellationToken cancellationToken = default)
    {
        var statut = StatutVersionBudgetaire.Normaliser(
            string.IsNullOrWhiteSpace(query.StatutConsultation)
                ? StatutVersionBudgetaire.Validee
                : query.StatutConsultation);
        if (!StatutVersionBudgetaire.IsValid(statut))
        {
            throw new ArgumentException("Statut de consultation invalide.");
        }

        var version = await _repository.GetVersionInfoAsync(query.IdVersion, cancellationToken)
            ?? throw new InvalidOperationException("Version budgétaire introuvable.");

        var structures = await _repository.GetStructuresAsync([], cancellationToken);

        RapportStructureNode? entite = null;
        if (query.IdEntite is long idE)
        {
            entite = RequireType(structures, idE, TypeStructureOrganisationnelle.Entite, "Entité");
        }

        RapportStructureNode? dept = null;
        if (query.IdDepartementStructure is long idD)
        {
            dept = RequireType(structures, idD, TypeStructureOrganisationnelle.Departement, "Département");
            if (entite is not null && !_orgResolver.IsUnderNode(idD, entite.IdStructure, structures))
            {
                throw new ArgumentException("Le département n'appartient pas à l'entité sélectionnée.");
            }
        }

        RapportStructureNode? division = null;
        if (query.IdDivision is long idDiv)
        {
            division = RequireType(structures, idDiv, TypeStructureOrganisationnelle.Division, "Division");
        }

        var allUbs = await _repository.GetUbsActivesAsync(cancellationToken);
        var resolutions = _orgResolver.ResolveAll(allUbs, structures);

        var ubs = FilterPerimetre(allUbs, resolutions, structures, query).ToList();
        if (ubs.Count == 0)
        {
            return Empty(query, statut, version, entite, dept, division);
        }

        var ubIds = ubs.Select(u => u.IdUB).ToList();
        var valides = await _repository.GetUbIdsParStatutAsync(query.IdVersion, ubIds, statut, cancellationToken);
        ubs = ubs.Where(u => valides.Contains(u.IdUB)).ToList();
        if (ubs.Count == 0)
        {
            return Empty(query, statut, version, entite, dept, division);
        }

        ubIds = ubs.Select(u => u.IdUB).ToList();
        var previsions = await _repository.GetPrevisionsDcAsync(query.IdVersion, ubIds, cancellationToken);
        var mensuelIds = previsions
            .Where(p => RapportPrevisionDcService.IsMensuel(p.CodeMode))
            .Select(p => p.IdPrevision)
            .ToList();
        var moisMap = mensuelIds.Count == 0
            ? (IReadOnlyDictionary<long, decimal[]>)new Dictionary<long, decimal[]>()
            : await _repository.GetRepartitionsMensuellesAsync(mensuelIds, cancellationToken);

        var rows = previsions
            .Where(p => RapportPrevisionDcService.HasMontant(p, moisMap))
            .ToList();

        var groupes = Consolider(rows, moisMap);
        var total = groupes.Sum(g => g.SousTotalDc);
        var titre =
            $"PRÉVISIONS BUDGÉTAIRES {version.Annee} MENSUALISÉES DES DÉPENSES COURANTES EN USD";

        var scopeCode = entite?.Code ?? dept?.Code ?? "GLOBAL";
        var enTete = new RapportDcConsolideEnTeteDto(
            titre,
            version.Annee,
            query.IdVersion,
            version.NumeroVersion,
            version.Libelle,
            entite?.IdStructure,
            entite?.Code,
            entite?.Libelle,
            dept?.IdStructure,
            dept?.Code,
            dept?.Libelle,
            division?.IdStructure,
            division?.Code,
            division?.Libelle,
            ubs.Count,
            total,
            "USD",
            statut,
            DateTime.Now,
            $"RAPPORT-DC-CONSOLIDE/{scopeCode}/{version.Annee}/V{version.NumeroVersion:00}");

        return new RapportDcConsolideDto(enTete, groupes);
    }

    public async Task<byte[]> GetPdfAsync(RapportDcConsolideQuery query, CancellationToken cancellationToken = default)
        => _pdfRenderer.Render(await GetAsync(query, cancellationToken));

    private static RapportStructureNode RequireType(
        IReadOnlyDictionary<long, RapportStructureNode> structures,
        long id,
        string expectedType,
        string label)
    {
        if (!structures.TryGetValue(id, out var node))
        {
            throw new InvalidOperationException($"{label} introuvable.");
        }

        if (!string.Equals(node.TypeStructure, expectedType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{label} invalide (type attendu : {expectedType}).");
        }

        return node;
    }

    private List<RapportUbOrgRow> FilterPerimetre(
        IReadOnlyList<RapportUbOrgRow> allUbs,
        IReadOnlyDictionary<long, RapportUbOrgResolution> resolutions,
        IReadOnlyDictionary<long, RapportStructureNode> structures,
        RapportDcConsolideQuery query)
    {
        IEnumerable<RapportUbOrgRow> q = allUbs;

        if (query.IdEntite is long idE)
        {
            q = q.Where(u => resolutions.TryGetValue(u.IdUB, out var r) && r.IdEntite == idE);
        }

        if (query.IdDepartementStructure is long idD)
        {
            q = q.Where(u => _orgResolver.IsUnderNode(u.IdStructure, idD, structures));
        }

        if (query.IdDivision is long idDiv)
        {
            q = q.Where(u => _orgResolver.IsUnderNode(u.IdStructure, idDiv, structures));
        }

        return q.OrderBy(u => u.CodeUB).ToList();
    }

    /// <summary>
    /// Consolidation par (IdGroupeRB, IdRB, famille de mode ANNUEL|MENSUEL).
    /// Une même RB ANNUELLE et MENSUELLE produisent deux lignes distinctes (pas de mensualisation artificielle).
    /// </summary>
    public static IReadOnlyList<RapportDcConsolideGroupeDto> Consolider(
        IReadOnlyList<RapportDcPrevisionRow> rows,
        IReadOnlyDictionary<long, decimal[]> moisMap)
    {
        var buckets = rows
            .GroupBy(r => (
                r.IdGroupeRB,
                r.IdRB,
                ModeFamily: RapportPrevisionDcService.IsMensuel(r.CodeMode) ? "MENSUEL" : "ANNUEL"))
            .Select(g =>
            {
                var first = g.First();
                var cumul = g.Sum(x => x.MontantAnnuel);
                decimal?[] mois = new decimal?[12];
                if (g.Key.ModeFamily == "MENSUEL")
                {
                    var sums = new decimal[12];
                    var any = false;
                    foreach (var p in g)
                    {
                        if (!moisMap.TryGetValue(p.IdPrevision, out var arr)) continue;
                        for (var i = 0; i < 12; i++)
                        {
                            if (arr[i] != 0) any = true;
                            sums[i] += arr[i];
                        }
                    }

                    if (any || cumul != 0)
                    {
                        for (var i = 0; i < 12; i++) mois[i] = sums[i];
                    }
                }

                // Exclure bucket totalement nul
                var moisNonNul = mois.Any(m => m is decimal d && d != 0);
                if (cumul == 0 && !moisNonNul) return null;

                return new
                {
                    first.IdGroupeRB,
                    CodeGroupe = first.IdGroupeRB is null ? "—" : (first.CodeGroupe ?? "—"),
                    LibelleGroupe = first.IdGroupeRB is null
                        ? "SANS GROUPE N1"
                        : (first.LibelleGroupe ?? "SANS GROUPE N1"),
                    Ordre = first.IdGroupeRB is null ? int.MaxValue : (first.OrdreAffichageGroupe ?? 9999),
                    Ligne = new RapportDcConsolideLigneDto(
                        first.IdRB,
                        first.CodeRB,
                        first.LibelleRB,
                        g.Key.ModeFamily,
                        cumul,
                        mois[0], mois[1], mois[2], mois[3], mois[4], mois[5],
                        mois[6], mois[7], mois[8], mois[9], mois[10], mois[11]),
                };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        return buckets
            .GroupBy(x => x.IdGroupeRB)
            .Select(g =>
            {
                var first = g.First();
                var lignes = g
                    .Select(x => x.Ligne)
                    .OrderBy(l => l.CodeRB)
                    .ThenBy(l => l.CodeMode)
                    .ToList();
                return new RapportDcConsolideGroupeDto(
                    first.IdGroupeRB,
                    first.CodeGroupe,
                    first.LibelleGroupe,
                    first.Ordre,
                    lignes.Sum(l => l.MontantCumul),
                    lignes);
            })
            .OrderBy(g => g.OrdreAffichage)
            .ThenBy(g => g.CodeGroupe, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Libelle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RapportDcConsolideDto Empty(
        RapportDcConsolideQuery query,
        string statut,
        (short Annee, int NumeroVersion, string? Libelle) version,
        RapportStructureNode? entite,
        RapportStructureNode? dept,
        RapportStructureNode? division)
    {
        var scopeCode = entite?.Code ?? dept?.Code ?? "GLOBAL";
        return new RapportDcConsolideDto(
            new RapportDcConsolideEnTeteDto(
                $"PRÉVISIONS BUDGÉTAIRES {version.Annee} MENSUALISÉES DES DÉPENSES COURANTES EN USD",
                version.Annee, query.IdVersion, version.NumeroVersion, version.Libelle,
                entite?.IdStructure, entite?.Code, entite?.Libelle,
                dept?.IdStructure, dept?.Code, dept?.Libelle,
                division?.IdStructure, division?.Code, division?.Libelle,
                0, 0, "USD", statut, DateTime.Now,
                $"RAPPORT-DC-CONSOLIDE/{scopeCode}/{version.Annee}/V{version.NumeroVersion:00}"),
            []);
    }
}
