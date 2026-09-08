namespace BudgetWeb.Domain.Enums;

/// <summary>
/// Statut de version dans le référentiel taux de change.
/// ACTIF = version courante unique par paire ; INACTIF = historique (toujours éligible par DateEffet).
/// </summary>
public static class StatutTauxChange
{
    public const string Actif = "ACTIF";
    public const string Inactif = "INACTIF";

    public static readonly IReadOnlyList<string> ValeursAutorisees =
    [
        Actif,
        Inactif
    ];

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && ValeursAutorisees.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}
