namespace BudgetWeb.Domain.Entities;

/// <summary>Profil applicatif affecté à un utilisateur (dpm.PROFIL_UTILISATEUR) — sans matricule.</summary>
public class ProfilUtilisateur
{
    public long IdProfilUtilisateur { get; set; }
    public long FK_Utilisateur { get; set; }
    public string CodeProfil { get; set; } = string.Empty;

    public Utilisateur Utilisateur { get; set; } = null!;
}
