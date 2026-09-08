using System.ComponentModel;
using System.Text.Json;
using BudgetWeb.Mcp.Client;
using BudgetWeb.Mcp.Security;
using ModelContextProtocol.Server;

namespace BudgetWeb.Mcp.Tools;

[McpServerToolType]
public sealed class RapportTools(BudgetWebApiClient api)
{
    [McpServerTool(Name = "get_rapport_previsions"), Description(
        "Retourne un rapport de prévisions Budget Web existant (JSON, pas PDF). " +
        "typeRapport : dc | ae | bi | dc-consolide. " +
        "idVersion obligatoire. " +
        "Les rapports d'organisation (sans idUB) ne sont autorisés que si l'utilisateur a " +
        "versions.controler, versions.valider, versions.rejeter ou admin.all. " +
        "Sinon idUB est obligatoire et doit appartenir aux UB accessibles.")]
    public Task<string> GetRapport(
        [Description("dc, ae, bi ou dc-consolide.")] string? typeRapport = null,
        [Description("Version budgétaire (obligatoire).")] long? idVersion = null,
        [Description("Niveau du rapport (ex. UB, DIVISION) — selon l'écran Budget Web.")] string? niveau = null,
        [Description("Mois du rapport AE (obligatoire pour ae).")] string? mois = null,
        [Description("UB — obligatoire sauf profil contrôle/validation.")] long? idUB = null,
        [Description("Entité organisationnelle.")] long? idEntite = null,
        [Description("Département structure.")] long? idDepartementStructure = null,
        [Description("Division.")] long? idDivision = null,
        [Description("Statut de consultation.")] string? statutConsultation = null,
        CancellationToken cancellationToken = default)
        => ToolRunner.Run(api, "get_rapport_previsions", new { typeRapport, idVersion, idUB, niveau }, async () =>
        {
            var type = (typeRapport ?? string.Empty).Trim().ToLowerInvariant();
            if (type is not ("dc" or "ae" or "bi" or "dc-consolide"))
                return "typeRapport doit valoir dc, ae, bi ou dc-consolide.";
            var version = ParameterGuard.RequirePositive(idVersion, "idVersion");

            if (!await PeutVoirRapportOrgAsync(cancellationToken))
            {
                if (idUB is not > 0)
                    return "idUB est obligatoire pour ce rapport (périmètre utilisateur).";
                if (!await UbEstAccessibleAsync(idUB.Value, cancellationToken))
                    return "UB hors périmètre.";
            }

            if (type == "ae" && string.IsNullOrWhiteSpace(mois))
                return "Le rapport AE exige le paramètre mois.";

            var path = type switch
            {
                "dc" => "/api/v1/rapports/previsions-dc",
                "ae" => "/api/v1/rapports/previsions-ae",
                "bi" => "/api/v1/rapports/previsions-bi",
                _ => "/api/v1/rapports/previsions-dc-consolide"
            };

            return await api.GetJsonAsync(path + BudgetWebApiClient.Query(
                ("idVersion", version),
                ("niveau", ParameterGuard.SanitizeSearch(niveau) ?? "UB"),
                ("mois", ParameterGuard.SanitizeSearch(mois)),
                ("idEntite", idEntite),
                ("idDepartementStructure", idDepartementStructure),
                ("idDivision", idDivision),
                ("idUB", idUB),
                ("statutConsultation", ParameterGuard.SanitizeSearch(statutConsultation))),
                cancellationToken);
        }, cancellationToken);

    private async Task<bool> PeutVoirRapportOrgAsync(CancellationToken cancellationToken)
    {
        var me = await api.GetJsonAsync("/api/v1/auth/me", cancellationToken);
        try
        {
            using var doc = JsonDocument.Parse(me);
            if (!TryGetPermissions(doc.RootElement, out var perms))
                return false;
            return perms.Contains("admin.all")
                   || perms.Contains("versions.controler")
                   || perms.Contains("versions.valider")
                   || perms.Contains("versions.rejeter");
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<bool> UbEstAccessibleAsync(long idUB, CancellationToken cancellationToken)
    {
        var json = await api.GetJsonAsync("/api/v1/unites-budgetaires?accessibles=true", cancellationToken);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("idUB", out var id) && id.TryGetInt64(out var v) && v == idUB)
                    return true;
                if (item.TryGetProperty("IdUB", out id) && id.TryGetInt64(out v) && v == idUB)
                    return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryGetPermissions(JsonElement root, out HashSet<string> perms)
    {
        perms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        JsonElement user = root;
        if (root.TryGetProperty("utilisateur", out var u) || root.TryGetProperty("Utilisateur", out u))
            user = u;
        if (!user.TryGetProperty("permissions", out var p) && !user.TryGetProperty("Permissions", out p))
            return false;
        if (p.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var item in p.EnumerateArray())
        {
            var s = item.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                perms.Add(s);
        }

        return true;
    }
}
