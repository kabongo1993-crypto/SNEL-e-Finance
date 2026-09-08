namespace BudgetWeb.Domain.Entities;

/// <summary>
/// Permission complémentaire attribuée directement à un utilisateur (dpm.PERMISSION_UTILISATEUR).
/// Indépendante des profils — fusionnée dans les claims JWT au login.
/// </summary>
public class PermissionUtilisateur
{
    public long IdPermissionUtilisateur { get; set; }
    public long FK_Utilisateur { get; set; }
    public string CodePermission { get; set; } = string.Empty;
    public DateTime DateAttribution { get; set; }

    public Utilisateur Utilisateur { get; set; } = null!;
}
