namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Validation des montants et conversion devise → USD (MontantUsd = MontantBrut / TauxConversion).</summary>
public static class DemandePaiementMontants
{
    public const decimal ToleranceUsd = 0.01m;

    public static decimal CalculerMontantUsd(decimal montantBrut, decimal tauxConversion)
        => montantBrut / tauxConversion;

    public static void Valider(decimal montantBrut, string devise, decimal tauxConversion, decimal montantUsd)
    {
        if (montantBrut < 0)
            throw new ArgumentException("Le montant brut ne peut pas être négatif.", nameof(montantBrut));

        if (tauxConversion <= 0)
            throw new ArgumentException("Le taux de conversion doit être strictement positif.", nameof(tauxConversion));

        if (montantUsd < 0)
            throw new ArgumentException("Le montant USD ne peut pas être négatif.", nameof(montantUsd));

        ValiderDevise(devise);

        var attendu = CalculerMontantUsd(montantBrut, tauxConversion);
        if (Math.Abs(montantUsd - attendu) >= ToleranceUsd)
            throw new ArgumentException(
                $"Incohérence MontantUsd : attendu {attendu}, reçu {montantUsd}.",
                nameof(montantUsd));
    }

    public static void ValiderDevise(string? devise)
    {
        var normalisee = NormaliserCodeDevise(devise);
        if (normalisee.Length != 3)
            throw new ArgumentException("La devise doit comporter exactement 3 caractères.", nameof(devise));
    }

    /// <summary>Normalise EURO → EUR et majuscules.</summary>
    public static string NormaliserCodeDevise(string? devise)
    {
        var n = (devise ?? string.Empty).Trim().ToUpperInvariant();
        return n is "EURO" ? "EUR" : n;
    }
}
