using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class GroupeTypeCompteServiceTests
{
    [Fact]
    public async Task List_RetourneVideSansDonneesFictives()
    {
        var service = CreateService(new FakeGroupeTypeCompteRepository());
        var rows = await service.ListAsync();
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Create_AttribueIdentifiantNumeriqueSequentiel()
    {
        var repo = new FakeGroupeTypeCompteRepository();
        var service = CreateService(repo);

        var a = await service.CreateAsync(new CreateGroupeTypeCompteRequest("Comptes courants", true));
        var b = await service.CreateAsync(new CreateGroupeTypeCompteRequest("Comptes dédiés", true));

        Assert.Equal(1, a.IdGroupeTypeCompte);
        Assert.Equal(2, b.IdGroupeTypeCompte);
        Assert.Equal("Comptes courants", a.Libelle);
        Assert.Null(a.DateModification);
        Assert.True(a.Actif);
    }

    [Fact]
    public async Task Create_RefuseLibelleVide()
    {
        var service = CreateService(new FakeGroupeTypeCompteRepository());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateGroupeTypeCompteRequest("   ", true)));
        Assert.Contains("libellé", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseDoublon()
    {
        var repo = new FakeGroupeTypeCompteRepository();
        var service = CreateService(repo);
        await service.CreateAsync(new CreateGroupeTypeCompteRequest("Comptes courants"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateGroupeTypeCompteRequest("comptes courants")));
        Assert.Equal("Un groupe de types de comptes portant ce libellé existe déjà.", ex.Message);
    }

    [Fact]
    public async Task Update_IdLectureSeuleEtActif()
    {
        var repo = new FakeGroupeTypeCompteRepository();
        var service = CreateService(repo);
        var created = await service.CreateAsync(new CreateGroupeTypeCompteRequest("Comptes courants"));

        var updated = await service.UpdateAsync(
            created.IdGroupeTypeCompte,
            new UpdateGroupeTypeCompteRequest("Comptes courants SNEL", false));

        Assert.NotNull(updated);
        Assert.Equal(created.IdGroupeTypeCompte, updated!.IdGroupeTypeCompte);
        Assert.Equal("Comptes courants SNEL", updated.Libelle);
        Assert.False(updated.Actif);
        Assert.NotNull(updated.DateModification);
    }

    [Fact]
    public async Task Update_RefuseDoublon()
    {
        var repo = new FakeGroupeTypeCompteRepository();
        var service = CreateService(repo);
        var a = await service.CreateAsync(new CreateGroupeTypeCompteRequest("Comptes courants"));
        await service.CreateAsync(new CreateGroupeTypeCompteRequest("Comptes dédiés"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(a.IdGroupeTypeCompte, new UpdateGroupeTypeCompteRequest("Comptes dédiés", true)));
        Assert.Equal("Un groupe de types de comptes portant ce libellé existe déjà.", ex.Message);
    }

    [Fact]
    public async Task List_AutoriseLecturePaiements()
    {
        var service = new GroupeTypeCompteService(
            new FakeGroupeTypeCompteRepository(),
            new FakeUser([AppPermissions.PaiementsLire]));
        var rows = await service.ListAsync();
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Create_RefuseSansPermission()
    {
        var service = new GroupeTypeCompteService(new FakeGroupeTypeCompteRepository(), new FakeUser([]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateGroupeTypeCompteRequest("X")));
    }

    private static GroupeTypeCompteService CreateService(FakeGroupeTypeCompteRepository repo)
        => new(repo, new FakeUser([AppPermissions.ReferentielsEcrire]));

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

        public Task<IReadOnlyList<GroupeTypeCompte>> ListAsync(
            bool? actifsSeulement,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<GroupeTypeCompte> q = Items;
            if (actifsSeulement == true)
                q = q.Where(g => g.Actif);
            return Task.FromResult<IReadOnlyList<GroupeTypeCompte>>(
                q.OrderBy(g => g.IdGroupeTypeCompte).ToList());
        }

        public Task<GroupeTypeCompte?> GetByIdAsync(long idGroupeTypeCompte, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(g => g.IdGroupeTypeCompte == idGroupeTypeCompte));

        public Task<bool> LibelleExistsAsync(
            string libelle,
            long? excludeId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(g =>
                string.Equals(g.Libelle, libelle, StringComparison.OrdinalIgnoreCase)
                && (excludeId == null || g.IdGroupeTypeCompte != excludeId)));

        public Task<GroupeTypeCompte> AddAsync(GroupeTypeCompte entity, CancellationToken cancellationToken = default)
        {
            if (entity.IdGroupeTypeCompte == 0)
                entity.IdGroupeTypeCompte = Items.Count == 0 ? 1 : Items.Max(g => g.IdGroupeTypeCompte) + 1;
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
