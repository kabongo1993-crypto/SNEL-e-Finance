namespace BudgetWeb.Domain.Entities;

/// <summary>
/// Ledger d'ajustement DG sur une prévision validée.
/// Le budget voté initial reste traçable via l'historique ;
/// <see cref="PrevisionBudgetaire.MontantAnnuel"/> porte le montant actuellement applicable après validation de l'ajustement.
/// </summary>
public class AjustementBudgetaire
{
    public long IdAjustement { get; set; }
    public string Reference { get; set; } = string.Empty;
    public long FK_PrevisionBudgetaire { get; set; }
    public long FK_VersionBudgetaire { get; set; }
    public long FK_UniteBudgetaire { get; set; }
    public long FK_ExerciceBudgetaire { get; set; }
    public decimal MontantAncien { get; set; }
    public decimal MontantNouveau { get; set; }
    public decimal Variation { get; set; }
    public string Motif { get; set; } = string.Empty;
    public string Statut { get; set; } = string.Empty;
    public long FK_UtilisateurCreation { get; set; }
    public DateTime DateCreation { get; set; }
    public long? FK_UtilisateurValidation { get; set; }
    public DateTime? DateValidation { get; set; }
    public long? FK_UtilisateurModification { get; set; }
    public DateTime? DateModification { get; set; }

    public PrevisionBudgetaire PrevisionBudgetaire { get; set; } = null!;
    public VersionBudgetaire VersionBudgetaire { get; set; } = null!;
    public UniteBudgetaire UniteBudgetaire { get; set; } = null!;
    public ExerciceBudgetaire ExerciceBudgetaire { get; set; } = null!;
    public Utilisateur UtilisateurCreation { get; set; } = null!;
    public Utilisateur? UtilisateurValidation { get; set; }
    public Utilisateur? UtilisateurModification { get; set; }
}
