using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class CompteFinancierServiceTests
{
    [Fact]
    public async Task List_RetourneVideSansDonneesFictives()
    {
        var rows = await CreateService().Service.ListAsync();
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Create_SansUtilisateur_EstValide()
    {
        var fx = CreateService();
        var created = await fx.Service.CreateAsync(NewCreate("001", "Compte test"));
        Assert.True(created.IdCompte > 0);
        Assert.Equal("001", created.NumeroCompte);
        Assert.Equal("RAWBANK", created.IdBanque);
        Assert.Equal("FCT", created.CodeTypeCompte);
        Assert.Null(created.IdUtilisateur);
        Assert.True(created.Actif);
        Assert.Null(created.DateCloture);
        Assert.Single(fx.Comptes.Items);
    }

    [Fact]
    public async Task Create_RefuseNumeroVide()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().Service.CreateAsync(NewCreate("  ", "Libelle")));
        Assert.Contains("numéro", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseDoublonBanqueNumero()
    {
        var fx = CreateService();
        await fx.Service.CreateAsync(NewCreate("001", "A"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fx.Service.CreateAsync(NewCreate("001", "B")));
        Assert.Contains("existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_ModifieLibelle()
    {
        var fx = CreateService();
        var created = await fx.Service.CreateAsync(NewCreate("001", "A"));
        var updated = await fx.Service.UpdateAsync(created.IdCompte, NewUpdate("001", "A modifie", true));
        Assert.NotNull(updated);
        Assert.Equal("A modifie", updated!.LibelleCompte);
        Assert.NotNull(updated.DateModification);
    }

    [Fact]
    public async Task Desactivation_RenseigneDateCloture_Reactivation_Lefface()
    {
        var fx = CreateService();
        var created = await fx.Service.CreateAsync(NewCreate("001", "A"));
        var off = await fx.Service.UpdateAsync(created.IdCompte, NewUpdate("001", "A", false));
        Assert.False(off!.Actif);
        Assert.NotNull(off.DateCloture);

        var on = await fx.Service.UpdateAsync(created.IdCompte, NewUpdate("001", "A", true));
        Assert.True(on!.Actif);
        Assert.Null(on.DateCloture);
        Assert.Single(fx.Comptes.Items);
    }

    [Fact]
    public async Task Preview_DetecteReferencesInconnuesEtDoublon()
    {
        var fx = CreateService();
        var preview = await fx.Service.PreviewImportAsync(new ImportComptesPreviewRequest("t.xlsx",
        [
            Raw(2, "23", "N1", "L1", "1", "INCONNUE", "FCT", "CDF", "KIN"),
            Raw(3, "24", "N2", "L2", "1", "RAWBANK", "INCONNU", "CDF", "KIN"),
            Raw(4, "25", "N3", "L3", "1", "RAWBANK", "FCT", "INCONNUE", "KIN"),
            Raw(5, "26", "N4", "L4", "1", "RAWBANK", "FCT", "CDF", "ZZZ"),
            Raw(6, "27", "", "L5", "1", "RAWBANK", "FCT", "CDF", "KIN"),
            Raw(7, "28", "DUP", "L6", "1", "RAWBANK", "FCT", "CDF", "KIN"),
            Raw(8, "29", "DUP", "L7", "1", "RAWBANK", "FCT", "CDF", "KIN"),
            Raw(9, "30", "OK", "L8", "1", "RAWBANK", "FCT", "EURO", "KIN"),
        ]));

        Assert.Equal(8, preview.Resume.Analysees);
        Assert.Equal(2, preview.Resume.AImporter);
        Assert.Equal(1, preview.Resume.Doublons);
        Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Banque INCONNUE introuvable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Type de compte INCONNU introuvable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Devise INCONNUE introuvable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Province ZZZ introuvable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Numéro de compte obligatoire", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Lignes, l => l.Statut == "doublon_fichier");
        Assert.Contains(preview.Lignes, l => l.Statut == "a_importer" && l.NumeroCompte == "OK");
    }

    [Fact]
    public async Task Import_ConserveIdHistorique_EtUtilisateurNull()
    {
        var fx = CreateService();
        var result = await fx.Service.ImportAsync(new ImportComptesRequest("COMPTE.xlsx",
        [
            Raw(2, "491", "HIST-001", "Compte historique", "1", "RAWBANK", "FCT", "CDF", "KIN"),
        ]));

        Assert.Equal(1, result.Importes);
        var row = Assert.Single(fx.Comptes.Items);
        Assert.Equal(491, row.IdCompte);
        Assert.Null(row.FK_Utilisateur);
        Assert.Equal("FCT", row.FK_TypeCompte);
        Assert.Equal("KIN", row.FK_Province);
        Assert.True(row.Actif);
        Assert.Null(row.DateCloture);
    }

    [Fact]
    public async Task Create_RefuseSansPermission()
    {
        var fx = CreateService(permissions: []);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fx.Service.CreateAsync(NewCreate("001", "A")));
    }

    [Fact]
    public async Task List_AutoriseLecturePaiements()
    {
        var fx = CreateService(permissions: [AppPermissions.PaiementsLire]);
        Assert.Empty(await fx.Service.ListAsync());
    }

    private static ImportCompteRawRequest Raw(
        int ligne,
        string id,
        string numero,
        string libelle,
        string direction,
        string banque,
        string type,
        string devise,
        string? province)
        => new(ligne, id, numero, libelle, direction, banque, type, devise, "36526", "1", "0", province);

    private static CreateCompteFinancierRequest NewCreate(string numero, string libelle)
        => new(numero, libelle, "RAWBANK", 1, "FCT", 1, "KIN", null, true);

    private static UpdateCompteFinancierRequest NewUpdate(string numero, string libelle, bool actif)
        => new(numero, libelle, "RAWBANK", 1, "FCT", 1, "KIN", null, actif);

    private static Fixture CreateService(IReadOnlyList<string>? permissions = null)
    {
        var comptes = new FakeCompteRepository();
        var banques = new FakeBanqueRepo();
        banques.Items.Add(new Banque { IdBanque = "RAWBANK", LibelleBanque = "Rawbank", Actif = true });
        var directions = new FakeDirectionRepo();
        directions.Items.Add(new DirectionTresorerie { IdDirection = 1, Libelle = "Kinshasa", Actif = true });
        var types = new FakeTypeRepo();
        types.Items.Add(new TypeCompte { Code = "FCT", Libelle = "Fonctionnement", Actif = true });
        types.Items.Add(new TypeCompte { Code = "MAINTENANCE", Libelle = "Maintenance", Actif = true });
        types.Items.Add(new TypeCompte { Code = "CAISSE", Libelle = "CAISSE DG", Actif = true });
        var devises = new FakeDeviseRepo();
        devises.Items.Add(new Devise { IdDevise = 1, Code = "CDF", Libelle = "Franc congolais", Actif = true });
        devises.Items.Add(new Devise { IdDevise = 2, Code = "EUR", Libelle = "Euro", Actif = true });
        var provinces = new FakeProvinceRepo();
        provinces.Items.Add(new Province { IdProvince = "KIN", Libelle = "Kinshasa", Actif = true });
        var users = new FakeAuthRepo();
        var perms = permissions ?? [AppPermissions.ReferentielsEcrire];
        var service = new CompteFinancierService(
            comptes, banques, directions, types, devises, provinces, users, new FakeUser(perms));
        return new Fixture(service, comptes);
    }

    private sealed record Fixture(CompteFinancierService Service, FakeCompteRepository Comptes);

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
        {
            IEnumerable<CompteFinancier> q = Items;
            if (actifsSeulement == true) q = q.Where(c => c.Actif);
            return Task.FromResult<IReadOnlyList<CompteFinancier>>(q.ToList());
        }

        public Task<CompteFinancier?> GetByIdAsync(long idCompte, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(c => c.IdCompte == idCompte));

        public Task<CompteFinancier?> GetTrackedByIdAsync(long idCompte, CancellationToken cancellationToken = default)
            => GetByIdAsync(idCompte, cancellationToken);

        public Task<bool> ExistsNumeroAsync(string fkBanque, string numeroCompte, long? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(c =>
                string.Equals(c.FK_Banque, fkBanque, StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.NumeroCompte, numeroCompte, StringComparison.OrdinalIgnoreCase)
                && (excludeId == null || c.IdCompte != excludeId)));

        public Task<bool> ExistsIdAsync(long idCompte, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(c => c.IdCompte == idCompte));

        public Task<IReadOnlyList<long>> ListIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<long>>(Items.Select(c => c.IdCompte).ToList());

        public Task<CompteFinancier> AddAsync(CompteFinancier entity, CancellationToken cancellationToken = default)
        {
            if (entity.IdCompte == 0)
                entity.IdCompte = Items.Count == 0 ? 1 : Items.Max(c => c.IdCompte) + 1;
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task AddRangeWithExplicitIdsAsync(IReadOnlyList<CompteFinancier> entities, CancellationToken cancellationToken = default)
        {
            Items.AddRange(entities);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeBanqueRepo : IBanqueRepository
    {
        public List<Banque> Items { get; } = [];
        public Task<IReadOnlyList<Banque>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Banque>>(Items);
        public Task<Banque?> GetByIdAsync(string idBanque, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(b => b.IdBanque == idBanque));
        public Task<bool> ExistsAsync(string idBanque, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(b => b.IdBanque == idBanque));
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
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDirectionRepo : IDirectionRepository
    {
        public List<DirectionTresorerie> Items { get; } = [];
        public Task<IReadOnlyList<DirectionTresorerie>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DirectionTresorerie>>(Items);
        public Task<DirectionTresorerie?> GetByIdAsync(long idDirection, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(d => d.IdDirection == idDirection));
        public Task<bool> LibelleExistsAsync(string libelle, long? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<DirectionTresorerie> AddAsync(DirectionTresorerie entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTypeRepo : ITypeCompteRepository
    {
        public List<TypeCompte> Items { get; } = [];
        public Task<IReadOnlyList<TypeCompte>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TypeCompte>>(Items);
        public Task<TypeCompte?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(t => t.Code == code));
        public Task<bool> CodeExistsAsync(string code, string? excludeCode, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(t => t.Code == code));
        public Task<int> CountComptesByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<TypeCompte> AddAsync(TypeCompte entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }
        public Task RenameCodeAsync(string currentCode, string newCode, TypeCompte updated, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDeviseRepo : IDeviseRepository
    {
        public List<Devise> Items { get; } = [];
        public Task<IReadOnlyList<Devise>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Devise>>(Items);
        public Task<Devise?> GetByIdAsync(long idDevise, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(d => d.IdDevise == idDevise));
        public Task<Devise?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(d => d.Code == code));
        public Task<bool> CodeExistsAsync(string code, long? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(d => d.Code == code));
        public Task<bool> EstUtiliseeAsync(long idDevise, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<Devise> AddAsync(Devise entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProvinceRepo : IProvinceRepository
    {
        public List<Province> Items { get; } = [];
        public Task<IReadOnlyList<Province>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Province>>(Items);
        public Task<Province?> GetByIdAsync(string idProvince, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(p => p.IdProvince == idProvince));
        public Task<bool> IdExistsAsync(string idProvince, string? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(p => p.IdProvince == idProvince));
        public Task<bool> LibelleExistsAsync(string libelle, string? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<int> CountComptesByIdAsync(string idProvince, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<Province> AddAsync(Province entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }
        public Task RenameIdAsync(string currentId, string newId, Province updated, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAuthRepo : IAuthRepository
    {
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<Utilisateur?> FindByNomUtilisateurAsync(string nomUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult<Utilisateur?>(null);
        public Task<Utilisateur?> FindByIdAsync(long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult<Utilisateur?>(null);
        public Task UpdateDerniereConnexionAsync(long idUtilisateur, DateTime dateUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task UpdateMotDePasseHashAsync(long idUtilisateur, string motDePasseHash, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<Utilisateur> CreateAsync(Utilisateur utilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult(utilisateur);
        public Task<IReadOnlyList<string>> ListProfilsAsync(long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> ListPermissionsIndividuellesAsync(long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
