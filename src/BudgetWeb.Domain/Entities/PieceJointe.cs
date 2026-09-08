namespace BudgetWeb.Domain.Entities;

/// <summary>Pièce jointe d'une demande de paiement (dpm.PIECE_JOINTE).</summary>
public class PieceJointe
{
    public long IdPieceJointe { get; set; }
    public long FK_DemandePaiement { get; set; }
    public long? FK_PieceObligatoire { get; set; }
    public string CodeTypePiece { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool EstObligatoire { get; set; }
    public string NomFichierOriginal { get; set; } = string.Empty;
    public string CheminRelatif { get; set; } = string.Empty;
    public string HashSha256 { get; set; } = string.Empty;
    public long TailleOctets { get; set; }
    public long FK_Utilisateur { get; set; }
    public DateTime DateUpload { get; set; }

    public DemandePaiement DemandePaiement { get; set; } = null!;
    public CasDossierPieceObligatoire? PieceObligatoire { get; set; }
    public Utilisateur Utilisateur { get; set; } = null!;
}
