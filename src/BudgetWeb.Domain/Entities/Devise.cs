namespace BudgetWeb.Domain.Entities;

/// <summary>Référentiel des devises DPM (dpm.DEVISE).</summary>
public class Devise
{
    public long IdDevise { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public string? Symbole { get; set; }
    public bool Actif { get; set; } = true;

    public ICollection<DemandePaiement> DemandesPaiement { get; set; } =
        new List<DemandePaiement>();
}
