using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace BudgetWeb.UnitTests.Auth;

public class AuthServiceTests
{
    [Fact]
    public async Task Status_NeedsBootstrap_Quand_Aucun_Utilisateur()
    {
        var service = CreateService(out _);
        var status = await service.GetStatusAsync();
        Assert.True(status.NeedsBootstrap);
        Assert.Equal(0, status.UtilisateurCount);
    }

    [Fact]
    public async Task Login_Refuse_Si_Champs_Vides()
    {
        var service = CreateService(out _);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginRequest("", "")));
    }

    [Fact]
    public async Task Login_Refuse_Si_Utilisateur_Inconnu()
    {
        var service = CreateService(out _);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginRequest("ghost", "secret")));
        Assert.Contains("incorrect", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_Refuse_Si_Inactif()
    {
        var hasher = new PasswordHasher<Utilisateur>();
        var user = CreateUser(hasher, "bob", "Secret123!", AppRoles.MatriculeAdminFull);
        user.Actif = false;
        var service = CreateService(out _, user);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginRequest("bob", "Secret123!")));
        Assert.Contains("désactivé", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_Valide_AdminFull_Retourne_Role()
    {
        var hasher = new PasswordHasher<Utilisateur>();
        var user = CreateUser(hasher, "erick", "Secret123!", AppRoles.MatriculeAdminFull);
        var service = CreateService(out var repo, user);

        var result = await service.LoginAsync(new LoginRequest("erick", "Secret123!"));

        Assert.Equal("token-test", result.AccessToken);
        Assert.Contains(AppRoles.UserAdminFull, result.Utilisateur.Roles);
        Assert.Contains(AppPermissions.AdminAll, result.Utilisateur.Permissions);
        Assert.True(repo.DerniereConnexionUpdated);
    }

    [Fact]
    public async Task Bootstrap_Cree_AdminFull_Si_Base_Vide()
    {
        var service = CreateService(out var repo);
        var result = await service.BootstrapAdminAsync(new BootstrapAdminRequest(
            "admin.snel",
            "MotDePasseSecurise1",
            "Administrateur",
            "SNEL",
            null,
            "admin@snel.cd"));

        Assert.Equal(1, repo.Users.Count);
        Assert.Equal(AppRoles.MatriculeAdminFull, repo.Users[0].Matricule);
        Assert.Contains(AppRoles.UserAdminFull, result.Utilisateur.Roles);
        Assert.False(string.IsNullOrWhiteSpace(repo.Users[0].MotDePasseHash));
        Assert.DoesNotContain("MotDePasseSecurise1", repo.Users[0].MotDePasseHash);
    }

    [Fact]
    public async Task Bootstrap_Refuse_Si_Utilisateur_Existe()
    {
        var hasher = new PasswordHasher<Utilisateur>();
        var existing = CreateUser(hasher, "deja", "Secret123!", "M-001");
        var service = CreateService(out _, existing);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.BootstrapAdminAsync(new BootstrapAdminRequest(
                "admin.snel", "MotDePasseSecurise1", "Admin", null, null, null)));
    }

    private static AuthService CreateService(out FakeAuthRepository repo, Utilisateur? user = null)
    {
        repo = new FakeAuthRepository(user);
        return new AuthService(repo, new FakeJwtTokenService());
    }

    private static Utilisateur CreateUser(
        PasswordHasher<Utilisateur> hasher,
        string login,
        string password,
        string matricule)
    {
        var user = new Utilisateur
        {
            IdUtilisateur = 42,
            NomUtilisateur = login,
            Nom = "Test",
            Prenom = "User",
            Matricule = matricule,
            Actif = true,
            DateCreation = DateTime.UtcNow
        };
        user.MotDePasseHash = hasher.HashPassword(user, password);
        return user;
    }

    private sealed class FakeAuthRepository : IAuthRepository
    {
        public List<Utilisateur> Users { get; } = [];
        public bool DerniereConnexionUpdated { get; private set; }

        public FakeAuthRepository(Utilisateur? user)
        {
            if (user is not null) Users.Add(user);
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Users.Count);

        public Task<Utilisateur?> FindByNomUtilisateurAsync(string nomUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult(
                Users.FirstOrDefault(u =>
                    string.Equals(u.NomUtilisateur, nomUtilisateur, StringComparison.OrdinalIgnoreCase)));

        public Task<Utilisateur?> FindByIdAsync(long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.IdUtilisateur == idUtilisateur));

        public Task UpdateDerniereConnexionAsync(long idUtilisateur, DateTime dateUtc, CancellationToken cancellationToken = default)
        {
            DerniereConnexionUpdated = true;
            return Task.CompletedTask;
        }

        public Task UpdateMotDePasseHashAsync(long idUtilisateur, string motDePasseHash, CancellationToken cancellationToken = default)
        {
            var user = Users.FirstOrDefault(u => u.IdUtilisateur == idUtilisateur);
            if (user is not null)
            {
                user.MotDePasseHash = motDePasseHash;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListProfilsAsync(
            long idUtilisateur,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(
                Profils.TryGetValue(idUtilisateur, out var p) ? p : []);

        public Task<IReadOnlyList<string>> ListPermissionsIndividuellesAsync(
            long idUtilisateur,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(
                PermissionsIndividuelles.TryGetValue(idUtilisateur, out var p) ? p : []);

        public Dictionary<long, List<string>> Profils { get; } = new();
        public Dictionary<long, List<string>> PermissionsIndividuelles { get; } = new();

        public Task<Utilisateur> CreateAsync(Utilisateur utilisateur, CancellationToken cancellationToken = default)
        {
            utilisateur.IdUtilisateur = Users.Count + 1;
            Users.Add(utilisateur);
            return Task.FromResult(utilisateur);
        }
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public (string Token, DateTime ExpiresAtUtc) CreateToken(AuthUserDto utilisateur)
            => ("token-test", DateTime.UtcNow.AddHours(1));
    }
}
