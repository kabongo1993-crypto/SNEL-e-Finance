namespace BudgetWeb.Domain.Entities;

public class ModePrevision
{
    public long IdModePrevision { get; set; }
    public string CodeMode { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool Actif { get; set; }

    public ICollection<PrevisionBudgetaire> PrevisionsBudgetaires { get; set; } = new List<PrevisionBudgetaire>();
}
