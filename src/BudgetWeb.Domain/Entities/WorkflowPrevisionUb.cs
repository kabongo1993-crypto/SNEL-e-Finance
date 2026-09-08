namespace BudgetWeb.Domain.Entities;

/// <summary>
/// État workflow opérationnel d'une paire Version × UB.
/// Les montants restent dans PREVISION_BUDGETAIRE.
/// </summary>
public class WorkflowPrevisionUb
{
    public long IdWorkflowPrevisionUB { get; set; }
    public long FK_VersionBudgetaire { get; set; }
    public long FK_UniteBudgetaire { get; set; }
    public string Statut { get; set; } = string.Empty;

    public long? FK_UtilisateurSoumission { get; set; }
    public DateTime? DateSoumission { get; set; }
    public long? FK_UtilisateurControle { get; set; }
    public DateTime? DateControle { get; set; }
    public long? FK_UtilisateurValidation { get; set; }
    public DateTime? DateValidation { get; set; }
    public long? FK_UtilisateurRejet { get; set; }
    public DateTime? DateRejet { get; set; }
    public string? MotifRejet { get; set; }

    public long FK_UtilisateurCreation { get; set; }
    public DateTime DateCreation { get; set; }
    public long? FK_UtilisateurModification { get; set; }
    public DateTime? DateModification { get; set; }

    public VersionBudgetaire VersionBudgetaire { get; set; } = null!;
    public UniteBudgetaire UniteBudgetaire { get; set; } = null!;
    public Utilisateur UtilisateurCreation { get; set; } = null!;
    public Utilisateur? UtilisateurModification { get; set; }
    public Utilisateur? UtilisateurSoumission { get; set; }
    public Utilisateur? UtilisateurControle { get; set; }
    public Utilisateur? UtilisateurValidation { get; set; }
    public Utilisateur? UtilisateurRejet { get; set; }
}
