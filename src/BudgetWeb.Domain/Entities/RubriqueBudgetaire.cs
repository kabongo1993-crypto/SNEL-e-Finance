namespace BudgetWeb.Domain.Entities;

public class RubriqueBudgetaire
{
    public long IdRB { get; set; }
    public string CodeRB { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public long? FK_RubriqueBudgetaireParent { get; set; }
    public int Niveau { get; set; }
    public bool Actif { get; set; }
    public DateTime DateCreation { get; set; }

    public RubriqueBudgetaire? RubriqueParent { get; set; }
    public ICollection<RubriqueBudgetaire> RubriquesEnfants { get; set; } = new List<RubriqueBudgetaire>();
    public ICollection<PrevisionBudgetaire> PrevisionsBudgetaires { get; set; } = new List<PrevisionBudgetaire>();
}
