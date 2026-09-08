namespace BudgetWeb.Domain.Entities;

/// <summary>
/// Snapshot budgétaire figé au visa (dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT).
/// Pas de colonne EncoursPipeline / EngagementEnCours.
/// Champs mensuels NULL pour AE/BI.
/// </summary>
public class DemandePaiementImputationSnapshot
{
    public long IdSnapshot { get; set; }
    public long FK_Imputation { get; set; }
    public long FK_DemandePaiement { get; set; }
    public DateTime DateSnapshot { get; set; }
    public decimal? BudgetMensuel { get; set; }
    public decimal? CreditEngageMensuel { get; set; }
    public decimal? CreditDisponibleMensuelAvantVisa { get; set; }
    public decimal BudgetAnnuel { get; set; }
    public decimal CreditEngageAnnuel { get; set; }
    public decimal CreditDisponibleAnnuelAvantVisa { get; set; }
    public decimal MontantPrevision { get; set; }
    public decimal EcartPrevisionImputation { get; set; }
    public decimal MontantBrut { get; set; }
    public string Devise { get; set; } = string.Empty;
    public decimal TauxConversion { get; set; }
    public decimal MontantUsd { get; set; }
    public long? FK_BudgetLigne { get; set; }

    public DemandePaiementImputation Imputation { get; set; } = null!;
    public DemandePaiement DemandePaiement { get; set; } = null!;
    public PrevisionBudgetaire? PrevisionBudgetaire { get; set; }
}
