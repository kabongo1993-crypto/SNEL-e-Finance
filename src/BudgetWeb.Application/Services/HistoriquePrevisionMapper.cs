using System.Text.Json;
using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Services;

/// <summary>
/// Reconstruit les événements métier Version×UB à partir des lignes JOURNAL_AUDIT
/// (Entite = WORKFLOW_PREVISION_UB). Ne invente pas d'événements absents du journal.
/// </summary>
public static class HistoriquePrevisionMapper
{
    public static readonly HashSet<string> OperationsWorkflow = new(StringComparer.OrdinalIgnoreCase)
    {
        "SOUMETTRE_UB",
        "CONTROLER_UB",
        "VALIDER_UB",
        "REJET_UB",
        "REOUVRIR_UB",
        "ANNULER_SOUMISSION_UB",
        "SOUMISSION_DEPARTEMENT",
        "CONTROLE_DEPARTEMENT",
        "VALIDATION_DEPARTEMENT",
        "REJET_DEPARTEMENT",
    };

    public static string MapAction(string operation)
    {
        return (operation ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "SOUMETTRE_UB" or "SOUMISSION_DEPARTEMENT" => "SOUMISSION",
            "CONTROLER_UB" or "CONTROLE_DEPARTEMENT" => "CONTROLE",
            "VALIDER_UB" or "VALIDATION_DEPARTEMENT" => "VALIDATION",
            "REJET_UB" or "REJET_DEPARTEMENT" => "REJET",
            "REOUVRIR_UB" => "REOUVERTURE",
            "ANNULER_SOUMISSION_UB" => "ANNULATION_SOUMISSION",
            _ => operation?.Trim().ToUpperInvariant() ?? "INCONNU",
        };
    }

    public static string MapActionLibelle(string action, string portee)
    {
        var a = (action ?? string.Empty).Trim().ToUpperInvariant();
        var p = (portee ?? "UB").Trim().ToUpperInvariant();
        var baseLabel = a switch
        {
            "SOUMISSION" => "SOUMISSION",
            "CONTROLE" => "CONTROLE",
            "VALIDATION" => "VALIDATION",
            "REJET" => "REJET",
            "REOUVERTURE" => "REOUVERTURE",
            "ANNULATION_SOUMISSION" => "ANNULATION SOUMISSION",
            "CREATION" => "CREATION",
            "SAISIE" => "SAISIE",
            "RESOUMISSION" => "RESOUMISSION",
            _ => a,
        };
        return p == "DEPARTEMENT" ? $"{baseLabel} — PORTÉE DÉPARTEMENT" : $"{baseLabel} — PORTÉE UB";
    }

    public static string? InferStatutApresDepartement(string operation)
        => (operation ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "SOUMISSION_DEPARTEMENT" => "SOUMISE",
            "CONTROLE_DEPARTEMENT" => "CONTROLEE",
            "VALIDATION_DEPARTEMENT" => "VALIDEE",
            "REJET_DEPARTEMENT" => "REJETEE",
            _ => null,
        };

    /// <summary>
    /// Expand une ligne journal en 0..N événements Version×UB.
    /// Les opérations AGREGAT / VERSION_BUDGETAIRE ne sont pas traitées ici.
    /// </summary>
    public static IReadOnlyList<HistoriqueAuditExpanded> ExpandJournalRow(
        long idAudit,
        DateTime dateHeure,
        string operation,
        long idUtilisateur,
        string? anciennesValeurs,
        string? nouvellesValeurs)
    {
        if (!OperationsWorkflow.Contains(operation ?? string.Empty))
        {
            return [];
        }

        var action = MapAction(operation ?? string.Empty);

        if (string.IsNullOrWhiteSpace(nouvellesValeurs))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(nouvellesValeurs);
        var root = doc.RootElement;
        var portee = GetString(root, "portee")?.ToUpperInvariant() ?? "UB";
        var op = operation ?? string.Empty;

        if (portee == "DEPARTEMENT")
        {
            return ExpandDepartement(idAudit, dateHeure, op, idUtilisateur, root, action);
        }

        var idVersion = GetLong(root, "idVersion");
        var idUB = GetLong(root, "idUB");
        if (idVersion is null or <= 0 || idUB is null or <= 0)
        {
            return [];
        }

        var statutAvant = GetString(root, "statutAvant");
        if (string.IsNullOrWhiteSpace(statutAvant) && !string.IsNullOrWhiteSpace(anciennesValeurs))
        {
            try
            {
                using var anc = JsonDocument.Parse(anciennesValeurs);
                statutAvant = GetString(anc.RootElement, "statut");
            }
            catch (JsonException)
            {
                // ignore
            }
        }

        var motif = GetString(root, "motif");
        if (string.IsNullOrWhiteSpace(motif) && root.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Object)
        {
            motif = GetString(extra, "motif");
        }

        return
        [
            new HistoriqueAuditExpanded(
                idAudit,
                dateHeure,
                op,
                action,
                "UB",
                idVersion.Value,
                idUB.Value,
                GetLong(root, "idDepartement"),
                statutAvant,
                GetString(root, "statutApres"),
                motif,
                idUtilisateur),
        ];
    }

    private static List<HistoriqueAuditExpanded> ExpandDepartement(
        long idAudit,
        DateTime dateHeure,
        string operation,
        long idUtilisateur,
        JsonElement root,
        string action)
    {
        var idVersion = GetLong(root, "idVersion");
        if (idVersion is null or <= 0)
        {
            return [];
        }

        var idDepartement = GetLong(root, "idDepartement");
        var motif = GetString(root, "motif");
        var statutApres = InferStatutApresDepartement(operation);
        var list = new List<HistoriqueAuditExpanded>();

        if (!root.TryGetProperty("ub", out var ubArr) || ubArr.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var ub in ubArr.EnumerateArray())
        {
            var idUB = GetLong(ub, "idUB");
            if (idUB is null or <= 0) continue;

            list.Add(new HistoriqueAuditExpanded(
                idAudit,
                dateHeure,
                operation,
                action,
                "DEPARTEMENT",
                idVersion.Value,
                idUB.Value,
                idDepartement,
                GetString(ub, "statutAvant"),
                statutApres,
                motif,
                idUtilisateur));
        }

        return list;
    }

    public static IReadOnlyList<string> OperationsForActionFilter(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return OperationsWorkflow.ToList();
        }

        return action.Trim().ToUpperInvariant() switch
        {
            "SOUMISSION" or "RESOUMISSION" => ["SOUMETTRE_UB", "SOUMISSION_DEPARTEMENT"],
            "CONTROLE" => ["CONTROLER_UB", "CONTROLE_DEPARTEMENT"],
            "VALIDATION" => ["VALIDER_UB", "VALIDATION_DEPARTEMENT"],
            "REJET" => ["REJET_UB", "REJET_DEPARTEMENT"],
            "REOUVERTURE" => ["REOUVRIR_UB"],
            "ANNULATION_SOUMISSION" => ["ANNULER_SOUMISSION_UB"],
            _ => OperationsWorkflow.ToList(),
        };
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            JsonValueKind.Null => null,
            _ => p.ToString(),
        };
    }

    private static long? GetLong(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n)) return n;
        if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out var s)) return s;
        return null;
    }
}

/// <summary>Événement intermédiaire avant enrichissement référentiel / montants.</summary>
public record HistoriqueAuditExpanded(
    long IdAudit,
    DateTime DateHeure,
    string Operation,
    string Action,
    string Portee,
    long IdVersion,
    long IdUB,
    long? IdDepartement,
    string? AncienStatut,
    string? NouveauStatut,
    string? Motif,
    long IdUtilisateur);
