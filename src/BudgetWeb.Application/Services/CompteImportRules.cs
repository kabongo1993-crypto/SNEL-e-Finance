using System.Globalization;
using System.Text;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Services;

/// <summary>
/// Règles d'interprétation de l'Excel historique COMPTE.xlsx.
/// Ne crée aucun référentiel manquant. Alias devises limités aux codes ISO déjà présents.
/// </summary>
public static class CompteImportRules
{
    public static readonly IReadOnlyDictionary<string, string> AliasDevises =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EURO"] = "EUR",
            ["EUR"] = "EUR",
            ["FCFA"] = "CFA",
            ["CFA"] = "CFA",
            ["XAF"] = "CFA",
            ["CDF"] = "CDF",
            ["USD"] = "USD",
        };

    public static string Fold(string? value)
    {
        var s = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    public static long? ParseIdCompte(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0) return null;
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
            return id;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            && serial > 0
            && Math.Abs(serial - Math.Round(serial)) < 0.0000001)
            return (long)Math.Round(serial);
        return null;
    }

    public static DateTime? ParseExcelDate(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0) return null;

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            || double.TryParse(s, NumberStyles.Float, CultureInfo.GetCultureInfo("fr-FR"), out serial))
        {
            if (serial is >= 1 and < 2)
                return null;
            if (serial is >= 60 and <= 60000)
            {
                var dt = DateTime.FromOADate(serial);
                return dt.Year < 1990 ? null : DateTime.SpecifyKind(dt.Date, DateTimeKind.Unspecified);
            }
        }

        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "yyyyMMdd"];
        if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || DateTime.TryParseExact(s, formats, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out parsed))
        {
            return parsed.Year < 1990 ? null : parsed.Date;
        }

        return null;
    }

    public static bool EstDateSentinelle(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0) return true;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 1 and < 2)
            return true;
        return ParseExcelDate(raw) is null && s is "0" or "1";
    }

    /// <summary>
    /// Access historique : Etat=0 et Date_Cloture=1 (sentinelle) = compte actif.
    /// Une vraie date de clôture (≥ 1990) force l'inactivité.
    /// </summary>
    public static (bool Actif, DateOnly? DateCloture) InterpretEtat(string? etatRaw, string? dateClotureRaw)
    {
        var cloture = EstDateSentinelle(dateClotureRaw) ? null : ParseExcelDate(dateClotureRaw);
        var dateOnly = cloture is null ? (DateOnly?)null : DateOnly.FromDateTime(cloture.Value);

        if (dateOnly is not null)
            return (false, dateOnly);

        var etat = (etatRaw ?? string.Empty).Trim();
        if (etat is "1" or "-1" or "Inactif" or "INACTIF" or "false" or "False")
            return (false, DateOnly.FromDateTime(DateTime.UtcNow.Date));

        return (true, null);
    }

    public static Devise? ResolveDevise(string? raw, IReadOnlyList<Devise> devises)
    {
        var folded = Fold(raw);
        if (folded.Length == 0) return null;
        if (AliasDevises.TryGetValue(folded, out var alias))
            folded = alias;
        return devises.FirstOrDefault(d => Fold(d.Code) == folded);
    }

    public static Banque? ResolveBanque(string? raw, IReadOnlyList<Banque> banques)
    {
        var folded = Fold(raw);
        if (folded.Length == 0) return null;
        return banques.FirstOrDefault(b => Fold(b.IdBanque) == folded);
    }

    public static Province? ResolveProvince(string? raw, IReadOnlyList<Province> provinces)
    {
        var folded = Fold(raw);
        if (folded.Length == 0) return null;
        return provinces.FirstOrDefault(p => Fold(p.IdProvince) == folded);
    }

    public static DirectionTresorerie? ResolveDirection(string? raw, IReadOnlyList<DirectionTresorerie> directions)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0) return null;
        var id = ParseIdCompte(s);
        if (id is long parsed)
            return directions.FirstOrDefault(d => d.IdDirection == parsed);
        var folded = Fold(s);
        return directions.FirstOrDefault(d => Fold(d.Libelle) == folded);
    }

    public static TypeCompte? ResolveTypeCompte(string? raw, IReadOnlyList<TypeCompte> types)
    {
        var folded = Fold(raw);
        if (folded.Length == 0) return null;

        var exactCode = types.FirstOrDefault(t => Fold(t.Code) == folded);
        if (exactCode is not null) return exactCode;

        var exactLibelle = types.FirstOrDefault(t => Fold(t.Libelle) == folded);
        if (exactLibelle is not null) return exactLibelle;

        if (folded.Length >= 6)
        {
            var prefix = types.Where(t => Fold(t.Code).StartsWith(folded, StringComparison.Ordinal)).ToList();
            if (prefix.Count == 1) return prefix[0];
        }

        return null;
    }
}
