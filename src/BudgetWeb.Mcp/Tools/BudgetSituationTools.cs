using System.ComponentModel;
using BudgetWeb.Mcp.Client;
using BudgetWeb.Mcp.Security;
using ModelContextProtocol.Server;

namespace BudgetWeb.Mcp.Tools;

[McpServerToolType]
public sealed class BudgetSituationTools(BudgetWebApiClient api)
{
    [McpServerTool(Name = "get_budget_situation"), Description(
        "Retourne la situation budgétaire accessible à l'utilisateur Budget Web courant. " +
        "Sans idVersion+idUB : synthèse des prévisions (mes-previsions) filtrée par le périmètre. " +
        "Avec idVersion et idUB : détail Version×UB (totaux DC/AE/BI déjà calculés par Budget Web). " +
        "Ne calcule rien côté MCP. Permissions et périmètre UB appliqués par l'API.")]
    public Task<string> GetBudgetSituation(
        [Description("Identifiant d'exercice budgétaire (optionnel).")] long? idExercice = null,
        [Description("Identifiant de version budgétaire (requis avec idUB pour le détail).")] long? idVersion = null,
        [Description("Identifiant d'unité budgétaire. Hors périmètre → refus API.")] long? idUB = null,
        [Description("Identifiant de département (filtre optionnel).")] long? idDepartement = null,
        [Description("Statut de prévision : BROUILLON, SOUMISE, CONTROLEE, VALIDEE, REJETEE.")] string? statut = null,
        [Description("Recherche texte sur le code/libellé UB (pas de SQL).")] string? searchUb = null,
        [Description("Pagination : nombre de lignes à ignorer.")] int? skip = null,
        [Description("Pagination : nombre max de lignes (1-50, défaut 20).")] int? take = null,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "get_budget_situation", new { idExercice, idVersion, idUB, idDepartement, statut }, async () =>
        {
            var search = ParameterGuard.SanitizeSearch(searchUb);
            if (idVersion is > 0 && idUB is > 0)
            {
                return await api.GetJsonAsync(
                    "/api/v1/suivi-previsions/ub-detail" + BudgetWebApiClient.Query(
                        ("idVersion", idVersion), ("idUB", idUB)),
                    cancellationToken);
            }

            var json = await api.GetJsonAsync(
                "/api/v1/suivi-previsions/mes-previsions" + BudgetWebApiClient.Query(
                    ("idExercice", idExercice),
                    ("idVersion", idVersion),
                    ("idDepartement", idDepartement),
                    ("statut", statut),
                    ("searchUb", search)),
                cancellationToken);
            var page = ToolRunner.Page(skip, take);
            return JsonSlice.Paginate(json, page.Skip, page.Take, out _, out _);
        }, cancellationToken);

    [McpServerTool(Name = "get_credits_disponibles"), Description(
        "Retourne les crédits disponibles d'une ligne budgétaire (budget, engagé, disponible) " +
        "via l'endpoint métier lignes-budgetaires-disponibles. " +
        "Paramètres obligatoires : idExercice, idUB, idTypeBudget. " +
        "Le type de budget impose les règles DC / AE / BI. L'accès UB est contrôlé par Budget Web.")]
    public Task<string> GetCreditsDisponibles(
        [Description("Exercice budgétaire (obligatoire).")] long? idExercice = null,
        [Description("Unité budgétaire (obligatoire).")] long? idUB = null,
        [Description("Type de budget DC/AE/BI (obligatoire).")] long? idTypeBudget = null,
        [Description("Rubrique budgétaire (recommandé pour DC).")] long? idRubriqueBudgetaire = null,
        [Description("Mois 1-12 pour un disponible mensuel.")] byte? mois = null,
        [Description("Libellé item AE (requis selon le type AE).")] string? libelleItemAE = null,
        [Description("Groupe item AE (optionnel).")] long? idGroupeItemAE = null,
        [Description("Item BI (requis selon le type BI).")] long? idItemBI = null,
        [Description("Détail BI (optionnel).")] string? detailBI = null,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "get_credits_disponibles", new { idExercice, idUB, idTypeBudget, idRubriqueBudgetaire, mois }, async () =>
        {
            var exercice = ParameterGuard.RequirePositive(idExercice, "idExercice");
            var ub = ParameterGuard.RequirePositive(idUB, "idUB");
            var type = ParameterGuard.RequirePositive(idTypeBudget, "idTypeBudget");
            return await api.GetJsonAsync(
                "/api/v1/demandes-paiement/lignes-budgetaires-disponibles" + BudgetWebApiClient.Query(
                    ("idExercice", exercice),
                    ("idUB", ub),
                    ("idTypeBudget", type),
                    ("idRubriqueBudgetaire", idRubriqueBudgetaire),
                    ("mois", mois),
                    ("libelleItemAE", ParameterGuard.SanitizeSearch(libelleItemAE)),
                    ("idGroupeItemAE", idGroupeItemAE),
                    ("idItemBI", idItemBI),
                    ("detailBI", ParameterGuard.SanitizeSearch(detailBI))),
                cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "get_budget_execution"), Description(
        "Retourne l'exécution budgétaire telle que calculée par Budget Web. " +
        "idVersion+idUB : totaux de prévision Version×UB. " +
        "codeType DC|AE|BI : lignes de prévision de ce type. " +
        "Si idTypeBudget est fourni avec idExercice et idUB : crédits (budget/engagé/disponible) de la ligne. " +
        "Aucun taux n'est inventé par le MCP.")]
    public Task<string> GetBudgetExecution(
        [Description("Version budgétaire (recommandé).")] long? idVersion = null,
        [Description("Unité budgétaire (recommandé).")] long? idUB = null,
        [Description("Exercice (requis avec crédits).")] long? idExercice = null,
        [Description("Type de budget pour les crédits.")] long? idTypeBudget = null,
        [Description("DC, AE ou BI pour charger les lignes de prévision.")] string? codeType = null,
        [Description("Rubrique pour les crédits.")] long? idRubriqueBudgetaire = null,
        [Description("Mois 1-12.")] byte? mois = null,
        [Description("Libellé item AE.")] string? libelleItemAE = null,
        [Description("Item BI.")] long? idItemBI = null,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "get_budget_execution", new { idVersion, idUB, idExercice, idTypeBudget, codeType }, async () =>
        {
            if (idVersion is not > 0 || idUB is not > 0)
                return "Indiquez idVersion et idUB. Pour les crédits (engagé/disponible), ajoutez idExercice et idTypeBudget.";

            var detail = await api.GetJsonAsync(
                "/api/v1/suivi-previsions/ub-detail" + BudgetWebApiClient.Query(
                    ("idVersion", idVersion), ("idUB", idUB)),
                cancellationToken);

            string? lignes = null;
            if (!string.IsNullOrWhiteSpace(codeType))
            {
                var code = codeType.Trim().ToUpperInvariant();
                if (code is not ("DC" or "AE" or "BI"))
                    return "codeType doit valoir DC, AE ou BI.";
                lignes = await api.GetJsonAsync(
                    "/api/v1/suivi-previsions/ub-detail/lignes" + BudgetWebApiClient.Query(
                        ("idVersion", idVersion), ("idUB", idUB), ("codeType", code)),
                    cancellationToken);
            }

            string? credits = null;
            if (idExercice is > 0 && idTypeBudget is > 0)
            {
                credits = await api.GetJsonAsync(
                    "/api/v1/demandes-paiement/lignes-budgetaires-disponibles" + BudgetWebApiClient.Query(
                        ("idExercice", idExercice),
                        ("idUB", idUB),
                        ("idTypeBudget", idTypeBudget),
                        ("idRubriqueBudgetaire", idRubriqueBudgetaire),
                        ("mois", mois),
                        ("libelleItemAE", ParameterGuard.SanitizeSearch(libelleItemAE)),
                        ("idItemBI", idItemBI)),
                    cancellationToken);
            }

            return $$"""
{"detailVersionUb":{{detail}},"lignes":{{lignes ?? "null"}},"credits":{{credits ?? "null"}},"note":"Les montants proviennent exclusivement des calculs Budget Web. Le MCP n'invente pas de taux d'exécution."}
""";
        }, cancellationToken);

    [McpServerTool(Name = "get_engagements"), Description(
        "Retourne les crédits engagés d'une ligne budgétaire (CreditEngageAnnuel / mensuel) " +
        "calculés par Budget Web. Mêmes paramètres que get_credits_disponibles.")]
    public Task<string> GetEngagements(
        [Description("Exercice (obligatoire).")] long? idExercice = null,
        [Description("UB (obligatoire).")] long? idUB = null,
        [Description("Type de budget (obligatoire).")] long? idTypeBudget = null,
        [Description("Rubrique budgétaire.")] long? idRubriqueBudgetaire = null,
        [Description("Mois 1-12.")] byte? mois = null,
        [Description("Libellé item AE.")] string? libelleItemAE = null,
        [Description("Item BI.")] long? idItemBI = null,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "get_engagements", new { idExercice, idUB, idTypeBudget, idRubriqueBudgetaire, mois }, async () =>
        {
            var exercice = ParameterGuard.RequirePositive(idExercice, "idExercice");
            var ub = ParameterGuard.RequirePositive(idUB, "idUB");
            var type = ParameterGuard.RequirePositive(idTypeBudget, "idTypeBudget");
            return await api.GetJsonAsync(
                "/api/v1/demandes-paiement/lignes-budgetaires-disponibles" + BudgetWebApiClient.Query(
                    ("idExercice", exercice),
                    ("idUB", ub),
                    ("idTypeBudget", type),
                    ("idRubriqueBudgetaire", idRubriqueBudgetaire),
                    ("mois", mois),
                    ("libelleItemAE", ParameterGuard.SanitizeSearch(libelleItemAE)),
                    ("idItemBI", idItemBI)),
                cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "get_previsions"), Description(
        "Consulte les prévisions accessibles. " +
        "Défaut : mes-previsions (périmètre utilisateur). " +
        "Si idVersion (et idéalement idUB) : lignes de prévisions-budgetaires filtrées par l'API.")]
    public Task<string> GetPrevisions(
        [Description("Exercice.")] long? idExercice = null,
        [Description("Version.")] long? idVersion = null,
        [Description("UB — fortement recommandé.")] long? idUB = null,
        [Description("Département.")] long? idDepartement = null,
        [Description("Type de budget.")] long? idTypeBudget = null,
        [Description("Statut de suivi.")] string? statut = null,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "get_previsions", new { idExercice, idVersion, idUB, idDepartement }, async () =>
        {
            if (idVersion is > 0)
            {
                var json = await api.GetJsonAsync(
                    "/api/v1/previsions-budgetaires" + BudgetWebApiClient.Query(
                        ("idVersion", idVersion),
                        ("idTypeBudget", idTypeBudget),
                        ("idUB", idUB)),
                    cancellationToken);
                var page = ToolRunner.Page(skip, take);
                return JsonSlice.Paginate(json, page.Skip, page.Take, out _, out _);
            }

            var liste = await api.GetJsonAsync(
                "/api/v1/suivi-previsions/mes-previsions" + BudgetWebApiClient.Query(
                    ("idExercice", idExercice),
                    ("idVersion", idVersion),
                    ("idDepartement", idDepartement),
                    ("statut", statut)),
                cancellationToken);
            var p = ToolRunner.Page(skip, take);
            return JsonSlice.Paginate(liste, p.Skip, p.Take, out _, out _);
        }, cancellationToken);
}
