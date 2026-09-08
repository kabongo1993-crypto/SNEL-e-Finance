namespace BudgetWeb.Domain.Entities;

/// <summary>Pièce de caisse établie (dpm.PIECE_CAISSE).</summary>
public class PieceCaisse
{
    public long IdPieceCaisse { get; set; }
    public long FK_DemandePaiement { get; set; }
    public string NumeroPiece { get; set; } = string.Empty;
    public DateOnly DatePiece { get; set; }
    public string Statut { get; set; } = string.Empty;

    public decimal MontantFc { get; set; }
    public string MontantEnLettres { get; set; } = string.Empty;
    public string ReferenceDemande { get; set; } = string.Empty;
    public string Motif { get; set; } = string.Empty;
    public string? PieceJustificative { get; set; }

    public string BeneficiaireAffichage { get; set; } = string.Empty;
    public string? BeneficiaireMatricule { get; set; }
    public string? BeneficiaireIdentite { get; set; }

    // Paramètres figés à l'établissement
    public string? RecuSnel { get; set; }
    public string? Sr { get; set; }
    public string? ComptabiliteGenerale { get; set; }
    public string? Cp { get; set; }
    public string? Cpa { get; set; }
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
