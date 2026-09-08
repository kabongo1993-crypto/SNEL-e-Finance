namespace BudgetWeb.Domain.Entities;

/// <summary>Minute de chèque / demande d'établissement O.P. (dpm.MINUTE_CHEQUE).</summary>
public class MinuteCheque
{
    public long IdMinuteCheque { get; set; }
    public long FK_DemandePaiement { get; set; }
    public string NumeroOp { get; set; } = string.Empty;
    public DateOnly DateDocument { get; set; }
    public string Statut { get; set; } = string.Empty;

    public decimal MontantPaiement { get; set; }
    public string DevisePaiement { get; set; } = string.Empty;
    public string MontantEnLettres { get; set; } = string.Empty;
    public string ReferenceDemande { get; set; } = string.Empty;
    public string Motif { get; set; } = string.Empty;

    public string BeneficiaireAffichage { get; set; } = string.Empty;
    public string? BeneficiaireAdresse { get; set; }
    public string? BeneficiaireBanque { get; set; }
    public string? BeneficiaireNumeroCompte { get; set; }

    public string? CompteGeneral { get; set; }
    public string? CpCa { get; set; }
    public string? Ls { get; set; }
    public string? SuiviExtraComptable { get; set; }
    public string? NumeroAppariement { get; set; }
    public decimal? MontantSuiviExtraComptable { get; set; }

    public string IdentifiantVerification { get; set; } = string.Empty;

    public long FK_UtilisateurEtabli { get; set; }
    public DateTime DateEtabli { get; set; }
    public long? FK_UtilisateurModification { get; set; }
    public DateTime? DateModification { get; set; }

    public DemandePaiement DemandePaiement { get; set; } = null!;
    public Utilisateur UtilisateurEtabli { get; set; } = null!;
    public Utilisateur? UtilisateurModification { get; set; }
}
