namespace BudgetWeb.Domain.Entities;

public class GroupeItemAE
{
    public long IdGroupeItemAE { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public bool Actif { get; set; }
    public DateTime DateCreation { get; set; }

    public ICollection<PrevisionBudgetaire> PrevisionsBudgetaires { get; set; } = new List<PrevisionBudgetaire>();
}
