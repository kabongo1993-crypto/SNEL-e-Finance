namespace BudgetWeb.Domain.Enums;

/// <summary>Statut du billet de conversion (dpm.BILLET_CONVERSION).</summary>
public static class StatutBilletConversion
{
    public const string Etabli = "ETABLI";

    public static readonly IReadOnlyList<string> Valeurs = [Etabli];

    public static bool IsValid(string? value)
        => Valeurs.Contains(Normaliser(value), StringComparer.OrdinalIgnoreCase);

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}
