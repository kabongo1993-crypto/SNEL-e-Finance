namespace BudgetWeb.Domain.Entities;

public class UniteBudgetaire
{
    public long IdUB { get; set; }
    public string CodeUB { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public long FK_Departement { get; set; }
    public long FK_StructureOrganisationnelle { get; set; }
    public bool Actif { get; set; }
    public DateTime DateCreation { get; set; }

    public Departement Departement { get; set; } = null!;
    public StructureOrganisationnelle StructureOrganisationnelle { get; set; } = null!;
    public ICollection<PrevisionBudgetaire> PrevisionsBudgetaires { get; set; } = new List<PrevisionBudgetaire>();
}
