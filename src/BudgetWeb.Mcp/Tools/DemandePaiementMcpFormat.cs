using System.Globalization;
using System.Text.Json;

namespace BudgetWeb.Mcp.Tools;

/// <summary>
/// Projection compacte des DPM pour ChatGPT. N'applique aucune règle d'accès :
/// l'API a déjà filtré. Tri métier = DateCreation DESC, IdDemandePaiement DESC.
/// </summary>
internal static class DemandePaiementMcpFormat
{
    private static readonly JsonSerializerOptions JsonOut = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool LooksLikeJsonPayload(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var t = text.TrimStart();
        return t.StartsWith('[') || t.StartsWith('{');
    }

    public static string Search(string apiJson, int skip, int take)
    {
        if (!TryReadRows(apiJson, out var rows))
            return apiJson;

        var ordered = Order(rows);
        var page = ordered.Skip(skip).Take(take).Select(ToSearchItem).ToList();
        return JsonSerializer.Serialize(new
        {
            pagination = new
            {
                skip,
                take,
                total = ordered.Count,
                returned = page.Count,
                orderBy = "dateCreation DESC"
            },
            items = page
        }, JsonOut);
    }

    public static bool TryGetLatestId(string listJson, out long id)
    {
        id = 0;
        if (!TryReadRows(listJson, out var rows) || rows.Count == 0)
            return false;
        id = Order(rows)[0].Id;
        return id > 0;
    }

    public static string Latest(string listJson, string? detailJson)
    {
        if (!TryReadRows(listJson, out var rows))
            return listJson;

        var ordered = Order(rows);
        if (ordered.Count == 0)
            return JsonSerializer.Serialize(new { found = false }, JsonOut);

        var row = ordered[0];
        var beneficiaire = ExtractBeneficiaire(detailJson);
        return JsonSerializer.Serialize(new
        {
            found = true,
            demandePaiement = new
            {
                id = row.Id,
                reference = row.Reference,
                dateEnregistrement = row.DateCreationText,
                dateEmission = row.DateEmissionText,
                beneficiaire,
                demandeur = row.Demandeur,
                objet = row.Objet,
                montant = row.Montant,
                montantUsd = row.MontantUsd,
                devise = row.Devise,
                uniteBudgetaire = row.UniteBudgetaire,
                codeUB = row.CodeUb,
                statut = row.Statut
            }
        }, JsonOut);
    }

    private static List<Row> Order(IEnumerable<Row> rows)
        => rows
            .OrderByDescending(r => r.DateCreation)
            .ThenByDescending(r => r.Id)
            .ToList();

    private static bool TryReadRows(string json, out List<Row> rows)
    {
        rows = [];
        if (!LooksLikeJsonPayload(json))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var el in doc.RootElement.EnumerateArray())
                rows.Add(ReadRow(el));
            return true;
        }
        catch (JsonException)
        {
            rows = [];
            return false;
        }
    }

    private static Row ReadRow(JsonElement el)
    {
        var id = ReadInt64(el, "idDemandePaiement") ?? ReadInt64(el, "IdDemandePaiement") ?? 0;
        var dateCreation = ReadDateTime(el, "dateCreation") ?? ReadDateTime(el, "DateCreation") ?? DateTime.MinValue;
        var dateEmission = ReadDateOnly(el, "dateEmission") ?? ReadDateOnly(el, "DateEmission");
        var codeUb = ReadString(el, "codeUB") ?? ReadString(el, "CodeUB");
        var libelleUb = ReadString(el, "libelleUB") ?? ReadString(el, "LibelleUB");
        var unite = string.Join(" — ", new[] { codeUb, libelleUb }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new Row(
            id,
            ReadString(el, "reference") ?? ReadString(el, "Reference") ?? string.Empty,
            dateCreation,
            FormatDateTime(dateCreation),
            dateEmission,
            ReadString(el, "objet") ?? ReadString(el, "Objet"),
            ReadDecimal(el, "montantBrut") ?? ReadDecimal(el, "MontantBrut"),
            ReadDecimal(el, "montantUsd") ?? ReadDecimal(el, "MontantUsd"),
            ReadString(el, "devise") ?? ReadString(el, "Devise"),
            string.IsNullOrWhiteSpace(unite) ? null : unite,
            codeUb,
            ReadString(el, "statut") ?? ReadString(el, "Statut"),
            ReadString(el, "libelleDemandeur") ?? ReadString(el, "LibelleDemandeur"));
    }

    private static object ToSearchItem(Row row)
        => new
        {
            id = row.Id,
            reference = row.Reference,
            dateEnregistrement = row.DateCreationText,
            dateEmission = row.DateEmissionText,
            objet = row.Objet,
            montant = row.Montant,
            montantUsd = row.MontantUsd,
            devise = row.Devise,
            uniteBudgetaire = row.UniteBudgetaire,
            statut = row.Statut,
            demandeur = row.Demandeur
        };

    internal static string? ExtractBeneficiaire(string? detailJson)
    {
        if (string.IsNullOrWhiteSpace(detailJson) || !LooksLikeJsonPayload(detailJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(detailJson);
            if (!TryGetProperty(doc.RootElement, "beneficiaires", "Beneficiaires", out var arr)
                || arr.ValueKind != JsonValueKind.Array)
                return null;

            string? principal = null;
            var names = new List<string>();
            foreach (var b in arr.EnumerateArray())
            {
                var nom = ReadString(b, "nomComplet") ?? ReadString(b, "NomComplet")
                    ?? ReadString(b, "raisonSociale") ?? ReadString(b, "RaisonSociale");
                if (string.IsNullOrWhiteSpace(nom))
                    continue;
                names.Add(nom);
                var estPrincipal = ReadBool(b, "estPrincipal") ?? ReadBool(b, "EstPrincipal");
                if (estPrincipal == true && principal is null)
                    principal = nom;
            }

            return principal ?? (names.Count == 0 ? null : string.Join(", ", names));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement el, string camel, string pascal, out JsonElement value)
    {
        if (el.TryGetProperty(camel, out value) || el.TryGetProperty(pascal, out value))
            return true;
        value = default;
        return false;
    }

    private static string? ReadString(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool? ReadBool(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && (p.ValueKind is JsonValueKind.True or JsonValueKind.False)
            ? p.GetBoolean()
            : null;

    private static long? ReadInt64(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n))
            return n;
        if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out n))
            return n;
        return null;
    }

    private static decimal? ReadDecimal(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var n))
            return n;
        if (p.ValueKind == JsonValueKind.String
            && decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out n))
            return n;
        return null;
    }

    private static DateTime? ReadDateTime(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString()))
        {
            var s = p.GetString()!;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                return dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt))
                return dt;
        }
        return null;
    }

    private static string? ReadDateOnly(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
            return null;
        return p.GetString();
    }

    private static string? FormatDateTime(DateTime dt)
        => dt == DateTime.MinValue ? null : dt.ToString("o", CultureInfo.InvariantCulture);

    private sealed record Row(
        long Id,
        string Reference,
        DateTime DateCreation,
        string? DateCreationText,
        string? DateEmissionText,
        string? Objet,
        decimal? Montant,
        decimal? MontantUsd,
        string? Devise,
        string? UniteBudgetaire,
        string? CodeUb,
        string? Statut,
        string? Demandeur);
}
