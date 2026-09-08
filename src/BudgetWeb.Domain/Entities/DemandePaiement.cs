namespace BudgetWeb.Domain.Entities;

/// <summary>Dossier principal de demande de paiement (dpm.DEMANDE_PAIEMENT).</summary>
public class DemandePaiement
{
    public long IdDemandePaiement { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateOnly DateEmission { get; set; }
    public string? LieuEmission { get; set; }
    public long FK_ExerciceBudgetaire { get; set; }
    public long? FK_VersionBudgetaire { get; set; }
    public long FK_UniteBudgetaire { get; set; }
    /// <summary>Demandeur (nullable SQL tant que données historiques non migrées).</summary>
    public long? FK_Demandeur { get; set; }
    public long FK_CasDossier { get; set; }
    /// <summary>Type budgétaire sollicité par le demandeur (DC / AE / BI) — déclaratif.</summary>
    public string? TypeBudgetSollicite { get; set; }
    /// <summary>Item N° sollicité (AE/BI) — texte libre, sans FK référentiel.</summary>
    public string? ItemSollicite { get; set; }
    /// <summary>Renseigné à l'orientation vers le Junior (aligné sur TypeBudgetSollicite).</summary>
    public long? FK_TypeBudget { get; set; }
    public string Objet { get; set; } = string.Empty;
    public string? CompteSection { get; set; }
    /// <summary>Montant sollicité (conservé ; non écrasé par la conversion ultérieure).</summary>
    public decimal MontantBrut { get; set; }
    /// <summary>Référentiel devise sollicitée (nullable pour historique non migré).</summary>
    public long? FK_Devise { get; set; }
    /// <summary>Code devise sollicitée (dénormalisé depuis dpm.DEVISE pour taux / contrôles).</summary>
    public string Devise { get; set; } = string.Empty;
    /// <summary>Taux appliqué au traitement (null à la création initiale).</summary>
    public decimal? TauxConversion { get; set; }
    /// <summary>Montant converti (null à la création initiale).</summary>
    public decimal? MontantUsd { get; set; }
    public long? FK_TauxChange { get; set; }
    /// <summary>Mode de paiement sollicité (CAISSE / BANQUE) — renseigné par le demandeur.</summary>
    public string? ModePaiementSollicite { get; set; }
    public string? TypeInstrumentPaiement { get; set; }
    /// <summary>Devise de paiement (ex. CDF caisse) — distincte de la devise sollicitée.</summary>
    public string? DevisePaiement { get; set; }
    public decimal? MontantPaiement { get; set; }
    public decimal? TauxPaiement { get; set; }
    public long? FK_TauxChangePaiement { get; set; }
    public string Statut { get; set; } = string.Empty;
    public string? MotifRetour { get; set; }
    public string? CommentaireRetour { get; set; }
    public long FK_UtilisateurCreation { get; set; }
    public DateTime DateCreation { get; set; }
    public long? FK_UtilisateurModification { get; set; }
    public DateTime? DateModification { get; set; }
    public long? FK_UtilisateurSoumission { get; set; }
    public DateTime? DateSoumission { get; set; }
    public long? FK_UtilisateurReception { get; set; }
    public DateTime? DateReception { get; set; }
    public long? FK_UtilisateurControle { get; set; }
    public DateTime? DateControle { get; set; }
    public long? FK_UtilisateurVisa { get; set; }
    public DateTime? DateVisa { get; set; }
    public long? FK_UtilisateurRetour { get; set; }
    public DateTime? DateRetour { get; set; }
    /// <summary>Détenteur nominatif courant ; NULL = mode pool (Lot 3.1 — structure uniquement).</summary>
    public long? FK_UtilisateurAssigne { get; set; }

    public ExerciceBudgetaire ExerciceBudgetaire { get; set; } = null!;
    public VersionBudgetaire? VersionBudgetaire { get; set; }
    public UniteBudgetaire UniteBudgetaire { get; set; } = null!;
    public Demandeur? Demandeur { get; set; }
    public CasDossier CasDossier { get; set; } = null!;
    public Devise? DeviseRef { get; set; }
    public TypeBudget? TypeBudget { get; set; }
    public TauxChange? TauxChange { get; set; }
    public TauxChange? TauxChangePaiement { get; set; }
    public Utilisateur UtilisateurCreation { get; set; } = null!;
    public Utilisateur? UtilisateurModification { get; set; }
    public Utilisateur? UtilisateurSoumission { get; set; }
    public Utilisateur? UtilisateurReception { get; set; }
    public Utilisateur? UtilisateurControle { get; set; }
    public Utilisateur? UtilisateurVisa { get; set; }
    public Utilisateur? UtilisateurRetour { get; set; }
    public Utilisateur? UtilisateurAssigne { get; set; }

    public ICollection<DemandePaiementRoutage> Routages { get; set; } =
        new List<DemandePaiementRoutage>();

    public ICollection<DemandePaiementBeneficiaire> Beneficiaires { get; set; } =
        new List<DemandePaiementBeneficiaire>();

    public ICollection<DemandePaiementImputation> Imputations { get; set; } =
        new List<DemandePaiementImputation>();

    public ICollection<PieceJointe> PiecesJointes { get; set; } =
        new List<PieceJointe>();

    public ICollection<DemandePaiementValidation> ValidationsEntite { get; set; } =
        new List<DemandePaiementValidation>();

    public BilletConversion? BilletConversion { get; set; }
    public PieceCaisse? PieceCaisse { get; set; }
    public BonProvisoire? BonProvisoire { get; set; }
    public MinuteCheque? MinuteCheque { get; set; }
}
