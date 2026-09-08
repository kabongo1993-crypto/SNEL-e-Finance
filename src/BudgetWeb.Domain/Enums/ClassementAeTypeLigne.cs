namespace BudgetWeb.Domain.Enums;

public static class ClassementAeTypeLigne
{
    public const string Groupe = "GROUPE";
    public const string Item = "ITEM";

    public static bool IsValid(string? value)
        => string.Equals(value, Groupe, StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, Item, StringComparison.OrdinalIgnoreCase);

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}
