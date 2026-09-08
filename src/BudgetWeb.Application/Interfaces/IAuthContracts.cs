using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface IAuthRepository
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<Utilisateur?> FindByNomUtilisateurAsync(string nomUtilisateur, CancellationToken cancellationToken = default);
    Task<Utilisateur?> FindByIdAsync(long idUtilisateur, CancellationToken cancellationToken = default);
    Task UpdateDerniereConnexionAsync(long idUtilisateur, DateTime dateUtc, CancellationToken cancellationToken = default);
    Task UpdateMotDePasseHashAsync(long idUtilisateur, string motDePasseHash, CancellationToken cancellationToken = default);
    Task<Utilisateur> CreateAsync(Utilisateur utilisateur, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListProfilsAsync(long idUtilisateur, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListPermissionsIndividuellesAsync(long idUtilisateur, CancellationToken cancellationToken = default);
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(AuthUserDto utilisateur);
}

public interface IAuthService
{
    Task<AuthStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<LoginResponseDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponseDto> BootstrapAdminAsync(BootstrapAdminRequest request, CancellationToken cancellationToken = default);
    Task<AuthMeDto?> GetMeAsync(long idUtilisateur, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(long idUtilisateur, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
