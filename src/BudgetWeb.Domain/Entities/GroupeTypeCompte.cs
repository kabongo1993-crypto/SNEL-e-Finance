namespace BudgetWeb.Domain.Entities;

/// <summary>Groupe de types de comptes financiers PCT (pct.GROUPE_TYPE_COMPTE).</summary>
public class GroupeTypeCompte
{
    public long IdGroupeTypeCompte { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }

    public ICollection<TypeCompte> TypesCompte { get; set; } = new List<TypeCompte>();
}
