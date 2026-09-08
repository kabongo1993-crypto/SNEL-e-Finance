namespace BudgetWeb.Domain.Enums;

/// <summary>Statut des documents instrument de paiement (pièce caisse, bon provisoire, minute chèque).</summary>
public static class StatutDocumentInstrumentPaiement
{
    public const string Etabli = "ETABLI";

    public static string Normaliser(string? statut)
        => string.IsNullOrWhiteSpace(statut) ? string.Empty : statut.Trim().ToUpperInvariant();
}
