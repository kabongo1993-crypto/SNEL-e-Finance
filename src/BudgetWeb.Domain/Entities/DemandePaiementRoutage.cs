namespace BudgetWeb.Domain.Entities;

/// <summary>
/// Transmission nominative entre acteurs sur une DPM (dpm.DEMANDE_PAIEMENT_ROUTAGE).
/// Complète JOURNAL_AUDIT : historique structuré source → cible pour le routage courant.
/// </summary>
public class DemandePaiementRoutage
{
    public long IdRoutage { get; set; }
    public long FK_DemandePaiement { get; set; }
    public long FK_UtilisateurSource { get; set; }
    public long FK_UtilisateurCible { get; set; }
    public string StatutSource { get; set; } = string.Empty;
    public string StatutCible { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime DateRoutage { get; set; }
    public bool EstActif { get; set; } = true;
    public string? Motif { get; set; }

    public DemandePaiement DemandePaiement { get; set; } = null!;
    public Utilisateur UtilisateurSource { get; set; } = null!;
    public Utilisateur UtilisateurCible { get; set; } = null!;
}
