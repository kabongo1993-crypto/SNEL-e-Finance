namespace BudgetWeb.Domain.Entities;

public class StructureOrganisationnelle
{
    public long IdStructure { get; set; }
    public long? FK_StructureOrganisationnelleParent { get; set; }
    public string TypeStructure { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool Actif { get; set; }
    public DateTime DateCreation { get; set; }

    public StructureOrganisationnelle? StructureParent { get; set; }
    public ICollection<StructureOrganisationnelle> StructuresEnfants { get; set; } = new List<StructureOrganisationnelle>();
    public ICollection<UniteBudgetaire> UnitesBudgetaires { get; set; } = new List<UniteBudgetaire>();
}
