namespace BudgetWeb.Domain.Entities;

public class Departement
{
    public long IdDepartement { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool Actif { get; set; }
    public DateTime DateCreation { get; set; }

    public ICollection<UniteBudgetaire> UnitesBudgetaires { get; set; } = new List<UniteBudgetaire>();
}
