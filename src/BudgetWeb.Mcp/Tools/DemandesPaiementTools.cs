using System.ComponentModel;
using BudgetWeb.Mcp.Client;
using BudgetWeb.Mcp.Security;
using ModelContextProtocol.Server;

namespace BudgetWeb.Mcp.Tools;

[McpServerToolType]
public sealed class DemandesPaiementTools(BudgetWebApiClient api)
{
    [McpServerTool(Name = "search_demandes_paiement"), Description(
        "Recherche les demandes de paiement (DPM) réellement accessibles à l'utilisateur. " +
        "L'API applique permissions, périmètre, routage et propriété. " +
        "Tri métier : DateCreation (date d'enregistrement) décroissante. " +
        "Filtres : exercice, UB, département, statut, référence, bénéficiaire, dates. " +
        "scope optionnel selon les modes de liste Budget Web. " +
        "Pagination skip/take sur cette liste déjà triée (1-50 par page). " +
        "Réponse compacte : id, référence, dateEnregistrement, montant, devise, UB, statut.")]
    public Task<string> SearchDemandes(
        [Description("Exercice.")] long? idExercice = null,
        [Description("UB.")] long? idUB = null,
        [Description("Département.")] long? idDepartement = null,
        [Description("Statut métier DPM.")] string? statut = null,
        [Description("Référence (texte, pas de SQL).")] string? reference = null,
        [Description("Bénéficiaire (texte).")] string? beneficiaire = null,
        [Description("Scope de liste Budget Web si applicable.")] string? scope = null,
        [Description("Date début (yyyy-MM-dd).")] string? dateDebut = null,
        [Description("Date fin (yyyy-MM-dd).")] string? dateFin = null,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "search_demandes_paiement", new { idExercice, idUB, idDepartement, statut, scope }, async () =>
        {
            var json = await api.GetJsonAsync(
                "/api/v1/demandes-paiement" + BudgetWebApiClient.Query(
                    ("scope", ParameterGuard.SanitizeSearch(scope)),
                    ("idExercice", idExercice),
                    ("idUB", idUB),
                    ("idDepartement", idDepartement),
                    ("statut", ParameterGuard.SanitizeSearch(statut)),
                    ("reference", ParameterGuard.SanitizeSearch(reference)),
                    ("beneficiaire", ParameterGuard.SanitizeSearch(beneficiaire)),
                    ("dateDebut", ParameterGuard.SanitizeSearch(dateDebut)),
                    ("dateFin", ParameterGuard.SanitizeSearch(dateFin))),
                truncateResponse: false,
                cancellationToken);
            if (!DemandePaiementMcpFormat.LooksLikeJsonPayload(json))
                return json;
            var page = ToolRunner.Page(skip, take);
            return DemandePaiementMcpFormat.Search(json, page.Skip, page.Take);
        }, cancellationToken);

    [McpServerTool(Name = "get_latest_demande_paiement"), Description(
        "Retourne la dernière DPM enregistrée (DateCreation la plus récente) parmi celles " +
        "réellement accessibles à l'utilisateur connecté. Lecture seule. " +
        "Mêmes permissions, périmètre et isolation que search_demandes_paiement. " +
        "Réponse compacte : found + une seule DPM (référence, date, bénéficiaire, montant, UB, statut).")]
    public Task<string> GetLatestDemande(
        [Description("Exercice (optionnel).")] long? idExercice = null,
        [Description("UB (optionnel, refusée si hors périmètre).")] long? idUB = null,
        [Description("Département (optionnel).")] long? idDepartement = null,
        [Description("Statut métier DPM (optionnel).")] string? statut = null,
        [Description("Scope de liste Budget Web si applicable.")] string? scope = null,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "get_latest_demande_paiement", new { idExercice, idUB, idDepartement, statut, scope }, async () =>
        {
            var listJson = await api.GetJsonAsync(
                "/api/v1/demandes-paiement" + BudgetWebApiClient.Query(
                    ("scope", ParameterGuard.SanitizeSearch(scope)),
                    ("idExercice", idExercice),
                    ("idUB", idUB),
                    ("idDepartement", idDepartement),
                    ("statut", ParameterGuard.SanitizeSearch(statut))),
                truncateResponse: false,
                cancellationToken);
            if (!DemandePaiementMcpFormat.LooksLikeJsonPayload(listJson))
                return listJson;
            if (!DemandePaiementMcpFormat.TryGetLatestId(listJson, out var id))
                return DemandePaiementMcpFormat.Latest(listJson, null);

            var detail = await api.GetJsonAsync(
                $"/api/v1/demandes-paiement/{id}",
                truncateResponse: false,
                cancellationToken);
            return DemandePaiementMcpFormat.Latest(listJson, detail);
        }, cancellationToken);

    [McpServerTool(Name = "get_demande_paiement"), Description(
        "Consulte une DPM par identifiant (vue consultation). " +
        "Refus si la DPM est hors périmètre ou sans permission.")]
    public Task<string> GetDemande(
        [Description("Identifiant de la demande (obligatoire).")] long? idDemande = null,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "get_demande_paiement", new { idDemande }, async () =>
        {
            var id = ParameterGuard.RequirePositive(idDemande, "idDemande");
            return await api.GetJsonAsync($"/api/v1/demandes-paiement/{id}", cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "get_imputations"), Description(
        "Consulte les grilles d'imputation DC, AE ou BI d'une DPM. " +
        "Respecte les permissions paiements.imputer_dc|ae|bi et les règles de structure. " +
        "niveau = DC | AE | BI. Pour AE : libelleItemAE recommandé. Pour BI : idItemBI requis.")]
    public Task<string> GetImputations(
        [Description("Identifiant DPM (obligatoire).")] long? idDemande = null,
        [Description("DC, AE ou BI (obligatoire).")] string? niveau = null,
        [Description("Mois 1-12.")] byte? mois = null,
        [Description("Libellé item AE (AE).")] string? libelleItemAE = null,
        [Description("Groupe item AE.")] long? idGroupeItemAE = null,
        [Description("Item BI (BI).")] long? idItemBI = null,
        [Description("Si true, ajoute la fiche d'imputation.")] bool inclureFiche = false,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "get_imputations", new { idDemande, niveau, mois }, async () =>
        {
            var id = ParameterGuard.RequirePositive(idDemande, "idDemande");
            var n = (niveau ?? string.Empty).Trim().ToUpperInvariant();
            if (n is not ("DC" or "AE" or "BI"))
                return "Le paramètre niveau doit valoir DC, AE ou BI.";

            var path = n switch
            {
                "DC" => $"/api/v1/demandes-paiement/{id}/imputation-dc" + BudgetWebApiClient.Query(("mois", mois)),
                "AE" => $"/api/v1/demandes-paiement/{id}/imputation-ae" + BudgetWebApiClient.Query(
                    ("libelleItemAE", ParameterGuard.SanitizeSearch(libelleItemAE)),
                    ("idGroupeItemAE", idGroupeItemAE),
                    ("mois", mois)),
                _ => $"/api/v1/demandes-paiement/{id}/imputation-bi" + BudgetWebApiClient.Query(
                    ("idItemBI", idItemBI),
                    ("mois", mois))
            };

            if (n == "BI" && idItemBI is not > 0)
                return "idItemBI est obligatoire pour le niveau BI.";

            var grille = await api.GetJsonAsync(path, cancellationToken);
            if (!inclureFiche)
                return grille;

            var fiche = await api.GetJsonAsync($"/api/v1/demandes-paiement/{id}/fiche-imputation", cancellationToken);
            return $$"""{"grille":{{grille}},"fiche":{{fiche}}}""";
        }, cancellationToken);
}
