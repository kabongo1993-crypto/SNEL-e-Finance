namespace BudgetWeb.Domain.Entities;

/// <summary>Référentiel des cas de dossier DPM (dpm.CAS_DOSSIER).</summary>
public class CasDossier
{
    public long IdCasDossier { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public int Ordre { get; set; }
    public bool Actif { get; set; }

    public ICollection<CasDossierPieceObligatoire> PiecesObligatoires { get; set; } =
        new List<CasDossierPieceObligatoire>();

    public ICollection<DemandePaiement> DemandesPaiement { get; set; } =
        new List<DemandePaiement>();
}
