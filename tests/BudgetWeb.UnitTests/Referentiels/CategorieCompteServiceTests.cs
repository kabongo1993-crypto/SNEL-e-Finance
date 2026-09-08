using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class CategorieCompteServiceTests
{
    [Fact]
    public async Task List_RetourneVideSansDonneesFictives()
    {
        var rows = await CreateService().ListAsync();
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Create_AttribueIdentifiantNumeriqueSequentiel()
    {
        var created = await CreateService().CreateAsync(
            new CreateCategorieCompteRequest("Kinshasa", "Kinshasa", true));

        Assert.Equal(1, created.IdCategorieCompte);
        Assert.Equal("Kinshasa", created.Libelle);
        Assert.Equal("Kinshasa", created.Orientation);
        Assert.True(created.Actif);
        Assert.Null(created.DateModification);
    }

    [Fact]
    public async Task Create_RefuseLibelleVide()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().CreateAsync(new CreateCategorieCompteRequest("   ", null)));
        Assert.Contains("libellé", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseLibelleDoublonApresTrim()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateCategorieCompteRequest("Kinshasa", "Kinshasa"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateCategorieCompteRequest(" Kinshasa ", "Autre")));
        Assert.Equal("Une catégorie de compte portant ce libellé existe déjà.", ex.Message);
    }

    [Fact]
    public async Task Create_AccepteOrientationNull()
    {
        var created = await CreateService().CreateAsync(
            new CreateCategorieCompteRequest("Sans orientation", "  ", true));
        Assert.Null(created.Orientation);
    }

    [Fact]
    public async Task Update_ModifieLibelleEtOrientation()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateCategorieCompteRequest("Kinshasa", "Kinshasa"));

        var updated = await service.UpdateAsync(
            created.IdCategorieCompte,
            new UpdateCategorieCompteRequest("Kinshasa B", "Kinshasa B", true));

        Assert.NotNull(updated);
        Assert.Equal(created.IdCategorieCompte, updated!.IdCategorieCompte);
        Assert.Equal("Kinshasa B", updated.Libelle);
        Assert.Equal("Kinshasa B", updated.Orientation);
        Assert.NotNull(updated.DateModification);
    }

    [Fact]
    public async Task Update_DesactiveSansSupprimer()
    {
        var repo = new FakeCategorieCompteRepository();
        var service = CreateService(repo);
        var created = await service.CreateAsync(new CreateCategorieCompteRequest("Kinshasa", null));

        var updated = await service.UpdateAsync(
            created.IdCategorieCompte,
            new UpdateCategorieCompteRequest("Kinshasa", null, false));

        Assert.False(updated!.Actif);
        Assert.Single(repo.Items);
        Assert.Equal(created.IdCategorieCompte, repo.Items[0].IdCategorieCompte);
    }

    [Fact]
    public async Task Update_Reactive()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateCategorieCompteRequest("Kinshasa", null, false));
        var updated = await service.UpdateAsync(
            created.IdCategorieCompte,
            new UpdateCategorieCompteRequest("Kinshasa", null, true));
        Assert.True(updated!.Actif);
    }

    [Fact]
    public async Task Create_RefuseSansPermission()
    {
        var service = new CategorieCompteService(new FakeCategorieCompteRepository(), new FakeUser([]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateCategorieCompteRequest("X", null)));
    }

    private static CategorieCompteService CreateService(FakeCategorieCompteRepository? repo = null)
        => new(repo ?? new FakeCategorieCompteRepository(), new FakeUser([AppPermissions.ReferentielsEcrire]));

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

    private sealed class FakeCategorieCompteRepository : ICategorieCompteRepository
    {
        public List<CategorieCompte> Items { get; } = [];

        public Task<IReadOnlyList<CategorieCompte>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
        {
            IEnumerable<CategorieCompte> q = Items;
            if (actifsSeulement == true)
                q = q.Where(c => c.Actif);
            return Task.FromResult<IReadOnlyList<CategorieCompte>>(
                q.OrderBy(c => c.IdCategorieCompte).ToList());
        }

        public Task<CategorieCompte?> GetByIdAsync(long idCategorieCompte, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(c => c.IdCategorieCompte == idCategorieCompte));

        public Task<bool> LibelleExistsAsync(string libelle, long? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(c =>
                string.Equals(c.Libelle, libelle, StringComparison.OrdinalIgnoreCase)
                && (excludeId == null || c.IdCategorieCompte != excludeId)));

        public Task<CategorieCompte> AddAsync(CategorieCompte entity, CancellationToken cancellationToken = default)
        {
            if (entity.IdCategorieCompte == 0)
                entity.IdCategorieCompte = Items.Count == 0 ? 1 : Items.Max(c => c.IdCategorieCompte) + 1;
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
