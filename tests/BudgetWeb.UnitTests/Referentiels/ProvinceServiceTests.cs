using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class ProvinceServiceTests
{
    [Fact]
    public async Task List_RetourneVideSansDonneesFictives()
    {
        var rows = await CreateService().ListAsync();
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Create_KinKindKisa_UtiliseIdMetier()
    {
        var service = CreateService();
        var kin = await service.CreateAsync(new CreateProvinceRequest("KIN", "Kinshasa", true));
        var kind = await service.CreateAsync(new CreateProvinceRequest("KIND", "Province KIND", true));
        var kisa = await service.CreateAsync(new CreateProvinceRequest("KISA", "Province KISA", true));

        Assert.Equal("KIN", kin.IdProvince);
        Assert.Equal("Kinshasa", kin.Libelle);
        Assert.Equal("KIND", kind.IdProvince);
        Assert.Equal("KISA", kisa.IdProvince);
        Assert.True(kin.Actif);
        Assert.Null(kin.DateModification);
    }

    [Fact]
    public async Task Create_RefuseIdVide()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().CreateAsync(new CreateProvinceRequest("", "Kinshasa")));
        Assert.Contains("identifiant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseIdEspaces()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().CreateAsync(new CreateProvinceRequest("   ", "Kinshasa")));
        Assert.Contains("identifiant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseIdDoublonApresTrim()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateProvinceRequest("KIN", "Kinshasa"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateProvinceRequest(" KIN ", "Autre")));
        Assert.Contains("existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseLibelleVide()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().CreateAsync(new CreateProvinceRequest("KIN", "")));
        Assert.Contains("libellé", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseLibelleEspaces()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().CreateAsync(new CreateProvinceRequest("KIN", "   ")));
        Assert.Contains("libellé", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseLibelleDoublonApresTrim()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateProvinceRequest("KIN", "Kinshasa"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateProvinceRequest("KIND", " Kinshasa ")));
        Assert.Equal("Une province portant ce libellé existe déjà.", ex.Message);
    }

    [Fact]
    public async Task Update_ModifieLibelleSansChangerId()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateProvinceRequest("KIN", "Kinshasa"));
        var updated = await service.UpdateAsync(
            "KIN",
            new UpdateProvinceRequest("KIN", "Kinshasa-capitale", true));
        Assert.Equal("KIN", updated!.IdProvince);
        Assert.Equal("Kinshasa-capitale", updated.Libelle);
        Assert.NotNull(updated.DateModification);
    }

    [Fact]
    public async Task Update_RenameIdSansUtilisation_Autorise()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateProvinceRequest("KIN", "Kinshasa"));
        var updated = await service.UpdateAsync(
            "KIN",
            new UpdateProvinceRequest("KIN2", "Kinshasa", true));
        Assert.Equal("KIN2", updated!.IdProvince);
        Assert.Null(await service.GetByIdAsync("KIN"));
        Assert.NotNull(await service.GetByIdAsync("KIN2"));
    }

    [Fact]
    public async Task Update_RenameIdAvecUtilisation_Refuse()
    {
        var repo = new FakeProvinceRepository();
        repo.ComptesParId["KIN"] = 1;
        var service = CreateService(repo);
        await service.CreateAsync(new CreateProvinceRequest("KIN", "Kinshasa"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync("KIN", new UpdateProvinceRequest("KIN2", "Kinshasa", true)));

        Assert.Equal(
            "Cette province est utilisée par un ou plusieurs comptes financiers. Son identifiant ne peut pas être modifié.",
            ex.Message);
        Assert.NotNull(await service.GetByIdAsync("KIN"));
    }

    [Fact]
    public async Task Update_DesactiveSansSupprimer()
    {
        var repo = new FakeProvinceRepository();
        var service = CreateService(repo);
        await service.CreateAsync(new CreateProvinceRequest("KIN", "Kinshasa"));
        var updated = await service.UpdateAsync(
            "KIN",
            new UpdateProvinceRequest("KIN", "Kinshasa", false));
        Assert.False(updated!.Actif);
        Assert.Single(repo.Items);
        Assert.Equal("KIN", repo.Items[0].IdProvince);
    }

    [Fact]
    public async Task Update_Reactive()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateProvinceRequest("KIN", "Kinshasa", false));
        var updated = await service.UpdateAsync(
            "KIN",
            new UpdateProvinceRequest("KIN", "Kinshasa", true));
        Assert.True(updated!.Actif);
    }

    [Fact]
    public async Task Create_RefuseSansPermission()
    {
        var service = new ProvinceService(new FakeProvinceRepository(), new FakeUser([]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateProvinceRequest("X", "X")));
    }

    private static ProvinceService CreateService(FakeProvinceRepository? repo = null)
        => new(repo ?? new FakeProvinceRepository(), new FakeUser([AppPermissions.ReferentielsEcrire]));

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

    private sealed class FakeProvinceRepository : IProvinceRepository
    {
        public List<Province> Items { get; } = [];
        public Dictionary<string, int> ComptesParId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<Province>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
        {
            IEnumerable<Province> q = Items;
            if (actifsSeulement == true)
                q = q.Where(p => p.Actif);
            return Task.FromResult<IReadOnlyList<Province>>(q.OrderBy(p => p.IdProvince).ToList());
        }

        public Task<Province?> GetByIdAsync(string idProvince, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(p =>
                string.Equals(p.IdProvince, idProvince, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> IdExistsAsync(string idProvince, string? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(p =>
                string.Equals(p.IdProvince, idProvince, StringComparison.OrdinalIgnoreCase)
                && (excludeId == null || !string.Equals(p.IdProvince, excludeId, StringComparison.OrdinalIgnoreCase))));

        public Task<bool> LibelleExistsAsync(string libelle, string? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(p =>
                string.Equals(p.Libelle, libelle, StringComparison.OrdinalIgnoreCase)
                && (excludeId == null || !string.Equals(p.IdProvince, excludeId, StringComparison.OrdinalIgnoreCase))));

        public Task<int> CountComptesByIdAsync(string idProvince, CancellationToken cancellationToken = default)
            => Task.FromResult(ComptesParId.TryGetValue(idProvince, out var n) ? n : 0);

        public Task<Province> AddAsync(Province entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task RenameIdAsync(string currentId, string newId, Province updated, CancellationToken cancellationToken = default)
        {
            var item = Items.First(p =>
                string.Equals(p.IdProvince, currentId, StringComparison.OrdinalIgnoreCase));
            item.IdProvince = newId;
            item.Libelle = updated.Libelle;
            item.Actif = updated.Actif;
            item.DateModification = updated.DateModification;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
