namespace BudgetWeb.Application.DTOs;

/// <summary>Mise à jour conditionnelle d'une demande (statut attendu vérifié atomiquement).</summary>
public sealed record DemandePaiementConditionalUpdatePatch
{
    public string? NouveauStatut { get; init; }
    public long? FK_UtilisateurModification { get; init; }
    public DateTime? DateModification { get; init; }
    public long? FK_UtilisateurSoumission { get; init; }
    public DateTime? DateSoumission { get; init; }
    public long? FK_UtilisateurReception { get; init; }
    public DateTime? DateReception { get; init; }
    public long? FK_UtilisateurControle { get; init; }
    public DateTime? DateControle { get; init; }
    public long? FK_UtilisateurVisa { get; init; }
    public DateTime? DateVisa { get; init; }
    public long? FK_UtilisateurRetour { get; init; }
    public DateTime? DateRetour { get; init; }
    public string? MotifRetour { get; init; }
    public string? CommentaireRetour { get; init; }
    public long? FK_TypeBudget { get; init; }
    public string? TypeInstrumentPaiement { get; init; }
    public string? DevisePaiement { get; init; }
    public decimal? MontantPaiement { get; init; }
    public decimal? TauxPaiement { get; init; }
    public long? FK_TauxChangePaiement { get; init; }
    public decimal? TauxConversion { get; init; }
    public decimal? MontantUsd { get; init; }
    public long? FK_TauxChange { get; init; }
    public string? ModePaiementSollicite { get; init; }
    public string? TypeBudgetSollicite { get; init; }
    public string? ItemSollicite { get; init; }
    public long? FK_UtilisateurAssigne { get; init; }
    public bool MettreAJourAssigne { get; init; }
    /// <summary>Remet à null la soumission (annulation avant réception Budget).</summary>
    public bool EffacerSoumission { get; init; }
}
