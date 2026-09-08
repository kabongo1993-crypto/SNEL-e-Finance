namespace BudgetWeb.Domain.Entities;

/// <summary>
/// Imputation budgétaire d'une demande de paiement (dpm.DEMANDE_PAIEMENT_IMPUTATION).
/// FK_BudgetLigne nullable : une imputation sans prévision est autorisée.
/// LibelleItemAE : identité de l'Item AE réellement imputé (pas un libellé décoratif).
/// </summary>
public class DemandePaiementImputation
{
    public long IdImputation { get; set; }
    public long FK_DemandePaiement { get; set; }
    public int Ordre { get; set; }
    public long FK_TypeBudget { get; set; }
    public long FK_UniteBudgetaire { get; set; }
    public long FK_ExerciceBudgetaire { get; set; }
    public long? FK_RubriqueBudgetaire { get; set; }
    public byte? Mois { get; set; }
    public string? LibelleItemAE { get; set; }
    public long? FK_GroupeItemAE { get; set; }
    public long? FK_ItemBI { get; set; }
    public string? DetailBI { get; set; }
    public long? FK_BudgetLigne { get; set; }
    public decimal MontantBrut { get; set; }
    public string Devise { get; set; } = string.Empty;
    public decimal TauxConversion { get; set; }
    public decimal MontantUsd { get; set; }
    public int? NumeroFicheSuivi { get; set; }
    public long FK_UtilisateurCreation { get; set; }
    public DateTime DateImputation { get; set; }

    public DemandePaiement DemandePaiement { get; set; } = null!;
    public TypeBudget TypeBudget { get; set; } = null!;
    public UniteBudgetaire UniteBudgetaire { get; set; } = null!;
    public ExerciceBudgetaire ExerciceBudgetaire { get; set; } = null!;
    public RubriqueBudgetaire? RubriqueBudgetaire { get; set; }
    public GroupeItemAE? GroupeItemAE { get; set; }
    public ItemBI? ItemBI { get; set; }
    public PrevisionBudgetaire? PrevisionBudgetaire { get; set; }
    public Utilisateur UtilisateurCreation { get; set; } = null!;

    public ICollection<DemandePaiementImputationSnapshot> Snapshots { get; set; } =
        new List<DemandePaiementImputationSnapshot>();
}
