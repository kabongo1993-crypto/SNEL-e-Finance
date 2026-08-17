namespace BudgetWeb.Domain.Entities;

public class OperationReduction
{
    public long IdOperation { get; set; }
    public long FK_PrevisionBudgetaire { get; set; }
    public decimal Montant { get; set; }
    public DateOnly DateEffet { get; set; }
    public string? Motif { get; set; }
    public long FK_Autorisation { get; set; }
    public DateTime DateCreation { get; set; }
    public long FK_UtilisateurCreation { get; set; }

    public PrevisionBudgetaire PrevisionBudgetaire { get; set; } = null!;
    public Autorisation Autorisation { get; set; } = null!;
    public Utilisateur UtilisateurCreation { get; set; } = null!;
}
