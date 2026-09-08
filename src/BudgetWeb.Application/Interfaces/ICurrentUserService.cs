namespace BudgetWeb.Application.Interfaces;

/// <summary>
/// Identité de l'utilisateur authentifié, résolue depuis le JWT / HttpContext.
/// Réutilisable par tous les modules métier.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    long? UserId { get; }
    string? Username { get; }
    string? DisplayName { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    bool IsInRole(string role);
    bool HasPermission(string permission);
    long RequireUserId();
}
