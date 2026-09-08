using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class StructureServiceTests
{
    [Fact]
    public async Task Delete_RefuseSiEnfants()
    {
        var repo = new FakeStructureRepository { Enfants = 2, Unites = 0 };
        repo.Items.Add(Dto(1));
        var service = new StructureService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(1));
        Assert.Contains("structures enfants", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_RefuseSiUnitesBudgetaires()
    {
        var repo = new FakeStructureRepository { Enfants = 0, Unites = 3 };
        repo.Items.Add(Dto(1));
        var service = new StructureService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(1));
        Assert.Contains("unités budgétaires", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_RefuseCycleParent()
    {
        var repo = new FakeStructureRepository { Cycle = true };
        repo.Items.Add(Dto(10));
        repo.Items.Add(Dto(11, parentId: 10));
        var service = new StructureService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(10, new UpdateStructureRequest("DIRECTION", "DIR", "Dir", 11, true)));
        Assert.Contains("boucle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseEntiteAvecParent()
    {
        var service = new StructureService(new FakeStructureRepository());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateStructureRequest("ENTITE", "SNEL", "SNEL", 1, true)));
        Assert.Contains("entité", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static StructureDto Dto(long id, long? parentId = null)
        => new(id, parentId, "DIRECTION", $"C{id}", $"Lib {id}", true, DateTime.UtcNow, null, null, 0, 0);

    private sealed class FakeStructureRepository : IStructureRepository
    {
        public List<StructureDto> Items { get; } = [];
        public int Enfants { get; set; }
        public int Unites { get; set; }
        public bool Cycle { get; set; }

        public Task<IReadOnlyList<StructureDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StructureDto>>(Items);

        public Task<StructureDto?> GetByIdAsync(long idStructure, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(s => s.IdStructure == idStructure));

        public Task<bool> ExistsByCodeAsync(string code, long? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(s =>
                s.Code.Equals(code, StringComparison.OrdinalIgnoreCase)
                && (excludeId is null || s.IdStructure != excludeId)));

        public Task<bool> ExistsByIdAsync(long idStructure, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(s => s.IdStructure == idStructure) || idStructure > 0);

        public Task<bool> WouldCreateCycleAsync(long idStructure, long parentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Cycle || idStructure == parentId);

        public Task<int> CountEnfantsAsync(long idStructure, CancellationToken cancellationToken = default)
            => Task.FromResult(Enfants);

        public Task<int> CountUnitesBudgetairesAsync(long idStructure, CancellationToken cancellationToken = default)
            => Task.FromResult(Unites);

        public Task<StructureDto> CreateAsync(string typeStructure, string code, string libelle, long? parentId, bool actif, CancellationToken cancellationToken = default)
        {
            var created = new StructureDto(99, parentId, typeStructure, code, libelle, actif, DateTime.UtcNow, null, null, 0, 0);
            Items.Add(created);
            return Task.FromResult(created);
        }

        public Task<StructureDto?> UpdateAsync(long idStructure, string typeStructure, string code, string libelle, long? parentId, bool actif, CancellationToken cancellationToken = default)
        {
            var current = Items.FirstOrDefault(s => s.IdStructure == idStructure);
            if (current is null) return Task.FromResult<StructureDto?>(null);
            var updated = current with
            {
                TypeStructure = typeStructure,
                Code = code,
                Libelle = libelle,
                ParentId = parentId,
                Actif = actif
            };
            Items[Items.IndexOf(current)] = updated;
            return Task.FromResult<StructureDto?>(updated);
        }

        public Task<bool> DeleteAsync(long idStructure, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.RemoveAll(s => s.IdStructure == idStructure) > 0);
    }
}
