using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class TypeCompteServiceTests
{
    [Fact]
    public async Task List_RetourneVideSansDonneesFictives()
    {
        var service = CreateService();
        var rows = await service.ListAsync();
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Create_Fct_UtiliseCodeCommeIdentifiant()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateTypeCompteRequest("FCT", "FCT", 1, true));

        Assert.Equal("FCT", created.Code);
        Assert.Equal("FCT", created.Libelle);
        Assert.Equal("Comptes courants", created.GroupeLibelle);
        Assert.Equal(1, created.IdGroupeTypeCompte);
        Assert.True(created.Actif);
        Assert.Null(created.DateModification);
    }

    [Fact]
    public async Task Create_RefuseCodeVide()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateTypeCompteRequest("  ", "FCT", 1)));
        Assert.Contains("code", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseLibelleVide()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateTypeCompteRequest("FCT", "  ", 1)));
        Assert.Contains("libellé", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseCodeDoublon()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateTypeCompteRequest("FCT", "FCT", 1));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateTypeCompteRequest("fct", "Autre", 1)));
        Assert.Contains("existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseGroupeInexistant()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateTypeCompteRequest("X", "X", 99)));
        Assert.Contains("introuvable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseGroupeInactif()
    {
        var groupes = new FakeGroupeTypeCompteRepository();
        groupes.Items.Add(new GroupeTypeCompte
        {
            IdGroupeTypeCompte = 1,
            Libelle = "Inactif",
            Actif = false,
            DateCreation = DateTime.UtcNow,
        });
        var service = CreateService(new FakeTypeCompteRepository(), groupes);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateTypeCompteRequest("X", "X", 1)));
        Assert.Contains("inactif", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_ModifieLibelleSansChangerLeCode()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateTypeCompteRequest("FCT", "FCT", 1));

        var updated = await service.UpdateAsync(
            "FCT",
            new UpdateTypeCompteRequest("FCT", "Fonctionnement", 1, true));

        Assert.NotNull(updated);
        Assert.Equal("FCT", updated!.Code);
        Assert.Equal("Fonctionnement", updated.Libelle);
        Assert.True(updated.Actif);
        Assert.NotNull(updated.DateModification);
    }

    [Fact]
    public async Task Update_RenameCodeSansUtilisation_Autorise()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateTypeCompteRequest("TESTTYPE", "Type test", 1));

        var updated = await service.UpdateAsync(
            "TESTTYPE",
            new UpdateTypeCompteRequest("TEST", "Type test", 1, true));

        Assert.NotNull(updated);
        Assert.Equal("TEST", updated!.Code);
        Assert.Null(await service.GetByCodeAsync("TESTTYPE"));
        Assert.NotNull(await service.GetByCodeAsync("TEST"));
    }

    [Fact]
    public async Task Update_RenameCodeAvecUtilisation_Refuse()
    {
        var repo = new FakeTypeCompteRepository();
        repo.ComptesParCode["FCT"] = 1;
        var service = CreateService(repo);
        await service.CreateAsync(new CreateTypeCompteRequest("FCT", "FCT", 1));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync("FCT", new UpdateTypeCompteRequest("FCT2", "FCT", 1, true)));

        Assert.Equal(
            "Ce type de compte est utilisé par un ou plusieurs comptes financiers. Son code ne peut pas être modifié.",
            ex.Message);
        Assert.NotNull(await service.GetByCodeAsync("FCT"));
    }

    [Fact]
    public async Task Update_DesactiveSansSupprimer()
    {
        var repo = new FakeTypeCompteRepository();
        var service = CreateService(repo);
        await service.CreateAsync(new CreateTypeCompteRequest("FCT", "FCT", 1));

        var updated = await service.UpdateAsync(
            "FCT",
            new UpdateTypeCompteRequest("FCT", "FCT", 1, false));

        Assert.NotNull(updated);
        Assert.False(updated!.Actif);
        Assert.Single(repo.Items);
        Assert.Equal("FCT", repo.Items[0].Code);
    }

    [Fact]
    public async Task Create_RefuseSansPermission()
    {
        var service = new TypeCompteService(
            new FakeTypeCompteRepository(),
            GroupesActifs(),
            new FakeUser([]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateTypeCompteRequest("X", "X", 1)));
    }

    private static TypeCompteService CreateService(
        FakeTypeCompteRepository? repo = null,
        FakeGroupeTypeCompteRepository? groupes = null)
        => new(
            repo ?? new FakeTypeCompteRepository(),
            groupes ?? GroupesActifs(),
            new FakeUser([AppPermissions.ReferentielsEcrire]));

    private static FakeGroupeTypeCompteRepository GroupesActifs()
    {
        var repo = new FakeGroupeTypeCompteRepository();
        repo.Items.Add(new GroupeTypeCompte
        {
            IdGroupeTypeCompte = 1,
            Libelle = "Comptes courants",
            Actif = true,
            DateCreation = DateTime.UtcNow,
        });
        return repo;
    }

    private sealed class FakeUser(IReadOnlyList<string> permissions) : ICurrentUserService
    {
        public long? UserId => 1;
        public string? Username => "test";
        public string? DisplayName => "Test";
        public IReadOnlyList<string> Roles { get; } = [];
        public IReadOnlyList<string> Permissions { get; } = permissions;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => false;
        public bool HasPermission(string permission) =>
            Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));
        public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException();
    }

    private sealed class FakeGroupeTypeCompteRepository : IGroupeTypeCompteRepository
    {
        public List<GroupeTypeCompte> Items { get; } = [];

        public Task<IReadOnlyList<GroupeTypeCompte>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
        {
            IEnumerable<GroupeTypeCompte> q = Items;
            if (actifsSeulement == true)
                q = q.Where(g => g.Actif);
            return Task.FromResult<IReadOnlyList<GroupeTypeCompte>>(q.ToList());
        }

        public Task<GroupeTypeCompte?> GetByIdAsync(long idGroupeTypeCompte, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(g => g.IdGroupeTypeCompte == idGroupeTypeCompte));

        public Task<bool> LibelleExistsAsync(string libelle, long? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GroupeTypeCompte> AddAsync(GroupeTypeCompte entity, CancellationToken cancellationToken = default)
            => Task.FromResult(entity);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeTypeCompteRepository : ITypeCompteRepository
    {
        public List<TypeCompte> Items { get; } = [];
        public Dictionary<string, int> ComptesParCode { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<TypeCompte>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
        {
            IEnumerable<TypeCompte> q = Items;
            if (actifsSeulement == true)
                q = q.Where(t => t.Actif);
            return Task.FromResult<IReadOnlyList<TypeCompte>>(q.OrderBy(t => t.Code).ToList());
        }

        public Task<TypeCompte?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(t =>
                string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> CodeExistsAsync(string code, string? excludeCode, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(t =>
                string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase)
                && (excludeCode == null || !string.Equals(t.Code, excludeCode, StringComparison.OrdinalIgnoreCase))));

        public Task<int> CountComptesByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(ComptesParCode.TryGetValue(code, out var n) ? n : 0);

        public Task<TypeCompte> AddAsync(TypeCompte entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task RenameCodeAsync(string currentCode, string newCode, TypeCompte updated, CancellationToken cancellationToken = default)
        {
            var item = Items.First(t =>
                string.Equals(t.Code, currentCode, StringComparison.OrdinalIgnoreCase));
            item.Code = newCode;
            item.Libelle = updated.Libelle;
            item.FK_GroupeTypeCompte = updated.FK_GroupeTypeCompte;
            item.Actif = updated.Actif;
            item.DateModification = updated.DateModification;
            item.GroupeTypeCompte = updated.GroupeTypeCompte;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
