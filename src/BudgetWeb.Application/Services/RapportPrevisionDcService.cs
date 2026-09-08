using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.Services;

public sealed class RapportPrevisionDcService : IRapportPrevisionDcService
{
    private readonly IRapportPrevisionDcRepository _repository;
    private readonly IRapportOrganisationResolver _orgResolver;
    private readonly IRapportPrevisionDcPdfRenderer _pdfRenderer;

    public RapportPrevisionDcService(
        IRapportPrevisionDcRepository repository,
        IRapportOrganisationResolver orgResolver,
        IRapportPrevisionDcPdfRenderer pdfRenderer)
    {
        _repository = repository;
        _orgResolver = orgResolver;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<RapportDcDto> GetAsync(RapportDcQuery query, CancellationToken cancellationToken = default)
    {
        var niveau = RapportBudgetaireNiveau.Normaliser(query.Niveau);
        if (!RapportBudgetaireNiveau.IsValid(niveau))
        {
            throw new ArgumentException("Niveau d'impression invalide (ENTITE | DEPARTEMENT | UB).");
        }

        var statut = StatutVersionBudgetaire.Normaliser(
            string.IsNullOrWhiteSpace(query.StatutConsultation)
                ? StatutVersionBudgetaire.Validee
                : query.StatutConsultation);
        if (!StatutVersionBudgetaire.IsValid(statut))
        {
            throw new ArgumentException("Statut de consultation invalide.");
        }

        ValidateScope(niveau, query);

        var version = await _repository.GetVersionInfoAsync(query.IdVersion, cancellationToken)
            ?? throw new InvalidOperationException("Version budgétaire introuvable.");

        var structures = await _repository.GetStructuresAsync([], cancellationToken);
        var entite = await RequireStructureAsync(query.IdEntite!.Value, TypeStructureOrganisationnelle.Entite, "Entité", structures, cancellationToken);

        RapportStructureNode? deptStruct = null;
        if (query.IdDepartementStructure is long idDeptStruct)
        {
            deptStruct = await RequireStructureAsync(idDeptStruct, TypeStructureOrganisationnelle.Departement, "Département", structures, cancellationToken);
            if (!_orgResolver.IsUnderNode(idDeptStruct, entite.IdStructure, structures))
            {
                throw new ArgumentException("Le département structurel n'appartient pas à l'entité sélectionnée.");
            }
        }

        RapportStructureNode? division = null;
        if (query.IdDivision is long idDiv)
        {
            division = await RequireStructureAsync(idDiv, TypeStructureOrganisationnelle.Division, "Division", structures, cancellationToken);
        }

        // 1) Périmètre UB (avant prévisions)
        var allUbs = await _repository.GetUbsActivesAsync(cancellationToken);
        var resolutions = _orgResolver.ResolveAll(allUbs, structures);

        var ubs = ResolvePerimetre(niveau, query, allUbs, resolutions, structures).ToList();
        if (ubs.Count == 0)
        {
            return Empty(query, niveau, statut, version, entite, deptStruct, division);
        }

        var ubIds = ubs.Select(u => u.IdUB).ToList();
        var valides = await _repository.GetUbIdsParStatutAsync(query.IdVersion, ubIds, statut, cancellationToken);
        ubs = ubs.Where(u => valides.Contains(u.IdUB)).ToList();
        if (ubs.Count == 0)
        {
            return Empty(query, niveau, statut, version, entite, deptStruct, division);
        }

        ubIds = ubs.Select(u => u.IdUB).ToList();
        var previsions = await _repository.GetPrevisionsDcAsync(query.IdVersion, ubIds, cancellationToken);
        var mensuelIds = previsions.Where(p => IsMensuel(p.CodeMode)).Select(p => p.IdPrevision).ToList();
        var moisMap = mensuelIds.Count == 0
            ? (IReadOnlyDictionary<long, decimal[]>)new Dictionary<long, decimal[]>()
            : await _repository.GetRepartitionsMensuellesAsync(mensuelIds, cancellationToken);

        var prevFiltrees = previsions.Where(p => HasMontant(p, moisMap)).ToList();
        var prevByUb = prevFiltrees.GroupBy(p => p.IdUB).ToDictionary(g => g.Key, g => g.ToList());
        ubs = ubs.Where(u => prevByUb.ContainsKey(u.IdUB)).OrderBy(u => u.CodeUB).ToList();

        var layout = DetermineLayout(prevFiltrees);
        var blocs = BuildUbBlocs(ubs, resolutions, prevByUb, moisMap, version, statut);
        var total = blocs.Sum(b => b.TotalDc);
        var groupesConsolides = RapportPrevisionDcConsolideService.Consolider(prevFiltrees, moisMap);

        var refCode = entite.Code;
        var enTete = new RapportDcEnTeteDto(
            $"PRÉVISIONS DES DÉPENSES COURANTES — EXERCICE {version.Annee} EN USD",
            version.Annee,
            query.IdVersion,
            version.NumeroVersion,
            version.Libelle,
            niveau,
            entite.IdStructure,
            entite.Code,
            entite.Libelle,
            deptStruct?.IdStructure,
            deptStruct?.Code,
            deptStruct?.Libelle,
            division?.IdStructure,
            division?.Code,
            division?.Libelle,
            blocs.Count,
            total,
            "USD",
            statut,
            DateTime.Now,
            $"RAPPORT-DC/{refCode}/{version.Annee}/V{version.NumeroVersion:00}/{niveau}");

        return new RapportDcDto(enTete, layout, blocs, groupesConsolides);
    }

    public async Task<byte[]> GetPdfAsync(RapportDcQuery query, CancellationToken cancellationToken = default)
        => _pdfRenderer.Render(await GetAsync(query, cancellationToken));

    private static void ValidateScope(string niveau, RapportDcQuery query)
    {
        if (query.IdEntite is null)
            throw new ArgumentException("IdEntite est obligatoire.");

        if (niveau == RapportBudgetaireNiveau.Departement && query.IdDepartementStructure is null)
            throw new ArgumentException("IdDepartementStructure est obligatoire pour le niveau DÉPARTEMENT.");

        if (niveau == RapportBudgetaireNiveau.Ub)
        {
            if (query.IdDepartementStructure is null)
                throw new ArgumentException("IdDepartementStructure est obligatoire pour le niveau UB.");
            if (query.IdUB is null)
                throw new ArgumentException("IdUB est obligatoire pour le niveau UB.");
        }
    }

    private async Task<RapportStructureNode> RequireStructureAsync(
        long id,
        string expectedType,
        string label,
        IReadOnlyDictionary<long, RapportStructureNode> cache,
        CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue(id, out var node))
        {
            node = await _repository.GetStructureAsync(id, cancellationToken)
                ?? throw new InvalidOperationException($"{label} introuvable.");
        }

        if (!string.Equals(node.TypeStructure, expectedType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{label} invalide (type attendu : {expectedType}).");
        }

        return node;
    }

    private List<RapportUbOrgRow> ResolvePerimetre(
        string niveau,
        RapportDcQuery query,
        IReadOnlyList<RapportUbOrgRow> allUbs,
        IReadOnlyDictionary<long, RapportUbOrgResolution> resolutions,
        IReadOnlyDictionary<long, RapportStructureNode> structures)
    {
        IEnumerable<RapportUbOrgRow> q = allUbs.Where(u =>
            resolutions.TryGetValue(u.IdUB, out var r) && r.IdEntite == query.IdEntite);

        if (niveau == RapportBudgetaireNiveau.Departement
            || query.IdDepartementStructure is not null)
        {
            var idDept = query.IdDepartementStructure!.Value;
            q = q.Where(u => _orgResolver.IsUnderNode(u.IdStructure, idDept, structures));
        }

        if (query.IdDivision is long idDiv)
        {
            q = q.Where(u => _orgResolver.IsUnderNode(u.IdStructure, idDiv, structures));
        }

        if (niveau == RapportBudgetaireNiveau.Ub || query.IdUB is long)
        {
            var idUb = query.IdUB!.Value;
            q = q.Where(u => u.IdUB == idUb);
        }

        return q.OrderBy(u => u.CodeUB).ToList();
    }

    private static List<RapportDcUbBlocDto> BuildUbBlocs(
        IReadOnlyList<RapportUbOrgRow> ubs,
        IReadOnlyDictionary<long, RapportUbOrgResolution> resolutions,
        IReadOnlyDictionary<long, List<RapportDcPrevisionRow>> prevByUb,
        IReadOnlyDictionary<long, decimal[]> moisMap,
        (short Annee, int NumeroVersion, string? Libelle) version,
        string statut)
    {
        var blocs = new List<RapportDcUbBlocDto>();
        foreach (var ub in ubs)
        {
            if (!prevByUb.TryGetValue(ub.IdUB, out var rows) || rows.Count == 0) continue;
            var r = resolutions[ub.IdUB];
            var groupes = BuildGroupes(rows, moisMap);
            var identite = new RapportDcUbIdentiteDto(
                r.IdEntite, r.CodeEntite, r.LibelleEntite,
                r.IdStructureDepartement, r.CodeStructureDepartement, r.LibelleStructureDepartement,
                ub.CodeDepartementTable, ub.LibelleDepartementTable,
                r.EstSansDivision ? null : r.IdDivision,
                r.EstSansDivision ? null : r.CodeDivision,
                r.EstSansDivision ? "SANS DIVISION" : r.LibelleDivision,
                ub.IdUB, ub.CodeUB, ub.LibelleUB,
                "DC", statut, "USD",
                version.Annee, version.NumeroVersion, version.Libelle);
            blocs.Add(new RapportDcUbBlocDto(identite, groupes.Sum(g => g.SousTotalDc), groupes));
        }

        return blocs;
    }

    public static bool IsMensuel(string? codeMode)
    {
        var c = (codeMode ?? "").Trim().ToUpperInvariant();
        return c is "MENSUEL" or "MENS";
    }

    public static bool HasMontant(RapportDcPrevisionRow p, IReadOnlyDictionary<long, decimal[]> moisMap)
    {
        if (IsMensuel(p.CodeMode))
        {
            if (p.MontantAnnuel != 0) return true;
            return moisMap.TryGetValue(p.IdPrevision, out var mois) && mois.Any(m => m != 0);
        }

        return p.MontantAnnuel != 0;
    }

    public static string DetermineLayout(IReadOnlyList<RapportDcPrevisionRow> rows)
    {
        if (rows.Count == 0) return RapportDcLayoutColonnes.Annuel;
        var hasA = rows.Any(r => !IsMensuel(r.CodeMode));
        var hasM = rows.Any(r => IsMensuel(r.CodeMode));
        if (hasA && hasM) return RapportDcLayoutColonnes.Mixte;
        return hasM ? RapportDcLayoutColonnes.Mensuel : RapportDcLayoutColonnes.Annuel;
    }

    public static IReadOnlyList<RapportDcGroupeDto> BuildGroupes(
        IReadOnlyList<RapportDcPrevisionRow> rows,
        IReadOnlyDictionary<long, decimal[]> moisMap)
    {
        return rows
            .GroupBy(r => r.IdGroupeRB)
            .Select(g =>
            {
                var first = g.First();
                var sans = g.Key is null;
                var lignes = g.OrderBy(x => x.CodeRB).Select(p => ToLigne(p, moisMap)).ToList();
                return new RapportDcGroupeDto(
                    g.Key,
                    sans ? "—" : (first.CodeGroupe ?? "—"),
                    sans ? "SANS GROUPE N1" : (first.LibelleGroupe ?? "SANS GROUPE N1"),
                    sans ? int.MaxValue : (first.OrdreAffichageGroupe ?? 9999),
                    lignes.Sum(l => l.MontantAnnuel),
                    lignes);
            })
            .OrderBy(g => g.OrdreAffichage)
            .ThenBy(g => g.CodeGroupe, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Libelle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>ANNUEL : mois toujours null (tiret à l'affichage). MENSUEL : mois enregistrés.</summary>
    public static RapportDcLigneRbDto ToLigne(RapportDcPrevisionRow p, IReadOnlyDictionary<long, decimal[]> moisMap)
    {
        decimal?[] m = new decimal?[12];
        if (IsMensuel(p.CodeMode) && moisMap.TryGetValue(p.IdPrevision, out var arr))
        {
            for (var i = 0; i < 12; i++) m[i] = arr[i];
        }

        return new RapportDcLigneRbDto(
            p.CodeRB, p.LibelleRB, (p.CodeMode ?? "").Trim().ToUpperInvariant(), p.MontantAnnuel,
            m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11]);
    }

    private static RapportDcDto Empty(
        RapportDcQuery query,
        string niveau,
        string statut,
        (short Annee, int NumeroVersion, string? Libelle) version,
        RapportStructureNode entite,
        RapportStructureNode? dept,
        RapportStructureNode? division)
    {
        return new RapportDcDto(
            new RapportDcEnTeteDto(
                $"PRÉVISIONS DES DÉPENSES COURANTES — EXERCICE {version.Annee} EN USD",
                version.Annee, query.IdVersion, version.NumeroVersion, version.Libelle,
                niveau,
                entite.IdStructure, entite.Code, entite.Libelle,
                dept?.IdStructure, dept?.Code, dept?.Libelle,
                division?.IdStructure, division?.Code, division?.Libelle,
                0, 0, "USD", statut, DateTime.Now,
                $"RAPPORT-DC/{entite.Code}/{version.Annee}/V{version.NumeroVersion:00}/{niveau}"),
            RapportDcLayoutColonnes.Annuel,
            [],
            []);
    }
}
