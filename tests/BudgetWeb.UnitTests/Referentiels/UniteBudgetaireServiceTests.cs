using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class UniteBudgetaireServiceTests
{
    [Fact]
    public async Task Create_RefuseSansDepartement()
    {
        var service = new UniteBudgetaireService(new FakeUniteBudgetaireRepository(), new NoopPerimetreAccesService());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateUniteBudgetaireRequest("UB1", "Libelle", 0, 1, true)));
        Assert.Contains("département", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseStructureHorsDepartement()
    {
        var repo = new FakeUniteBudgetaireRepository
        {
            CodeDepartement = "DCG",
            CodeDepartementStructure = "DFC"
        };
        var service = new UniteBudgetaireService(repo, new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateUniteBudgetaireRequest("UB1", "Libelle", 1, 2, true)));
        Assert.Contains("n'appartient pas au département", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_RefuseCodeDuplique()
    {
        var repo = new FakeUniteBudgetaireRepository();
        repo.Items.Add(Dto(1, "UB-A"));
        repo.Items.Add(Dto(2, "UB-B"));
        var service = new UniteBudgetaireService(repo, new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(2, new UpdateUniteBudgetaireRequest("UB-A", "Libelle", 1, 1, true)));
        Assert.Contains("existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_RefuseSiPrevisions()
    {
        var repo = new FakeUniteBudgetaireRepository { Previsions = 4 };
        repo.Items.Add(Dto(8, "UB-X"));
        var service = new UniteBudgetaireService(repo, new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(8));
        Assert.Equal(
            "Cette unité budgétaire ne peut pas être supprimée car elle est utilisée par une ou plusieurs prévisions budgétaires.",
            ex.Message);
    }

    [Fact]
    public async Task Delete_AutoriseSansDependance()
    {
        var repo = new FakeUniteBudgetaireRepository { Previsions = 0 };
        repo.Items.Add(Dto(9, "ZZTESTUB"));
        var service = new UniteBudgetaireService(repo, new NoopPerimetreAccesService());

        Assert.True(await service.DeleteAsync(9));
        Assert.Empty(repo.Items);
    }

    private static UniteBudgetaireDto Dto(long id, string code)
        => new(id, code, "Libelle", 1, "DCG", "Dir", 1, "DCG", "Dir", "DEPARTEMENT", true, DateTime.UtcNow, 0);

    private sealed class FakeUniteBudgetaireRepository : IUniteBudgetaireRepository
    {
        public List<UniteBudgetaireDto> Items { get; } = [];
        public int Previsions { get; set; }
        public string CodeDepartement { get; set; } = "DCG";
        public string? CodeDepartementStructure { get; set; } = "DCG";

        public Task<IReadOnlyList<UniteBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UniteBudgetaireDto>>(Items);

        public Task<UniteBudgetaireDto?> GetByIdAsync(long idUb, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(u => u.IdUB == idUb));

        public Task<bool> ExistsByCodeAsync(string codeUb, long? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(u =>
                u.CodeUB.Equals(codeUb, StringComparison.OrdinalIgnoreCase)
                && (excludeId is null || u.IdUB != excludeId)));

        public Task<bool> ExistsDepartementAsync(long idDepartement, CancellationToken cancellationToken = default)
            => Task.FromResult(idDepartement > 0);

        public Task<bool> ExistsStructureAsync(long idStructure, CancellationToken cancellationToken = default)
            => Task.FromResult(idStructure > 0);

        public Task<string?> GetCodeDepartementAsync(long idDepartement, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(CodeDepartement);

        public Task<string?> GetCodeDepartementOrganisationnelAsync(long idStructure, CancellationToken cancellationToken = default)
            => Task.FromResult(CodeDepartementStructure);

        public Task<int> CountPrevisionsAsync(long idUb, CancellationToken cancellationToken = default)
            => Task.FromResult(Previsions);

        public Task<UniteBudgetaireDto> CreateAsync(string codeUb, string libelle, long idDepartement, long idStructure, bool actif, CancellationToken cancellationToken = default)
        {
            var created = new UniteBudgetaireDto(1, codeUb, libelle, idDepartement, CodeDepartement, "Dir", idStructure, "ST", "St", "DIRECTION", actif, DateTime.UtcNow, 0);
            Items.Add(created);
            return Task.FromResult(created);
        }

        public Task<UniteBudgetaireDto?> UpdateAsync(long idUb, string codeUb, string libelle, long idDepartement, long idStructure, bool actif, CancellationToken cancellationToken = default)
        {
            var current = Items.FirstOrDefault(u => u.IdUB == idUb);
            if (current is null) return Task.FromResult<UniteBudgetaireDto?>(null);
            var updated = current with { CodeUB = codeUb, Libelle = libelle, Actif = actif, IdDepartement = idDepartement, IdStructure = idStructure };
            Items[Items.IndexOf(current)] = updated;
            return Task.FromResult<UniteBudgetaireDto?>(updated);
        }

        public Task<bool> DeleteAsync(long idUb, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.RemoveAll(u => u.IdUB == idUb) > 0);
    }
}
