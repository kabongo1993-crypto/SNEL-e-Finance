using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.Services;

public sealed class RapportPrevisionBiService : IRapportPrevisionBiService
{
    public const string CatFondsPropres = "FINANCEMENTS SUR FONDS PROPRES";
    public const string CatExterieurs = "FINANCEMENTS EXTERIEURS";
    public const string SansCategorie = "SANS CATÉGORIE";

    private readonly IRapportPrevisionBiRepository _repository;
    private readonly IRapportOrganisationResolver _orgResolver;
    private readonly IRapportPrevisionBiPdfRenderer _pdfRenderer;

    public RapportPrevisionBiService(
        IRapportPrevisionBiRepository repository,
        IRapportOrganisationResolver orgResolver,
        IRapportPrevisionBiPdfRenderer pdfRenderer)
    {
        _repository = repository;
        _orgResolver = orgResolver;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<RapportBiDto> GetAsync(RapportBiQuery query, CancellationToken cancellationToken = default)
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
        var entite = await RequireStructureAsync(
            query.IdEntite!.Value, TypeStructureOrganisationnelle.Entite, "Entité", structures, cancellationToken);

        RapportStructureNode? deptStruct = null;
        if (query.IdDepartementStructure is long idDeptStruct)
        {
            deptStruct = await RequireStructureAsync(
                idDeptStruct, TypeStructureOrganisationnelle.Departement, "Département", structures, cancellationToken);
            if (!_orgResolver.IsUnderNode(idDeptStruct, entite.IdStructure, structures))
            {
                throw new ArgumentException("Le département structurel n'appartient pas à l'entité sélectionnée.");
            }
        }

        RapportStructureNode? division = null;
        if (query.IdDivision is long idDiv)
        {
            division = await RequireStructureAsync(
                idDiv, TypeStructureOrganisationnelle.Division, "Division", structures, cancellationToken);
        }

        if (niveau == RapportBudgetaireNiveau.Ub && query.IdUB is long idUbCheck)
        {
            var allForCheck = await _repository.GetUbsActivesAsync(cancellationToken);
            if (allForCheck.All(u => u.IdUB != idUbCheck))
            {
                throw new InvalidOperationException("Unité budgétaire introuvable.");
            }
        }

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
        var catalogue = await _repository.GetItemsBiAsync(cancellationToken);
        var previsions = await _repository.GetPrevisionsBiAsync(query.IdVersion, ubIds, cancellationToken);
        var mensuelIds = previsions.Where(p => RapportPrevisionDcService.IsMensuel(p.CodeMode)).Select(p => p.IdPrevision).ToList();
        var moisMap = mensuelIds.Count == 0
            ? (IReadOnlyDictionary<long, decimal[]>)new Dictionary<long, decimal[]>()
            : await _repository.GetRepartitionsMensuellesAsync(mensuelIds, cancellationToken);

        var prevByUb = previsions
            .Where(p => HasMontant(p, moisMap))
            .GroupBy(p => p.IdUB)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RapportBiPrevisionRow>)g.ToList());

        ubs = ubs.Where(u => prevByUb.ContainsKey(u.IdUB)).OrderBy(u => u.CodeUB, StringComparer.OrdinalIgnoreCase).ToList();
        if (ubs.Count == 0)
        {
            return Empty(query, niveau, statut, version, entite, deptStruct, division);
        }

        var layout = DetermineLayout(prevByUb.Values.SelectMany(x => x).ToList());
        var catalogueById = catalogue.ToDictionary(c => c.IdItemBI);
        var childrenByParent = catalogue
            .Where(c => c.ParentId is not null)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CodeItem, StringComparer.OrdinalIgnoreCase).ToList());

        var deptGroups = GroupByDepartement(ubs, resolutions, niveau);
        var deptBlocs = new List<RapportBiDepartementBlocDto>();
        var ordre = 0;
        foreach (var (key, code, libelle, ubsDept) in deptGroups)
        {
            var ubBlocs = new List<RapportBiUbBlocDto>();
            foreach (var ub in ubsDept)
            {
                var r = resolutions[ub.IdUB];
                var lignes = ConstruireLignesUb(prevByUb[ub.IdUB], moisMap, catalogueById, childrenByParent);
                var totalUb = lignes.Where(l => l.TypeLigne == "TOTAL_UB").Select(l => l.MontantCumul).FirstOrDefault();
                var layoutUb = DetermineLayout(prevByUb[ub.IdUB]);
                var identite = new RapportBiUbIdentiteDto(
                    r.IdEntite, r.CodeEntite, r.LibelleEntite,
                    r.IdStructureDepartement, r.CodeStructureDepartement, r.LibelleStructureDepartement,
                    ub.CodeDepartementTable, ub.LibelleDepartementTable,
                    r.EstSansDivision ? null : r.IdDivision,
                    r.EstSansDivision ? null : r.CodeDivision,
                    r.EstSansDivision ? "SANS DIVISION" : r.LibelleDivision,
                    ub.IdUB, ub.CodeUB, ub.LibelleUB,
                    "BI", statut, "USD",
                    version.Annee, version.NumeroVersion, version.Libelle);
                ubBlocs.Add(new RapportBiUbBlocDto(identite, layoutUb, lignes, totalUb));
            }

            deptBlocs.Add(new RapportBiDepartementBlocDto(
                key, code, libelle, ordre++, ubBlocs, ubBlocs.Sum(u => u.TotalUb)));
        }

        RapportBiSyntheseEntiteDto? synthese = null;
        if (niveau == RapportBudgetaireNiveau.Entite && deptBlocs.Count > 0)
        {
            synthese = ConstruireSyntheseEntite(
                version.Annee, deptBlocs, prevByUb, moisMap, catalogueById, childrenByParent, resolutions);
        }

        var totalBi = deptBlocs.Sum(d => d.TotalDepartement);
        var titre = $"BUDGET DÉTAILLÉ DES INVESTISSEMENTS — EXERCICE {version.Annee} EN USD";
        var titreSyn = synthese is null
            ? null
            : $"BUDGET DÉTAILLÉ DES INVESTISSEMENTS — SYNTHÈSE DE L'ENTITÉ — EXERCICE {version.Annee} EN USD";

        var enTete = new RapportBiEnTeteDto(
            titre, titreSyn,
            version.Annee, query.IdVersion, version.NumeroVersion, version.Libelle,
            niveau,
            entite.IdStructure, entite.Code, entite.Libelle,
            deptStruct?.IdStructure, deptStruct?.Code, deptStruct?.Libelle,
            division?.IdStructure, division?.Code, division?.Libelle,
            ubs.Count, deptBlocs.Count, totalBi, "USD", statut, DateTime.Now,
            $"RAPPORT-BI/{entite.Code}/{version.Annee}/V{version.NumeroVersion:00}/{niveau}");

        return new RapportBiDto(enTete, layout, deptBlocs, synthese);
    }

    public async Task<byte[]> GetPdfAsync(RapportBiQuery query, CancellationToken cancellationToken = default)
        => _pdfRenderer.Render(await GetAsync(query, cancellationToken));

    // ─── Public helpers (tests) ───────────────────────────────────────────

    public static string NormaliserCategorie(string? categorie)
    {
        var c = (categorie ?? string.Empty).Trim();
        if (string.Equals(c, CatFondsPropres, StringComparison.OrdinalIgnoreCase)) return CatFondsPropres;
        if (string.Equals(c, CatExterieurs, StringComparison.OrdinalIgnoreCase)) return CatExterieurs;
        return SansCategorie;
    }

    public static (string? CodeAffichage, string Libelle) LibelleCategorie(string categorieNorm)
    {
        if (categorieNorm == CatFondsPropres) return ("I.", CatFondsPropres);
        if (categorieNorm == CatExterieurs) return ("II.", CatExterieurs);
        return (null, SansCategorie);
    }

    public static string ResolveCategorieRacine(
        long idItem,
        IReadOnlyDictionary<long, RapportBiItemCatalogueRow> catalogue)
    {
        var current = idItem;
        var seen = new HashSet<long>();
        while (catalogue.TryGetValue(current, out var item) && seen.Add(current))
        {
            if (item.ParentId is null || !catalogue.ContainsKey(item.ParentId.Value))
            {
                return NormaliserCategorie(item.Categorie);
            }

            current = item.ParentId.Value;
        }

        return SansCategorie;
    }

    public static (decimal Cumul, decimal?[] Mois, bool HasMensuel) NormaliserPrevision(
        RapportBiPrevisionRow p,
        IReadOnlyDictionary<long, decimal[]> moisMap)
    {
        var mois = new decimal?[12];
        if (RapportPrevisionDcService.IsMensuel(p.CodeMode))
        {
            if (moisMap.TryGetValue(p.IdPrevision, out var arr))
            {
                for (var i = 0; i < 12; i++) mois[i] = arr[i];
            }
            else
            {
                for (var i = 0; i < 12; i++) mois[i] = 0m;
            }

            return (p.MontantAnnuel, mois, true);
        }

        return (p.MontantAnnuel, mois, false);
    }

    public static (decimal Cumul, decimal?[] Mois) Agreger(
        IEnumerable<(decimal Cumul, decimal?[] Mois, bool HasMensuel)> parts)
    {
        decimal cumul = 0;
        var moisSum = new decimal[12];
        var hasMensuel = false;
        foreach (var p in parts)
        {
            cumul += p.Cumul;
            if (!p.HasMensuel) continue;
            hasMensuel = true;
            for (var i = 0; i < 12; i++)
            {
                moisSum[i] += p.Mois[i] ?? 0m;
            }
        }

        var mois = new decimal?[12];
        if (hasMensuel)
        {
            for (var i = 0; i < 12; i++) mois[i] = moisSum[i];
        }

        return (cumul, mois);
    }

    public static string DetermineLayout(IReadOnlyList<RapportBiPrevisionRow> rows)
    {
        var hasA = rows.Any(r => !RapportPrevisionDcService.IsMensuel(r.CodeMode));
        var hasM = rows.Any(r => RapportPrevisionDcService.IsMensuel(r.CodeMode));
        if (hasA && hasM) return RapportDcLayoutColonnes.Mixte;
        return hasM ? RapportDcLayoutColonnes.Mensuel : RapportDcLayoutColonnes.Annuel;
    }

    public static bool HasMontant(RapportBiPrevisionRow p, IReadOnlyDictionary<long, decimal[]> moisMap)
    {
        if (RapportPrevisionDcService.IsMensuel(p.CodeMode))
        {
            if (p.MontantAnnuel != 0) return true;
            return moisMap.TryGetValue(p.IdPrevision, out var mois) && mois.Any(m => m != 0);
        }

        return p.MontantAnnuel != 0;
    }

    public static IReadOnlyList<RapportBiLigneDto> ConstruireLignesUb(
        IReadOnlyList<RapportBiPrevisionRow> previsionsUb,
        IReadOnlyDictionary<long, decimal[]> moisMap,
        IReadOnlyDictionary<long, RapportBiItemCatalogueRow> catalogue,
        IReadOnlyDictionary<long, List<RapportBiItemCatalogueRow>> childrenByParent)
    {
        var prevByItem = previsionsUb.GroupBy(p => p.IdItemBI)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.DetailBI, StringComparer.OrdinalIgnoreCase).ToList());

        var needed = new HashSet<long>();
        foreach (var id in prevByItem.Keys)
        {
            var cur = id;
            while (catalogue.ContainsKey(cur) && needed.Add(cur))
            {
                if (catalogue[cur].ParentId is long pid && catalogue.ContainsKey(pid))
                {
                    cur = pid;
                }
                else break;
            }
        }

        var rootsByCat = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        foreach (var id in needed)
        {
            var item = catalogue[id];
            var isRootInNeeded = item.ParentId is null
                || !catalogue.ContainsKey(item.ParentId.Value)
                || !needed.Contains(item.ParentId.Value);
            if (!isRootInNeeded) continue;

            var cat = ResolveCategorieRacine(id, catalogue);
            if (!rootsByCat.TryGetValue(cat, out var list))
            {
                list = [];
                rootsByCat[cat] = list;
            }

            list.Add(id);
        }

        foreach (var list in rootsByCat.Values)
        {
            list.Sort((a, b) => string.Compare(catalogue[a].CodeItem, catalogue[b].CodeItem, StringComparison.OrdinalIgnoreCase));
        }

        var lignes = new List<RapportBiLigneDto>();
        var nodeAgg = new Dictionary<long, (decimal Cumul, decimal?[] Mois)>();

        (decimal Cumul, decimal?[] Mois) ComputeItem(long idItem)
        {
            if (nodeAgg.TryGetValue(idItem, out var cached)) return cached;

            var parts = new List<(decimal, decimal?[], bool)>();
            if (prevByItem.TryGetValue(idItem, out var directs))
            {
                foreach (var p in directs)
                {
                    parts.Add(NormaliserPrevision(p, moisMap));
                }
            }

            if (childrenByParent.TryGetValue(idItem, out var children))
            {
                foreach (var child in children.Where(c => needed.Contains(c.IdItemBI)))
                {
                    var childAgg = ComputeItem(child.IdItemBI);
                    var hasM = childAgg.Mois.Any(m => m is not null);
                    parts.Add((childAgg.Cumul, childAgg.Mois, hasM));
                }
            }

            var agg = Agreger(parts);
            nodeAgg[idItem] = agg;
            return agg;
        }

        foreach (var id in needed) ComputeItem(id);

        var catOrder = new[] { CatFondsPropres, CatExterieurs, SansCategorie };
        var totalParts = new List<(decimal, decimal?[], bool)>();

        foreach (var cat in catOrder)
        {
            if (!rootsByCat.TryGetValue(cat, out var roots) || roots.Count == 0) continue;

            var catParts = roots.Select(id =>
            {
                var a = nodeAgg[id];
                return (a.Cumul, a.Mois, a.Mois.Any(m => m is not null));
            }).ToList();
            var catAgg = Agreger(catParts);
            totalParts.AddRange(catParts);

            var (codeAff, libCat) = cat == SansCategorie
                ? ("", SansCategorie)
                : cat == CatFondsPropres
                    ? ("I.", CatFondsPropres)
                    : ("II.", CatExterieurs);

            lignes.Add(Ligne("CATEGORIE", 0, null, null, libCat, string.IsNullOrEmpty(codeAff) ? null : codeAff,
                null, null, true, catAgg.Cumul, catAgg.Mois, null));

            var itemIndex = 0;
            foreach (var rootId in roots)
            {
                EmitItem(rootId, 1, ref itemIndex, lignes, prevByItem, needed, childrenByParent, catalogue, nodeAgg, moisMap);
            }
        }

        var totalAgg = Agreger(totalParts);
        lignes.Add(Ligne("TOTAL_UB", 0, null, null, "TOTAL UB", null, null, null, true, totalAgg.Cumul, totalAgg.Mois, null));
        return lignes;
    }

    private static void EmitItem(
        long idItem,
        int niveauHier,
        ref int itemIndex,
        List<RapportBiLigneDto> lignes,
        Dictionary<long, List<RapportBiPrevisionRow>> prevByItem,
        HashSet<long> needed,
        IReadOnlyDictionary<long, List<RapportBiItemCatalogueRow>> childrenByParent,
        IReadOnlyDictionary<long, RapportBiItemCatalogueRow> catalogue,
        Dictionary<long, (decimal Cumul, decimal?[] Mois)> nodeAgg,
        IReadOnlyDictionary<long, decimal[]> moisMap)
    {
        var item = catalogue[idItem];
        var agg = nodeAgg[idItem];
        itemIndex++;
        var numItem = itemIndex;
        // CodeAffichage item = CodeItem (identité métier) ; numérotation 1/1.1 dérivée en plus pour les détails.
        lignes.Add(Ligne("ITEM", niveauHier, idItem, item.CodeItem, item.Libelle, item.CodeItem,
            null, null, true, agg.Cumul, agg.Mois, null));

        var detailIndex = 0;
        if (prevByItem.TryGetValue(idItem, out var directs))
        {
            foreach (var p in directs)
            {
                detailIndex++;
                var n = NormaliserPrevision(p, moisMap);
                var mode = (p.CodeMode ?? "").Trim().ToUpperInvariant();
                lignes.Add(Ligne("DETAIL", niveauHier + 1, idItem, item.CodeItem, p.DetailBI,
                    $"{numItem}.{detailIndex}", p.DetailBI, mode, false, n.Cumul, n.Mois, p.IdPrevision));
            }
        }

        if (childrenByParent.TryGetValue(idItem, out var children))
        {
            foreach (var child in children.Where(c => needed.Contains(c.IdItemBI)))
            {
                EmitItem(child.IdItemBI, niveauHier + 1, ref itemIndex, lignes, prevByItem, needed,
                    childrenByParent, catalogue, nodeAgg, moisMap);
            }
        }
    }

    public static RapportBiSyntheseEntiteDto ConstruireSyntheseEntite(
        short annee,
        IReadOnlyList<RapportBiDepartementBlocDto> deptBlocs,
        IReadOnlyDictionary<long, IReadOnlyList<RapportBiPrevisionRow>> prevByUb,
        IReadOnlyDictionary<long, decimal[]> moisMap,
        IReadOnlyDictionary<long, RapportBiItemCatalogueRow> catalogue,
        IReadOnlyDictionary<long, List<RapportBiItemCatalogueRow>> childrenByParent,
        IReadOnlyDictionary<long, RapportUbOrgResolution>? _resolutions = null)
    {
        var colonnes = deptBlocs.Select(d => new RapportBiSyntheseColonneDto(
            d.IdDepartementStructure, d.CodeDepartement, d.LibelleDepartement)).ToList();
        var n = colonnes.Count;

        // Descendants map
        var descendants = BuildDescendantsMap(catalogue, childrenByParent);

        // All previsions flat with dept index
        var deptIndexByUb = new Dictionary<long, int>();
        for (var di = 0; di < deptBlocs.Count; di++)
        {
            foreach (var ub in deptBlocs[di].Ubs)
            {
                deptIndexByUb[ub.Identite.IdUB] = di;
            }
        }

        var allPrev = prevByUb.SelectMany(kv => kv.Value).ToList();

        decimal CumulPourItem(long idItem, int deptIdx)
        {
            decimal sum = 0;
            var set = descendants[idItem];
            foreach (var p in allPrev)
            {
                if (!deptIndexByUb.TryGetValue(p.IdUB, out var di) || di != deptIdx) continue;
                if (p.IdItemBI != idItem && !set.Contains(p.IdItemBI)) continue;
                sum += NormaliserPrevision(p, moisMap).Cumul;
            }

            return sum;
        }

        // Items with any amount
        var itemsWithAmount = new HashSet<long>();
        foreach (var item in catalogue.Values)
        {
            var any = false;
            for (var di = 0; di < n; di++)
            {
                if (CumulPourItem(item.IdItemBI, di) != 0) { any = true; break; }
            }

            if (!any) continue;
            var cur = item.IdItemBI;
            while (catalogue.ContainsKey(cur) && itemsWithAmount.Add(cur))
            {
                if (catalogue[cur].ParentId is long pid && catalogue.ContainsKey(pid)) cur = pid;
                else break;
            }
        }

        var rootsByCat = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        foreach (var id in itemsWithAmount)
        {
            var item = catalogue[id];
            var isRoot = item.ParentId is null || !catalogue.ContainsKey(item.ParentId.Value)
                || !itemsWithAmount.Contains(item.ParentId.Value);
            if (!isRoot) continue;
            var cat = ResolveCategorieRacine(id, catalogue);
            if (!rootsByCat.TryGetValue(cat, out var list))
            {
                list = [];
                rootsByCat[cat] = list;
            }

            list.Add(id);
        }

        foreach (var list in rootsByCat.Values)
        {
            list.Sort((a, b) => string.Compare(catalogue[a].CodeItem, catalogue[b].CodeItem, StringComparison.OrdinalIgnoreCase));
        }

        var lignes = new List<RapportBiSyntheseLigneDto>();
        var totauxDept = new decimal[n];

        void EmitSynItem(long idItem)
        {
            var montants = new decimal[n];
            for (var di = 0; di < n; di++) montants[di] = CumulPourItem(idItem, di);
            var item = catalogue[idItem];
            lignes.Add(new RapportBiSyntheseLigneDto(
                "ITEM", item.CodeItem, item.Libelle, idItem, montants, montants.Sum()));

            if (childrenByParent.TryGetValue(idItem, out var children))
            {
                foreach (var c in children.Where(x => itemsWithAmount.Contains(x.IdItemBI)))
                {
                    EmitSynItem(c.IdItemBI);
                }
            }
        }

        foreach (var cat in new[] { CatFondsPropres, CatExterieurs, SansCategorie })
        {
            if (!rootsByCat.TryGetValue(cat, out var roots) || roots.Count == 0) continue;

            var montants = new decimal[n];
            foreach (var rootId in roots)
            {
                for (var di = 0; di < n; di++) montants[di] += CumulPourItem(rootId, di);
            }

            var (codeAff, lib) = cat == SansCategorie
                ? ("", SansCategorie)
                : cat == CatFondsPropres ? ("I.", CatFondsPropres) : ("II.", CatExterieurs);

            lignes.Add(new RapportBiSyntheseLigneDto(
                "CATEGORIE", string.IsNullOrEmpty(codeAff) ? null : codeAff, lib, null, montants, montants.Sum()));

            foreach (var rootId in roots) EmitSynItem(rootId);
        }

        for (var di = 0; di < n; di++)
        {
            totauxDept[di] = deptBlocs[di].TotalDepartement;
        }

        lignes.Add(new RapportBiSyntheseLigneDto(
            "TOTAL", null, "TOTAL", null, totauxDept, totauxDept.Sum()));

        return new RapportBiSyntheseEntiteDto(
            $"BUDGET DÉTAILLÉ DES INVESTISSEMENTS — SYNTHÈSE DE L'ENTITÉ — EXERCICE {annee} EN USD",
            colonnes,
            lignes,
            totauxDept,
            totauxDept.Sum());
    }

    private static Dictionary<long, HashSet<long>> BuildDescendantsMap(
        IReadOnlyDictionary<long, RapportBiItemCatalogueRow> catalogue,
        IReadOnlyDictionary<long, List<RapportBiItemCatalogueRow>> childrenByParent)
    {
        var map = new Dictionary<long, HashSet<long>>();
        HashSet<long> Recurse(long id)
        {
            if (map.TryGetValue(id, out var cached)) return cached;
            var set = new HashSet<long>();
            if (childrenByParent.TryGetValue(id, out var children))
            {
                foreach (var c in children)
                {
                    set.Add(c.IdItemBI);
                    foreach (var d in Recurse(c.IdItemBI)) set.Add(d);
                }
            }

            map[id] = set;
            return set;
        }

        foreach (var id in catalogue.Keys) Recurse(id);
        return map;
    }

    private static RapportBiLigneDto Ligne(
        string type, int niveau, long? idItem, string? codeItem, string libelle, string? codeAff,
        string? detail, string? mode, bool agrege, decimal cumul, decimal?[] mois, long? idPrev)
        => new(type, niveau, idItem, codeItem, libelle, codeAff, detail, mode, agrege, cumul,
            mois[0], mois[1], mois[2], mois[3], mois[4], mois[5],
            mois[6], mois[7], mois[8], mois[9], mois[10], mois[11], idPrev);

    // ─── Org helpers ─────────────────────────────────────────────────────

    public static List<(long? Id, string Code, string Libelle, List<RapportUbOrgRow> Ubs)> GroupByDepartement(
        IReadOnlyList<RapportUbOrgRow> ubs,
        IReadOnlyDictionary<long, RapportUbOrgResolution> resolutions,
        string niveau)
    {
        if (niveau == RapportBudgetaireNiveau.Ub)
        {
            var ub = ubs[0];
            var r = resolutions[ub.IdUB];
            return
            [
                (r.IdStructureDepartement,
                    r.CodeStructureDepartement ?? "—",
                    r.LibelleStructureDepartement ?? "—",
                    ubs.ToList())
            ];
        }

        var groups = ubs
            .GroupBy(u =>
            {
                var r = resolutions[u.IdUB];
                return r.IdStructureDepartement;
            })
            .Select(g =>
            {
                var first = resolutions[g.First().IdUB];
                var id = g.Key;
                var code = id is null ? "SANS DÉPARTEMENT" : (first.CodeStructureDepartement ?? "—");
                var lib = id is null ? "SANS DÉPARTEMENT" : (first.LibelleStructureDepartement ?? "—");
                var list = g.OrderBy(u => u.CodeUB, StringComparer.OrdinalIgnoreCase).ToList();
                return (Id: id, Code: code, Libelle: lib, Ubs: list, Sans: id is null);
            })
            .OrderBy(x => x.Sans)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => (x.Id, x.Code, x.Libelle, x.Ubs))
            .ToList();

        return groups;
    }

    private static List<RapportUbOrgRow> ResolvePerimetre(
        string niveau,
        RapportBiQuery query,
        IReadOnlyList<RapportUbOrgRow> allUbs,
        IReadOnlyDictionary<long, RapportUbOrgResolution> resolutions,
        IReadOnlyDictionary<long, RapportStructureNode> structures)
    {
        IEnumerable<RapportUbOrgRow> q = allUbs.Where(u =>
            resolutions.TryGetValue(u.IdUB, out var r) && r.IdEntite == query.IdEntite);

        if (niveau == RapportBudgetaireNiveau.Departement || query.IdDepartementStructure is not null)
        {
            var idDept = query.IdDepartementStructure!.Value;
            q = q.Where(u =>
            {
                // Need org resolver — passed via structures walk. Use IdStructure.
                return IsUnder(u.IdStructure, idDept, structures);
            });
        }

        if (query.IdDivision is long idDiv)
        {
            q = q.Where(u => IsUnder(u.IdStructure, idDiv, structures));
        }

        if (niveau == RapportBudgetaireNiveau.Ub || query.IdUB is long)
        {
            var idUb = query.IdUB!.Value;
            q = q.Where(u => u.IdUB == idUb);
        }

        return q.OrderBy(u => u.CodeUB, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsUnder(long idStructure, long idNode, IReadOnlyDictionary<long, RapportStructureNode> structures)
    {
        var cur = idStructure;
        var seen = new HashSet<long>();
        while (seen.Add(cur))
        {
            if (cur == idNode) return true;
            if (!structures.TryGetValue(cur, out var n) || n.ParentId is null) return false;
            cur = n.ParentId.Value;
        }

        return false;
    }

    public static void ValidateScope(string niveau, RapportBiQuery query)
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

    private static RapportBiDto Empty(
        RapportBiQuery query,
        string niveau,
        string statut,
        (short Annee, int NumeroVersion, string? Libelle) version,
        RapportStructureNode entite,
        RapportStructureNode? dept,
        RapportStructureNode? division)
    {
        return new RapportBiDto(
            new RapportBiEnTeteDto(
                $"BUDGET DÉTAILLÉ DES INVESTISSEMENTS — EXERCICE {version.Annee} EN USD",
                null,
                version.Annee, query.IdVersion, version.NumeroVersion, version.Libelle,
                niveau,
                entite.IdStructure, entite.Code, entite.Libelle,
                dept?.IdStructure, dept?.Code, dept?.Libelle,
                division?.IdStructure, division?.Code, division?.Libelle,
                0, 0, 0, "USD", statut, DateTime.Now,
                $"RAPPORT-BI/{entite.Code}/{version.Annee}/V{version.NumeroVersion:00}/{niveau}"),
            RapportDcLayoutColonnes.Annuel,
            [],
            null);
    }
}
