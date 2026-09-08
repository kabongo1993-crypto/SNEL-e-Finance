namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Invariants du référentiel Demandeur (sans accès SQL).</summary>
public static class DemandeurRules
{
    public const int CodeMaxLength = 60;
    public const int LibelleMaxLength = 200;

    public static string NormaliserCode(string? code)
    {
        var value = (code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Le code demandeur est obligatoire.", nameof(code));
        if (value.Length > CodeMaxLength)
            throw new ArgumentException($"Le code demandeur ne peut pas dépasser {CodeMaxLength} caractères.", nameof(code));
        return value.ToUpperInvariant();
    }

    public static string NormaliserLibelle(string? libelle)
    {
        var value = (libelle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Le libellé demandeur est obligatoire.", nameof(libelle));
        if (value.Length > LibelleMaxLength)
            throw new ArgumentException($"Le libellé demandeur ne peut pas dépasser {LibelleMaxLength} caractères.", nameof(libelle));
        return value;
    }

    public static void ExigerActif(bool actif)
    {
        if (!actif)
            throw new InvalidOperationException("Le demandeur est inactif.");
    }

    public static void ExigerUb(long fkUniteBudgetaire)
    {
        if (fkUniteBudgetaire <= 0)
            throw new ArgumentException("L'unité budgétaire du demandeur est obligatoire.");
    }

    /// <summary>
    /// La DPM doit porter l'UB du demandeur. Une UB client contradictoire est rejetée.
    /// </summary>
    public static void ExigerCoherenceUbDemandeur(long fkUbDemandeur, long? fkUbProposee)
    {
        if (fkUbDemandeur <= 0)
            throw new ArgumentException("L'UB du demandeur est invalide.");

        if (fkUbProposee is long proposee && proposee > 0 && proposee != fkUbDemandeur)
            throw new InvalidOperationException(
                "L'unité budgétaire de la demande doit être celle du demandeur sélectionné.");
    }
}
