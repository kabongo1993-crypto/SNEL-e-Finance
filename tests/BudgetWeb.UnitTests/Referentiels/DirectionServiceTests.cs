using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class DirectionServiceTests
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
        var service = CreateService();
        var a = await service.CreateAsync(new CreateDirectionRequest("Kinshasa", true));
        var b = await service.CreateAsync(new CreateDirectionRequest("Lubumbashi", true));

        Assert.Equal(1, a.IdDirection);
        Assert.Equal(2, b.IdDirection);
        Assert.Equal("Kinshasa", a.Libelle);
        Assert.True(a.Actif);
        Assert.Null(a.DateModification);
    }

    [Fact]
    public async Task Create_NeDemandePasIdDirection()
    {
        var created = await CreateService().CreateAsync(new CreateDirectionRequest("Goma"));
        Assert.True(created.IdDirection > 0);
    }

    [Fact]
    public async Task Create_RefuseLibelleVide()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().CreateAsync(new CreateDirectionRequest("", true)));
        Assert.Contains("libellé", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseLibelleEspaces()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().CreateAsync(new CreateDirectionRequest("   ")));
        Assert.Contains("libellé", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseLibelleDoublonApresTrim()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateDirectionRequest("Kinshasa"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateDirectionRequest(" Kinshasa ")));
        Assert.Equal("Une direction portant ce libellé existe déjà.", ex.Message);
    }

    [Fact]
    public async Task Update_ModifieLibelle()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateDirectionRequest("Kinshasa"));
        var updated = await service.UpdateAsync(
            created.IdDirection,
            new UpdateDirectionRequest("Kinshasa Est", true));
        Assert.Equal(created.IdDirection, updated!.IdDirection);
        Assert.Equal("Kinshasa Est", updated.Libelle);
        Assert.NotNull(updated.DateModification);
    }

    [Fact]
    public async Task Update_DesactiveSansSupprimer()
    {
        var repo = new FakeDirectionRepository();
        var service = CreateService(repo);
        var created = await service.CreateAsync(new CreateDirectionRequest("Kinshasa"));
        var updated = await service.UpdateAsync(
            created.IdDirection,
            new UpdateDirectionRequest("Kinshasa", false));
        Assert.False(updated!.Actif);
        Assert.Single(repo.Items);
        Assert.Equal(created.IdDirection, repo.Items[0].IdDirection);
    }

    [Fact]
    public async Task Update_Reactive()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateDirectionRequest("Kinshasa", false));
        var updated = await service.UpdateAsync(
            created.IdDirection,
            new UpdateDirectionRequest("Kinshasa", true));
        Assert.True(updated!.Actif);
    }

    [Fact]
    public async Task Create_RefuseSansPermission()
    {
        var service = new DirectionService(new FakeDirectionRepository(), new FakeUser([]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateDirectionRequest("X")));
    }

    private static DirectionService CreateService(FakeDirectionRepository? repo = null)
        => new(repo ?? new FakeDirectionRepository(), new FakeUser([AppPermissions.ReferentielsEcrire]));

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

    private sealed class FakeDirectionRepository : IDirectionRepository
    {
        public List<DirectionTresorerie> Items { get; } = [];

        public Task<IReadOnlyList<DirectionTresorerie>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
        {
            IEnumerable<DirectionTresorerie> q = Items;
            if (actifsSeulement == true)
                q = q.Where(d => d.Actif);
            return Task.FromResult<IReadOnlyList<DirectionTresorerie>>(
                q.OrderBy(d => d.IdDirection).ToList());
        }

        public Task<DirectionTresorerie?> GetByIdAsync(long idDirection, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(d => d.IdDirection == idDirection));

        public Task<bool> LibelleExistsAsync(string libelle, long? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(d =>
                string.Equals(d.Libelle, libelle, StringComparison.OrdinalIgnoreCase)
                && (excludeId == null || d.IdDirection != excludeId)));

        public Task<DirectionTresorerie> AddAsync(DirectionTresorerie entity, CancellationToken cancellationToken = default)
        {
            if (entity.IdDirection == 0)
                entity.IdDirection = Items.Count == 0 ? 1 : Items.Max(d => d.IdDirection) + 1;
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
