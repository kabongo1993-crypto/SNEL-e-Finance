namespace BudgetWeb.Domain.DetailBI;

public static class DetailBILibelle
{
    public static string Normaliser(string? libelle)
    {
        if (string.IsNullOrWhiteSpace(libelle))
            return string.Empty;

        return string.Join(
            ' ',
            libelle.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool SontEquivalent(string? a, string? b)
        => string.Equals(Normaliser(a), Normaliser(b), StringComparison.OrdinalIgnoreCase);
}
