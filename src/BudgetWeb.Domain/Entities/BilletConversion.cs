namespace BudgetWeb.Domain.Entities;

/// <summary>Billet de conversion — justification du montant payé en FC (dpm.BILLET_CONVERSION).</summary>
public class BilletConversion
{
    public long IdBilletConversion { get; set; }
    public long FK_DemandePaiement { get; set; }
    public DateOnly DateConversion { get; set; }
    /// <summary>Devise d'origine figée à l'établissement (intégrité documentaire).</summary>
    public string DeviseOrigine { get; set; } = string.Empty;
    /// <summary>Montant en devise d'origine figé à l'établissement.</summary>
    public decimal MontantDeviseOrigine { get; set; }
    public decimal TauxApplique { get; set; }
    public decimal MontantCdf { get; set; }
    public long? FK_TauxChange { get; set; }
    public string? DemandeChequeNumero { get; set; }
    public string? CoursEchangeBanque { get; set; }
    public decimal? SoldeAPayerDevise { get; set; }
    public string Statut { get; set; } = string.Empty;
    public long FK_UtilisateurEtabli { get; set; }
    public DateTime DateEtabli { get; set; }
    public long? FK_UtilisateurApprouve { get; set; }
    public long? FK_UtilisateurVisa { get; set; }
    public long? FK_UtilisateurModification { get; set; }
    public DateTime? DateModification { get; set; }

    public DemandePaiement DemandePaiement { get; set; } = null!;
    public TauxChange? TauxChange { get; set; }
    public Utilisateur UtilisateurEtabli { get; set; } = null!;
    public Utilisateur? UtilisateurApprouve { get; set; }
    public Utilisateur? UtilisateurVisa { get; set; }
    public Utilisateur? UtilisateurModification { get; set; }
}
