namespace BudgetWeb.Domain.Entities;

public class TypeBudget
{
    public long IdTypeBudget { get; set; }
    public string CodeType { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public int OrdreAffichage { get; set; }
    public bool Actif { get; set; }

    public ICollection<PrevisionBudgetaire> PrevisionsBudgetaires { get; set; } = new List<PrevisionBudgetaire>();
}
