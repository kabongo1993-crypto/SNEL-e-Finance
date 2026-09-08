using System.ComponentModel;
using BudgetWeb.Mcp.Client;
using BudgetWeb.Mcp.Security;
using ModelContextProtocol.Server;

namespace BudgetWeb.Mcp.Tools;

[McpServerToolType]
public sealed class ReferentielTools(BudgetWebApiClient api)
{
    [McpServerTool(Name = "list_referentiels"), Description(
        "Retourne les référentiels nécessaires pour formuler une demande : " +
        "exercices, unités budgétaires accessibles (périmètre), départements, types de budget, devises, versions. " +
        "catalogue = exercices | unites-budgetaires | departements | types-budget | devises | versions-budgetaires | rubriques-budgetaires | moi. " +
        "Les UB sont toujours demandées avec accessibles=true.")]
    public Task<string> ListReferentiels(
        [Description("Nom du catalogue (voir description).")] string? catalogue = null,
        [Description("Contexte UB : dpm, prevision, etc. si supporté par l'API.")] string? contexte = null,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "list_referentiels", new { catalogue, contexte }, async () =>
        {
            var name = (catalogue ?? string.Empty).Trim().ToLowerInvariant();
            var path = name switch
            {
                "exercices" => "/api/v1/exercices",
                "unites-budgetaires" or "ub" or "" =>
                    "/api/v1/unites-budgetaires?accessibles=true"
                    + (string.IsNullOrWhiteSpace(contexte) ? string.Empty : "&contexte=" + Uri.EscapeDataString(contexte)),
                "departements" => "/api/v1/departements",
                "types-budget" => "/api/v1/types-budget",
                "devises" => "/api/v1/devises",
                "versions-budgetaires" or "versions" => "/api/v1/versions-budgetaires",
                "rubriques-budgetaires" or "rubriques" => "/api/v1/rubriques-budgetaires",
                "moi" or "me" => "/api/v1/auth/me",
                _ => null
            };

            if (path is null)
            {
                return "catalogue inconnu. Valeurs : exercices, unites-budgetaires, departements, types-budget, devises, versions-budgetaires, rubriques-budgetaires, moi.";
            }

            var json = await api.GetJsonAsync(path, cancellationToken);
            if (name is "moi" or "me")
                return json;
            var page = ToolRunner.Page(skip, take);
            return JsonSlice.Paginate(json, page.Skip, page.Take, out _, out _);
        }, cancellationToken);
}
