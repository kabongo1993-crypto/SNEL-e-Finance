namespace BudgetWeb.Domain.Enums;

public static class TypeBeneficiaireDemandePaiement
{
    public const string Agent = "AGENT";
    public const string Tiers = "TIERS";
    public const string Divers = "DIVERS";

    public static readonly IReadOnlyList<string> ValeursAutorisees =
    [
        Agent,
        Tiers,
        Divers
    ];

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && ValeursAutorisees.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}
