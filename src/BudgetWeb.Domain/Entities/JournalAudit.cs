namespace BudgetWeb.Domain.Entities;

public class JournalAudit
{
    public long IdAudit { get; set; }
    public long FK_Utilisateur { get; set; }
    public DateTime DateHeure { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Entite { get; set; } = string.Empty;
    public long IdEntite { get; set; }
    public string? AnciennesValeurs { get; set; }
    public string? NouvellesValeurs { get; set; }
    public string? AdresseIP { get; set; }

    public Utilisateur Utilisateur { get; set; } = null!;
}
