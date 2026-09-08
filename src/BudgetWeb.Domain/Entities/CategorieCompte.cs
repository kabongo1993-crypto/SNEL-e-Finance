namespace BudgetWeb.Domain.Entities;

/// <summary>Catégorie de compte financier PCT (pct.CATEGORIE_COMPTE) — identifiant technique IDENTITY.</summary>
public class CategorieCompte
{
    public long IdCategorieCompte { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public string? Orientation { get; set; }
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }

    public ICollection<CompteCategorie> ComptesCategories { get; set; } = new List<CompteCategorie>();
}
