using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.Services;

public sealed class RapportPrevisionAeService : IRapportPrevisionAeService
{
    private readonly IRapportPrevisionAeRepository _repository;
    private readonly IRapportOrganisationResolver _orgResolver;
    private readonly IClassementAeService _classementAeService;
    private readonly IRapportPrevisionAePdfRenderer _pdfRenderer;

    public RapportPrevisionAeService(
        IRapportPrevisionAeRepository repository,
        IRapportOrganisationResolver orgResolver,
        IClassementAeService classementAeService,
        IRapportPrevisionAePdfRenderer pdfRenderer)
    {
        _repository = repository;
        _orgResolver = orgResolver;
        _classementAeService = classementAeService;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<RapportAeDto> GetAsync(RapportAeQuery query, CancellationToken cancellationToken = default)
    {
        var moisSelection = RapportAeMoisCodes.Parse(query.Mois);
        var estTous = moisSelection is null;

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

        var allUbs = await _repository.GetUbsActivesAsync(cancellationToken);
        var resolutions = _orgResolver.ResolveAll(allUbs, structures);
        var ubs = ResolvePerimetre(niveau, query, allUbs, resolutions, structures).ToList();
        if (ubs.Count == 0)
        {
            return Empty(query, niveau, statut, version, entite, deptStruct, division, moisSelection, estTous);
        }

        var ubIds = ubs.Select(u => u.IdUB).ToList();
        var valides = await _repository.GetUbIdsParStatutAsync(query.IdVersion, ubIds, statut, cancellationToken);
        ubs = ubs.Where(u => valides.Contains(u.IdUB)).ToList();
        if (ubs.Count == 0)
        {
            return Empty(query, niveau, statut, version, entite, deptStruct, division, moisSelection, estTous);
        }

        ubIds = ubs.Select(u => u.IdUB).ToList();
        var previsions = await _repository.GetPrevisionsAeAsync(query.IdVersion, ubIds, cancellationToken);
        if (previsions.Count == 0)
        {
            return Empty(query, niveau, statut, version, entite, deptStruct, division, moisSelection, estTous);
        }

        var mensuelIds = previsions
            .Where(p => RapportPrevisionDcService.IsMensuel(p.CodeMode))
            .Select(p => p.IdPrevision)
            .ToList();
        var moisMap = mensuelIds.Count == 0
            ? (IReadOnlyDictionary<long, decimal[]>)new Dictionary<long, decimal[]>()
            : await _repository.GetRepartitionsMensuellesAsync(mensuelIds, cancellationToken);

        var prevByUb = previsions.GroupBy(p => p.IdUB).ToDictionary(g => g.Key, g => (IReadOnlyList<RapportAePrevisionRow>)g.ToList());

        foreach (var ub in ubs.Where(u => prevByUb.ContainsKey(u.IdUB)))
        {
            if (!await _repository.ExistsClassementAeAsync(query.IdVersion, ub.IdUB, cancellationToken))
            {
                await _classementAeService.InitialiserManquantsAsync(cancellationToken);
                break;
            }
        }

        var moisCibles = estTous
            ? Enumerable.Range(1, 12).ToList()
            : [moisSelection!.Value];

        var blocs = new List<RapportAeUbBlocDto>();
        foreach (var ub in ubs)
        {
            if (!prevByUb.TryGetValue(ub.IdUB, out var prevsUb) || prevsUb.Count == 0)
            {
                continue;
            }

            var classement = await _repository.GetClassementAeAsync(query.IdVersion, ub.IdUB, cancellationToken);
            if (classement.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Le classement AE est absent pour l'UB {ub.CodeUB} (version {query.IdVersion}). "
                    + "Initialisez le classement via POST /api/v1/classements-ae/initialiser.");
            }

            var details = new List<RapportAeDetailMensuelDto>();
            foreach (var mois in moisCibles)
            {
                var detail = ConstruireDetailMensuel(mois, prevsUb, moisMap, classement);
                if (detail.Lignes.Count > 0 || estTous)
                {
                    // En mode unique, n'ajouter le bloc UB que s'il y a des lignes ; en mode tous, garder la rupture même vide.
                    if (!estTous && detail.Lignes.Count == 0)
                    {
                        continue;
                    }

                    details.Add(detail);
                }
            }

            if (details.Count == 0)
            {
                continue;
            }

            var r = resolutions[ub.IdUB];
            var identite = new RapportAeUbIdentiteDto(
                r.IdEntite, r.CodeEntite, r.LibelleEntite,
                r.IdStructureDepartement, r.CodeStructureDepartement, r.LibelleStructureDepartement,
                ub.CodeDepartementTable, ub.LibelleDepartementTable,
                r.EstSansDivision ? null : r.IdDivision,
                r.EstSansDivision ? null : r.CodeDivision,
                r.EstSansDivision ? "SANS DIVISION" : r.LibelleDivision,
                ub.IdUB, ub.CodeUB, ub.LibelleUB,
                "AE", statut, "USD",
                version.Annee, version.NumeroVersion, version.Libelle);

            blocs.Add(new RapportAeUbBlocDto(identite, details));
        }

        RapportAeSyntheseItemDto? syntheseItem = null;
        RapportAeSyntheseRbDto? syntheseRb = null;
        if (estTous && blocs.Count > 0)
        {
            // Synthèses sur le périmètre (toutes les UB du rapport).
            var classements = new Dictionary<long, IReadOnlyList<ClassementAeLigneDto>>();
            foreach (var b in blocs)
            {
                classements[b.Identite.IdUB] = await _repository.GetClassementAeAsync(
                    query.IdVersion, b.Identite.IdUB, cancellationToken);
            }

            syntheseItem = ConstruireSyntheseItem(version.Annee, blocs, prevByUb, moisMap, classements);
            syntheseRb = ConstruireSyntheseRb(version.Annee, previsions.Where(p => prevByUb.ContainsKey(p.IdUB)).ToList(), moisMap);
        }

        var totalEnTete = estTous
            ? (syntheseRb?.TotalGeneral ?? 0)
            : blocs.Sum(b => b.DetailsMensuels.Sum(d => d.TotalGeneral));

        var modeMois = estTous ? RapportAeMoisCodes.Tous : moisSelection!.Value.ToString();
        var libelleMois = estTous ? "TOUS LES MOIS" : RapportAeMoisCodes.Libelles[moisSelection!.Value - 1];

        var enTete = new RapportAeEnTeteDto(
            $"BUDGET DÉTAILLÉ DES ACTIONS D'EXPLOITATION PAR ITEM — EXERCICE {version.Annee} EN USD",
            version.Annee, query.IdVersion, version.NumeroVersion, version.Libelle,
            niveau, modeMois, libelleMois,
            entite.IdStructure, entite.Code, entite.Libelle,
            deptStruct?.IdStructure, deptStruct?.Code, deptStruct?.Libelle,
            division?.IdStructure, division?.Code, division?.Libelle,
            blocs.Count, totalEnTete, "USD", statut, DateTime.Now,
            $"RAPPORT-AE/{entite.Code}/{version.Annee}/V{version.NumeroVersion:00}/{niveau}/{modeMois}");

        return new RapportAeDto(enTete, estTous, blocs, syntheseItem, syntheseRb);
    }

    public async Task<byte[]> GetPdfAsync(RapportAeQuery query, CancellationToken cancellationToken = default)
        => _pdfRenderer.Render(await GetAsync(query, cancellationToken));

    // ─── Détail mensuel ───────────────────────────────────────────────────

    public static RapportAeDetailMensuelDto ConstruireDetailMensuel(
        int mois,
        IReadOnlyList<RapportAePrevisionRow> previsionsUb,
        IReadOnlyDictionary<long, decimal[]> moisMap,
        IReadOnlyList<ClassementAeLigneDto> classement)
    {
        if (mois is < 1 or > 12)
        {
            throw new ArgumentException("Mois invalide.");
        }

        var colonnes = DeterminerColonnesRb(previsionsUb);
        var lignes = new List<RapportAeLigneDetailDto>();
        ClassementAeLigneDto? pendingGroupe = null;
        var groupeEmis = false;
        var compteur = 0;
        var totauxRb = new decimal[colonnes.Count];

        foreach (var line in classement.OrderBy(c => c.OrdreAffichage))
        {
            if (string.Equals(line.TypeLigne, "GROUPE", StringComparison.OrdinalIgnoreCase))
            {
                pendingGroupe = line;
                groupeEmis = false;
                continue;
            }

            if (!string.Equals(line.TypeLigne, "ITEM", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(line.LibelleItemAE))
            {
                continue;
            }

            var itemPrevs = previsionsUb
                .Where(p => string.Equals(p.LibelleItemAE, line.LibelleItemAE, StringComparison.Ordinal))
                .ToList();
            if (itemPrevs.Count == 0)
            {
                continue;
            }

            var montants = new decimal?[colonnes.Count];
            decimal totalItem = 0;

            for (var i = 0; i < colonnes.Count; i++)
            {
                var idRb = colonnes[i].IdRB;
                var cell = MontantCelluleMois(itemPrevs.Where(p => p.IdRB == idRb), mois, moisMap);
                montants[i] = cell.Valeur;
                if (cell.EstMensuelContribue && cell.Valeur is decimal d)
                {
                    totalItem += d;
                    totauxRb[i] += d;
                }
            }

            if (pendingGroupe is not null && !groupeEmis)
            {
                lignes.Add(new RapportAeLigneDetailDto(
                    "GROUPE", pendingGroupe.OrdreAffichage, pendingGroupe.IdGroupeItemAE,
                    pendingGroupe.LibelleGroupe, null, null, [], null));
                groupeEmis = true;
            }

            compteur++;
            lignes.Add(new RapportAeLigneDetailDto(
                "ITEM", line.OrdreAffichage, null, null,
                compteur.ToString("00"), line.LibelleItemAE, montants, totalItem));
        }

        return new RapportAeDetailMensuelDto(
            mois,
            RapportAeMoisCodes.Libelles[mois - 1],
            colonnes,
            lignes,
            totauxRb,
            totauxRb.Sum());
    }

    /// <summary>
    /// Cellule Item×RB×mois :
    /// - MENSUEL → somme REPARTITION_MENSUELLE[mois] (0 si répartition absente/nulle) ;
    /// - uniquement ANNUEL → null (= tiret, pas de contribution mensuelle) ;
    /// - aucune prévision pour cette RB → 0 (RB présente sur l'UB mais pas sur l'item).
    /// EstMensuelContribue = true si au moins une prévision MENSUELLE pour cette RB.
    /// </summary>
    public static (decimal? Valeur, bool EstMensuelContribue) MontantCelluleMois(
        IEnumerable<RapportAePrevisionRow> previsionsRb,
        int mois,
        IReadOnlyDictionary<long, decimal[]> moisMap)
    {
        var list = previsionsRb as IList<RapportAePrevisionRow> ?? previsionsRb.ToList();
        if (list.Count == 0)
        {
            return (0m, false);
        }

        decimal sum = 0;
        var hasMensuel = false;
        foreach (var p in list)
        {
            if (!RapportPrevisionDcService.IsMensuel(p.CodeMode))
            {
                continue;
            }

            hasMensuel = true;
            if (moisMap.TryGetValue(p.IdPrevision, out var arr) && mois is >= 1 and <= 12)
            {
                sum += arr[mois - 1];
            }
        }

        if (!hasMensuel)
        {
            // Prévisions présentes mais toutes ANNUELLES → tiret.
            return (null, false);
        }

        return (sum, true);
    }

    public static IReadOnlyList<RapportAeColonneRbDto> DeterminerColonnesRb(
        IReadOnlyList<RapportAePrevisionRow> previsionsUb)
    {
        return previsionsUb
            .GroupBy(p => p.IdRB)
            .Select(g =>
            {
                var f = g.First();
                return new RapportAeColonneRbDto(
                    f.IdRB,
                    f.CodeRB,
                    f.LibelleRB,
                    f.IdGroupeRB,
                    f.IdGroupeRB is null ? int.MaxValue : (f.OrdreAffichageGroupe ?? 9999));
            })
            .OrderBy(c => c.OrdreAffichage)
            .ThenBy(c => c.CodeRB, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ─── Synthèse Item ────────────────────────────────────────────────────

    public static RapportAeSyntheseItemDto ConstruireSyntheseItem(
        short annee,
        IReadOnlyList<RapportAeUbBlocDto> blocs,
        IReadOnlyDictionary<long, IReadOnlyList<RapportAePrevisionRow>> prevByUb,
        IReadOnlyDictionary<long, decimal[]> moisMap,
        IReadOnlyDictionary<long, IReadOnlyList<ClassementAeLigneDto>> classements)
    {
        // Une synthèse par UB empilée (comme détail multi-UB) — pour 1 UB c'est transparent.
        // Si plusieurs UB avec même libellé : on produit séquentiellement par UB pour respecter classement.
        var lignes = new List<RapportAeSyntheseItemLigneDto>();
        decimal totalGeneral = 0;

        foreach (var bloc in blocs)
        {
            if (!prevByUb.TryGetValue(bloc.Identite.IdUB, out var prevs)
                || !classements.TryGetValue(bloc.Identite.IdUB, out var classement))
            {
                continue;
            }

            ClassementAeLigneDto? pendingGroupe = null;
            var groupeEmis = false;
            var compteur = 0;

            foreach (var line in classement.OrderBy(c => c.OrdreAffichage))
            {
                if (string.Equals(line.TypeLigne, "GROUPE", StringComparison.OrdinalIgnoreCase))
                {
                    pendingGroupe = line;
                    groupeEmis = false;
                    continue;
                }

                if (!string.Equals(line.TypeLigne, "ITEM", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(line.LibelleItemAE))
                {
                    continue;
                }

                var itemPrevs = prevs
                    .Where(p => string.Equals(p.LibelleItemAE, line.LibelleItemAE, StringComparison.Ordinal))
                    .ToList();
                if (itemPrevs.Count == 0)
                {
                    continue;
                }

                var moisVals = new decimal?[12];
                for (var m = 1; m <= 12; m++)
                {
                    decimal sum = 0;
                    var any = false;
                    foreach (var p in itemPrevs.Where(x => RapportPrevisionDcService.IsMensuel(x.CodeMode)))
                    {
                        any = true;
                        if (moisMap.TryGetValue(p.IdPrevision, out var arr))
                        {
                            sum += arr[m - 1];
                        }
                    }

                    moisVals[m - 1] = any ? sum : null;
                }

                var totalAnnuel = itemPrevs.Sum(p => p.MontantAnnuel);
                if (totalAnnuel == 0 && moisVals.All(v => v is null or 0))
                {
                    continue;
                }

                if (pendingGroupe is not null && !groupeEmis)
                {
                    lignes.Add(new RapportAeSyntheseItemLigneDto(
                        "GROUPE", pendingGroupe.OrdreAffichage, pendingGroupe.IdGroupeItemAE,
                        pendingGroupe.LibelleGroupe, null, null,
                        null, null, null, null, null, null, null, null, null, null, null, null, null));
                    groupeEmis = true;
                }

                compteur++;
                lignes.Add(new RapportAeSyntheseItemLigneDto(
                    "ITEM", line.OrdreAffichage, null, null,
                    compteur.ToString("00"), line.LibelleItemAE,
                    moisVals[0], moisVals[1], moisVals[2], moisVals[3],
                    moisVals[4], moisVals[5], moisVals[6], moisVals[7],
                    moisVals[8], moisVals[9], moisVals[10], moisVals[11],
                    totalAnnuel));
                totalGeneral += totalAnnuel;
            }
        }

        return new RapportAeSyntheseItemDto(
            $"BUDGET DÉTAILLÉ DES ACTIONS D'EXPLOITATION PAR ITEM — EXERCICE {annee} EN USD",
            lignes,
            totalGeneral);
    }

    // ─── Synthèse RB (clone logique Consolider DC) ────────────────────────

    public static RapportAeSyntheseRbDto ConstruireSyntheseRb(
        short annee,
        IReadOnlyList<RapportAePrevisionRow> rows,
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

                var moisNonNul = mois.Any(m => m is decimal d && d != 0);
                if (cumul == 0 && !moisNonNul) return null;

                return new
                {
                    first.IdGroupeRB,
                    CodeGroupe = first.IdGroupeRB is null ? "—" : (first.CodeGroupeRB ?? "—"),
                    LibelleGroupe = first.IdGroupeRB is null
                        ? "SANS GROUPE N1"
                        : (first.LibelleGroupeRB ?? "SANS GROUPE N1"),
                    Ordre = first.IdGroupeRB is null ? int.MaxValue : (first.OrdreAffichageGroupe ?? 9999),
                    Ligne = new RapportAeSyntheseRbLigneDto(
                        first.IdRB, first.CodeRB, first.LibelleRB, g.Key.ModeFamily, cumul,
                        mois[0], mois[1], mois[2], mois[3], mois[4], mois[5],
                        mois[6], mois[7], mois[8], mois[9], mois[10], mois[11]),
                };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        var groupes = buckets
            .GroupBy(x => x.IdGroupeRB)
            .Select(g =>
            {
                var first = g.First();
                var lignes = g.Select(x => x.Ligne)
                    .OrderBy(l => l.CodeRB)
                    .ThenBy(l => l.CodeMode)
                    .ToList();
                return new RapportAeSyntheseRbGroupeDto(
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

        return new RapportAeSyntheseRbDto(
            $"BUDGET DÉTAILLÉ DES ACTIONS D'EXPLOITATION PAR RUBRIQUE BUDGÉTAIRE — EXERCICE {annee} EN USD",
            groupes,
            groupes.Sum(g => g.SousTotal));
    }

    // ─── Helpers scope ────────────────────────────────────────────────────

    private static void ValidateScope(string niveau, RapportAeQuery query)
    {
        if (query.IdEntite is null)
        {
            throw new ArgumentException("IdEntite est obligatoire.");
        }

        if (niveau == RapportBudgetaireNiveau.Departement && query.IdDepartementStructure is null)
        {
            throw new ArgumentException("IdDepartementStructure est obligatoire pour le niveau DÉPARTEMENT.");
        }

        if (niveau == RapportBudgetaireNiveau.Ub)
        {
            if (query.IdDepartementStructure is null)
            {
                throw new ArgumentException("IdDepartementStructure est obligatoire pour le niveau UB.");
            }

            if (query.IdUB is null)
            {
                throw new ArgumentException("IdUB est obligatoire pour le niveau UB.");
            }
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
        RapportAeQuery query,
        IReadOnlyList<RapportUbOrgRow> allUbs,
        IReadOnlyDictionary<long, RapportUbOrgResolution> resolutions,
        IReadOnlyDictionary<long, RapportStructureNode> structures)
    {
        IEnumerable<RapportUbOrgRow> q = allUbs.Where(u =>
            resolutions.TryGetValue(u.IdUB, out var r) && r.IdEntite == query.IdEntite);

        if (niveau == RapportBudgetaireNiveau.Departement || query.IdDepartementStructure is not null)
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

    private static RapportAeDto Empty(
        RapportAeQuery query,
        string niveau,
        string statut,
        (short Annee, int NumeroVersion, string? Libelle) version,
        RapportStructureNode entite,
        RapportStructureNode? dept,
        RapportStructureNode? division,
        int? moisSelection,
        bool estTous)
    {
        var modeMois = estTous ? RapportAeMoisCodes.Tous : moisSelection!.Value.ToString();
        var libelleMois = estTous ? "TOUS LES MOIS" : RapportAeMoisCodes.Libelles[moisSelection!.Value - 1];
        return new RapportAeDto(
            new RapportAeEnTeteDto(
                $"BUDGET DÉTAILLÉ DES ACTIONS D'EXPLOITATION PAR ITEM — EXERCICE {version.Annee} EN USD",
                version.Annee, query.IdVersion, version.NumeroVersion, version.Libelle,
                niveau, modeMois, libelleMois,
                entite.IdStructure, entite.Code, entite.Libelle,
                dept?.IdStructure, dept?.Code, dept?.Libelle,
                division?.IdStructure, division?.Code, division?.Libelle,
                0, 0, "USD", statut, DateTime.Now,
                $"RAPPORT-AE/{entite.Code}/{version.Annee}/V{version.NumeroVersion:00}/{niveau}/{modeMois}"),
            estTous,
            [],
            null,
            null);
    }
}
