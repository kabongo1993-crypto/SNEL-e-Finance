using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class BanqueServiceTests
{
    [Fact]
    public async Task List_RetourneVideSansDonneesFictives()
    {
        var service = CreateService(new FakeBanqueRepository());
        var rows = await service.ListAsync();
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Create_RefuseIdVide()
    {
        var service = CreateService(new FakeBanqueRepository());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateBanqueRequest("  ", "TEST BANK", "RDC")));
        Assert.Contains("ID Banque", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseDoublon()
    {
        var repo = new FakeBanqueRepository();
        repo.Items.Add(new Banque { IdBanque = "RAWBANK", LibelleBanque = "Rawbank", Actif = true, DateCreation = DateTime.UtcNow });
        var service = CreateService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateBanqueRequest("rawbank", "RAWBANK", "RDC")));
        Assert.Contains("existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ConserveLaCasse()
    {
        var repo = new FakeBanqueRepository();
        var service = CreateService(repo);
        var created = await service.CreateAsync(new CreateBanqueRequest("TESTBANK", "TEST BANK", "RDC"));
        Assert.Equal("TESTBANK", created.IdBanque);
        Assert.Equal("TEST BANK", created.LibelleBanque);
        Assert.True(created.Actif);
        Assert.Null(created.DateModification);
    }

    [Fact]
    public async Task Update_IdLectureSeule()
    {
        var repo = new FakeBanqueRepository();
        repo.Items.Add(new Banque
        {
            IdBanque = "TESTBANK",
            LibelleBanque = "TEST BANK",
            Pays = "RDC",
            Actif = true,
            DateCreation = DateTime.UtcNow,
        });
        var service = CreateService(repo);

        var updated = await service.UpdateAsync("TESTBANK", new UpdateBanqueRequest("TEST BANK SA", "RD Congo", false));
        Assert.NotNull(updated);
        Assert.Equal("TESTBANK", updated!.IdBanque);
        Assert.Equal("TEST BANK SA", updated.LibelleBanque);
        Assert.Equal("RD Congo", updated.Pays);
        Assert.False(updated.Actif);
        Assert.NotNull(updated.DateModification);
    }

    [Fact]
    public async Task Import_IgnoreDejaExistantesEtDoublonsLot()
    {
        var repo = new FakeBanqueRepository();
        repo.Items.Add(new Banque { IdBanque = "BCC", LibelleBanque = "BCC", Actif = true, DateCreation = DateTime.UtcNow });
        var service = CreateService(repo);

        var result = await service.ImportAsync(new ImportBanquesRequest(
        [
            new ImportBanqueItemRequest("AFRILAND", "AFRILAND BANK", "Rdc"),
            new ImportBanqueItemRequest("BCC", "BCC", "Rdc"),
            new ImportBanqueItemRequest("AFRILAND", "AFRILAND BANK bis", "Rdc"),
            new ImportBanqueItemRequest("", "SANS ID", "Rdc"),
        ]));

        Assert.Equal(4, result.Total);
        Assert.Equal(1, result.Crees);
        Assert.Equal(1, result.DejaExistantes);
        Assert.Equal(2, result.Erreurs);
        Assert.Contains(repo.Items, b => b.IdBanque == "AFRILAND");
    }

    [Fact]
    public async Task List_AutoriseLecturePaiements()
    {
        var service = new BanqueService(new FakeBanqueRepository(), new FakeUser([AppPermissions.PaiementsLire]));
        var rows = await service.ListAsync();
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Create_RefuseSansPermission()
    {
        var service = new BanqueService(new FakeBanqueRepository(), new FakeUser([]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateBanqueRequest("X", "X", null)));
    }

    private static BanqueService CreateService(FakeBanqueRepository repo)
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

    private sealed class FakeBanqueRepository : IBanqueRepository
    {
        public List<Banque> Items { get; } = [];

        public Task<IReadOnlyList<Banque>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
        {
            IEnumerable<Banque> q = Items;
            if (actifsSeulement == true)
                q = q.Where(b => b.Actif);
            return Task.FromResult<IReadOnlyList<Banque>>(q.OrderBy(b => b.IdBanque).ToList());
        }

        public Task<Banque?> GetByIdAsync(string idBanque, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(b =>
                string.Equals(b.IdBanque, idBanque, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> ExistsAsync(string idBanque, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(b =>
                string.Equals(b.IdBanque, idBanque, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Items.Select(b => b.IdBanque).ToList());

        public Task<Banque> AddAsync(Banque entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task AddRangeInTransactionAsync(IReadOnlyList<Banque> entities, CancellationToken cancellationToken = default)
        {
            Items.AddRange(entities);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
