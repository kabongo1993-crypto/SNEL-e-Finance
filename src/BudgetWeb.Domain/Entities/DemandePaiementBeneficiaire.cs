namespace BudgetWeb.Domain.Entities;

/// <summary>Bénéficiaire d'une demande de paiement (dpm.DEMANDE_PAIEMENT_BENEFICIAIRE).</summary>
public class DemandePaiementBeneficiaire
{
    public long IdBeneficiaire { get; set; }
    public long FK_DemandePaiement { get; set; }
    public string TypeBeneficiaire { get; set; } = string.Empty;
    public string NomComplet { get; set; } = string.Empty;
    public string? Matricule { get; set; }
    public string? Fonction { get; set; }
    public string? RaisonSociale { get; set; }
    public string? Rccm { get; set; }
    public string? Adresse { get; set; }
    public string? Banque { get; set; }
    public string? NumeroCompte { get; set; }
    public bool EstPrincipal { get; set; } = true;
    public int Ordre { get; set; } = 1;

    public DemandePaiement DemandePaiement { get; set; } = null!;
}
