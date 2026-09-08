using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.SnelComptes.Import.DTOs;
using BudgetWeb.Application.SnelComptes.Import.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BudgetWeb.Application.SnelComptes.Import.Services;

public class SnelComptesImportService : ISnelComptesImportService
{
    public const string NomFeuille = "Comptes SYSCOHADA";
    public const string NomFichierDefaut = "Tableau_Comptes_SYSCOHADA_Sections_Colore.xlsx";

    public const string StatutACreer = "A_CREER";
    public const string StatutDejaExistant = "DEJA_EXISTANT";
    public const string StatutConflit = "CONFLIT";

    private readonly ISnelComptesWorkbookReader _workbookReader;
    private readonly IRubriqueBudgetaireService _rubriqueService;
    private readonly IItemBIService _itemBIService;
    private readonly ISnelComptesImportTransaction _transaction;
    private readonly IConfiguration _configuration;

    public SnelComptesImportService(
        ISnelComptesWorkbookReader workbookReader,
        IRubriqueBudgetaireService rubriqueService,
        IItemBIService itemBIService,
        ISnelComptesImportTransaction transaction,
        IConfiguration configuration)
    {
        _workbookReader = workbookReader;
        _rubriqueService = rubriqueService;
        _itemBIService = itemBIService;
        _transaction = transaction;
        _configuration = configuration;
    }

    public async Task<SnelComptesPreviewDto> PrevisualiserAsync(
        string? fichierSource,
        CancellationToken cancellationToken = default)
    {
        var chemin = ResoudreCheminFichier(fichierSource);
        if (!File.Exists(chemin))
        {
            return PreviewFichierIntrouvable(chemin);
        }

        var rows = await _workbookReader.LireAsync(chemin, cancellationToken);
        var parsed = SnelComptesImportParser.Parse(rows);

        var rbExistantes = await _rubriqueService.GetAllAsync(cancellationToken);
        var itemsExistants = await _itemBIService.GetAllAsync(cancellationToken);
        var rbParCode = rbExistantes.ToDictionary(r => r.CodeRB, StringComparer.OrdinalIgnoreCase);
        var itemsParCode = itemsExistants.ToDictionary(i => i.CodeItem, StringComparer.OrdinalIgnoreCase);

        var rubriquesDetail = parsed.Rubriques
            .Select(r => MapRb(r, rbParCode, parsed.Anomalies))
            .ToList();
        var itemsDetail = parsed.ItemsBI
            .Select(i => MapItem(i, itemsParCode, parsed.Anomalies))
            .ToList();

        var erreurs = parsed.Anomalies
            .Any(a => a.Severite.Equals("Error", StringComparison.OrdinalIgnoreCase));
        var conflits = rubriquesDetail.Count(r => r.Statut == StatutConflit)
            + itemsDetail.Count(i => i.Statut == StatutConflit);
        var peutImporter = !erreurs && conflits == 0;

        var anomaliesRbCodes = CodesAnomalies(parsed.Anomalies, rubriquesDetail.Select(r => r.CodeImport));
        var anomaliesBiCodes = CodesAnomalies(parsed.Anomalies, itemsDetail.Select(i => i.CodeImport));

        var message = peutImporter
            ? "Prévisualisation prête. Aucune écriture en base tant que l'import n'est pas confirmé."
            : "Import bloqué : corriger les anomalies bloquantes ou les conflits avant exécution.";

        return new SnelComptesPreviewDto(
            chemin,
            NomFeuille,
            Compteurs(rubriquesDetail, anomaliesRbCodes),
            Compteurs(itemsDetail, anomaliesBiCodes),
            rubriquesDetail,
            itemsDetail,
            ConstruireArbreRb(rubriquesDetail),
            ConstruireArbreItems(itemsDetail),
            parsed.LignesIgnorees,
            parsed.Anomalies
                .OrderBy(a => SeveriteOrdre(a.Severite))
                .ThenBy(a => a.NumeroLigne)
                .ToList(),
            peutImporter,
            message);
    }

    public async Task<SnelComptesExecuteResultDto> ExecuterAsync(
        SnelComptesExecuteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Confirm)
        {
            return new SnelComptesExecuteResultDto(
                false,
                "Import refusé : confirmation explicite requise (Confirm=true).",
                0, 0, 0, 0, 0, [], []);
        }

        var preview = await PrevisualiserAsync(request.FichierSource, cancellationToken);
        if (!preview.PeutImporter)
        {
            return new SnelComptesExecuteResultDto(
                false,
                preview.Message,
                0,
                0,
                preview.LignesIgnorees.Count,
                preview.Rubriques.EnConflit + preview.ItemsBI.EnConflit,
                preview.Anomalies.Count,
                preview.ArbreRubriques,
                preview.ArbreItemsBI);
        }

        var rubriquesInserees = 0;
        var itemsInserees = 0;

        try
        {
            await _transaction.ExecuteAsync(async ct =>
            {
                rubriquesInserees = await InsererRubriquesAsync(preview.RubriquesDetail, ct);
                itemsInserees = await InsererItemsAsync(preview.ItemsBIDetail, ct);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return new SnelComptesExecuteResultDto(
                false,
                $"Import annulé : {ex.Message}",
                0,
                0,
                preview.LignesIgnorees.Count,
                preview.Rubriques.EnConflit + preview.ItemsBI.EnConflit,
                preview.Anomalies.Count,
                preview.ArbreRubriques,
                preview.ArbreItemsBI);
        }

        var arbreRb = MapperArbreRb(await _rubriqueService.GetArbreAsync(cancellationToken));
        var arbreBi = MapperArbreItems(await _itemBIService.GetArbreAsync(cancellationToken));

        var message = rubriquesInserees == 0 && itemsInserees == 0
            ? "Aucune insertion : les 64 RB et 11 Items BI sont déjà présents (import idempotent)."
            : $"Import terminé : {rubriquesInserees} RB et {itemsInserees} Items BI insérés. Aucune donnée existante n'a été modifiée.";

        return new SnelComptesExecuteResultDto(
            true,
            message,
            rubriquesInserees,
            itemsInserees,
            preview.LignesIgnorees.Count,
            0,
            preview.Anomalies.Count,
            arbreRb,
            arbreBi);
    }

    private async Task<int> InsererRubriquesAsync(
        IReadOnlyList<SnelComptesRbPreviewItemDto> lignes,
        CancellationToken cancellationToken)
    {
        var existantes = (await _rubriqueService.GetAllAsync(cancellationToken))
            .ToDictionary(r => r.CodeRB, StringComparer.OrdinalIgnoreCase);
        var inserees = 0;

        foreach (var ligne in lignes.Where(l => l.Statut == StatutACreer).OrderBy(l => l.Niveau).ThenBy(l => l.CodeImport))
        {
            long? parentId = null;
            if (ligne.CodeParent is not null)
            {
                if (!existantes.TryGetValue(ligne.CodeParent, out var parent))
                {
                    throw new InvalidOperationException(
                        $"Le parent {ligne.CodeParent} de la rubrique {ligne.CodeImport} est introuvable au moment de l'insertion.");
                }

                parentId = parent.IdRB;
            }

            var created = await _rubriqueService.CreateAsync(
                new CreateRubriqueBudgetaireRequest(ligne.CodeImport, ligne.Libelle, parentId, true),
                cancellationToken);
            existantes[created.CodeRB] = created;
            inserees++;
        }

        return inserees;
    }

    private async Task<int> InsererItemsAsync(
        IReadOnlyList<SnelComptesItemBiPreviewItemDto> lignes,
        CancellationToken cancellationToken)
    {
        var existants = (await _itemBIService.GetAllAsync(cancellationToken))
            .ToDictionary(i => i.CodeItem, StringComparer.OrdinalIgnoreCase);
        var inserees = 0;

        foreach (var ligne in lignes.Where(l => l.Statut == StatutACreer).OrderBy(l => l.Niveau).ThenBy(l => l.CodeImport))
        {
            long? parentId = null;
            if (ligne.CodeParent is not null)
            {
                if (!existants.TryGetValue(ligne.CodeParent, out var parent))
                {
                    throw new InvalidOperationException(
                        $"Le parent {ligne.CodeParent} de l'item {ligne.CodeImport} est introuvable au moment de l'insertion.");
                }

                parentId = parent.IdItemBI;
            }

            var created = await _itemBIService.CreateAsync(
                new CreateItemBIRequest(ligne.CodeImport, ligne.Libelle, parentId, ligne.Categorie, true),
                cancellationToken);
            existants[created.CodeItem] = created;
            inserees++;
        }

        return inserees;
    }

    private static SnelComptesRbPreviewItemDto MapRb(
        SnelComptesParsedRb parsed,
        IReadOnlyDictionary<string, RubriqueBudgetaireDto> existantes,
        List<SnelComptesImportAnomalieDto> anomalies)
    {
        if (!existantes.TryGetValue(parsed.CodeImport, out var existing))
        {
            return new SnelComptesRbPreviewItemDto(
                parsed.NumeroLigne,
                parsed.CodeExcel,
                parsed.CodeImport,
                parsed.Libelle,
                parsed.CodeParent,
                parsed.Niveau,
                StatutACreer);
        }

        if (!LibellesIdentiques(existing.Libelle, parsed.Libelle))
        {
            anomalies.Add(new SnelComptesImportAnomalieDto(
                "Error",
                "CONFLIT_LIBELLE",
                $"La rubrique {parsed.CodeImport} existe déjà avec le libellé « {existing.Libelle} » (fichier : « {parsed.Libelle} »). Aucune donnée n'a été écrasée.",
                parsed.NumeroLigne,
                parsed.CodeImport));
            return new SnelComptesRbPreviewItemDto(
                parsed.NumeroLigne,
                parsed.CodeExcel,
                parsed.CodeImport,
                parsed.Libelle,
                parsed.CodeParent,
                parsed.Niveau,
                StatutConflit);
        }

        if (!string.Equals(existing.ParentCode ?? string.Empty, parsed.CodeParent ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            anomalies.Add(new SnelComptesImportAnomalieDto(
                "Warning",
                "HIERARCHIE_EXISTANTE_DIFFERENTE",
                $"La rubrique {parsed.CodeImport} existe déjà avec un parent différent. Elle ne sera ni modifiée ni réinsérée.",
                parsed.NumeroLigne,
                parsed.CodeImport));
        }

        return new SnelComptesRbPreviewItemDto(
            parsed.NumeroLigne,
            parsed.CodeExcel,
            parsed.CodeImport,
            parsed.Libelle,
            parsed.CodeParent,
            parsed.Niveau,
            StatutDejaExistant);
    }

    private static SnelComptesItemBiPreviewItemDto MapItem(
        SnelComptesParsedItemBi parsed,
        IReadOnlyDictionary<string, ItemBIDto> existants,
        List<SnelComptesImportAnomalieDto> anomalies)
    {
        if (!existants.TryGetValue(parsed.CodeImport, out var existing))
        {
            return new SnelComptesItemBiPreviewItemDto(
                parsed.NumeroLigne,
                parsed.CodeExcel,
                parsed.CodeImport,
                parsed.Libelle,
                parsed.CodeParent,
                parsed.Niveau,
                parsed.Categorie,
                StatutACreer);
        }

        if (!LibellesIdentiques(existing.Libelle, parsed.Libelle))
        {
            anomalies.Add(new SnelComptesImportAnomalieDto(
                "Error",
                "CONFLIT_LIBELLE",
                $"L'item BI {parsed.CodeImport} existe déjà avec le libellé « {existing.Libelle} » (fichier : « {parsed.Libelle} »). Aucune donnée n'a été écrasée.",
                parsed.NumeroLigne,
                parsed.CodeImport));
            return new SnelComptesItemBiPreviewItemDto(
                parsed.NumeroLigne,
                parsed.CodeExcel,
                parsed.CodeImport,
                parsed.Libelle,
                parsed.CodeParent,
                parsed.Niveau,
                parsed.Categorie,
                StatutConflit);
        }

        return new SnelComptesItemBiPreviewItemDto(
            parsed.NumeroLigne,
            parsed.CodeExcel,
            parsed.CodeImport,
            parsed.Libelle,
            parsed.CodeParent,
            parsed.Niveau,
            parsed.Categorie,
            StatutDejaExistant);
    }

    private static bool LibellesIdentiques(string existant, string incoming)
        => string.Equals(existant.Trim(), incoming.Trim(), StringComparison.Ordinal);

    private static SnelComptesCompteursDto Compteurs(
        IReadOnlyList<SnelComptesRbPreviewItemDto> items,
        int anomalies)
        => new(
            items.Count,
            items.Count(i => i.Statut == StatutACreer),
            items.Count(i => i.Statut == StatutDejaExistant),
            items.Count(i => i.Statut == StatutConflit),
            anomalies);

    private static SnelComptesCompteursDto Compteurs(
        IReadOnlyList<SnelComptesItemBiPreviewItemDto> items,
        int anomalies)
        => new(
            items.Count,
            items.Count(i => i.Statut == StatutACreer),
            items.Count(i => i.Statut == StatutDejaExistant),
            items.Count(i => i.Statut == StatutConflit),
            anomalies);

    private static int CodesAnomalies(IEnumerable<SnelComptesImportAnomalieDto> anomalies, IEnumerable<string> codes)
    {
        var set = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
        return anomalies.Count(a => a.CodeElement is not null && set.Contains(a.CodeElement));
    }

    private static IReadOnlyList<SnelComptesNoeudPreviewDto> ConstruireArbreRb(
        IReadOnlyList<SnelComptesRbPreviewItemDto> items)
    {
        var byParent = items
            .GroupBy(i => i.CodeParent ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CodeImport, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        List<SnelComptesNoeudPreviewDto> Enfants(string? parent)
        {
            var key = parent ?? string.Empty;
            if (!byParent.TryGetValue(key, out var children))
            {
                return [];
            }

            return children.Select(c => new SnelComptesNoeudPreviewDto(
                c.CodeImport,
                c.Libelle,
                c.Niveau,
                null,
                c.Statut,
                Enfants(c.CodeImport))).ToList();
        }

        return Enfants(null);
    }

    private static IReadOnlyList<SnelComptesNoeudPreviewDto> ConstruireArbreItems(
        IReadOnlyList<SnelComptesItemBiPreviewItemDto> items)
    {
        var byParent = items
            .GroupBy(i => i.CodeParent ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CodeImport, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        List<SnelComptesNoeudPreviewDto> Enfants(string? parent)
        {
            var key = parent ?? string.Empty;
            if (!byParent.TryGetValue(key, out var children))
            {
                return [];
            }

            return children.Select(c => new SnelComptesNoeudPreviewDto(
                c.CodeImport,
                c.Libelle,
                c.Niveau,
                c.Categorie,
                c.Statut,
                Enfants(c.CodeImport))).ToList();
        }

        return Enfants(null);
    }

    private static IReadOnlyList<SnelComptesNoeudPreviewDto> MapperArbreRb(
        IReadOnlyList<RubriqueBudgetaireNoeudDto> noeuds)
        => noeuds.Select(n => new SnelComptesNoeudPreviewDto(
            n.CodeRB,
            n.Libelle,
            n.Niveau,
            null,
            StatutDejaExistant,
            MapperArbreRb(n.Enfants))).ToList();

    private static IReadOnlyList<SnelComptesNoeudPreviewDto> MapperArbreItems(
        IReadOnlyList<ItemBINoeudDto> noeuds)
        => noeuds.Select(n => new SnelComptesNoeudPreviewDto(
            n.CodeItem,
            n.Libelle,
            n.Niveau,
            n.Categorie,
            StatutDejaExistant,
            MapperArbreItems(n.Enfants))).ToList();

    private static int SeveriteOrdre(string severite)
        => severite.Equals("Error", StringComparison.OrdinalIgnoreCase) ? 0
            : severite.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? 1
            : 2;

    private static SnelComptesPreviewDto PreviewFichierIntrouvable(string chemin)
        => new(
            chemin,
            NomFeuille,
            new SnelComptesCompteursDto(0, 0, 0, 0, 1),
            new SnelComptesCompteursDto(0, 0, 0, 0, 0),
            [],
            [],
            [],
            [],
            [],
            [new SnelComptesImportAnomalieDto(
                "Error",
                "FICHIER_INTROUVABLE",
                $"Fichier Excel introuvable : {chemin}",
                null,
                null)],
            false,
            "Import bloqué : fichier Excel introuvable.");

    internal string ResoudreCheminFichier(string? fichierSource)
    {
        if (!string.IsNullOrWhiteSpace(fichierSource))
        {
            return Path.GetFullPath(fichierSource);
        }

        var configure = _configuration["Import:SnelComptesFilePath"];
        var names = new[] { NomFichierDefaut };
        if (!string.IsNullOrWhiteSpace(configure))
        {
            var configuredPath = Path.GetFullPath(configure);
            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }

            names = [Path.GetFileName(configure), NomFichierDefaut];
        }

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
            {
                foreach (var name in names)
                {
                    var candidate = Path.Combine(dir.FullName, name);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), NomFichierDefaut));
    }
}
