using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class WorkflowPrevisionUbServiceTests
{
    [Fact]
    public async Task ControlerDepartement_UniquementSoumises()
    {
        var repo = FakeRepo.WithUb(
            (1, "A001", 10, StatutVersionBudgetaire.Soumise),
            (2, "A002", 10, StatutVersionBudgetaire.Validee),
            (3, "A003", 10, StatutVersionBudgetaire.Soumise));
        var service = Create(repo, AppPermissions.VersionsControler);

        var result = await service.ControlerDepartementAsync(1, 10);

        Assert.Equal(2, result.Traitees);
        Assert.Equal(1, result.Ignorees);
        Assert.Equal(StatutVersionBudgetaire.Controlee, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Validee, repo.GetStatut(1, 2));
        Assert.Equal(StatutVersionBudgetaire.Controlee, repo.GetStatut(1, 3));
    }

    [Fact]
    public async Task ValiderDepartement_UniquementControlees()
    {
        var repo = FakeRepo.WithUb(
            (1, "A001", 10, StatutVersionBudgetaire.Controlee),
            (2, "A002", 10, StatutVersionBudgetaire.Soumise),
            (3, "A003", 10, StatutVersionBudgetaire.Controlee));
        var service = Create(repo, AppPermissions.VersionsValider);

        var result = await service.ValiderDepartementAsync(1, 10);

        Assert.Equal(2, result.Traitees);
        Assert.Equal(1, result.Ignorees);
        Assert.Equal(StatutVersionBudgetaire.Validee, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Soumise, repo.GetStatut(1, 2));
        Assert.Equal(StatutVersionBudgetaire.Validee, repo.GetStatut(1, 3));
    }

    [Fact]
    public async Task Rejeter_Ub_IsoleLesAutresUb()
    {
        var repo = FakeRepo.WithUb(
            (1, "A001", 10, StatutVersionBudgetaire.Soumise),
            (2, "A002", 10, StatutVersionBudgetaire.Soumise),
            (3, "A003", 10, StatutVersionBudgetaire.Soumise));
        var service = Create(repo, AppPermissions.VersionsRejeter);

        await service.RejeterAsync(1, 2, "Erreur A002");

        Assert.Equal(StatutVersionBudgetaire.Soumise, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Rejetee, repo.GetStatut(1, 2));
        Assert.Equal(StatutVersionBudgetaire.Soumise, repo.GetStatut(1, 3));
        Assert.Equal(StatutVersionBudgetaire.Rejetee, repo.StatutVersion);
        Assert.Contains(repo.Audits, a => a == "REJET_UB");
    }

    [Fact]
    public async Task RejeterDepartement_NeTouchePasAutreDepartement()
    {
        var repo = FakeRepo.WithUb(
            (1, "A001", 10, StatutVersionBudgetaire.Soumise),
            (2, "A002", 10, StatutVersionBudgetaire.Soumise),
            (3, "B001", 20, StatutVersionBudgetaire.Soumise));
        var service = Create(repo, AppPermissions.VersionsRejeter);

        var result = await service.RejeterDepartementAsync(1, 10, "Rejet dept A");

        Assert.Equal(2, result.Traitees);
        Assert.Equal(0, result.Ignorees);
        Assert.Equal(StatutVersionBudgetaire.Rejetee, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Rejetee, repo.GetStatut(1, 2));
        Assert.Equal(StatutVersionBudgetaire.Soumise, repo.GetStatut(1, 3));
        Assert.Contains(repo.Audits, a => a == "REJET_DEPARTEMENT");
    }

    [Fact]
    public async Task RejeterDepartement_IgnoreValidee()
    {
        var repo = FakeRepo.WithUb(
            (1, "A001", 10, StatutVersionBudgetaire.Validee),
            (2, "A002", 10, StatutVersionBudgetaire.Soumise));
        var service = Create(repo, AppPermissions.VersionsRejeter);

        var result = await service.RejeterDepartementAsync(1, 10, "Partiel");

        Assert.Equal(1, result.Traitees);
        Assert.Equal(1, result.Ignorees);
        Assert.Equal(StatutVersionBudgetaire.Validee, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Rejetee, repo.GetStatut(1, 2));
    }

    [Fact]
    public async Task Rejeter_RefuseVersionOuUbInexistante()
    {
        var repo = FakeRepo.WithUb((1, "A001", 10, StatutVersionBudgetaire.Soumise));
        var service = Create(repo, AppPermissions.VersionsRejeter);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejeterAsync(99, 1, "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejeterAsync(1, 99, "x"));
    }

    [Fact]
    public async Task Rejeter_RefuseMotifVide()
    {
        var repo = FakeRepo.WithUb((1, "A001", 10, StatutVersionBudgetaire.Soumise));
        var service = Create(repo, AppPermissions.VersionsRejeter);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejeterAsync(1, 1, "   "));
    }

    [Fact]
    public async Task Rejeter_RefuseSansPermission()
    {
        var repo = FakeRepo.WithUb((1, "A001", 10, StatutVersionBudgetaire.Soumise));
        var service = Create(repo);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RejeterAsync(1, 1, "motif"));
    }

    [Fact]
    public async Task Rejeter_RefuseTransitionValidee()
    {
        var repo = FakeRepo.WithUb((1, "A001", 10, StatutVersionBudgetaire.Validee));
        var service = Create(repo, AppPermissions.VersionsRejeter);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejeterAsync(1, 1, "interdit"));
        Assert.Contains("Transition interdite", ex.Message);
    }

    [Theory]
    [InlineData(new[] { "SOUMISE", "SOUMISE", "REJETEE" }, "REJETEE")]
    [InlineData(new[] { "VALIDEE", "VALIDEE" }, "VALIDEE")]
    [InlineData(new[] { "CONTROLEE", "VALIDEE" }, "CONTROLEE")]
    [InlineData(new[] { "SOUMISE", "CONTROLEE" }, "SOUMISE")]
    [InlineData(new[] { "BROUILLON", "SOUMISE" }, "BROUILLON")]
    [InlineData(new string[0], "BROUILLON")]
    public void RecalculAgrege_Combinaisons(string[] statuts, string attendu)
    {
        Assert.Equal(attendu, VersionStatutAgrege.Calculer(statuts));
    }

    [Fact]
    public async Task AnnulerSoumission_SoumiseVersBrouillon()
    {
        var repo = FakeRepo.WithUb((1, "A001", 10, StatutVersionBudgetaire.Soumise));
        var service = Create(repo, AppPermissions.PrevisionsSoumettre);

        var result = await service.AnnulerSoumissionAsync(1, 1);

        Assert.Equal(StatutVersionBudgetaire.Brouillon, result.Statut);
        Assert.Equal(StatutVersionBudgetaire.Brouillon, repo.GetStatut(1, 1));
        Assert.Contains(repo.Audits, a => a == "ANNULER_SOUMISSION_UB");
    }

    [Fact]
    public async Task AnnulerSoumission_RefuseDepuisControlee()
    {
        var repo = FakeRepo.WithUb((1, "A001", 10, StatutVersionBudgetaire.Controlee));
        var service = Create(repo, AppPermissions.PrevisionsSoumettre);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnnulerSoumissionAsync(1, 1));
        Assert.Contains("Transition interdite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnnulerSoumission_RefuseSansPermission()
    {
        var repo = FakeRepo.WithUb((1, "A001", 10, StatutVersionBudgetaire.Soumise));
        var service = Create(repo);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AnnulerSoumissionAsync(1, 1));
    }

    [Fact]
    public async Task Soumettre_PuisControler_PuisValider_RecalculeVersion()
    {
        var repo = FakeRepo.WithUb(
            (1, "A001", 10, StatutVersionBudgetaire.Brouillon),
            (2, "A002", 10, StatutVersionBudgetaire.Brouillon));
        var service = Create(
            repo,
            AppPermissions.PrevisionsSoumettre,
            AppPermissions.VersionsControler,
            AppPermissions.VersionsValider);

        await service.SoumettreAsync(1, 1);
        await service.SoumettreAsync(1, 2);
        Assert.Equal(StatutVersionBudgetaire.Soumise, repo.StatutVersion);

        await service.ControlerAsync(1, 1);
        await service.ControlerAsync(1, 2);
        Assert.Equal(StatutVersionBudgetaire.Controlee, repo.StatutVersion);

        await service.ValiderAsync(1, 1);
        await service.ValiderAsync(1, 2);
        Assert.Equal(StatutVersionBudgetaire.Validee, repo.StatutVersion);
    }

    [Fact]
    public async Task RejeterDepartement_TransactionRollback_AucuneModificationPartielle()
    {
        var repo = FakeRepo.WithUb(
            (1, "A001", 10, StatutVersionBudgetaire.Soumise),
            (2, "A002", 10, StatutVersionBudgetaire.Soumise));
        repo.FailOnDepartementRejet = true;
        var service = Create(repo, AppPermissions.VersionsRejeter);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejeterDepartementAsync(1, 10, "boom"));

        Assert.Equal(StatutVersionBudgetaire.Soumise, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Soumise, repo.GetStatut(1, 2));
        Assert.Equal(StatutVersionBudgetaire.Soumise, repo.StatutVersion);
        Assert.DoesNotContain(repo.Audits, a => a == "REJET_DEPARTEMENT");
    }

    [Fact]
    public async Task TestIsolationWorkflowEntreDepartements()
    {
        var repo = FakeRepo.WithUb(
            (1, "A1", 10, StatutVersionBudgetaire.Soumise),
            (2, "A2", 10, StatutVersionBudgetaire.Soumise),
            (3, "B1", 20, StatutVersionBudgetaire.Brouillon),
            (4, "B2", 20, StatutVersionBudgetaire.Brouillon));
        var service = Create(repo, AppPermissions.VersionsRejeter, AppPermissions.PrevisionsSoumettre);

        await service.RejeterDepartementAsync(1, 10, "Rejet A");

        Assert.Equal(StatutVersionBudgetaire.Rejetee, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Rejetee, repo.GetStatut(1, 2));
        Assert.Equal(StatutVersionBudgetaire.Brouillon, repo.GetStatut(1, 3));
        Assert.Equal(StatutVersionBudgetaire.Brouillon, repo.GetStatut(1, 4));
        Assert.True(PrevisionBudgetaireService.EstModificationAutorisee(repo.GetStatut(1, 3)));
        Assert.True(PrevisionBudgetaireService.EstModificationAutorisee(repo.GetStatut(1, 1)));
    }

    [Fact]
    public async Task TestModificationUbBrouillonAvecVersionSoumise()
    {
        var repo = FakeRepo.WithUb(
            (1, "A1", 10, StatutVersionBudgetaire.Soumise),
            (2, "B1", 20, StatutVersionBudgetaire.Brouillon));
        repo.StatutVersion = StatutVersionBudgetaire.Soumise;
        var service = Create(repo, AppPermissions.VersionsControler);

        Assert.Equal(StatutVersionBudgetaire.Brouillon, await service.GetStatutOperationnelAsync(1, 2));
        Assert.True(PrevisionBudgetaireService.EstModificationAutorisee(await service.GetStatutOperationnelAsync(1, 2)));
        Assert.False(PrevisionBudgetaireService.EstModificationAutorisee(await service.GetStatutOperationnelAsync(1, 1)));
    }

    [Fact]
    public async Task TestRejetDepartementNeTouchePasAutreDepartement()
    {
        var repo = FakeRepo.WithUb(
            (1, "A1", 10, StatutVersionBudgetaire.Soumise),
            (2, "B1", 20, StatutVersionBudgetaire.Controlee));
        var service = Create(repo, AppPermissions.VersionsRejeter);

        await service.RejeterDepartementAsync(1, 10, "x");
        Assert.Equal(StatutVersionBudgetaire.Rejetee, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Controlee, repo.GetStatut(1, 2));
    }

    [Fact]
    public async Task TestSoumissionDepartementNeTouchePasAutreDepartement()
    {
        var repo = FakeRepo.WithUb(
            (1, "A1", 10, StatutVersionBudgetaire.Brouillon),
            (2, "B1", 20, StatutVersionBudgetaire.Brouillon));
        var service = Create(repo, AppPermissions.PrevisionsSoumettre);

        await service.SoumettreDepartementAsync(1, 10);
        Assert.Equal(StatutVersionBudgetaire.Soumise, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Brouillon, repo.GetStatut(1, 2));
    }

    [Fact]
    public async Task TestControleDepartementNeTouchePasAutreDepartement()
    {
        var repo = FakeRepo.WithUb(
            (1, "A1", 10, StatutVersionBudgetaire.Soumise),
            (2, "B1", 20, StatutVersionBudgetaire.Soumise));
        var service = Create(repo, AppPermissions.VersionsControler);

        await service.ControlerDepartementAsync(1, 10);
        Assert.Equal(StatutVersionBudgetaire.Controlee, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Soumise, repo.GetStatut(1, 2));
    }

    [Fact]
    public async Task TestValidationDepartementNeTouchePasAutreDepartement()
    {
        var repo = FakeRepo.WithUb(
            (1, "A1", 10, StatutVersionBudgetaire.Controlee),
            (2, "B1", 20, StatutVersionBudgetaire.Controlee));
        var service = Create(repo, AppPermissions.VersionsValider);

        await service.ValiderDepartementAsync(1, 10);
        Assert.Equal(StatutVersionBudgetaire.Validee, repo.GetStatut(1, 1));
        Assert.Equal(StatutVersionBudgetaire.Controlee, repo.GetStatut(1, 2));
    }

    [Fact]
    public async Task TestNouvelleUbAvecVersionGlobalementSoumise()
    {
        var repo = FakeRepo.WithUb((1, "A1", 10, StatutVersionBudgetaire.Soumise));
        repo.StatutVersion = StatutVersionBudgetaire.Soumise;
        var service = Create(repo, AppPermissions.PrevisionsEcrire);

        await service.EnsureExistsAsync(1, 99, 42);
        Assert.Equal(StatutVersionBudgetaire.Brouillon, repo.GetStatut(1, 99));
        Assert.Equal(StatutVersionBudgetaire.Brouillon, await service.GetStatutOperationnelAsync(1, 99));
        Assert.True(PrevisionBudgetaireService.EstModificationAutorisee(await service.GetStatutOperationnelAsync(1, 99)));
    }

    [Fact]
    public async Task TestModificationUbRejeteeAvecAutreDepartementSoumis()
    {
        var repo = FakeRepo.WithUb(
            (1, "A1", 10, StatutVersionBudgetaire.Rejetee),
            (2, "B1", 20, StatutVersionBudgetaire.Soumise));
        repo.StatutVersion = StatutVersionBudgetaire.Rejetee;
        Assert.True(PrevisionBudgetaireService.EstModificationAutorisee(repo.GetStatut(1, 1)));
        Assert.False(PrevisionBudgetaireService.EstModificationAutorisee(repo.GetStatut(1, 2)));
        await Task.CompletedTask;
    }

    private static WorkflowPrevisionUbService Create(FakeRepo repo, params string[] permissions)
        => new(repo, new FakeCurrentUser(permissions), new NoOpDocuments(), new NoopPerimetreAccesService());

    private sealed class NoOpDocuments : IDocumentPrevisionService
    {
        public Task<DocumentPrevisionDto> GenererSoumissionUbAsync(long idVersion, long idUB, long idUtilisateur, long? idAudit, CancellationToken cancellationToken = default)
            => Task.FromResult(Dummy());
        public Task<DocumentPrevisionDto?> GenererSoumissionDepartementAsync(long idVersion, long idDepartement, long idUtilisateur, long? idAudit, IReadOnlyList<long> ubTraitees, CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentPrevisionDto?>(null);
        public Task<DocumentPrevisionDto> GenererRejetUbAsync(long idVersion, long idUB, long idUtilisateur, string motif, string? statutAvant, long? idAudit, CancellationToken cancellationToken = default)
            => Task.FromResult(Dummy());
        public Task<DocumentPrevisionDto?> GenererRejetDepartementAsync(long idVersion, long idDepartement, long idUtilisateur, string motif, long? idAudit, IReadOnlyList<(long IdUB, string? StatutAvant)> ubTraitees, CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentPrevisionDto?>(null);
        public Task<DocumentPrevisionDto> GenererControleUbAsync(long idVersion, long idUB, long idUtilisateur, long? idAudit, CancellationToken cancellationToken = default)
            => Task.FromResult(Dummy());
        public Task<DocumentPrevisionDto?> GenererControleDepartementAsync(long idVersion, long idDepartement, long idUtilisateur, long? idAudit, IReadOnlyList<long> ubTraitees, CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentPrevisionDto?>(null);
        public Task<DocumentPrevisionDto> GenererValidationUbAsync(long idVersion, long idUB, long idUtilisateur, long? idAudit, CancellationToken cancellationToken = default)
            => Task.FromResult(Dummy());
        public Task<DocumentPrevisionDto?> GenererValidationDepartementAsync(long idVersion, long idDepartement, long idUtilisateur, long? idAudit, IReadOnlyList<long> ubTraitees, CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentPrevisionDto?>(null);
        public Task<DocumentPrevisionDto?> GetByIdAsync(long idDocument, CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentPrevisionDto?>(null);
        public Task<DocumentPrevisionDto?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentPrevisionDto?>(null);
        public Task<DocumentPrevisionDto?> GetByAuditAsync(long idAudit, CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentPrevisionDto?>(null);
        public Task<byte[]?> GetPdfBytesAsync(long idDocument, CancellationToken cancellationToken = default)
            => Task.FromResult<byte[]?>(null);

        private static DocumentPrevisionDto Dummy() => new(
            0, "REF", "SUB", "T", null, 1, 2026, 1, 1, "D", "Dept", null, null, null, "UB", 1,
            1, "U", DateTime.Now, null, null, null, 0, 0, 0, 0, DateTime.Now, 0);
    }

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

    private sealed class FakeRepo : IWorkflowPrevisionUbRepository
    {
        public Dictionary<(long Version, long Ub), WorkflowRow> Rows { get; } = new();
        public HashSet<(long Version, long Ub)> Previsions { get; } = new();
        public string StatutVersion { get; set; } = StatutVersionBudgetaire.Brouillon;
        public List<string> Audits { get; } = [];
        public bool FailOnDepartementRejet { get; set; }

        public static FakeRepo WithUb(params (long IdUb, string Code, long IdDept, string Statut)[] items)
        {
            var repo = new FakeRepo();
            foreach (var item in items)
            {
                repo.Rows[(1, item.IdUb)] = new WorkflowRow(
                    item.IdUb, item.Code, item.IdDept, item.Statut);
                repo.Previsions.Add((1, item.IdUb));
            }

            repo.StatutVersion = VersionStatutAgrege.Calculer(items.Select(i => i.Statut));
            return repo;
        }

        public string GetStatut(long idVersion, long idUb) => Rows[(idVersion, idUb)].Statut;

        public Task EnsureExistsAsync(long idVersion, long idUB, long idUtilisateur, CancellationToken cancellationToken = default)
        {
            if (!Rows.ContainsKey((idVersion, idUB)))
            {
                Rows[(idVersion, idUB)] = new WorkflowRow(idUB, "U" + idUB, 0, StatutVersionBudgetaire.Brouillon);
            }
            return Task.CompletedTask;
        }

        public Task<string?> GetStatutAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(Rows.TryGetValue((idVersion, idUB), out var r) ? r.Statut : null);

        public Task<bool> ExistsPrevisionForPairAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult(Previsions.Contains((idVersion, idUB)));

        public Task<bool> ExistsVersionAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(idVersion == 1 || Rows.Keys.Any(k => k.Version == idVersion));

        public Task<bool> ExistsUbAsync(long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult(Rows.Keys.Any(k => k.Ub == idUB));

        public Task<(long IdDepartement, string CodeDepartement, string LibelleDepartement)?> GetUbDepartementAsync(
            long idUB, CancellationToken cancellationToken = default)
        {
            var row = Rows.Values.FirstOrDefault(r => r.IdUb == idUB);
            return Task.FromResult<(long, string, string)?>(
                row is null ? null : (row.IdDepartement, "D" + row.IdDepartement, "Dept"));
        }

        public Task<WorkflowPrevisionUbDto?> GetAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
        {
            if (!Rows.TryGetValue((idVersion, idUB), out var r))
            {
                return Task.FromResult<WorkflowPrevisionUbDto?>(null);
            }

            return Task.FromResult<WorkflowPrevisionUbDto?>(ToDto(idVersion, r));
        }

        public Task TransitionnerUbAsync(
            long idVersion,
            long idUB,
            string nouveauStatut,
            long idUtilisateur,
            string operationAudit,
            string portee,
            Action<WorkflowPrevisionUb> appliquerTrace,
            object? auditExtra,
            CancellationToken cancellationToken = default)
        {
            var row = Rows[(idVersion, idUB)];
            if (!StatutVersionBudgetaire.EstTransitionAutorisee(row.Statut, nouveauStatut))
            {
                throw new InvalidOperationException(
                    $"Transition interdite : {row.Statut} → {StatutVersionBudgetaire.Normaliser(nouveauStatut)}.");
            }

            var entity = new WorkflowPrevisionUb { Statut = row.Statut };
            appliquerTrace(entity);
            row.Statut = StatutVersionBudgetaire.Normaliser(nouveauStatut);
            row.MotifRejet = entity.MotifRejet;
            Audits.Add(operationAudit);
            StatutVersion = VersionStatutAgrege.Calculer(Rows.Where(x => x.Key.Version == idVersion).Select(x => x.Value.Statut));
            return Task.CompletedTask;
        }

        public Task<WorkflowDepartementBulkResultDto> SoumettreDepartementAsync(
            long idVersion,
            long idDepartement,
            long idUtilisateur,
            CancellationToken cancellationToken = default)
            => BulkAsync(idVersion, idDepartement,
                s => s is StatutVersionBudgetaire.Brouillon or StatutVersionBudgetaire.Rejetee,
                StatutVersionBudgetaire.Soumise, "SOUMISE", "SOUMISSION_DEPARTEMENT");

        public Task<WorkflowDepartementBulkResultDto> ControlerDepartementAsync(
            long idVersion,
            long idDepartement,
            long idUtilisateur,
            CancellationToken cancellationToken = default)
            => BulkAsync(idVersion, idDepartement, s => s == StatutVersionBudgetaire.Soumise,
                StatutVersionBudgetaire.Controlee, "CONTROLEE", "CONTROLE_DEPARTEMENT");

        public Task<WorkflowDepartementBulkResultDto> ValiderDepartementAsync(
            long idVersion,
            long idDepartement,
            long idUtilisateur,
            CancellationToken cancellationToken = default)
            => BulkAsync(idVersion, idDepartement, s => s == StatutVersionBudgetaire.Controlee,
                StatutVersionBudgetaire.Validee, "VALIDEE", "VALIDATION_DEPARTEMENT");

        public Task<WorkflowDepartementBulkResultDto> RejeterDepartementAsync(
            long idVersion,
            long idDepartement,
            string motif,
            long idUtilisateur,
            CancellationToken cancellationToken = default)
        {
            if (FailOnDepartementRejet)
            {
                throw new InvalidOperationException("Erreur simulée");
            }

            var result = BulkAsync(
                idVersion,
                idDepartement,
                s => s is StatutVersionBudgetaire.Soumise or StatutVersionBudgetaire.Controlee,
                StatutVersionBudgetaire.Rejetee,
                "REJETEE",
                "REJET_DEPARTEMENT",
                motif);
            return result;
        }

        private Task<WorkflowDepartementBulkResultDto> BulkAsync(
            long idVersion,
            long idDepartement,
            Func<string, bool> eligible,
            string nouveau,
            string outcome,
            string audit,
            string? motif = null)
        {
            var details = new List<WorkflowUbRejetDetailDto>();
            var traitees = 0;
            var ignorees = 0;
            foreach (var kv in Rows.Where(x => x.Key.Version == idVersion && x.Value.IdDepartement == idDepartement).ToList())
            {
                var avant = kv.Value.Statut;
                if (eligible(avant))
                {
                    kv.Value.Statut = nouveau;
                    if (motif is not null) kv.Value.MotifRejet = motif;
                    traitees++;
                    details.Add(new WorkflowUbRejetDetailDto(kv.Key.Ub, kv.Value.CodeUb, avant, nouveau, outcome));
                }
                else
                {
                    ignorees++;
                    details.Add(new WorkflowUbRejetDetailDto(kv.Key.Ub, kv.Value.CodeUb, avant, null, "IGNORE"));
                }
            }

            Audits.Add(audit);
            StatutVersion = VersionStatutAgrege.Calculer(Rows.Where(x => x.Key.Version == idVersion).Select(x => x.Value.Statut));
            return Task.FromResult(new WorkflowDepartementBulkResultDto(traitees, ignorees, details));
        }

        public Task RecalculerStatutVersionAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default)
        {
            StatutVersion = VersionStatutAgrege.Calculer(Rows.Where(x => x.Key.Version == idVersion).Select(x => x.Value.Statut));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListStatutsUbAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(
                Rows.Where(x => x.Key.Version == idVersion).Select(x => x.Value.Statut).ToList());

        private static WorkflowPrevisionUbDto ToDto(long idVersion, WorkflowRow r)
            => new(r.IdUb, idVersion, r.IdUb, r.CodeUb, r.CodeUb, r.IdDepartement, "D", "Dept",
                r.Statut, null, null, null, null, null, null, null, null, r.MotifRejet);
    }

    private sealed class WorkflowRow(long idUb, string codeUb, long idDepartement, string statut)
    {
        public long IdUb { get; } = idUb;
        public string CodeUb { get; } = codeUb;
        public long IdDepartement { get; } = idDepartement;
        public string Statut { get; set; } = statut;
        public string? MotifRejet { get; set; }
    }
}
