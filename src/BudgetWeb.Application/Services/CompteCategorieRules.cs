using System.Globalization;

namespace BudgetWeb.Application.Services;

/// <summary>
/// Règles d'affectation historique compte ↔ catégorie et d'interprétation de COMPTE_CATEGORIE.xlsx.
/// </summary>
public static class CompteCategorieRules
{
    public const string MessageConflitActif =
        "Ce compte possède déjà une affectation active pour la période concernée.";

    public const string MessageChevauchement =
        "Ce compte possède déjà une affectation pour la période concernée.";

    public static bool PeriodesSeChevauchent(DateOnly debutA, DateOnly? finA, DateOnly debutB, DateOnly? finB)
    {
        var finEffectiveA = finA ?? DateOnly.MaxValue;
        var finEffectiveB = finB ?? DateOnly.MaxValue;
        return debutA <= finEffectiveB && debutB <= finEffectiveA;
    }

    public static void ValiderPeriode(DateOnly dateDebut, DateOnly? dateFin)
    {
        if (dateFin is DateOnly fin && fin < dateDebut)
            throw new ArgumentException("La date de fin doit être postérieure ou égale à la date de début.");
    }

    public static string MessageConflit(bool autreActive, bool nouvelleActive)
        => autreActive || nouvelleActive ? MessageConflitActif : MessageChevauchement;

    public static long? ParseId(string? raw) => CompteImportRules.ParseIdCompte(raw);

    /// <summary>
    /// Date_Fin historique 1900-01-01 (ou sérial Excel 1) = absence de fin.
    /// </summary>
    public static bool EstDateFinSentinelle1900(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0) return false;

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            || double.TryParse(s, NumberStyles.Float, CultureInfo.GetCultureInfo("fr-FR"), out serial))
        {
            if (serial is >= 1 and < 2)
                return true;
        }

        if (TryParseDateBrute(s, out var date)
            && ((date.Year == 1900 && date.Month == 1 && date.Day == 1)
                || (date.Year == 1899 && date.Month == 12 && date.Day >= 30)))
            return true;

        return false;
    }

    public static DateOnly? ParseDate(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0) return null;
        return TryParseDateBrute(s, out var date) ? date : null;
    }

    public static DateOnly? ParseDateFinImport(string? raw, out bool sentinelleConvertie)
    {
        sentinelleConvertie = EstDateFinSentinelle1900(raw);
        if (sentinelleConvertie)
            return null;
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return ParseDate(raw);
    }

    private static bool TryParseDateBrute(string s, out DateOnly date)
    {
        date = default;

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            || double.TryParse(s, NumberStyles.Float, CultureInfo.GetCultureInfo("fr-FR"), out serial))
        {
            if (serial is >= 1 and < 2)
            {
                date = new DateOnly(1900, 1, 1);
                return true;
            }

            if (serial is >= 2 and <= 80000)
            {
                var dt = DateTime.FromOADate(serial);
                date = DateOnly.FromDateTime(dt.Date);
                return true;
            }
        }

        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "yyyyMMdd", "dd-MM-yyyy"];
        if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || DateTime.TryParseExact(s, formats, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out parsed)
            || DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            || DateTime.TryParse(s, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out parsed))
        {
            date = DateOnly.FromDateTime(parsed.Date);
            return true;
        }

        return false;
    }

    public static string FormaterDate(DateOnly? date)
        => date is DateOnly d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
}
