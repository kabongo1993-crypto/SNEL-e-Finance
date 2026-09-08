namespace BudgetWeb.Domain.Entities;

/// <summary>Compte financier PCT — banque ou caisse (pct.COMPTE). Distinct de CompteSection (DPM / SYSCOHADA).</summary>
public class CompteFinancier
{
    public long IdCompte { get; set; }
    public string NumeroCompte { get; set; } = string.Empty;
    public string LibelleCompte { get; set; } = string.Empty;
    public string FK_Banque { get; set; } = string.Empty;
    public long FK_Direction { get; set; }
    public string FK_TypeCompte { get; set; } = string.Empty;
    public long FK_Devise { get; set; }
    public string? FK_Province { get; set; }
    /// <summary>Responsable PCT chargé du suivi — ≠ critère d'autorisation applicative.</summary>
    public long? FK_Utilisateur { get; set; }
    public DateTime DateCreation { get; set; }
    public DateOnly? DateCloture { get; set; }
    public DateTime? DateModification { get; set; }
    public bool Actif { get; set; } = true;

    public Banque Banque { get; set; } = null!;
    public DirectionTresorerie Direction { get; set; } = null!;
    public TypeCompte TypeCompte { get; set; } = null!;
    public Devise Devise { get; set; } = null!;
    public Province? Province { get; set; }
    public Utilisateur? Utilisateur { get; set; }
    public ICollection<CompteCategorie> Categories { get; set; } = new List<CompteCategorie>();
}
