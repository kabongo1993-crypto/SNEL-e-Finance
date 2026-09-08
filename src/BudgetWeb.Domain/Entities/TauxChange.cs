namespace BudgetWeb.Domain.Entities;

/// <summary>Taux de change centralisé (dpm.TAUX_CHANGE) — orientation canonique : DeviseSource/DeviseCible = paire de référence.</summary>
public class TauxChange
{
    public long IdTauxChange { get; set; }
    public string DeviseSource { get; set; } = string.Empty;
    public string DeviseCible { get; set; } = "USD";
    public decimal Taux { get; set; }
    public DateOnly DateEffet { get; set; }
    public string Statut { get; set; } = string.Empty;
    public long FK_UtilisateurCreation { get; set; }
    public DateTime DateCreation { get; set; }
    public long? FK_UtilisateurModification { get; set; }
    public DateTime? DateModification { get; set; }

    public Utilisateur UtilisateurCreation { get; set; } = null!;
    public Utilisateur? UtilisateurModification { get; set; }
    public ICollection<DemandePaiement> DemandesPaiement { get; set; } =
        new List<DemandePaiement>();
}
