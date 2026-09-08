using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IAuthService authService, ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    [AllowAnonymous]
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
        => Ok(await _authService.GetStatusAsync(cancellationToken));

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Crée le premier utilisateur (User Admin Full) uniquement si UTILISATEUR est vide.
    /// Le mot de passe est fourni par l'administrateur et hashé (ASP.NET Identity PasswordHasher).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("bootstrap")]
    public async Task<IActionResult> Bootstrap(
        [FromBody] BootstrapAdminRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.BootstrapAdminAsync(request, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var id = _currentUser.UserId;
        if (id is null)
        {
            return Unauthorized(new { message = "Jeton invalide." });
        }

        var me = await _authService.GetMeAsync(id.Value, cancellationToken);
        return me is null ? Unauthorized(new { message = "Utilisateur introuvable ou inactif." }) : Ok(me);
    }

    /// <summary>
    /// Change le mot de passe de l'utilisateur connecté (mot de passe actuel requis).
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var id = _currentUser.UserId;
        if (id is null)
        {
            return Unauthorized(new { message = "Jeton invalide." });
        }

        await _authService.ChangePasswordAsync(id.Value, request, cancellationToken);
        return Ok(new { message = "Mot de passe mis à jour." });
    }
}

public static class ClaimsPrincipalExtensions
{
    public static long? GetUtilisateurId(this ClaimsPrincipal user)
    {
        var raw =
            user.FindFirstValue(JwtTokenService.ClaimUtilisateurId)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(raw, out var id) ? id : null;
    }
}
