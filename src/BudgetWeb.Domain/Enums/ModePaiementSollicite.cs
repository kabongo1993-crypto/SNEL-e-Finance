namespace BudgetWeb.Domain.Enums;

/// <summary>Mode de paiement sollicité sur la demande (phase 1).</summary>
public static class ModePaiementSollicite
{
    public const string Caisse = "CAISSE";
    public const string Banque = "BANQUE";

    public static readonly IReadOnlyList<string> ValeursAutorisees =
    [
        Caisse,
        Banque
    ];

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && ValeursAutorisees.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}
