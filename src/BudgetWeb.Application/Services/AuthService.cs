using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Microsoft.AspNetCore.Identity;

namespace BudgetWeb.Application.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _repository;
    private readonly IJwtTokenService _tokenService;
    private readonly PasswordHasher<Utilisateur> _passwordHasher = new();

    public AuthService(IAuthRepository repository, IJwtTokenService tokenService)
    {
        _repository = repository;
        _tokenService = tokenService;
    }

    public async Task<AuthStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var count = await _repository.CountAsync(cancellationToken);
        return new AuthStatusDto(NeedsBootstrap: count == 0, UtilisateurCount: count);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var nom = (request.NomUtilisateur ?? string.Empty).Trim();
        var motDePasse = request.MotDePasse ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nom) || string.IsNullOrEmpty(motDePasse))
        {
            throw new UnauthorizedAccessException("Nom d'utilisateur ou mot de passe incorrect.");
        }

        var utilisateur = await _repository.FindByNomUtilisateurAsync(nom, cancellationToken);
        if (utilisateur is null)
        {
            throw new UnauthorizedAccessException("Nom d'utilisateur ou mot de passe incorrect.");
        }

        if (!utilisateur.Actif)
        {
            throw new UnauthorizedAccessException("Ce compte utilisateur est désactivé.");
        }

        if (string.IsNullOrWhiteSpace(utilisateur.MotDePasseHash))
        {
            throw new UnauthorizedAccessException("Nom d'utilisateur ou mot de passe incorrect.");
        }

        var result = _passwordHasher.VerifyHashedPassword(utilisateur, utilisateur.MotDePasseHash, motDePasse);
        if (result is PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Nom d'utilisateur ou mot de passe incorrect.");
        }

        await _repository.UpdateDerniereConnexionAsync(utilisateur.IdUtilisateur, DateTime.UtcNow, cancellationToken);

        var profils = await _repository.ListProfilsAsync(utilisateur.IdUtilisateur, cancellationToken);
        var individuelles = await _repository.ListPermissionsIndividuellesAsync(utilisateur.IdUtilisateur, cancellationToken);
        return BuildLoginResponse(utilisateur, profils, individuelles);
    }

    public async Task<LoginResponseDto> BootstrapAdminAsync(
        BootstrapAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        var count = await _repository.CountAsync(cancellationToken);
        if (count > 0)
        {
            throw new InvalidOperationException(
                "L'initialisation est impossible : des utilisateurs existent déjà dans UTILISATEUR. " +
                "Utilisez la connexion standard.");
        }

        var nomUtilisateur = (request.NomUtilisateur ?? string.Empty).Trim();
        var motDePasse = request.MotDePasse ?? string.Empty;
        var nom = (request.Nom ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(nomUtilisateur) || nomUtilisateur.Length < 3)
        {
            throw new InvalidOperationException("Le nom d'utilisateur doit contenir au moins 3 caractères.");
        }

        if (string.IsNullOrWhiteSpace(motDePasse) || motDePasse.Length < 8)
        {
            throw new InvalidOperationException("Le mot de passe initial doit contenir au moins 8 caractères.");
        }

        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new InvalidOperationException("Le nom est obligatoire.");
        }

        var utilisateur = new Utilisateur
        {
            Matricule = AppRoles.MatriculeAdminFull,
            Nom = nom,
            Prenom = string.IsNullOrWhiteSpace(request.Prenom) ? null : request.Prenom.Trim(),
            Postnom = string.IsNullOrWhiteSpace(request.Postnom) ? null : request.Postnom.Trim(),
            NomUtilisateur = nomUtilisateur,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Actif = true,
            DateCreation = DateTime.UtcNow,
            DateDerniereConnexion = DateTime.UtcNow
        };

        utilisateur.MotDePasseHash = _passwordHasher.HashPassword(utilisateur, motDePasse);

        var created = await _repository.CreateAsync(utilisateur, cancellationToken);
        return BuildLoginResponse(created, [], []);
    }

    public async Task<AuthMeDto?> GetMeAsync(long idUtilisateur, CancellationToken cancellationToken = default)
    {
        var utilisateur = await _repository.FindByIdAsync(idUtilisateur, cancellationToken);
        if (utilisateur is null || !utilisateur.Actif)
        {
            return null;
        }

        var profils = await _repository.ListProfilsAsync(idUtilisateur, cancellationToken);
        var individuelles = await _repository.ListPermissionsIndividuellesAsync(idUtilisateur, cancellationToken);
        return new AuthMeDto(MapUser(utilisateur, profils, individuelles));
    }

    public async Task ChangePasswordAsync(
        long idUtilisateur,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var actuel = request.MotDePasseActuel ?? string.Empty;
        var nouveau = request.NouveauMotDePasse ?? string.Empty;
        var confirmation = request.ConfirmationMotDePasse ?? string.Empty;

        if (string.IsNullOrEmpty(actuel) || string.IsNullOrEmpty(nouveau))
        {
            throw new InvalidOperationException("Le mot de passe actuel et le nouveau mot de passe sont obligatoires.");
        }

        if (nouveau.Length < 8)
        {
            throw new InvalidOperationException("Le nouveau mot de passe doit contenir au moins 8 caractères.");
        }

        if (!string.Equals(nouveau, confirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("La confirmation ne correspond pas au nouveau mot de passe.");
        }

        if (string.Equals(actuel, nouveau, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Le nouveau mot de passe doit être différent de l'actuel.");
        }

        var utilisateur = await _repository.FindByIdAsync(idUtilisateur, cancellationToken);
        if (utilisateur is null || !utilisateur.Actif)
        {
            throw new UnauthorizedAccessException("Utilisateur introuvable ou inactif.");
        }

        if (string.IsNullOrWhiteSpace(utilisateur.MotDePasseHash))
        {
            throw new UnauthorizedAccessException("Mot de passe actuel incorrect.");
        }

        var verify = _passwordHasher.VerifyHashedPassword(utilisateur, utilisateur.MotDePasseHash, actuel);
        if (verify is PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Mot de passe actuel incorrect.");
        }

        var newHash = _passwordHasher.HashPassword(utilisateur, nouveau);
        await _repository.UpdateMotDePasseHashAsync(idUtilisateur, newHash, cancellationToken);
    }

    private LoginResponseDto BuildLoginResponse(
        Utilisateur utilisateur,
        IReadOnlyList<string> profils,
        IReadOnlyList<string>? permissionsIndividuelles = null)
    {
        var dto = MapUser(utilisateur, profils, permissionsIndividuelles);
        var (token, expires) = _tokenService.CreateToken(dto);
        return new LoginResponseDto(token, "Bearer", expires, dto);
    }

    public static AuthUserDto MapUser(
        Utilisateur u,
        IReadOnlyList<string>? profils = null,
        IReadOnlyList<string>? permissionsIndividuelles = null)
    {
        var isAdminFull = string.Equals(
            u.Matricule?.Trim(),
            AppRoles.MatriculeAdminFull,
            StringComparison.OrdinalIgnoreCase);
        var isDg = string.Equals(
            u.Matricule?.Trim(),
            AppRoles.MatriculeDg,
            StringComparison.OrdinalIgnoreCase);

        var roles = new List<string>();
        var permissions = new List<string>();
        if (isAdminFull)
        {
            roles.Add(AppRoles.UserAdminFull);
            roles.Add(AppRoles.Admin);
            permissions.AddRange(AppPermissions.AdminFull);
        }

        if (isDg)
        {
            roles.Add(AppRoles.UserDg);
            foreach (var p in AppPermissions.Dg)
            {
                if (!permissions.Contains(p, StringComparer.OrdinalIgnoreCase))
                    permissions.Add(p);
            }
        }

        foreach (var code in profils ?? [])
        {
            var profil = code.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(profil))
                continue;
            if (!roles.Contains(profil, StringComparer.OrdinalIgnoreCase))
                roles.Add(profil);
            foreach (var p in AppPermissions.PermissionsPourProfil(profil))
            {
                if (!permissions.Contains(p, StringComparer.OrdinalIgnoreCase))
                    permissions.Add(p);
            }
        }

        foreach (var raw in permissionsIndividuelles ?? [])
        {
            var code = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;
            if (!permissions.Contains(code, StringComparer.OrdinalIgnoreCase))
                permissions.Add(code);
        }

        return new AuthUserDto(
            u.IdUtilisateur,
            u.NomUtilisateur,
            u.Nom,
            u.Prenom,
            u.Postnom,
            u.Email,
            u.Matricule,
            u.Actif,
            roles,
            permissions);
    }
}
