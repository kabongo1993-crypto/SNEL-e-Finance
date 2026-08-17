namespace BudgetWeb.Domain.Entities;

public class PrevisionBudgetaire
{
    public long IdPrevision { get; set; }
    public long FK_VersionBudgetaire { get; set; }
    public long FK_TypeBudget { get; set; }
    public long FK_UniteBudgetaire { get; set; }
    public long? FK_RubriqueBudgetaire { get; set; }
    public long? FK_ItemBI { get; set; }
    public long? FK_GroupeItemAE { get; set; }
    public string? LibelleItemAE { get; set; }
    public string? DetailBI { get; set; }
    public long FK_ModePrevision { get; set; }
    public decimal MontantAnnuel { get; set; }
    public DateTime DateCreation { get; set; }
    public long FK_UtilisateurCreation { get; set; }
    public DateTime? DateModification { get; set; }
    public long? FK_UtilisateurModification { get; set; }

    public VersionBudgetaire VersionBudgetaire { get; set; } = null!;
    public TypeBudget TypeBudget { get; set; } = null!;
    public UniteBudgetaire UniteBudgetaire { get; set; } = null!;
    public RubriqueBudgetaire? RubriqueBudgetaire { get; set; }
    public ItemBI? ItemBI { get; set; }
    public GroupeItemAE? GroupeItemAE { get; set; }
    public ModePrevision ModePrevision { get; set; } = null!;
    public Utilisateur UtilisateurCreation { get; set; } = null!;
    public Utilisateur? UtilisateurModification { get; set; }
    public ICollection<RepartitionMensuelle> RepartitionsMensuelles { get; set; } = new List<RepartitionMensuelle>();
    public ICollection<OperationReduction> OperationsReduction { get; set; } = new List<OperationReduction>();
    public ICollection<TransfertBudgetaire> TransfertsSource { get; set; } = new List<TransfertBudgetaire>();
    public ICollection<TransfertBudgetaire> TransfertsDestination { get; set; } = new List<TransfertBudgetaire>();
}
