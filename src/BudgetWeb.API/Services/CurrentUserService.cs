using System.Security.Claims;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;

namespace BudgetWeb.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public long? UserId
    {
        get
        {
            var raw =
                Principal?.FindFirstValue(JwtTokenService.ClaimUtilisateurId)
                ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? Principal?.FindFirstValue("sub");
            return long.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Username =>
        Principal?.FindFirstValue(JwtTokenService.ClaimNomUtilisateur)
        ?? Principal?.Identity?.Name;

    public string? DisplayName => Principal?.FindFirstValue(ClaimTypes.Name) ?? Username;

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray()
        ?? Array.Empty<string>();

    public IReadOnlyList<string> Permissions =>
        Principal?.FindAll("permission").Select(c => c.Value).Distinct().ToArray()
        ?? Array.Empty<string>();

    public bool IsInRole(string role) =>
        Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

    public bool HasPermission(string permission) =>
        Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase))
        || IsInRole(Domain.Security.AppRoles.UserAdminFull);

    public long RequireUserId()
    {
        if (UserId is null)
        {
            throw new UnauthorizedAccessException("Utilisateur authentifié introuvable. Veuillez vous reconnecter.");
        }

        return UserId.Value;
    }
}
