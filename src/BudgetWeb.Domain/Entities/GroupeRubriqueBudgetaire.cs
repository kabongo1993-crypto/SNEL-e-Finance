namespace BudgetWeb.Domain.Entities;

public class GroupeRubriqueBudgetaire
{
    public long IdGroupeRB { get; set; }
    public string CodeGroupe { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public int OrdreAffichage { get; set; }
    public bool Actif { get; set; }
    public DateTime DateCreation { get; set; }

    public ICollection<RubriqueBudgetaire> RubriquesBudgetaires { get; set; } = new List<RubriqueBudgetaire>();
}
