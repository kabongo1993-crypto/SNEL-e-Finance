namespace BudgetWeb.Domain.Entities;

/// <summary>Validation interne entité émettrice (N1 / N2) — dpm.DEMANDE_PAIEMENT_VALIDATION.</summary>
public class DemandePaiementValidation
{
    public long IdValidation { get; set; }
    public long FK_DemandePaiement { get; set; }
    /// <summary>1 = Responsable service ; 2 = Responsable entité.</summary>
    public byte Niveau { get; set; }
    public byte Ordre { get; set; }
    public string Statut { get; set; } = string.Empty;
    public string? ModeValidation { get; set; }
    /// <summary>Utilisateur connecté ayant validé électroniquement.</summary>
    public long? FK_UtilisateurValidateur { get; set; }
    /// <summary>Utilisateur e-finance ayant déclaré une validation physique.</summary>
    public long? FK_UtilisateurDeclarant { get; set; }
    public string? NomSignatairePhysique { get; set; }
    public string? FonctionSignatairePhysique { get; set; }
    public DateOnly? DateSignaturePhysique { get; set; }
    public DateTime? DateValidation { get; set; }
    public string? Commentaire { get; set; }
    /// <summary>Empreinte des données DPM au moment de la validation (invalidation si modification).</summary>
    public string? EmpreinteDonnees { get; set; }

    public DemandePaiement DemandePaiement { get; set; } = null!;
    public Utilisateur? UtilisateurValidateur { get; set; }
    public Utilisateur? UtilisateurDeclarant { get; set; }
}
