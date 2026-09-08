namespace BudgetWeb.Domain.Enums;

public static class StatutVersionBudgetaire
{
    public const string Brouillon = "BROUILLON";
    public const string Soumise = "SOUMISE";
    public const string Controlee = "CONTROLEE";
    public const string Validee = "VALIDEE";
    public const string Rejetee = "REJETEE";

    public static readonly IReadOnlyList<string> ValeursAutorisees =
    [
        Brouillon,
        Soumise,
        Controlee,
        Validee,
        Rejetee
    ];

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && ValeursAutorisees.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Transitions métier autorisées (workflow version).</summary>
    public static bool EstTransitionAutorisee(string? de, string? vers)
    {
        var from = Normaliser(de);
        var to = Normaliser(vers);
        return (from, to) switch
        {
            (Brouillon, Soumise) => true,
            (Rejetee, Soumise) => true,
            (Rejetee, Brouillon) => true,
            (Soumise, Controlee) => true,
            (Soumise, Rejetee) => true,
            (Soumise, Brouillon) => true,
            (Controlee, Validee) => true,
            (Controlee, Rejetee) => true,
            _ => false
        };
    }
}
