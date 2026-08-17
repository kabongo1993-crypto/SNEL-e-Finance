using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Enums;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Models;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Services;
using Microsoft.Extensions.Configuration;

namespace BudgetWeb.Application.ReferentielOrganisationnel.Import.Services;

public class ReferentielImportService : IReferentielImportService
{
    private readonly ICorrespondanceWorkbookReader _workbookReader;
    private readonly IReferentielImportRepository _repository;
    private readonly IConfiguration _configuration;

    public ReferentielImportService(
        ICorrespondanceWorkbookReader workbookReader,
        IReferentielImportRepository repository,
        IConfiguration configuration)
    {
        _workbookReader = workbookReader;
        _repository = repository;
        _configuration = configuration;
    }

    public async Task<ImportAnalyseResultDto> AnalyserAsync(string? fichierSource, CancellationToken cancellationToken = default)
    {
        var (_, resultat) = await ConstruirePipelineAsync(fichierSource, cancellationToken);
        return resultat;
    }

    public async Task<ImportPreviewDto> PrevisualiserAsync(string? fichierSource, CancellationToken cancellationToken = default)
    {
        var (graph, resultat) = await ConstruirePipelineAsync(fichierSource, cancellationToken);
        var peutImporter = !graph.Anomalies.Any(a => a.Severite >= ImportIssueSeverity.Error);

        return new ImportPreviewDto(
            resultat,
            graph.Entites.Select(e => new EntitePreviewItemDto(e.Value.Code, e.Value.Libelle, 1)).OrderBy(e => e.Code).ToList(),
            graph.Departements.Select(d => new DepartementPreviewItemDto(d.Value.Sigle, d.Value.Libelle, 1)).OrderBy(d => d.Sigle).ToList(),
            graph.RelationsEntiteDepartement.Select(r => new RelationEntiteDepartementPreviewDto(r.EntiteCode, r.DepartementSigle)).OrderBy(r => r.EntiteCode).ThenBy(r => r.DepartementSigle).ToList(),
            graph.Structures.Values.Select(s => new StructurePreviewItemDto(s.CleMetier, s.TypeStructure, s.Code, s.Libelle, s.CleMetierParent, s.LignesSource, s.SansUbRattachee)).OrderBy(s => s.CleMetier).ToList(),
            graph.UnitesBudgetaires.Values.Select(u => new UniteBudgetairePreviewItemDto(u.CodeUB, u.Libelle, u.DepartementSigle, u.CleMetierStructure, u.LignesSource)).OrderBy(u => u.CodeUB).ToList(),
            peutImporter,
            peutImporter
                ? "Prévisualisation prête. L'import réel nécessite Confirm=true."
                : "Import bloqué : corriger les anomalies critiques avant exécution.");
    }

    public async Task<ImportExecuteResultDto> ExecuterAsync(ImportExecuteRequestDto request, CancellationToken cancellationToken = default)
    {
        var debut = DateTime.UtcNow;

        if (!request.Confirm)
        {
            return new ImportExecuteResultDto(
                false,
                "Import refusé : confirmation explicite requise (Confirm=true).",
                new ImportCompteursDto(0, 0, 0, 0, 0, 0, 0, 0, 0),
                [],
                DateTime.UtcNow - debut);
        }

        var preview = await PrevisualiserAsync(request.FichierSource, cancellationToken);
        if (!preview.PeutImporter)
        {
            return new ImportExecuteResultDto(
                false,
                "Import refusé : anomalies critiques détectées lors de la prévisualisation.",
                preview.Analyse.Compteurs,
                preview.Analyse.Anomalies.Where(a => a.Severite >= ImportIssueSeverity.Error).ToList(),
                DateTime.UtcNow - debut);
        }

        if (!await _repository.ReferentielOrganisationnelEstVideAsync(cancellationToken))
        {
            return new ImportExecuteResultDto(
                false,
                "Import refusé : le référentiel organisationnel n'est pas vide.",
                preview.Analyse.Compteurs,
                [new ImportValidationIssueDto(ImportIssueSeverity.Critical, "REFERENTIEL_NON_VIDE", "DEPARTEMENT, STRUCTURE_ORGANISATIONNELLE ou UNITE_BUDGETAIRE contient déjà des données.", null, null, null, null)],
                DateTime.UtcNow - debut);
        }

        try
        {
            var compteurs = await _repository.ImporterAsync(preview, cancellationToken);
            return new ImportExecuteResultDto(
                true,
                "Import transactionnel terminé avec succès.",
                compteurs,
                [],
                DateTime.UtcNow - debut);
        }
        catch (Exception ex)
        {
            var detail = FlattenException(ex);
            return new ImportExecuteResultDto(
                false,
                $"Import annulé (rollback) : {detail}",
                preview.Analyse.Compteurs,
                [new ImportValidationIssueDto(ImportIssueSeverity.Critical, "IMPORT_ROLLBACK", detail, null, null, null, null)],
                DateTime.UtcNow - debut);
        }
    }

    private static string FlattenException(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) &&
                (parts.Count == 0 || !parts[^1].Equals(current.Message, StringComparison.Ordinal)))
            {
                parts.Add(current.Message);
            }
        }

        return string.Join(" → ", parts);
    }

    private async Task<(ImportGraphModel Graph, ImportAnalyseResultDto Resultat)> ConstruirePipelineAsync(
        string? fichierSource,
        CancellationToken cancellationToken)
    {
        var chemin = ResoudreCheminFichier(fichierSource);
        if (!File.Exists(chemin))
        {
            throw new FileNotFoundException($"Fichier Excel introuvable : {chemin}");
        }

        var workbook = await _workbookReader.LireAsync(chemin, cancellationToken);
        var graph = CorrespondanceImportGraphBuilder.Construire(workbook);

        IReadOnlyList<string> feuilles = new List<string>
        {
            "01_ENTITES",
            "02_DEPARTEMENTS",
            "03_ENTITE_DEPARTEMENT",
            "04_UB_A_IMPORTER",
            "05_ELEMENTS_RATTACHES",
            "06_SANS_CODE_UB",
            "07_ARBRE_SOURCE"
        };

        var resultat = new ImportAnalyseResultDto(
            chemin,
            feuilles,
            graph.ToCompteurs(),
            graph.Anomalies.OrderBy(a => a.Feuille).ThenBy(a => a.NumeroLigne).ToList());

        return (graph, resultat);
    }

    private string ResoudreCheminFichier(string? fichierSource)
    {
        if (!string.IsNullOrWhiteSpace(fichierSource))
        {
            return Path.GetFullPath(fichierSource);
        }

        var configure = _configuration["Import:CorrespondanceFilePath"];
        if (!string.IsNullOrWhiteSpace(configure))
        {
            var configuredPath = Path.GetFullPath(configure);
            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Correspondance_Organisationnelle_Budget_Web.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "import", "Correspondance_Organisationnelle_Budget_Web.xlsx"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Correspondance_Organisationnelle_Budget_Web.xlsx")
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return Path.GetFullPath(candidates[0]);
    }
}
