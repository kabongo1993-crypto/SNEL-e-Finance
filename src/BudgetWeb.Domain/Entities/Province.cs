namespace BudgetWeb.Domain.Entities;

/// <summary>Référentiel des provinces / sites PCT (pct.PROVINCE). Identifiant métier = IdProvince VARCHAR.</summary>
public class Province
{
    public string IdProvince { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }

    public ICollection<CompteFinancier> Comptes { get; set; } = new List<CompteFinancier>();
}
