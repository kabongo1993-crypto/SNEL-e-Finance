namespace BudgetWeb.Domain.Enums;

public static class StatutAjustementBudgetaire
{
    public const string Brouillon = "BROUILLON";
    public const string Valide = "VALIDE";
    public const string Annule = "ANNULE";

    public static readonly IReadOnlyList<string> ValeursAutorisees =
    [
        Brouillon,
        Valide,
        Annule
    ];

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && ValeursAutorisees.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}
