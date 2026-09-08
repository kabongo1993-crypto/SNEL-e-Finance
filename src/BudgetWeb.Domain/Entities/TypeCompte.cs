namespace BudgetWeb.Domain.Entities;

/// <summary>Type de compte financier PCT (pct.TYPE_COMPTE) — identifiant métier = Code.</summary>
public class TypeCompte
{
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public long FK_GroupeTypeCompte { get; set; }
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }

    public GroupeTypeCompte GroupeTypeCompte { get; set; } = null!;
    public ICollection<CompteFinancier> Comptes { get; set; } = new List<CompteFinancier>();
}
