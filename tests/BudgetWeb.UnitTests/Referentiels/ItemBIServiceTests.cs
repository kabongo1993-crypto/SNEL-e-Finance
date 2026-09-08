using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class ItemBIServiceTests
{
    [Fact]
    public async Task Create_RefuseCodeDuplique()
    {
        var repo = new FakeItemRepository();
        repo.Items.Add(Dto(1, "BI01", niveau: 0));
        var service = new ItemBIService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateItemBIRequest("bi01", "Autre", null, null, true)));
        Assert.Contains("existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseParentInexistant()
    {
        var repo = new FakeItemRepository();
        var service = new ItemBIService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateItemBIRequest("BI02", "Enfant", 99, null, true)));
        Assert.Contains("n'existe pas", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_NiveauRacineEstZero()
    {
        var repo = new FakeItemRepository();
        var service = new ItemBIService(repo);

        var created = await service.CreateAsync(new CreateItemBIRequest("BI01", "Racine", null, null, true));
        Assert.Equal(0, created.Niveau);
        Assert.Equal(0, repo.LastNiveau);
    }

    [Fact]
    public async Task Create_NiveauEnfantEstParentPlusUn()
    {
        var repo = new FakeItemRepository();
        repo.Items.Add(Dto(1, "BI01", niveau: 1));
        var service = new ItemBIService(repo);

        var created = await service.CreateAsync(new CreateItemBIRequest("BI02", "Enfant", 1, "Ouvrages", true));
        Assert.Equal(2, created.Niveau);
        Assert.Equal("Ouvrages", created.Categorie);
    }

    [Fact]
    public async Task Update_RefuseCycleParent()
    {
        var repo = new FakeItemRepository { Cycle = true };
        repo.Items.Add(Dto(10, "BI10", niveau: 0));
        repo.Items.Add(Dto(11, "BI11", parentId: 10, niveau: 1));
        var service = new ItemBIService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(10, new UpdateItemBIRequest("BI10", "Racine", 11, null, true)));
        Assert.Contains("boucle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetActif_DesactiveLItem()
    {
        var repo = new FakeItemRepository();
        repo.Items.Add(Dto(4, "BI04", niveau: 0, actif: true));
        var service = new ItemBIService(repo);

        var updated = await service.SetActifAsync(4, new SetActifRequest(false));
        Assert.NotNull(updated);
        Assert.False(updated!.Actif);
    }

    [Fact]
    public async Task Delete_RefuseSiEnfants()
    {
        var repo = new FakeItemRepository { Enfants = 1 };
        repo.Items.Add(Dto(1, "BI01", niveau: 0));
        var service = new ItemBIService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(1));
        Assert.Contains("enfants", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Désactivez", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_RefuseSiPrevisions()
    {
        var repo = new FakeItemRepository { Previsions = 2 };
        repo.Items.Add(Dto(1, "BI01", niveau: 0));
        var service = new ItemBIService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(1));
        Assert.Contains("prévisions", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Désactivez", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetArbre_ConstruitLaHierarchie()
    {
        var repo = new FakeItemRepository();
        repo.Items.Add(Dto(1, "A", niveau: 0));
        repo.Items.Add(Dto(2, "B", parentId: 1, niveau: 1));
        repo.Items.Add(Dto(3, "C", parentId: 2, niveau: 2));
        var service = new ItemBIService(repo);

        var arbre = await service.GetArbreAsync();
        Assert.Single(arbre);
        Assert.Equal("A", arbre[0].CodeItem);
        Assert.Single(arbre[0].Enfants);
        Assert.Equal("B", arbre[0].Enfants[0].CodeItem);
        Assert.Equal("C", arbre[0].Enfants[0].Enfants[0].CodeItem);
    }

    private static ItemBIDto Dto(
        long id,
        string code,
        long? parentId = null,
        int niveau = 0,
        bool actif = true)
        => new(id, code, $"Lib {code}", parentId, null, null, niveau, null, actif, DateTime.Now, 0, 0);

    private sealed class FakeItemRepository : IItemBIRepository
    {
        public List<ItemBIDto> Items { get; } = [];
        public int Enfants { get; set; }
        public int Previsions { get; set; }
        public bool Cycle { get; set; }
        public int LastNiveau { get; private set; }

        public Task<IReadOnlyList<ItemBIDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ItemBIDto>>(Items);

        public Task<ItemBIDto?> GetByIdAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.IdItemBI == idItemBI));

        public Task<bool> ExistsByCodeAsync(string codeItem, long? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(i =>
                i.CodeItem.Equals(codeItem, StringComparison.OrdinalIgnoreCase)
                && (excludeId is null || i.IdItemBI != excludeId)));

        public Task<bool> ExistsByIdAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(i => i.IdItemBI == idItemBI));

        public Task<bool> WouldCreateCycleAsync(long idItemBI, long parentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Cycle || idItemBI == parentId);

        public Task<int> CountEnfantsAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult(Enfants);

        public Task<int> CountPrevisionsAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult(Previsions);

        public Task<ItemBIDto> CreateAsync(
            string codeItem, string libelle, long? parentId, int niveau, string? categorie, bool actif,
            CancellationToken cancellationToken = default)
        {
            LastNiveau = niveau;
            var created = new ItemBIDto(
                99, codeItem, libelle, parentId, null, null, niveau, categorie, actif, DateTime.Now, 0, 0);
            Items.Add(created);
            return Task.FromResult(created);
        }

        public Task<ItemBIDto?> UpdateAsync(
            long idItemBI, string codeItem, string libelle, long? parentId, int niveau, string? categorie, bool actif,
            CancellationToken cancellationToken = default)
        {
            LastNiveau = niveau;
            var current = Items.FirstOrDefault(i => i.IdItemBI == idItemBI);
            if (current is null) return Task.FromResult<ItemBIDto?>(null);
            var updated = current with
            {
                CodeItem = codeItem,
                Libelle = libelle,
                ParentId = parentId,
                Niveau = niveau,
                Categorie = categorie,
                Actif = actif
            };
            Items[Items.IndexOf(current)] = updated;
            return Task.FromResult<ItemBIDto?>(updated);
        }

        public Task<ItemBIDto?> SetActifAsync(long idItemBI, bool actif, CancellationToken cancellationToken = default)
        {
            var current = Items.FirstOrDefault(i => i.IdItemBI == idItemBI);
            if (current is null) return Task.FromResult<ItemBIDto?>(null);
            var updated = current with { Actif = actif };
            Items[Items.IndexOf(current)] = updated;
            return Task.FromResult<ItemBIDto?>(updated);
        }

        public Task<bool> DeleteAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.RemoveAll(i => i.IdItemBI == idItemBI) > 0);
    }
}
