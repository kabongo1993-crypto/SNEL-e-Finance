using BudgetWeb.Application;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Enums;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var filePath = args.Length > 1 ? args[1] : null;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "src", "BudgetWeb.API"))
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
{
    configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .Build();
}

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddApplication();
services.AddInfrastructure(configuration);

await using var provider = services.BuildServiceProvider();
var importService = provider.GetRequiredService<IReferentielImportService>();
var workbookReader = provider.GetRequiredService<ICorrespondanceWorkbookReader>();

switch (command)
{
    case "analyze":
        try
        {
            await ExecuterAnalyse(importService, filePath);
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine("Placez le fichier dans : data/import/Correspondance_Organisationnelle_Budget_Web.xlsx");
            Environment.ExitCode = 1;
        }
        break;
    case "preview":
        try
        {
            await ExecuterPreview(importService, filePath);
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine("Placez le fichier dans : data/import/Correspondance_Organisationnelle_Budget_Web.xlsx");
            Environment.ExitCode = 1;
        }
        break;
    case "preview-detail":
        try
        {
            await ExecuterPreviewDetail(importService, workbookReader, filePath);
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine(ex.Message);
            Environment.ExitCode = 1;
        }
        break;
    case "execute":
        if (args.Length < 2 || !string.Equals(args[^1], "--confirm", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Import refusé : ajouter --confirm en dernier argument.");
            Environment.ExitCode = 1;
            break;
        }

        var executePath = args.Length > 2 ? args[1] : null;
        await ExecuterImport(importService, executePath);
        break;
    default:
        AfficherAide();
        break;
}

static async Task ExecuterAnalyse(IReferentielImportService service, string? filePath)
{
    var analyse = await service.AnalyserAsync(filePath);
    AfficherCompteurs(analyse.Compteurs, analyse.FichierSource, analyse.FeuillesAnalysees);
    AfficherAnomalies(analyse.Anomalies);
}

static async Task ExecuterPreview(IReferentielImportService service, string? filePath)
{
    var preview = await service.PrevisualiserAsync(filePath);
    AfficherCompteurs(preview.Analyse.Compteurs, preview.Analyse.FichierSource, preview.Analyse.FeuillesAnalysees);
    AfficherAnomalies(preview.Analyse.Anomalies);
    Console.WriteLine();
    Console.WriteLine($"Peut importer : {preview.PeutImporter}");
    Console.WriteLine(preview.MessageConfirmation);
    Console.WriteLine();
    Console.WriteLine("--- Aperçu entités (10 premières) ---");
    foreach (var entite in preview.Entites.Take(10))
    {
        Console.WriteLine($"  {entite.Code} | {entite.Libelle}");
    }

    Console.WriteLine("--- Aperçu départements ---");
    foreach (var dept in preview.Departements)
    {
        Console.WriteLine($"  {dept.Sigle} | {dept.Libelle}");
    }

    Console.WriteLine($"--- Relations entité/département : {preview.RelationsEntiteDepartement.Count} ---");
    Console.WriteLine($"--- Structures : {preview.Structures.Count} ---");
    Console.WriteLine($"--- UB : {preview.UnitesBudgetaires.Count} ---");
}

static async Task ExecuterPreviewDetail(
    IReferentielImportService service,
    ICorrespondanceWorkbookReader workbookReader,
    string? filePath)
{
    var resolved = ResolvePath(filePath);
    Console.WriteLine("=== PRÉVISUALISATION DÉTAILLÉE (AUCUNE ÉCRITURE SQL) ===");
    Console.WriteLine($"Fichier : {resolved}");
    Console.WriteLine();

    var workbook = await workbookReader.LireAsync(resolved);
    var preview = await service.PrevisualiserAsync(resolved);
    var structuresByCle = preview.Structures.ToDictionary(s => s.CleMetier, StringComparer.OrdinalIgnoreCase);
    var ubByCode = preview.UnitesBudgetaires.ToDictionary(u => u.CodeUB, StringComparer.OrdinalIgnoreCase);

    // 1. Entités
    Console.WriteLine("## 1. Les 11 entités (code + libellé)");
    foreach (var e in preview.Entites)
    {
        Console.WriteLine($"  - {e.Code} | {e.Libelle}");
    }

    Console.WriteLine();
    Console.WriteLine("## 2. Les 15 départements (sigle + libellé complet)");
    foreach (var d in preview.Departements)
    {
        Console.WriteLine($"  - {d.Sigle} | {d.Libelle}");
    }

    Console.WriteLine();
    Console.WriteLine("## 3. Les 41 relations Entité → Département");
    foreach (var r in preview.RelationsEntiteDepartement)
    {
        Console.WriteLine($"  - {r.EntiteCode} → {r.DepartementSigle}");
    }

    Console.WriteLine();
    Console.WriteLine("## 4. Structures par TypeStructure");
    foreach (var type in TypeStructureOrganisationnelle.OrderedHierarchy)
    {
        var count = preview.Structures.Count(s => s.TypeStructure.Equals(type, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine($"  - {type}: {count}");
    }

    Console.WriteLine();
    Console.WriteLine("## 5 & 6. Exemples d'arborescence représentatifs");

    // Entité avec plusieurs départements
    var entiteMultiDept = preview.RelationsEntiteDepartement
        .GroupBy(r => r.EntiteCode, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(g => g.Count())
        .First();
    Console.WriteLine();
    Console.WriteLine($"### 6a. Entité contenant plusieurs départements : {entiteMultiDept.Key} ({entiteMultiDept.Count()} départements)");
    foreach (var dept in entiteMultiDept)
    {
        Console.WriteLine($"  {entiteMultiDept.Key} → {dept.DepartementSigle}");
    }

    var sampleUbForEntite = preview.UnitesBudgetaires
        .Where(u => preview.RelationsEntiteDepartement.Any(r =>
            r.EntiteCode.Equals(entiteMultiDept.Key, StringComparison.OrdinalIgnoreCase)
            && r.DepartementSigle.Equals(u.DepartementSigle, StringComparison.OrdinalIgnoreCase)
            && u.CleMetierStructure.Contains($"ENTITE|{entiteMultiDept.Key}", StringComparison.OrdinalIgnoreCase)))
        .Take(2)
        .ToList();
    foreach (var ub in sampleUbForEntite)
    {
        AfficherArbre(ub, structuresByCle);
    }

    // Fallback: pick any UBs under that entite from structure key
    if (sampleUbForEntite.Count == 0)
    {
        foreach (var ub in preview.UnitesBudgetaires
                     .Where(u => u.CleMetierStructure.Contains($"ENTITE|{entiteMultiDept.Key}", StringComparison.OrdinalIgnoreCase))
                     .Take(2))
        {
            AfficherArbre(ub, structuresByCle);
        }
    }

    // DDI multi-entités
    Console.WriteLine();
    Console.WriteLine("### 6b / 9. DDI rattaché à plusieurs entités");
    var ddiRelations = preview.RelationsEntiteDepartement
        .Where(r => r.DepartementSigle.Equals("DDI", StringComparison.OrdinalIgnoreCase))
        .OrderBy(r => r.EntiteCode)
        .ToList();
    var expectedDdiEntites = new[] { "AC", "DBD", "DRK", "DPE", "DOC", "DRO", "DRS", "DNK", "DNE", "DSK" };
    foreach (var expected in expectedDdiEntites)
    {
        var ok = ddiRelations.Any(r => r.EntiteCode.Equals(expected, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine($"  DDI → {expected} : {(ok ? "OK" : "MANQUANT")}");
    }

    Console.WriteLine($"  Total relations DDI : {ddiRelations.Count}");
    foreach (var r in ddiRelations)
    {
        Console.WriteLine($"  - {r.EntiteCode} → DDI");
    }

    var ddiUbSamples = preview.UnitesBudgetaires
        .Where(u => u.DepartementSigle.Equals("DDI", StringComparison.OrdinalIgnoreCase))
        .Take(3)
        .ToList();
    foreach (var ub in ddiUbSamples)
    {
        AfficherArbre(ub, structuresByCle);
    }

    // UB apparaissant plusieurs fois dans le fichier source
    Console.WriteLine();
    Console.WriteLine("### 6c / 10. UB apparaissant plusieurs fois dans le fichier source");
    var ubRepetees = workbook.ArbreSource
        .Where(r => !string.IsNullOrWhiteSpace(r.CodeUB))
        .GroupBy(r => r.CodeUB!.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .OrderByDescending(g => g.Count())
        .ToList();
    Console.WriteLine($"  Codes UB répétés dans 07_ARBRE_SOURCE : {ubRepetees.Count}");
    var topRepete = ubRepetees.FirstOrDefault();
    if (topRepete is not null)
    {
        Console.WriteLine($"  Exemple CodeUB={topRepete.Key} — {topRepete.Count()} lignes source");
        foreach (var ligne in topRepete.Take(8))
        {
            Console.WriteLine($"    L{ligne.LigneSource}: {ligne.SigleEntite}/{ligne.SigleDepartement} | {ligne.TypeElementSource} | {ligne.LibelleUB} | TypeLigne={ligne.TypeLigne}");
        }

        if (ubByCode.TryGetValue(topRepete.Key, out var ubUnique))
        {
            Console.WriteLine($"  → UB unique construite : CodeUB={ubUnique.CodeUB} | Dept={ubUnique.DepartementSigle} | Structure={ubUnique.CleMetierStructure} | LignesSource={ubUnique.LignesSource}");
            AfficherArbre(ubUnique, structuresByCle);
        }
        else
        {
            Console.WriteLine("  → ANOMALIE : aucune UB unique construite pour ce code.");
        }
    }

    var doublonsUbGraphe = preview.UnitesBudgetaires
        .GroupBy(u => u.CodeUB, StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .ToList();
    Console.WriteLine($"  Doublons CodeUB dans le graphe d'import : {doublonsUbGraphe.Count} (attendu: 0)");
    Console.WriteLine($"  UB uniques construites : {preview.UnitesBudgetaires.Count}");
    Console.WriteLine($"  Distinct CodeUB dans 07_ARBRE_SOURCE : {workbook.ArbreSource.Where(r => !string.IsNullOrWhiteSpace(r.CodeUB)).Select(r => r.CodeUB!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");
    Console.WriteLine($"  Total lignes 07_ARBRE_SOURCE avec CodeUB : {workbook.ArbreSource.Count(r => !string.IsNullOrWhiteSpace(r.CodeUB))}");

    // UB avec plusieurs éléments organisationnels
    Console.WriteLine();
    Console.WriteLine("### 6d. UB avec plusieurs éléments organisationnels (feuille 05)");
    var multiElements = workbook.ElementsRattaches
        .GroupBy(e => e.CodeUB, StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .OrderByDescending(g => g.Count())
        .ToList();
    Console.WriteLine($"  UB avec >1 élément rattaché : {multiElements.Count}");
    var multiEx = multiElements.FirstOrDefault();
    if (multiEx is not null)
    {
        Console.WriteLine($"  Exemple CodeUB={multiEx.Key} — {multiEx.Count()} éléments");
        foreach (var el in multiEx)
        {
            Console.WriteLine($"    L{el.LigneSource}: {el.SigleEntite}/{el.SigleDepartement} | {el.TypeElementSource} | Div={el.Division} | Contenu={el.ContenuUB}");
        }

        if (ubByCode.TryGetValue(multiEx.Key, out var ubMulti))
        {
            AfficherArbre(ubMulti, structuresByCle);
        }
    }

    // Ligne sans Code_UB example tree
    Console.WriteLine();
    Console.WriteLine("### 6e. Exemple de ligne sans Code_UB");
    var sansUbStruct = preview.Structures.Where(s => s.SansUbRattachee).Take(3).ToList();
    foreach (var s in sansUbStruct)
    {
        AfficherChaineStructure(s.CleMetier, structuresByCle, "  (sans UB)");
    }

    // Additional representative trees to reach >= 10
    Console.WriteLine();
    Console.WriteLine("### Arbres complémentaires (échantillons diversifiés)");
    var complements = preview.UnitesBudgetaires
        .GroupBy(u => ExtractTypeFeuille(u.CleMetierStructure))
        .SelectMany(g => g.Take(2))
        .Take(10)
        .ToList();
    var shown = 0;
    foreach (var ub in complements)
    {
        AfficherArbre(ub, structuresByCle);
        shown++;
        if (shown >= 10)
        {
            break;
        }
    }

    // 7. 12 lignes sans Code_UB
    Console.WriteLine();
    Console.WriteLine("## 7. Les 12 lignes sans Code_UB");
    Console.WriteLine($"  Feuille 06 count: {workbook.SansCodeUb.Count}");
    foreach (var row in workbook.SansCodeUb)
    {
        var mappedType = MapType(row.TypeElementSource);
        var parentDeptCle = $"ENTITE|{row.SigleEntite}>DEPARTEMENT|{row.SigleDepartement}";
        var code = ResolveCode(row);
        var libelle = !string.IsNullOrWhiteSpace(row.ContenuUB) ? row.ContenuUB
            : !string.IsNullOrWhiteSpace(row.LibelleUB) ? row.LibelleUB
            : code ?? "(sans libellé)";
        string? structureCle = null;
        string? parentRetenu = parentDeptCle;
        string typeRetenu = mappedType;

        if (mappedType.Equals(TypeStructureOrganisationnelle.Departement, StringComparison.OrdinalIgnoreCase))
        {
            structureCle = parentDeptCle;
            parentRetenu = $"ENTITE|{row.SigleEntite}";
        }
        else if (!string.IsNullOrWhiteSpace(code))
        {
            structureCle = $"{parentDeptCle}>{mappedType}|{code}";
        }

        StructurePreviewItemDto? structMatch = null;
        if (structureCle is not null && structuresByCle.TryGetValue(structureCle, out var sm))
        {
            structMatch = sm;
        }
        else
        {
            structMatch = preview.Structures.FirstOrDefault(s =>
                s.SansUbRattachee
                && s.Libelle.Contains(row.LibelleUB, StringComparison.OrdinalIgnoreCase)
                && s.CleMetier.Contains($"ENTITE|{row.SigleEntite}", StringComparison.OrdinalIgnoreCase)
                && s.CleMetier.Contains($"DEPARTEMENT|{row.SigleDepartement}", StringComparison.OrdinalIgnoreCase));
        }

        Console.WriteLine($"  - L{row.LigneSource}");
        Console.WriteLine($"      Entité            : {row.SigleEntite}");
        Console.WriteLine($"      Département       : {row.SigleDepartement}");
        Console.WriteLine($"      Libellé           : {libelle}");
        Console.WriteLine($"      TypeStructure     : {structMatch?.TypeStructure ?? typeRetenu}");
        Console.WriteLine($"      Parent retenu     : {structMatch?.CleMetierParent ?? parentRetenu}");
        Console.WriteLine($"      CleMetier         : {structMatch?.CleMetier ?? structureCle ?? "(non résolu)"}");
        Console.WriteLine("      Raison sans UB    : présente en feuille 06 / TypeLigne STRUCTURE_SANS_CODE_UB — structure organisationnelle conservée sans rattachement UB.");
    }

    // 8. 556 UB
    Console.WriteLine();
    Console.WriteLine("## 8. Unités budgétaires (556)");
    var distinctCodes = preview.UnitesBudgetaires.Select(u => u.CodeUB).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    Console.WriteLine($"  Count UB : {preview.UnitesBudgetaires.Count}");
    Console.WriteLine($"  Distinct CodeUB : {distinctCodes}");
    Console.WriteLine($"  Doublons CodeUB : {preview.UnitesBudgetaires.Count - distinctCodes} (attendu: 0)");
    Console.WriteLine("  --- 20 exemples ---");
    foreach (var ub in preview.UnitesBudgetaires.Take(20))
    {
        Console.WriteLine($"  - CodeUB={ub.CodeUB} | Libellé={ub.Libelle}");
        Console.WriteLine($"      Département={ub.DepartementSigle}");
        Console.WriteLine($"      FK_StructureOrganisationnelle (clé logique)={ub.CleMetierStructure}");
        Console.WriteLine($"      LignesSource={ub.LignesSource}");
    }

    // 11. Parents
    Console.WriteLine();
    Console.WriteLine("## 11. Rattachement parent des structures");
    var requiresParent = preview.Structures
        .Where(s => !s.TypeStructure.Equals(TypeStructureOrganisationnelle.Entite, StringComparison.OrdinalIgnoreCase))
        .ToList();
    var missingParent = requiresParent
        .Where(s => string.IsNullOrWhiteSpace(s.CleMetierParent) || !structuresByCle.ContainsKey(s.CleMetierParent!))
        .ToList();
    Console.WriteLine($"  Structures nécessitant un parent : {requiresParent.Count}");
    Console.WriteLine($"  Parents manquants / invalides     : {missingParent.Count}");
    foreach (var m in missingParent.Take(20))
    {
        Console.WriteLine($"    ANOMALIE: {m.CleMetier} parent={m.CleMetierParent}");
    }

    var entitesSansParent = preview.Structures.Count(s =>
        s.TypeStructure.Equals(TypeStructureOrganisationnelle.Entite, StringComparison.OrdinalIgnoreCase)
        && s.CleMetierParent is null);
    Console.WriteLine($"  Entités racines (sans parent) : {entitesSansParent}");

    // 12. Résumé
    var critiques = preview.Analyse.Anomalies.Count(a => a.Severite >= ImportIssueSeverity.Error);
    var warnings = preview.Analyse.Anomalies.Count(a => a.Severite == ImportIssueSeverity.Warning);
    Console.WriteLine();
    Console.WriteLine("## 12. Résumé final");
    Console.WriteLine($"  Entités                : {preview.Analyse.Compteurs.Entites}");
    Console.WriteLine($"  Départements           : {preview.Analyse.Compteurs.Departements}");
    Console.WriteLine($"  Relations              : {preview.Analyse.Compteurs.RelationsEntiteDepartement}");
    Console.WriteLine($"  Structures             : {preview.Analyse.Compteurs.Structures}");
    Console.WriteLine($"  UB                     : {preview.Analyse.Compteurs.UnitesBudgetaires}");
    Console.WriteLine($"  Lignes sans CodeUB     : {preview.Analyse.Compteurs.LignesSansCodeUB}");
    Console.WriteLine($"  Anomalies critiques    : {critiques}");
    Console.WriteLine($"  Anomalies avertissement: {warnings}");
    Console.WriteLine($"  PeutImporter           : {preview.PeutImporter}");
    Console.WriteLine();
    AfficherAnomalies(preview.Analyse.Anomalies);
    Console.WriteLine();
    Console.WriteLine("=== FIN PRÉVISUALISATION — AUCUNE ÉCRITURE SQL EFFECTUÉE ===");
}

static void AfficherArbre(
    UniteBudgetairePreviewItemDto ub,
    IReadOnlyDictionary<string, StructurePreviewItemDto> structuresByCle)
{
    Console.WriteLine();
    Console.WriteLine($"  [Arbre UB {ub.CodeUB}]");
    var chain = BuildChain(ub.CleMetierStructure, structuresByCle);
    foreach (var (node, depth) in chain)
    {
        var indent = new string(' ', 2 + depth * 3);
        var arrow = depth == 0 ? "" : "↓ ";
        Console.WriteLine($"{indent}{arrow}{node.TypeStructure}: {node.Code} — {node.Libelle}");
    }

    var indentUb = new string(' ', 2 + chain.Count * 3);
    Console.WriteLine($"{indentUb}↓ UB: {ub.CodeUB} — {ub.Libelle} (Dept={ub.DepartementSigle})");
}

static void AfficherChaineStructure(
    string cle,
    IReadOnlyDictionary<string, StructurePreviewItemDto> structuresByCle,
    string suffix)
{
    Console.WriteLine();
    Console.WriteLine($"  [Structure {cle}] {suffix}");
    var chain = BuildChain(cle, structuresByCle);
    foreach (var (node, depth) in chain)
    {
        var indent = new string(' ', 2 + depth * 3);
        var arrow = depth == 0 ? "" : "↓ ";
        Console.WriteLine($"{indent}{arrow}{node.TypeStructure}: {node.Code} — {node.Libelle}");
    }
}

static List<(StructurePreviewItemDto Node, int Depth)> BuildChain(
    string cle,
    IReadOnlyDictionary<string, StructurePreviewItemDto> structuresByCle)
{
    var stack = new Stack<StructurePreviewItemDto>();
    var current = cle;
    var guard = 0;
    while (!string.IsNullOrWhiteSpace(current) && structuresByCle.TryGetValue(current, out var node) && guard++ < 20)
    {
        stack.Push(node);
        current = node.CleMetierParent ?? string.Empty;
    }

    var result = new List<(StructurePreviewItemDto, int)>();
    var depth = 0;
    while (stack.Count > 0)
    {
        result.Add((stack.Pop(), depth++));
    }

    return result;
}

static string ExtractTypeFeuille(string cle)
{
    var last = cle.Split('>').LastOrDefault() ?? cle;
    var pipe = last.IndexOf('|');
    return pipe > 0 ? last[..pipe] : last;
}

static string MapType(string typeElement)
    => typeElement.ToUpperInvariant() switch
    {
        "DEPARTEMENT" => TypeStructureOrganisationnelle.Departement,
        "DIRECTION" => TypeStructureOrganisationnelle.Direction,
        "DIVISION" => TypeStructureOrganisationnelle.Division,
        "SERVICE" => TypeStructureOrganisationnelle.Service,
        "BUREAU" => TypeStructureOrganisationnelle.Section,
        _ => TypeStructureOrganisationnelle.Autre
    };

static string? ResolveCode(SansCodeUbSheetRow row)
{
    // Aligné sur CorrespondanceImportGraphBuilder : sigle compact entre parenthèses
    // (GCC/GCE/…) prioritaire ; sinon Division (ex. 53, 63…).
    var sigleMetier = ExtractSigleMetierCompact(row.LibelleUB);
    if (!string.IsNullOrWhiteSpace(sigleMetier))
    {
        return sigleMetier;
    }

    if (!string.IsNullOrWhiteSpace(row.Division))
    {
        return row.Division.Trim();
    }

    if (!string.IsNullOrWhiteSpace(row.LibelleUB))
    {
        var t = row.LibelleUB.Trim();
        return t[..Math.Min(30, t.Length)];
    }

    return null;
}

static string? ExtractSigleMetierCompact(string? libelle)
{
    if (string.IsNullOrWhiteSpace(libelle))
    {
        return null;
    }

    var text = libelle.Trim();
    var close = text.LastIndexOf(')');
    if (close < 0)
    {
        return null;
    }

    var open = text.LastIndexOf('(', close);
    if (open < 0 || close <= open + 1)
    {
        return null;
    }

    var sigle = text[(open + 1)..close].Trim();
    if (sigle.Length is < 2 or > 8)
    {
        return null;
    }

    for (var i = 0; i < sigle.Length; i++)
    {
        if (!char.IsLetterOrDigit(sigle[i]))
        {
            return null;
        }
    }

    return sigle.ToUpperInvariant();
}

static string ResolvePath(string? filePath)
{
    if (!string.IsNullOrWhiteSpace(filePath))
    {
        return Path.GetFullPath(filePath);
    }

    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "Correspondance_Organisationnelle_Budget_Web.xlsx"),
        Path.Combine(Directory.GetCurrentDirectory(), "data", "import", "Correspondance_Organisationnelle_Budget_Web.xlsx")
    };
    foreach (var c in candidates)
    {
        var full = Path.GetFullPath(c);
        if (File.Exists(full))
        {
            return full;
        }
    }

    return Path.GetFullPath(candidates[0]);
}

static async Task ExecuterImport(IReferentielImportService service, string? filePath)
{
    var result = await service.ExecuterAsync(new ImportExecuteRequestDto(filePath, Confirm: true));
    Console.WriteLine(result.Message);
    if (result.Succes)
    {
        AfficherCompteurs(result.CompteursInseres, filePath ?? "(config)", []);
    }
    else
    {
        AfficherAnomalies(result.Erreurs);
        Environment.ExitCode = 1;
    }
}

static void AfficherCompteurs(ImportCompteursDto c, string fichier, IReadOnlyList<string> feuilles)
{
    Console.WriteLine("=== Analyse référentiel organisationnel ===");
    Console.WriteLine($"Fichier : {fichier}");
    if (feuilles.Count > 0)
    {
        Console.WriteLine($"Feuilles : {string.Join(", ", feuilles)}");
    }

    Console.WriteLine($"Entités                      : {c.Entites}");
    Console.WriteLine($"Départements                 : {c.Departements}");
    Console.WriteLine($"Relations entité/département : {c.RelationsEntiteDepartement}");
    Console.WriteLine($"Structures                   : {c.Structures}");
    Console.WriteLine($"UB                           : {c.UnitesBudgetaires}");
    Console.WriteLine($"Lignes sans Code_UB          : {c.LignesSansCodeUB}");
    Console.WriteLine($"Lignes source                : {c.LignesSource}");
    Console.WriteLine($"Anomalies                    : {c.Anomalies} (critiques: {c.AnomaliesCritiques})");
}

static void AfficherAnomalies(IReadOnlyList<ImportValidationIssueDto> anomalies)
{
    if (anomalies.Count == 0)
    {
        Console.WriteLine("Aucune anomalie détectée.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine("--- Anomalies ---");
    foreach (var anomaly in anomalies.Take(50))
    {
        Console.WriteLine($"[{anomaly.Severite}] {anomaly.Code} | Feuille={anomaly.Feuille} Ligne={anomaly.NumeroLigne} | {anomaly.Message}");
    }

    if (anomalies.Count > 50)
    {
        Console.WriteLine($"... {anomalies.Count - 50} anomalies supplémentaires");
    }
}

static void AfficherAide()
{
    Console.WriteLine("Budget Web – Import référentiel organisationnel");
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project tools/BudgetWeb.ImportCli -- analyze [chemin-fichier.xlsx]");
    Console.WriteLine("  dotnet run --project tools/BudgetWeb.ImportCli -- preview [chemin-fichier.xlsx]");
    Console.WriteLine("  dotnet run --project tools/BudgetWeb.ImportCli -- preview-detail [chemin-fichier.xlsx]");
    Console.WriteLine("  dotnet run --project tools/BudgetWeb.ImportCli -- execute [chemin-fichier.xlsx] --confirm");
}
