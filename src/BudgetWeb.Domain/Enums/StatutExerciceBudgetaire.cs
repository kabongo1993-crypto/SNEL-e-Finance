namespace BudgetWeb.Domain.Enums;

public static class StatutExerciceBudgetaire
{
    public const string Ouvert = "OUVERT";
    public const string Cloture = "CLOTURE";

    public static readonly IReadOnlyList<string> ValeursAutorisees =
    [
        Ouvert,
        Cloture
    ];

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && ValeursAutorisees.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}
