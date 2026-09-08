using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class TypeBudgetServiceTests
{
    [Fact]
    public async Task Create_RefuseCodeVide()
    {
        var service = new TypeBudgetService(new FakeTypeBudgetRepository());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateTypeBudgetRequest("  ", "Libelle", 4, true)));
        Assert.Contains("obligatoire", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseOrdreManquant()
    {
        var service = new TypeBudgetService(new FakeTypeBudgetRepository());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateTypeBudgetRequest("XX", "Libelle", null, true)));
        Assert.Contains("ordre", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseCodeDuplique()
    {
        var repo = new FakeTypeBudgetRepository();
        repo.Items.Add(Dto(1, "DC", 1));
        var service = new TypeBudgetService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateTypeBudgetRequest("dc", "Autre", 9, true)));
        Assert.Contains("existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseOrdreDuplique()
    {
        var repo = new FakeTypeBudgetRepository();
        repo.Items.Add(Dto(1, "DC", 1));
        var service = new TypeBudgetService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateTypeBudgetRequest("XX", "Autre", 1, true)));
        Assert.Contains("ordre d'affichage", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_AutoriseMemeCodeEtOrdre()
    {
        var repo = new FakeTypeBudgetRepository();
        repo.Items.Add(Dto(1, "DC", 1, true));
        var service = new TypeBudgetService(repo);

        var updated = await service.UpdateAsync(1, new UpdateTypeBudgetRequest("DC", "Dépenses Courantes", 1, false));
        Assert.NotNull(updated);
        Assert.False(updated!.Actif);
        Assert.Equal("DC", updated.CodeType);
    }

    [Fact]
    public async Task Delete_RefuseSiPrevisions()
    {
        var repo = new FakeTypeBudgetRepository { Previsions = 3 };
        repo.Items.Add(Dto(2, "AE", 2));
        var service = new TypeBudgetService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(2));
        Assert.Equal(
            "Ce type de budget ne peut pas être supprimé car il est utilisé par une ou plusieurs prévisions budgétaires.",
            ex.Message);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task Delete_AutoriseSansDependance()
    {
        var repo = new FakeTypeBudgetRepository { Previsions = 0 };
        repo.Items.Add(Dto(9, "ZZ", 9));
        var service = new TypeBudgetService(repo);

        Assert.True(await service.DeleteAsync(9));
        Assert.Empty(repo.Items);
    }

    private static TypeBudgetDto Dto(long id, string code, int ordre, bool actif = true)
        => new(id, code, $"Lib {code}", ordre, actif, 0);

    private sealed class FakeTypeBudgetRepository : ITypeBudgetRepository
    {
        public List<TypeBudgetDto> Items { get; } = [];
        public int Previsions { get; set; }

        public Task<IReadOnlyList<TypeBudgetDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TypeBudgetDto>>(Items);

        public Task<TypeBudgetDto?> GetByIdAsync(long idTypeBudget, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(t => t.IdTypeBudget == idTypeBudget));

        public Task<bool> ExistsByCodeAsync(string codeType, long? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(t =>
                t.CodeType.Equals(codeType, StringComparison.OrdinalIgnoreCase)
                && (excludeId is null || t.IdTypeBudget != excludeId)));

        public Task<bool> ExistsByOrdreAsync(int ordreAffichage, long? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(t =>
                t.OrdreAffichage == ordreAffichage
                && (excludeId is null || t.IdTypeBudget != excludeId)));

        public Task<int> CountPrevisionsAsync(long idTypeBudget, CancellationToken cancellationToken = default)
            => Task.FromResult(Previsions);

        public Task<TypeBudgetDto> CreateAsync(string codeType, string libelle, int ordreAffichage, bool actif, CancellationToken cancellationToken = default)
        {
            var created = new TypeBudgetDto(Items.Count + 1, codeType, libelle, ordreAffichage, actif, 0);
            Items.Add(created);
            return Task.FromResult(created);
        }

        public Task<TypeBudgetDto?> UpdateAsync(long idTypeBudget, string codeType, string libelle, int ordreAffichage, bool actif, CancellationToken cancellationToken = default)
        {
            var index = Items.FindIndex(t => t.IdTypeBudget == idTypeBudget);
            if (index < 0) return Task.FromResult<TypeBudgetDto?>(null);
            var updated = Items[index] with
            {
                CodeType = codeType,
                Libelle = libelle,
                OrdreAffichage = ordreAffichage,
                Actif = actif
            };
            Items[index] = updated;
            return Task.FromResult<TypeBudgetDto?>(updated);
        }

        public Task<bool> DeleteAsync(long idTypeBudget, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.RemoveAll(t => t.IdTypeBudget == idTypeBudget) > 0);
    }
}
