namespace BudgetWeb.Domain.Entities;

public class Autorisation
{
    public long IdAutorisation { get; set; }
    public string TypeOperation { get; set; } = string.Empty;
    public DateTime DateAutorisation { get; set; }
    public long FK_UtilisateurAutorisation { get; set; }
    public string? Motif { get; set; }
    public string Statut { get; set; } = string.Empty;

    public Utilisateur UtilisateurAutorisation { get; set; } = null!;
    public ICollection<OperationReduction> OperationsReduction { get; set; } = new List<OperationReduction>();
    public ICollection<TransfertBudgetaire> TransfertsBudgetaires { get; set; } = new List<TransfertBudgetaire>();
}
