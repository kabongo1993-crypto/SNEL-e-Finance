namespace BudgetWeb.Domain.Entities;

/// <summary>Direction / implantation de Trésorerie PCT (pct.DIRECTION) — distincte de l'organigramme budget.</summary>
public class DirectionTresorerie
{
    public long IdDirection { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }

    public ICollection<CompteFinancier> Comptes { get; set; } = new List<CompteFinancier>();
}
