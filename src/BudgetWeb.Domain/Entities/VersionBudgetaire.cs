namespace BudgetWeb.Domain.Entities;

public class VersionBudgetaire
{
    public long IdVersion { get; set; }
    public long FK_ExerciceBudgetaire { get; set; }
    public int NumeroVersion { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public long? FK_VersionBudgetairePrecedente { get; set; }
    public DateTime DateCreation { get; set; }
    public DateOnly DateDebutEffet { get; set; }
    public DateOnly? DateFinEffet { get; set; }
    public string? Motif { get; set; }
    public string Statut { get; set; } = string.Empty;
    public long FK_UtilisateurCreation { get; set; }
    public long? FK_UtilisateurValidation { get; set; }
    public DateTime? DateValidation { get; set; }

    public long? FK_UtilisateurSoumission { get; set; }
    public DateTime? DateSoumission { get; set; }
    public long? FK_UtilisateurControle { get; set; }
    public DateTime? DateControle { get; set; }
    public long? FK_UtilisateurRejet { get; set; }
    public DateTime? DateRejet { get; set; }
    public string? MotifRejet { get; set; }

    public ExerciceBudgetaire ExerciceBudgetaire { get; set; } = null!;
    public VersionBudgetaire? VersionPrecedente { get; set; }
    public ICollection<VersionBudgetaire> VersionsSuivantes { get; set; } = new List<VersionBudgetaire>();
    public Utilisateur UtilisateurCreation { get; set; } = null!;
    public Utilisateur? UtilisateurValidation { get; set; }
    public Utilisateur? UtilisateurSoumission { get; set; }
    public Utilisateur? UtilisateurControle { get; set; }
    public Utilisateur? UtilisateurRejet { get; set; }
    public ICollection<PrevisionBudgetaire> PrevisionsBudgetaires { get; set; } = new List<PrevisionBudgetaire>();
    public ICollection<TransfertBudgetaire> TransfertsBudgetaires { get; set; } = new List<TransfertBudgetaire>();
}
