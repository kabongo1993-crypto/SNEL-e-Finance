namespace BudgetWeb.Domain.Entities;

public class ItemBI
{
    public long IdItemBI { get; set; }
    public string CodeItem { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public long? FK_ItemBIParent { get; set; }
    public int Niveau { get; set; }
    public string? Categorie { get; set; }
    public bool Actif { get; set; }
    public DateTime DateCreation { get; set; }

    public ItemBI? ItemParent { get; set; }
    public ICollection<ItemBI> ItemsEnfants { get; set; } = new List<ItemBI>();
    public ICollection<PrevisionBudgetaire> PrevisionsBudgetaires { get; set; } = new List<PrevisionBudgetaire>();
}
