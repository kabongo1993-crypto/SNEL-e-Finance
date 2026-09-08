namespace BudgetWeb.Domain.Entities;

/// <summary>
/// Ordre d'affichage des groupes et items AE pour une paire Version × UB.
/// Les montants restent dans PREVISION_BUDGETAIRE.
/// Sur les lignes ITEM, FK_GroupeItemAE est toujours null (groupe déduit des prévisions).
/// </summary>
public class ClassementAE
{
    public long IdClassementAE { get; set; }
    public long FK_VersionBudgetaire { get; set; }
    public long FK_UniteBudgetaire { get; set; }
    public string TypeLigne { get; set; } = string.Empty;
    public long? FK_GroupeItemAE { get; set; }
    public string? LibelleItemAE { get; set; }
    public int OrdreAffichage { get; set; }
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }

    public VersionBudgetaire VersionBudgetaire { get; set; } = null!;
    public UniteBudgetaire UniteBudgetaire { get; set; } = null!;
    public GroupeItemAE? GroupeItemAE { get; set; }
}
