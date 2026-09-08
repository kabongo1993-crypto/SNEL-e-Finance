using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class RubriqueBudgetaireServiceTests
{
    [Fact]
    public async Task Create_RefuseCodeDuplique()
    {
        var repo = new FakeRubriqueRepository();
        repo.Items.Add(Dto(1, "RB01", niveau: 0));
        var service = new RubriqueBudgetaireService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateRubriqueBudgetaireRequest("rb01", "Autre", null, true)));
        Assert.Contains("existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseParentInexistant()
    {
        var repo = new FakeRubriqueRepository();
        var service = new RubriqueBudgetaireService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateRubriqueBudgetaireRequest("RB02", "Enfant", 99, true)));
        Assert.Contains("n'existe pas", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_NiveauRacineEstZero()
    {
        var repo = new FakeRubriqueRepository();
        var service = new RubriqueBudgetaireService(repo);

        var created = await service.CreateAsync(new CreateRubriqueBudgetaireRequest("RB01", "Racine", null, true));
        Assert.Equal(0, created.Niveau);
        Assert.Equal(0, repo.LastNiveau);
    }

    [Fact]
    public async Task Create_NiveauEnfantEstParentPlusUn()
    {
        var repo = new FakeRubriqueRepository();
        repo.Items.Add(Dto(1, "RB01", niveau: 2));
        var service = new RubriqueBudgetaireService(repo);

        var created = await service.CreateAsync(new CreateRubriqueBudgetaireRequest("RB02", "Enfant", 1, true));
        Assert.Equal(3, created.Niveau);
        Assert.Equal(3, repo.LastNiveau);
    }

    [Fact]
    public async Task Update_RefuseCycleParent()
    {
        var repo = new FakeRubriqueRepository { Cycle = true };
        repo.Items.Add(Dto(10, "RB10", niveau: 0));
        repo.Items.Add(Dto(11, "RB11", parentId: 10, niveau: 1));
        var service = new RubriqueBudgetaireService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(10, new UpdateRubriqueBudgetaireRequest("RB10", "Racine", 11, true)));
        Assert.Contains("boucle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetActif_DesactiveLaRubrique()
    {
        var repo = new FakeRubriqueRepository();
        repo.Items.Add(Dto(4, "RB04", niveau: 0, actif: true));
        var service = new RubriqueBudgetaireService(repo);

        var updated = await service.SetActifAsync(4, new SetActifRequest(false));
        Assert.NotNull(updated);
        Assert.False(updated!.Actif);
    }

    [Fact]
    public async Task Delete_RefuseSiEnfants()
    {
        var repo = new FakeRubriqueRepository { Enfants = 2 };
        repo.Items.Add(Dto(1, "RB01", niveau: 0));
        var service = new RubriqueBudgetaireService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(1));
        Assert.Contains("enfants", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Désactivez", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_RefuseSiPrevisions()
    {
        var repo = new FakeRubriqueRepository { Previsions = 3 };
        repo.Items.Add(Dto(1, "RB01", niveau: 0));
        var service = new RubriqueBudgetaireService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(1));
        Assert.Contains("prévisions", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Désactivez", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetArbre_ConstruitLaHierarchie()
    {
        var repo = new FakeRubriqueRepository();
        repo.Items.Add(Dto(1, "A", niveau: 0));
        repo.Items.Add(Dto(2, "B", parentId: 1, niveau: 1));
        repo.Items.Add(Dto(3, "C", parentId: 2, niveau: 2));
        var service = new RubriqueBudgetaireService(repo);

        var arbre = await service.GetArbreAsync();
        Assert.Single(arbre);
        Assert.Equal("A", arbre[0].CodeRB);
        Assert.Single(arbre[0].Enfants);
        Assert.Equal("B", arbre[0].Enfants[0].CodeRB);
        Assert.Single(arbre[0].Enfants[0].Enfants);
        Assert.Equal("C", arbre[0].Enfants[0].Enfants[0].CodeRB);
    }

    private static RubriqueBudgetaireDto Dto(
        long id,
        string code,
        long? parentId = null,
        int niveau = 0,
        bool actif = true)
        => new(id, code, $"Lib {code}", parentId, null, null, niveau, actif, DateTime.Now, 0, 0);

    private sealed class FakeRubriqueRepository : IRubriqueBudgetaireRepository
    {
        public List<RubriqueBudgetaireDto> Items { get; } = [];
        public int Enfants { get; set; }
        public int Previsions { get; set; }
        public bool Cycle { get; set; }
        public int LastNiveau { get; private set; }

        public Task<IReadOnlyList<RubriqueBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RubriqueBudgetaireDto>>(Items);

        public Task<RubriqueBudgetaireDto?> GetByIdAsync(long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(r => r.IdRB == idRB));

        public Task<bool> ExistsByCodeAsync(string codeRB, long? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(r =>
                r.CodeRB.Equals(codeRB, StringComparison.OrdinalIgnoreCase)
                && (excludeId is null || r.IdRB != excludeId)));

        public Task<bool> ExistsByIdAsync(long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(r => r.IdRB == idRB));

        public Task<bool> WouldCreateCycleAsync(long idRB, long parentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Cycle || idRB == parentId);

        public Task<int> CountEnfantsAsync(long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult(Enfants);

        public Task<int> CountPrevisionsAsync(long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult(Previsions);

        public Task<RubriqueBudgetaireDto> CreateAsync(
            string codeRB, string libelle, long? parentId, int niveau, bool actif,
            CancellationToken cancellationToken = default)
        {
            LastNiveau = niveau;
            var created = new RubriqueBudgetaireDto(
                99, codeRB, libelle, parentId, null, null, niveau, actif, DateTime.Now, 0, 0);
            Items.Add(created);
            return Task.FromResult(created);
        }

        public Task<RubriqueBudgetaireDto?> UpdateAsync(
            long idRB, string codeRB, string libelle, long? parentId, int niveau, bool actif,
            CancellationToken cancellationToken = default)
        {
            LastNiveau = niveau;
            var current = Items.FirstOrDefault(r => r.IdRB == idRB);
            if (current is null) return Task.FromResult<RubriqueBudgetaireDto?>(null);
            var updated = current with
            {
                CodeRB = codeRB,
                Libelle = libelle,
                ParentId = parentId,
                Niveau = niveau,
                Actif = actif
            };
            Items[Items.IndexOf(current)] = updated;
            return Task.FromResult<RubriqueBudgetaireDto?>(updated);
        }

        public Task<RubriqueBudgetaireDto?> SetActifAsync(long idRB, bool actif, CancellationToken cancellationToken = default)
        {
            var current = Items.FirstOrDefault(r => r.IdRB == idRB);
            if (current is null) return Task.FromResult<RubriqueBudgetaireDto?>(null);
            var updated = current with { Actif = actif };
            Items[Items.IndexOf(current)] = updated;
            return Task.FromResult<RubriqueBudgetaireDto?>(updated);
        }

        public Task<bool> DeleteAsync(long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.RemoveAll(r => r.IdRB == idRB) > 0);
    }
}
