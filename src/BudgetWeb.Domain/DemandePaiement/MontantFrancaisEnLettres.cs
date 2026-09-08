using System.Globalization;
using System.Text;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Conversion montant décimal → texte français (francs congolais).</summary>
public static class MontantFrancaisEnLettres
{
    private static readonly string[] Unites =
    [
        "zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf",
        "dix", "onze", "douze", "treize", "quatorze", "quinze", "seize", "dix-sept", "dix-huit", "dix-neuf",
    ];

    private static readonly string[] Dizaines =
    [
        "", "", "vingt", "trente", "quarante", "cinquante", "soixante", "soixante", "quatre-vingt", "quatre-vingt",
    ];

    public static string Convertir(decimal montant, string deviseLibelle = "francs congolais")
    {
        if (montant < 0m)
            throw new ArgumentOutOfRangeException(nameof(montant));

        var entier = decimal.Truncate(montant);
        var cents = decimal.Round((montant - entier) * 100m, 0, MidpointRounding.AwayFromZero);

        var sb = new StringBuilder();
        if (entier == 0m)
            sb.Append("zéro");
        else
            sb.Append(ConvertirEntier((long)entier));

        sb.Append(' ').Append(deviseLibelle);
        if (cents > 0m)
        {
            sb.Append(" et ").Append(ConvertirEntier((long)cents)).Append(" centimes");
        }

        var texte = sb.ToString();
        return char.ToUpper(texte[0], CultureInfo.GetCultureInfo("fr-FR")) + texte[1..];
    }

    private static string ConvertirEntier(long n)
    {
        if (n < 20)
            return Unites[n];
        if (n < 100)
            return ConvertirDeuxChiffres(n);
        if (n < 1_000)
            return ConvertirCentaines(n);
        if (n < 1_000_000)
            return ConvertirGroupe(n, 1_000, "mille", false);
        if (n < 1_000_000_000)
            return ConvertirGroupe(n, 1_000_000, "million", true);

        return ConvertirGroupe(n, 1_000_000_000, "milliard", true);
    }

    private static string ConvertirDeuxChiffres(long n)
    {
        if (n < 20)
            return Unites[n];

        var diz = (int)(n / 10);
        var uni = (int)(n % 10);

        if (diz == 7 || diz == 9)
        {
            var baseDiz = diz == 7 ? "soixante" : "quatre-vingt";
            var reste = diz == 7 ? 10 + uni : 10 + uni;
            return reste == 11 && diz == 7
                ? "soixante et onze"
                : $"{baseDiz}-{Unites[reste]}";
        }

        if (diz == 8 && uni == 0)
            return "quatre-vingts";

        if (uni == 0)
            return Dizaines[diz];

        if (uni == 1 && diz != 8)
            return $"{Dizaines[diz]} et un";

        return $"{Dizaines[diz]}-{Unites[uni]}";
    }

    private static string ConvertirCentaines(long n)
    {
        var c = (int)(n / 100);
        var reste = n % 100;
        var prefix = c switch
        {
            0 => string.Empty,
            1 => "cent",
            _ => $"{Unites[c]} cent",
        };

        if (reste == 0)
            return c > 1 ? $"{prefix}s" : prefix;

        return string.IsNullOrEmpty(prefix)
            ? ConvertirEntier(reste)
            : $"{prefix} {ConvertirEntier(reste)}";
    }

    private static string ConvertirGroupe(long n, long diviseur, string libelle, bool plurielPossible)
    {
        var quotient = n / diviseur;
        var reste = n % diviseur;
        var part = quotient == 1 && diviseur == 1_000
            ? "mille"
            : $"{ConvertirEntier(quotient)} {libelle}{(quotient > 1 && plurielPossible ? "s" : string.Empty)}";

        if (reste == 0)
            return part;

        return $"{part} {ConvertirEntier(reste)}";
    }
}
