namespace BudgetWeb.Domain.Entities;

/// <summary>Bon provisoire établi (dpm.BON_PROVISOIRE).</summary>
public class BonProvisoire
{
    public long IdBonProvisoire { get; set; }
    public long FK_DemandePaiement { get; set; }
    public string NumeroBon { get; set; } = string.Empty;
    public DateOnly DateBon { get; set; }
    public string Statut { get; set; } = string.Empty;

    public decimal MontantFc { get; set; }
    public string MontantEnLettres { get; set; } = string.Empty;
    public string ReferenceDemande { get; set; } = string.Empty;
    public string Motif { get; set; } = string.Empty;
    public string? MentionJustificationRetrait { get; set; }

    public string BeneficiaireAffichage { get; set; } = string.Empty;
    public string? BeneficiaireMatricule { get; set; }
    public string? BeneficiaireIdentite { get; set; }
    public string? DirectionBeneficiaire { get; set; }

    public string? RecuCaisseCentrale { get; set; }
    public string? CompteGeneral { get; set; }
    public string? CompteParticulier { get; set; }
    public string? NumeroAppariement { get; set; }

    public string IdentifiantVerification { get; set; } = string.Empty;

    public long FK_UtilisateurEtabli { get; set; }
    public DateTime DateEtabli { get; set; }
    public long? FK_UtilisateurModification { get; set; }
    public DateTime? DateModification { get; set; }

    public DemandePaiement DemandePaiement { get; set; } = null!;
    public Utilisateur UtilisateurEtabli { get; set; } = null!;
    public Utilisateur? UtilisateurModification { get; set; }
}
