using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class VersionBudgetaireServiceTests
{
    [Fact]
    public async Task Create_RefuseSansExercice()
    {
        var service = CreateService(new FakeVersionRepository());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Request(idExercice: 0)));
        Assert.Contains("exercice", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseNumeroInvalide()
    {
        var repo = new FakeVersionRepository { ExerciceExiste = true, UtilisateurExiste = true };
        var service = CreateService(repo);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Request(numero: 0)));
        Assert.Contains("supérieur à 0", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ForceToujoursBrouillon()
    {
        var repo = new FakeVersionRepository { ExerciceExiste = true, UtilisateurExiste = true };
        var service = CreateService(repo);
        var created = await service.CreateAsync(Request(statut: "VALIDEE"));
        Assert.Equal(StatutVersionBudgetaire.Brouillon, created.Statut);
        Assert.Equal(StatutVersionBudgetaire.Brouillon, repo.Items[0].Statut);
    }

    [Fact]
    public async Task Create_RefuseUtilisateurInexistant()
    {
        var repo = new FakeVersionRepository { ExerciceExiste = true, UtilisateurExiste = false };
        var service = CreateService(repo);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Request()));
        Assert.Contains("UTILISATEUR", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseNumeroDuplique()
    {
        var repo = new FakeVersionRepository { ExerciceExiste = true, UtilisateurExiste = true };
        repo.Items.Add(Dto(1, 1, 1));
        var service = CreateService(repo);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Request(idExercice: 1, numero: 1)));
        Assert.Contains("existe déjà", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_RefuseSiPrevisions()
    {
        var repo = new FakeVersionRepository { Previsions = 2 };
        repo.Items.Add(Dto(5, 1, 1));
        var service = CreateService(repo);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(5));
        Assert.Contains("prévisions", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_RefuseSiVersionSuivante()
    {
        var repo = new FakeVersionRepository { Suivantes = 1 };
        repo.Items.Add(Dto(5, 1, 1));
        var service = CreateService(repo);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(5));
        Assert.Contains("version précédente", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_AutoriseSansDependance()
    {
        var repo = new FakeVersionRepository();
        repo.Items.Add(Dto(8, 1, 1));
        var service = CreateService(repo);
        Assert.True(await service.DeleteAsync(8));
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Update_NeChangePasLeStatut()
    {
        var repo = new FakeVersionRepository { ExerciceExiste = true, UtilisateurExiste = true };
        repo.Items.Add(Dto(1, 1, 1) with { Statut = StatutVersionBudgetaire.Soumise });
        var service = CreateService(repo);
        var updated = await service.UpdateAsync(1, new UpdateVersionBudgetaireRequest(
            1, 1, "Libelle maj", null, new DateOnly(2026, 1, 1), null, null, 1));
        Assert.NotNull(updated);
        Assert.Equal(StatutVersionBudgetaire.Soumise, updated!.Statut);
    }

    [Theory]
    [InlineData(StatutVersionBudgetaire.Brouillon, StatutVersionBudgetaire.Soumise)]
    [InlineData(StatutVersionBudgetaire.Rejetee, StatutVersionBudgetaire.Soumise)]
    public async Task Soumettre_TransitionsOk(string de, string vers)
    {
        var repo = RepoWith(de);
        var service = CreateService(repo, AppPermissions.PrevisionsSoumettre);
        var result = await service.SoumettreAsync(1, 42);
        Assert.Equal(vers, result.Statut);
        Assert.Equal(42, result.IdUtilisateurSoumission);
        Assert.NotNull(result.DateSoumission);
        Assert.Contains(repo.Audits, a => a.Operation == "SOUMETTRE");
    }

    [Fact]
    public async Task Controler_SoumiseVersControlee()
    {
        var repo = RepoWith(StatutVersionBudgetaire.Soumise);
        var service = CreateService(repo, AppPermissions.VersionsControler);
        var result = await service.ControlerAsync(1, 42);
        Assert.Equal(StatutVersionBudgetaire.Controlee, result.Statut);
        Assert.Equal(42, result.IdUtilisateurControle);
        Assert.NotNull(result.DateControle);
    }

    [Fact]
    public async Task Valider_ControleeVersValidee()
    {
        var repo = RepoWith(StatutVersionBudgetaire.Controlee);
        var service = CreateService(repo, AppPermissions.VersionsValider);
        var result = await service.ValiderAsync(1, 42);
        Assert.Equal(StatutVersionBudgetaire.Validee, result.Statut);
        Assert.Equal(42, result.IdUtilisateurValidation);
        Assert.NotNull(result.DateValidation);
    }

    [Theory]
    [InlineData(StatutVersionBudgetaire.Soumise)]
    [InlineData(StatutVersionBudgetaire.Controlee)]
    public async Task Rejeter_TransitionsOk(string de)
    {
        var repo = RepoWith(de);
        var service = CreateService(repo, AppPermissions.VersionsRejeter);
        var result = await service.RejeterAsync(1, 42, "Montants incohérents");
        Assert.Equal(StatutVersionBudgetaire.Rejetee, result.Statut);
        Assert.Equal(42, result.IdUtilisateurRejet);
        Assert.Equal("Montants incohérents", result.MotifRejet);
        Assert.NotNull(result.DateRejet);
    }

    [Fact]
    public async Task Reouvrir_RejeteeVersBrouillon()
    {
        var repo = RepoWith(StatutVersionBudgetaire.Rejetee);
        var service = CreateService(repo, AppPermissions.PrevisionsEcrire);
        var result = await service.ReouvrirAsync(1, 42);
        Assert.Equal(StatutVersionBudgetaire.Brouillon, result.Statut);
    }

    [Fact]
    public void Transition_SoumiseVersBrouillon_Autorisee()
    {
        Assert.True(StatutVersionBudgetaire.EstTransitionAutorisee(
            StatutVersionBudgetaire.Soumise,
            StatutVersionBudgetaire.Brouillon));
    }

    [Theory]
    [InlineData(StatutVersionBudgetaire.Brouillon, StatutVersionBudgetaire.Validee)]
    [InlineData(StatutVersionBudgetaire.Brouillon, StatutVersionBudgetaire.Controlee)]
    [InlineData(StatutVersionBudgetaire.Soumise, StatutVersionBudgetaire.Validee)]
    [InlineData(StatutVersionBudgetaire.Validee, StatutVersionBudgetaire.Brouillon)]
    [InlineData(StatutVersionBudgetaire.Validee, StatutVersionBudgetaire.Rejetee)]
    [InlineData(StatutVersionBudgetaire.Controlee, StatutVersionBudgetaire.Brouillon)]
    public void TransitionsInterdites_RefuseesParMoteur(string de, string vers)
    {
        Assert.False(StatutVersionBudgetaire.EstTransitionAutorisee(de, vers));
    }

    [Fact]
    public async Task Soumettre_RefuseDepuisValidee()
    {
        var repo = RepoWith(StatutVersionBudgetaire.Validee);
        var service = CreateService(repo, AppPermissions.PrevisionsSoumettre);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SoumettreAsync(1, 42));
        Assert.Contains("Transition interdite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Valider_RefuseDepuisSoumise()
    {
        var repo = RepoWith(StatutVersionBudgetaire.Soumise);
        var service = CreateService(repo, AppPermissions.VersionsValider);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValiderAsync(1, 42));
        Assert.Contains("Transition interdite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejeter_RefuseSansMotif()
    {
        var repo = RepoWith(StatutVersionBudgetaire.Soumise);
        var service = CreateService(repo, AppPermissions.VersionsRejeter);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejeterAsync(1, 42, "  "));
        Assert.Contains("motif", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejeter_RefuseDepuisValidee()
    {
        var repo = RepoWith(StatutVersionBudgetaire.Validee);
        var service = CreateService(repo, AppPermissions.VersionsRejeter);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejeterAsync(1, 42, "Trop tard"));
        Assert.Contains("Transition interdite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Soumettre_RefuseSansPermission()
    {
        var repo = RepoWith(StatutVersionBudgetaire.Brouillon);
        var service = CreateServiceSansPermission(repo);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SoumettreAsync(1, 42));
    }

    [Fact]
    public async Task Controler_RefuseSansPermission()
    {
        var repo = RepoWith(StatutVersionBudgetaire.Soumise);
        var service = CreateService(repo, AppPermissions.PrevisionsEcrire);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ControlerAsync(1, 42));
    }

    private static FakeVersionRepository RepoWith(string statut)
    {
        var repo = new FakeVersionRepository { UtilisateurExiste = true, ExerciceExiste = true };
        repo.Items.Add(Dto(1, 1, 1) with { Statut = statut });
        return repo;
    }

    private static VersionBudgetaireService CreateService(FakeVersionRepository repo)
        => new(repo, new FakeCurrentUser(AppPermissions.VersionsEcrire, AppPermissions.PrevisionsEcrire));

    private static VersionBudgetaireService CreateService(
        FakeVersionRepository repo,
        params string[] permissions)
        => new(repo, new FakeCurrentUser(permissions));

    private static VersionBudgetaireService CreateServiceSansPermission(FakeVersionRepository repo)
        => new(repo, new FakeCurrentUser());

    private static CreateVersionBudgetaireRequest Request(
        long idExercice = 1,
        int? numero = 1,
        string statut = "BROUILLON")
        => new(
            idExercice,
            numero,
            "Budget initial",
            null,
            new DateOnly(2026, 1, 1),
            null,
            null,
            statut,
            1,
            null,
            null);

    private static VersionBudgetaireDto Dto(long id, long idExercice, int numero)
        => new(id, idExercice, 2026, "OUVERT", numero, "Lib", null, null, null,
            DateTime.Now, new DateOnly(2026, 1, 1), null, null, "BROUILLON",
            1, "admin", null, null, null,
            null, null, null,
            null, null, null,
            null, null, null, null,
            0, 0, 0);

    private sealed class FakeCurrentUser(params string[] permissions) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public long? UserId => 42;
        public string? Username => "tester";
        public string? DisplayName => "Tester";
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => permissions;
        public bool IsInRole(string role) => false;
        public bool HasPermission(string permission) =>
            Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));
        public long RequireUserId() => 42;
    }

    private sealed class FakeVersionRepository : IVersionBudgetaireRepository
    {
        public List<VersionBudgetaireDto> Items { get; } = [];
        public List<(string Operation, long IdUtilisateur)> Audits { get; } = [];
        public bool ExerciceExiste { get; set; }
        public bool UtilisateurExiste { get; set; }
        public int Previsions { get; set; }
        public int Transferts { get; set; }
        public int Suivantes { get; set; }

        public Task<IReadOnlyList<VersionBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VersionBudgetaireDto>>(Items);

        public Task<VersionBudgetaireDto?> GetByIdAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(v => v.IdVersion == idVersion));

        public Task<bool> ExistsByExerciceNumeroAsync(long idExercice, int numeroVersion, long? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(v =>
                v.IdExercice == idExercice && v.NumeroVersion == numeroVersion
                && (excludeId is null || v.IdVersion != excludeId)));

        public Task<bool> ExistsExerciceAsync(long idExercice, CancellationToken cancellationToken = default)
            => Task.FromResult(ExerciceExiste && idExercice > 0);

        public Task<bool> ExistsVersionAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(v => v.IdVersion == idVersion));

        public Task<bool> ExistsUtilisateurAsync(long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult(UtilisateurExiste && idUtilisateur > 0);

        public Task<bool> WouldCreateCycleAsync(long idVersion, long idVersionPrecedente, CancellationToken cancellationToken = default)
            => Task.FromResult(idVersion == idVersionPrecedente);

        public Task<int> CountPrevisionsAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(Previsions);

        public Task<int> CountTransfertsAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(Transferts);

        public Task<int> CountVersionsSuivantesAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(Suivantes);

        public Task<IReadOnlyList<UtilisateurLookupDto>> GetUtilisateursAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UtilisateurLookupDto>>([]);

        public Task<VersionBudgetaireDto> CreateAsync(
            long idExercice, int numeroVersion, string libelle, long? idVersionPrecedente,
            DateOnly dateDebutEffet, DateOnly? dateFinEffet, string? motif, string statut,
            long idUtilisateurCreation,
            CancellationToken cancellationToken = default)
        {
            var created = Dto(99, idExercice, numeroVersion) with { Libelle = libelle, Statut = statut };
            Items.Add(created);
            return Task.FromResult(created);
        }

        public Task<VersionBudgetaireDto?> UpdateAsync(
            long idVersion, long idExercice, int numeroVersion, string libelle, long? idVersionPrecedente,
            DateOnly dateDebutEffet, DateOnly? dateFinEffet, string? motif,
            long idUtilisateurCreation,
            CancellationToken cancellationToken = default)
        {
            var current = Items.FirstOrDefault(v => v.IdVersion == idVersion);
            if (current is null) return Task.FromResult<VersionBudgetaireDto?>(null);
            var updated = current with { Libelle = libelle, NumeroVersion = numeroVersion };
            Items[Items.IndexOf(current)] = updated;
            return Task.FromResult<VersionBudgetaireDto?>(updated);
        }

        public Task<bool> DeleteAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.RemoveAll(v => v.IdVersion == idVersion) > 0);

        public Task<VersionBudgetaireDto?> AppliquerTransitionAsync(
            long idVersion,
            string nouveauStatut,
            long idUtilisateur,
            string operation,
            Action<Domain.Entities.VersionBudgetaire> appliquerTrace,
            CancellationToken cancellationToken = default)
        {
            var current = Items.FirstOrDefault(v => v.IdVersion == idVersion);
            if (current is null) return Task.FromResult<VersionBudgetaireDto?>(null);

            var entity = new Domain.Entities.VersionBudgetaire
            {
                IdVersion = idVersion,
                Statut = current.Statut,
                FK_UtilisateurSoumission = current.IdUtilisateurSoumission,
                DateSoumission = current.DateSoumission,
                FK_UtilisateurControle = current.IdUtilisateurControle,
                DateControle = current.DateControle,
                FK_UtilisateurValidation = current.IdUtilisateurValidation,
                DateValidation = current.DateValidation,
                FK_UtilisateurRejet = current.IdUtilisateurRejet,
                DateRejet = current.DateRejet,
                MotifRejet = current.MotifRejet,
            };
            entity.Statut = nouveauStatut;
            appliquerTrace(entity);
            Audits.Add((operation, idUtilisateur));

            var updated = current with
            {
                Statut = nouveauStatut,
                IdUtilisateurSoumission = entity.FK_UtilisateurSoumission,
                DateSoumission = entity.DateSoumission,
                IdUtilisateurControle = entity.FK_UtilisateurControle,
                DateControle = entity.DateControle,
                IdUtilisateurValidation = entity.FK_UtilisateurValidation,
                DateValidation = entity.DateValidation,
                IdUtilisateurRejet = entity.FK_UtilisateurRejet,
                DateRejet = entity.DateRejet,
                MotifRejet = entity.MotifRejet,
            };
            Items[Items.IndexOf(current)] = updated;
            return Task.FromResult<VersionBudgetaireDto?>(updated);
        }
    }
}
