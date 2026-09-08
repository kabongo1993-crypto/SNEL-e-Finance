namespace BudgetWeb.Domain.Entities;

/// <summary>Référentiel des banques / entités financières PCT (pct.BANQUE).</summary>
public class Banque
{
    /// <summary>Référence métier/historique SNEL (Excel ID_Banque). Clé fournie, pas IDENTITY.</summary>
    public string IdBanque { get; set; } = string.Empty;
    public string LibelleBanque { get; set; } = string.Empty;
    public string? Pays { get; set; }
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }

    public ICollection<CompteFinancier> Comptes { get; set; } = new List<CompteFinancier>();
}
