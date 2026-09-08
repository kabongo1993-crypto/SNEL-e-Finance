namespace BudgetWeb.Domain.Entities;

/// <summary>Référentiel demandeur DPM — un demandeur est rattaché à une seule UB (dpm.DEMANDEUR).</summary>
public class Demandeur
{
    public long IdDemandeur { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public long FK_UniteBudgetaire { get; set; }
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }
    public long FK_UtilisateurCreation { get; set; }
    public long? FK_UtilisateurModification { get; set; }

    public UniteBudgetaire UniteBudgetaire { get; set; } = null!;
    public Utilisateur UtilisateurCreation { get; set; } = null!;
    public Utilisateur? UtilisateurModification { get; set; }

    public ICollection<DemandePaiement> DemandesPaiement { get; set; } = new List<DemandePaiement>();
}
