namespace BudgetWeb.Domain.Entities;

/// <summary>Pièce obligatoire par cas de dossier (dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE).</summary>
public class CasDossierPieceObligatoire
{
    public long IdPieceObligatoire { get; set; }
    public long FK_CasDossier { get; set; }
    public string CodeTypePiece { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public int Ordre { get; set; }
    public bool Actif { get; set; } = true;
    public bool Obligatoire { get; set; } = true;

    public CasDossier CasDossier { get; set; } = null!;
    public ICollection<PieceJointe> PiecesJointes { get; set; } =
        new List<PieceJointe>();
}
