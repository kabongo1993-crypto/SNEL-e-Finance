using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>
/// Validation de la destination budgétaire sollicitée (en-tête DPM, phase demandeur).
/// Information déclarative — sans FK vers référentiels budgétaires.
/// </summary>
public static class DestinationBudgetaireSolliciteeRules
{
    public static readonly IReadOnlyList<string> TypesAutorises =
    [
        TypeBudgetCode.DepensesCourantes,
        TypeBudgetCode.ActionsExploitation,
        TypeBudgetCode.BudgetInvestissement,
    ];

    public static string NormaliserType(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool EstTypeValide(string? value)
        => TypesAutorises.Contains(NormaliserType(value), StringComparer.Ordinal);

    public static string? NormaliserItem(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static void Valider(string? typeBudgetSollicite, string? itemSollicite)
    {
        var type = NormaliserType(typeBudgetSollicite);
        if (string.IsNullOrEmpty(type))
            throw new ArgumentException("Le type de budget sollicité est obligatoire.");

        if (!EstTypeValide(type))
            throw new ArgumentException("Le type de budget sollicité doit être DC, AE ou BI.");

        var item = NormaliserItem(itemSollicite);

        switch (type)
        {
            case TypeBudgetCode.DepensesCourantes:
                if (item is not null)
                    throw new ArgumentException("Aucun item ne doit être renseigné pour une destination DC.");
                break;
            case TypeBudgetCode.ActionsExploitation:
            case TypeBudgetCode.BudgetInvestissement:
                if (item is null)
                    throw new ArgumentException("L'item sollicité est obligatoire pour AE et BI.");
                break;
        }
    }

    /// <summary>Libellé d'affichage document / UI (BI affiché BI/IVT).</summary>
    public static string LibelleTypeAffichage(string? typeBudgetSollicite)
        => NormaliserType(typeBudgetSollicite) switch
        {
            TypeBudgetCode.DepensesCourantes => "DC",
            TypeBudgetCode.ActionsExploitation => "AE",
            TypeBudgetCode.BudgetInvestissement => "BI / IVT",
            _ => typeBudgetSollicite ?? "—",
        };

    /// <summary>Texte complet pour impression : « AE — Item N° 025 ».</summary>
    public static string FormaterDestination(string? typeBudgetSollicite, string? itemSollicite)
    {
        var type = NormaliserType(typeBudgetSollicite);
        if (string.IsNullOrEmpty(type))
            return "—";

        var libelle = LibelleTypeAffichage(type);
        var item = NormaliserItem(itemSollicite);
        return item is null ? libelle : $"{libelle} — Item N° {item}";
    }
}
