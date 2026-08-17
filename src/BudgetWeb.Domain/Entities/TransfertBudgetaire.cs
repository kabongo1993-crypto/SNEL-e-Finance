namespace BudgetWeb.Domain.Entities;

public class TransfertBudgetaire
{
    public long IdTransfert { get; set; }
    public long FK_VersionBudgetaire { get; set; }
    public long FK_PrevisionBudgetaireSource { get; set; }
    public long FK_PrevisionBudgetaireDestination { get; set; }
    public decimal Montant { get; set; }
    public DateOnly DateEffet { get; set; }
    public string? Motif { get; set; }
    public long FK_Autorisation { get; set; }
    public DateTime DateCreation { get; set; }
    public long FK_UtilisateurCreation { get; set; }

    public VersionBudgetaire VersionBudgetaire { get; set; } = null!;
    public PrevisionBudgetaire PrevisionSource { get; set; } = null!;
    public PrevisionBudgetaire PrevisionDestination { get; set; } = null!;
    public Autorisation Autorisation { get; set; } = null!;
    public Utilisateur UtilisateurCreation { get; set; } = null!;
}
