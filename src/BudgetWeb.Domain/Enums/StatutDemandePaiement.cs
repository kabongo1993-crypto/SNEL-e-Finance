namespace BudgetWeb.Domain.Enums;

/// <summary>Statuts DPM jusqu'au visa budgétaire (hors Trésorerie).</summary>
public static class StatutDemandePaiement
{
    public const string Brouillon = "BROUILLON";
    /// <summary>En attente de validation Responsable N1 (entité émettrice).</summary>
    public const string EnValidationN1 = "EN_VALIDATION_N1";
    /// <summary>En attente de validation Responsable N2 (entité émettrice).</summary>
    public const string EnValidationN2 = "EN_VALIDATION_N2";
    /// <summary>Validations N1+N2 satisfaites — prête pour soumission Budget.</summary>
    public const string ValideeEntite = "VALIDEE_ENTITE";
    public const string Soumise = "SOUMISE";
    public const string EnTraitementDpm = "EN_TRAITEMENT_DPM";
    /// <summary>Ancien statut — normalisé vers <see cref="EnTraitementDpm"/>.</summary>
    public const string ReceptionneeBudgets = "RECEPTIONNEE_BUDGETS";
    public const string EnControleBudgetaire = "EN_CONTROLE_BUDGETAIRE";
    public const string ACorriger = "A_CORRIGER";
    public const string ViseeBudgetairement = "VISEE_BUDGETAIREMENT";

    public static readonly IReadOnlyList<string> ValeursAutorisees =
    [
        Brouillon,
        EnValidationN1,
        EnValidationN2,
        ValideeEntite,
        Soumise,
        EnTraitementDpm,
        EnControleBudgetaire,
        ACorriger,
        ViseeBudgetairement
    ];

    public static bool IsValid(string? value)
        => ValeursAutorisees.Contains(Normaliser(value), StringComparer.OrdinalIgnoreCase);

    public static string Normaliser(string? value)
    {
        var n = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (string.Equals(n, ReceptionneeBudgets, StringComparison.Ordinal))
            return EnTraitementDpm;
        return n;
    }

    public static bool EstTransitionAutorisee(string? de, string? vers)
    {
        var from = Normaliser(de);
        var to = Normaliser(vers);
        return (from, to) switch
        {
            (Brouillon, EnValidationN1) => true,
            (Brouillon, EnTraitementDpm) => true,
            (EnValidationN1, EnValidationN2) => true,
            (EnValidationN1, ACorriger) => true,
            (EnValidationN2, ValideeEntite) => true,
            (EnValidationN2, EnValidationN1) => true,
            (ValideeEntite, Soumise) => true,
            (Soumise, ValideeEntite) => true,
            (ValideeEntite, EnValidationN1) => true,
            (ValideeEntite, Brouillon) => true,
            (EnValidationN1, Brouillon) => true,
            (Soumise, EnTraitementDpm) => true,
            (EnTraitementDpm, EnControleBudgetaire) => true,
            (EnTraitementDpm, ACorriger) => true,
            (EnControleBudgetaire, EnTraitementDpm) => true,
            (ACorriger, Brouillon) => true,
            (EnControleBudgetaire, ViseeBudgetairement) => true,
            (EnControleBudgetaire, EnControleBudgetaire) => true,
            _ => false
        };
    }

    /// <summary>
    /// Indique si <paramref name="actuel"/> est strictement en aval de <paramref name="depart"/>
    /// sur le chemin nominal du workflow (hors branche A_CORRIGER).
    /// </summary>
    public static bool EstProgressionApres(string? depart, string? actuel)
    {
        var from = Normaliser(depart);
        var to = Normaliser(actuel);
        if (string.Equals(from, to, StringComparison.Ordinal))
            return false;

        var rangDepart = RangWorkflowNominal(from);
        var rangActuel = RangWorkflowNominal(to);
        if (rangDepart < 0 || rangActuel < 0)
            return false;

        return rangActuel > rangDepart;
    }

    private static int RangWorkflowNominal(string statut)
        => statut switch
        {
            Brouillon => 0,
            EnValidationN1 => 1,
            EnValidationN2 => 2,
            ValideeEntite => 3,
            Soumise => 4,
            EnTraitementDpm => 5,
            EnControleBudgetaire => 6,
            ViseeBudgetairement => 7,
            _ => -1,
        };
}
