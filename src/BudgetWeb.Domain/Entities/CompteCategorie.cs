namespace BudgetWeb.Domain.Entities;

/// <summary>Historisation de la catégorie d'un compte financier PCT (pct.COMPTE_CATEGORIE).</summary>
public class CompteCategorie
{
    public long IdCompteCategorie { get; set; }
    public long FK_Compte { get; set; }
    public long FK_CategorieCompte { get; set; }
    public DateOnly DateDebut { get; set; }
    public DateOnly? DateFin { get; set; }

    public CompteFinancier Compte { get; set; } = null!;
    public CategorieCompte CategorieCompte { get; set; } = null!;
}
