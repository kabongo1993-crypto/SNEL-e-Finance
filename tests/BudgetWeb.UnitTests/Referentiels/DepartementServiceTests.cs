using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class DepartementServiceTests
{
    [Fact]
    public async Task Create_RefuseCodeVide()
    {
        var service = new DepartementService(new FakeDepartementRepository());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateDepartementRequest("  ", "Libelle", true)));
        Assert.Contains("obligatoire", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseCodeDuplique()
    {
        var repo = new FakeDepartementRepository();
        repo.Items.Add(Dto(1, "DFC", "Direction", true, 0));
        var service = new DepartementService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateDepartementRequest("dfc", "Autre", true)));
        Assert.Contains("existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_AutoriseMemeCode()
    {
        var repo = new FakeDepartementRepository();
        repo.Items.Add(Dto(1, "DFC", "Direction", true, 0));
        var service = new DepartementService(repo);

        var updated = await service.UpdateAsync(1, new UpdateDepartementRequest("DFC", "Direction Financière", false));
        Assert.NotNull(updated);
        Assert.Equal("Direction Financière", updated!.Libelle);
        Assert.False(updated.Actif);
    }

    [Fact]
    public async Task Delete_RefuseSiUnitesBudgetaires()
    {
        var repo = new FakeDepartementRepository();
        repo.Items.Add(Dto(1, "DCG", "Direction", true, 12));
        var service = new DepartementService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(1));
        Assert.Equal(
            "Ce département ne peut pas être supprimé car il est utilisé par une ou plusieurs unités budgétaires.",
            ex.Message);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task Delete_AutoriseSansDependance()
    {
        var repo = new FakeDepartementRepository();
        repo.Items.Add(Dto(2, "ZZTEST", "Test", true, 0));
        var service = new DepartementService(repo);

        var deleted = await service.DeleteAsync(2);
        Assert.True(deleted);
        Assert.Empty(repo.Items);
    }

    private static DepartementDto Dto(long id, string code, string libelle, bool actif, int ub)
        => new(id, code, libelle, actif, DateTime.UtcNow, ub);

    private sealed class FakeDepartementRepository : IDepartementRepository
    {
        public List<DepartementDto> Items { get; } = [];

        public Task<IReadOnlyList<DepartementDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DepartementDto>>(Items);

        public Task<DepartementDto?> GetByIdAsync(long idDepartement, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(d => d.IdDepartement == idDepartement));

        public Task<bool> ExistsByCodeAsync(string code, long? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(d =>
                d.Code.Equals(code, StringComparison.OrdinalIgnoreCase)
                && (excludeId is null || d.IdDepartement != excludeId)));

        public Task<int> CountUnitesBudgetairesAsync(long idDepartement, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(d => d.IdDepartement == idDepartement)?.NombreUnitesBudgetaires ?? 0);

        public Task<DepartementDto> CreateAsync(string code, string libelle, bool actif, CancellationToken cancellationToken = default)
        {
            var created = new DepartementDto(Items.Count + 1, code, libelle, actif, DateTime.UtcNow, 0);
            Items.Add(created);
            return Task.FromResult(created);
        }

        public Task<DepartementDto?> UpdateAsync(long idDepartement, string code, string libelle, bool actif, CancellationToken cancellationToken = default)
        {
            var index = Items.FindIndex(d => d.IdDepartement == idDepartement);
            if (index < 0) return Task.FromResult<DepartementDto?>(null);
            var current = Items[index];
            var updated = current with { Code = code, Libelle = libelle, Actif = actif };
            Items[index] = updated;
            return Task.FromResult<DepartementDto?>(updated);
        }

        public Task<bool> DeleteAsync(long idDepartement, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.RemoveAll(d => d.IdDepartement == idDepartement) > 0);
    }
}
