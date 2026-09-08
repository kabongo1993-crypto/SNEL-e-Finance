using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class CompteCategorieServiceTests
{
    [Fact]
    public async Task List_CompteInconnu_404()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateFixture().Service.ListByCompteAsync(99));
    }

    [Fact]
    public async Task List_RetourneVideSansDonneesFictives()
    {
        var fx = CreateFixture();
        var rows = await fx.Service.ListByCompteAsync(1);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Create_SansDateFin_EstActive()
    {
        var fx = CreateFixture();
        var created = await fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(1, new DateOnly(2020, 1, 1), null));
        Assert.True(created.Active);
        Assert.Null(created.DateFin);
        Assert.Equal("Kinshasa", created.CategorieLibelle);
        Assert.Single(fx.Relations.Items);
    }

    [Fact]
    public async Task Create_AvecDateFin_EstHistorique()
    {
        var fx = CreateFixture();
        var created = await fx.Service.CreateAsync(
            1, new UpsertCompteCategorieRequest(1, new DateOnly(2018, 1, 1), new DateOnly(2019, 12, 31)));
        Assert.False(created.Active);
        Assert.Equal(new DateOnly(2019, 12, 31), created.DateFin);
    }

    [Fact]
    public async Task Create_DateFinAvantDebut_Refuse()
    {
        var fx = CreateFixture();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(1, new DateOnly(2025, 1, 1), new DateOnly(2024, 1, 1))));
    }

    [Fact]
    public async Task Create_DeuxiemeActive_Refuse()
    {
        var fx = CreateFixture();
        await fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(1, new DateOnly(2020, 1, 1), null));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(2, new DateOnly(2025, 1, 1), null)));
        Assert.Equal(CompteCategorieRules.MessageConflitActif, ex.Message);
    }

    [Fact]
    public async Task Create_ChevauchementHistorique_Refuse()
    {
        var fx = CreateFixture();
        await fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(1, new DateOnly(2020, 1, 1), new DateOnly(2024, 12, 31)));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(2, new DateOnly(2023, 1, 1), new DateOnly(2025, 12, 31))));
        Assert.Contains("période concernée", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_SuccessionSansChevauchement_Ok()
    {
        var fx = CreateFixture();
        await fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(1, new DateOnly(2020, 1, 1), new DateOnly(2024, 12, 31)));
        var seconde = await fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(2, new DateOnly(2025, 1, 1), null));
        Assert.True(seconde.Active);
        Assert.Equal(2, fx.Relations.Items.Count);
    }

    [Fact]
    public async Task Create_CategorieInactive_Refuse()
    {
        var fx = CreateFixture();
        fx.Categories.Items.Add(new CategorieCompte { IdCategorieCompte = 9, Libelle = "Inactive", Actif = false });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(9, new DateOnly(2020, 1, 1), null)));
        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_CategorieInconnue_Refuse()
    {
        var fx = CreateFixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(99, new DateOnly(2020, 1, 1), null)));
    }

    [Fact]
    public async Task Create_CompteInactif_RefuseActive()
    {
        var fx = CreateFixture();
        fx.Comptes.Items[0].Actif = false;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(1, new DateOnly(2020, 1, 1), null)));
        Assert.Contains("inactif", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_CompteInactif_AutoriseHistorique()
    {
        var fx = CreateFixture();
        fx.Comptes.Items[0].Actif = false;
        var created = await fx.Service.CreateAsync(
            1, new UpsertCompteCategorieRequest(1, new DateOnly(2018, 1, 1), new DateOnly(2019, 12, 31)));
        Assert.False(created.Active);
    }

    [Fact]
    public async Task Update_ModifieCategorie()
    {
        var fx = CreateFixture();
        var created = await fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(1, new DateOnly(2020, 1, 1), null));
        var updated = await fx.Service.UpdateAsync(
            1, created.IdCompteCategorie, new UpsertCompteCategorieRequest(2, new DateOnly(2020, 1, 1), null));
        Assert.Equal(2, updated!.IdCategorieCompte);
        Assert.Equal("Province", updated.CategorieLibelle);
        Assert.Single(fx.Relations.Items);
    }

    [Fact]
    public async Task Cloturer_RenseigneDateFin_ConserveHistorique()
    {
        var fx = CreateFixture();
        var created = await fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(1, new DateOnly(2020, 1, 1), null));
        var closed = await fx.Service.CloturerAsync(
            1, created.IdCompteCategorie, new CloturerCompteCategorieRequest(new DateOnly(2025, 12, 31)));
        Assert.False(closed!.Active);
        Assert.Equal(new DateOnly(2025, 12, 31), closed.DateFin);
        Assert.Single(fx.Relations.Items);
    }

    [Fact]
    public async Task Preview_DetecteReferencesInconnuesDoublonsEtSentinelle()
    {
        var fx = CreateFixture();
        var preview = await fx.Service.PreviewImportAsync(new ImportCompteCategoriesPreviewRequest("t.xlsx",
        [
            new(2, "101", "2020-01-01", "1900-01-01", "1", "1"),
            new(3, "102", "2020-01-01", "1", "1", "1"),
            new(4, "103", "2020-01-01", "1900-01-01", "1", "999"),
            new(5, "104", "2020-01-01", "1900-01-01", "99", "1"),
            new(6, "105", "2023-01-01", "2025-12-31", "2", "1"),
        ]));

        Assert.Equal(5, preview.Resume.Analysees);
        Assert.Equal(1, preview.Resume.AImporter);
        Assert.Equal(1, preview.Resume.Doublons);
        Assert.Equal(1, preview.Resume.ConflitsPeriode);
        Assert.Equal(2, preview.Resume.Erreurs);
        Assert.Equal(4, preview.Resume.DatesSentinelleConverties);
        Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Compte 999 introuvable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Catégorie 99 introuvable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Lignes, l => l.Statut == "a_importer");
        Assert.Contains(preview.Lignes, l => l.Statut == "doublon_fichier");
        Assert.Contains(preview.Lignes, l => l.Statut == "conflit_periode");
    }

    [Fact]
    public async Task Import_ConserveIdHistorique_EtConvertitSentinelle()
    {
        var fx = CreateFixture();
        var result = await fx.Service.ImportAsync(new ImportCompteCategoriesRequest("COMPTE_CATEGORIE.xlsx",
        [
            new(2, "272", "2020-01-01", "1900-01-01", "1", "1"),
        ]));

        Assert.Equal(1, result.Importes);
        var row = Assert.Single(fx.Relations.Items);
        Assert.Equal(272, row.IdCompteCategorie);
        Assert.Equal(1, row.FK_Compte);
        Assert.Equal(1, row.FK_CategorieCompte);
        Assert.Null(row.DateFin);
        Assert.Equal(1, result.Resume.DatesSentinelleConverties);
    }

    [Fact]
    public async Task Import_NeCreePasLesReferentielsManquants()
    {
        var fx = CreateFixture();
        var result = await fx.Service.ImportAsync(new ImportCompteCategoriesRequest("x.xlsx",
        [
            new(2, "1", "2020-01-01", "1900-01-01", "1", "999"),
        ]));
        Assert.Equal(0, result.Importes);
        Assert.Empty(fx.Relations.Items);
        Assert.Equal(1, fx.Comptes.Items.Count);
        Assert.Equal(2, fx.Categories.Items.Count);
    }

    [Fact]
    public async Task Create_RefuseSansPermission()
    {
        var fx = CreateFixture(permissions: []);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fx.Service.CreateAsync(1, new UpsertCompteCategorieRequest(1, new DateOnly(2020, 1, 1), null)));
    }

    private static Fixture CreateFixture(IReadOnlyList<string>? permissions = null)
    {
        var comptes = new FakeCompteRepository();
        comptes.Items.Add(new CompteFinancier
        {
            IdCompte = 1,
            NumeroCompte = "123456789",
            LibelleCompte = "Compte test",
            Actif = true,
        });
        var categories = new FakeCategorieRepository();
        categories.Items.Add(new CategorieCompte { IdCategorieCompte = 1, Libelle = "Kinshasa", Actif = true });
        categories.Items.Add(new CategorieCompte { IdCategorieCompte = 2, Libelle = "Province", Actif = true });
        var relations = new FakeRelationRepository();
        var perms = permissions ?? [AppPermissions.ReferentielsEcrire];
        var service = new CompteCategorieService(relations, comptes, categories, new FakeUser(perms));
        return new Fixture(service, comptes, categories, relations);
    }

    private sealed record Fixture(
        CompteCategorieService Service,
        FakeCompteRepository Comptes,
        FakeCategorieRepository Categories,
        FakeRelationRepository Relations);

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

    private sealed class FakeCompteRepository : ICompteFinancierRepository
    {
        public List<CompteFinancier> Items { get; } = [];
        public Task<IReadOnlyList<CompteFinancier>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CompteFinancier>>(Items);
        public Task<CompteFinancier?> GetByIdAsync(long idCompte, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(c => c.IdCompte == idCompte));
        public Task<CompteFinancier?> GetTrackedByIdAsync(long idCompte, CancellationToken cancellationToken = default)
            => GetByIdAsync(idCompte, cancellationToken);
        public Task<bool> ExistsNumeroAsync(string fkBanque, string numeroCompte, long? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<bool> ExistsIdAsync(long idCompte, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(c => c.IdCompte == idCompte));
        public Task<IReadOnlyList<long>> ListIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<long>>(Items.Select(c => c.IdCompte).ToList());
        public Task<CompteFinancier> AddAsync(CompteFinancier entity, CancellationToken cancellationToken = default)
            => Task.FromResult(entity);
        public Task AddRangeWithExplicitIdsAsync(IReadOnlyList<CompteFinancier> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCategorieRepository : ICategorieCompteRepository
    {
        public List<CategorieCompte> Items { get; } = [];
        public Task<IReadOnlyList<CategorieCompte>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
        {
            IEnumerable<CategorieCompte> q = Items;
            if (actifsSeulement == true) q = q.Where(c => c.Actif);
            return Task.FromResult<IReadOnlyList<CategorieCompte>>(q.ToList());
        }
        public Task<CategorieCompte?> GetByIdAsync(long idCategorieCompte, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(c => c.IdCategorieCompte == idCategorieCompte));
        public Task<bool> LibelleExistsAsync(string libelle, long? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<CategorieCompte> AddAsync(CategorieCompte entity, CancellationToken cancellationToken = default)
            => Task.FromResult(entity);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRelationRepository : ICompteCategorieRepository
    {
        public List<CompteCategorie> Items { get; } = [];

        public Task<IReadOnlyList<CompteCategorie>> ListByCompteAsync(long idCompte, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CompteCategorie>>(Items.Where(i => i.FK_Compte == idCompte).ToList());

        public Task<IReadOnlyList<CompteCategorie>> ListAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CompteCategorie>>(Items.ToList());

        public Task<CompteCategorie?> GetByIdAsync(long idCompteCategorie, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.IdCompteCategorie == idCompteCategorie));

        public Task<CompteCategorie?> GetTrackedByIdAsync(long idCompteCategorie, CancellationToken cancellationToken = default)
            => GetByIdAsync(idCompteCategorie, cancellationToken);

        public Task<bool> ExistsIdAsync(long idCompteCategorie, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(i => i.IdCompteCategorie == idCompteCategorie));

        public Task<IReadOnlyList<long>> ListIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<long>>(Items.Select(i => i.IdCompteCategorie).ToList());

        public Task<CompteCategorie> AddAsync(CompteCategorie entity, CancellationToken cancellationToken = default)
        {
            if (entity.IdCompteCategorie == 0)
                entity.IdCompteCategorie = Items.Count == 0 ? 1 : Items.Max(i => i.IdCompteCategorie) + 1;
            Hydrate(entity);
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task AddRangeWithExplicitIdsAsync(IReadOnlyList<CompteCategorie> entities, CancellationToken cancellationToken = default)
        {
            foreach (var e in entities)
            {
                Hydrate(e);
                Items.Add(e);
            }
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var e in Items) Hydrate(e);
            return Task.CompletedTask;
        }

        public Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
            => action();

        private static void Hydrate(CompteCategorie e)
        {
            e.Compte = new CompteFinancier
            {
                IdCompte = e.FK_Compte,
                NumeroCompte = "123456789",
                LibelleCompte = "Compte test",
                Actif = true,
            };
            e.CategorieCompte = new CategorieCompte
            {
                IdCategorieCompte = e.FK_CategorieCompte,
                Libelle = e.FK_CategorieCompte == 2 ? "Province" : "Kinshasa",
                Actif = true,
            };
        }
    }
}
