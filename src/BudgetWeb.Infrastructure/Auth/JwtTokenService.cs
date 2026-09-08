using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BudgetWeb.Infrastructure.Auth;

public class JwtTokenService : IJwtTokenService
{
    public const string ClaimUtilisateurId = "uid";
    public const string ClaimNomUtilisateur = "uname";

    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public static string ResolveSecret(IConfiguration configuration)
    {
        var secret = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey doit être configuré (minimum 32 caractères) dans appsettings ou les variables d'environnement.");
        }

        return secret;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(AuthUserDto utilisateur)
    {
        var issuer = _configuration["Jwt:Issuer"] ?? "BudgetWeb-SNEL";
        var audience = _configuration["Jwt:Audience"] ?? "BudgetWeb-Client";
        var secret = ResolveSecret(_configuration);

        var expiresMinutes = 480;
        if (int.TryParse(_configuration["Jwt:ExpiresMinutes"], out var configured) && configured > 0)
        {
            expiresMinutes = configured;
        }

        var expires = DateTime.UtcNow.AddMinutes(expiresMinutes);
        var displayName = string.Join(' ', new[] { utilisateur.Prenom, utilisateur.Nom }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, utilisateur.IdUtilisateur.ToString()),
            new(ClaimUtilisateurId, utilisateur.IdUtilisateur.ToString()),
            new(ClaimNomUtilisateur, utilisateur.NomUtilisateur),
            new(ClaimTypes.NameIdentifier, utilisateur.IdUtilisateur.ToString()),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(displayName) ? utilisateur.NomUtilisateur : displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        foreach (var role in utilisateur.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in utilisateur.Permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
