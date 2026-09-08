namespace BudgetWeb.Application.Rapports.Common;

/// <summary>
/// Abréviation d'affichage pour la colonne MODE des rapports de prévisions.
/// Ne remplace pas les codes métier ANNUEL/MENSUEL utilisés par les calculs.
/// </summary>
public static class RapportModeAffichage
{
    /// <summary>ANNUEL → A, MENSUEL → M. Autres valeurs : inchangées (trim/upper).</summary>
    public static string Abbreviate(string? codeMode)
    {
        if (string.IsNullOrWhiteSpace(codeMode))
        {
            return string.Empty;
        }

        var c = codeMode.Trim().ToUpperInvariant();
        return c switch
        {
            "ANNUEL" or "ANN" or "A" => "A",
            "MENSUEL" or "MENS" or "M" => "M",
            _ => c,
        };
    }
}
