using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class SuiviPrevisionServiceTests
{
    [Fact]
    public async Task GetMesPrevisions_FiltreParUtilisateurCourant()
    {
        var repo = new FakeSuiviRepo();
        var user = new FakeCurrentUser(10, [AppPermissions.PrevisionsEcrire]);
        var service = SuiviPrevisionServiceTestFactory.Create(repo, user);

        await service.GetMesPrevisionsAsync(null, null, null, null, null);

        Assert.Equal(10, repo.LastUserIdFilter);
    }

    [Fact]
    public async Task GetSoumissions_RefuseSansPermission()
    {
        var service = SuiviPrevisionServiceTestFactory.Create(
            new FakeSuiviRepo(), new FakeCurrentUser(1, []));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetSoumissionsAsync(null, null, null, null, null));
    }

    [Fact]
    public async Task GetSoumissions_ExclutBrouillonsParDefaut()
    {
        var repo = new FakeSuiviRepo();
        repo.Seed(
            Row(1, 1, StatutVersionBudgetaire.Brouillon, 100, 0, 0),
            Row(1, 2, StatutVersionBudgetaire.Soumise, 200, 50, 0),
            Row(2, 1, StatutVersionBudgetaire.Validee, 10, 10, 10));
        var service = SuiviPrevisionServiceTestFactory.Create(
            repo, new FakeCurrentUser(1, [AppPermissions.VersionsControler]));

        var result = await service.GetSoumissionsAsync(null, null, null, null, null);

        Assert.Equal(2, result.Lignes.Count);
        Assert.DoesNotContain(result.Lignes, l => l.Statut == StatutVersionBudgetaire.Brouillon);
        Assert.Equal(1, result.Compteurs.Soumises);
        Assert.Equal(1, result.Compteurs.Validees);
        Assert.Equal(0, result.Compteurs.Brouillons);
    }

    [Fact]
    public async Task GetSoumissions_FiltreStatutSoumise()
    {
        var repo = new FakeSuiviRepo();
        repo.Seed(
            Row(1, 1, StatutVersionBudgetaire.Soumise, 100, 0, 0),
            Row(1, 2, StatutVersionBudgetaire.Controlee, 200, 0, 0));
        var service = SuiviPrevisionServiceTestFactory.Create(
            repo, new FakeCurrentUser(1, [AppPermissions.VersionsControler]));

        var result = await service.GetSoumissionsAsync(null, null, null, "SOUMISE", null);

        Assert.Equal("SOUMISE", repo.LastStatutFilter);
        Assert.Single(result.Lignes);
        Assert.Equal(StatutVersionBudgetaire.Soumise, result.Lignes[0].Statut);
    }

    [Fact]
    public async Task GetUbDetail_CalculeTotauxEtFlagsPermissions()
    {
        var repo = new FakeSuiviRepo();
        repo.Detail = new SuiviUbDetailDto(
            1, 1, "V1", StatutVersionBudgetaire.Soumise, 5, 2026,
            10, "A001", "UB A", 2, "DG", "Direction G",
            1000m, 200m, 50m, 1250m,
            DateTime.UtcNow, "Jean X", null, null, null, null, null, null, null,
            false, false, false);
        var service = SuiviPrevisionServiceTestFactory.Create(
            repo, new FakeCurrentUser(99, [AppPermissions.VersionsControler, AppPermissions.VersionsRejeter]));

        var detail = await service.GetUbDetailAsync(1, 10);

        Assert.NotNull(detail);
        Assert.Equal(1250m, detail!.MontantTotal);
        Assert.True(detail.PeutControler);
        Assert.False(detail.PeutValider);
        Assert.True(detail.PeutRejeter);
    }

    [Fact]
    public async Task GetUbDetail_RefuseSansAcces()
    {
        var repo = new FakeSuiviRepo();
        repo.Detail = new SuiviUbDetailDto(
            1, 1, "V1", StatutVersionBudgetaire.Soumise, 5, 2026,
            10, "A001", "UB A", 2, "DG", "Direction G",
            100m, 0, 0, 100m,
            null, null, null, null, null, null, null, null, null,
            false, false, false);
        var service = SuiviPrevisionServiceTestFactory.Create(
            repo, new FakeCurrentUser(5, [AppPermissions.PrevisionsEcrire]));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetUbDetailAsync(1, 10));
    }

    private static SuiviPrevisionUbResumeDto Row(
        long idVersion, long idUb, string statut, decimal dc, decimal ae, decimal bi)
        => new(
            idVersion, 1, "V1", statut, 1, 2026,
            idUb, $"UB{idUb}", $"Lib {idUb}", 1, "D1", "Dept",
            dc, ae, bi, dc + ae + bi,
            DateTime.UtcNow, null, null, null, null, null);

    private sealed class FakeCurrentUser(long userId, IReadOnlyList<string> permissions) : ICurrentUserService
    {
        public long? UserId => userId;
        public string? Username => "test";
        public string? DisplayName => "Test";
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => permissions;
        public bool IsInRole(string role) => false;
        public bool HasPermission(string permission) =>
            permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));
        public long RequireUserId() => userId;
    }

    private sealed class FakeSuiviRepo : ISuiviPrevisionRepository
    {
        public long? LastUserIdFilter { get; private set; }
        public string? LastStatutFilter { get; private set; }
        public SuiviUbDetailDto? Detail { get; set; }
        private IReadOnlyList<SuiviPrevisionUbResumeDto> _rows = [];

        public void Seed(params SuiviPrevisionUbResumeDto[] rows) => _rows = rows;

        public Task<SuiviPrevisionListeDto> GetResumeParUbAsync(
            long? idUtilisateurCreation,
            long? idExercice,
            long? idVersion,
            long? idDepartement,
            string? statut,
            string? searchUb,
            CancellationToken cancellationToken = default)
        {
            LastUserIdFilter = idUtilisateurCreation;
            LastStatutFilter = statut;
            var lignes = _rows.AsEnumerable();
            if (idUtilisateurCreation is > 0)
                lignes = lignes.Where(_ => true);
            if (!string.IsNullOrEmpty(statut))
                lignes = lignes.Where(l => l.Statut == statut);
            var list = lignes.ToList();
            var c = new SuiviPrevisionCompteursDto(
                list.Count(l => l.Statut == StatutVersionBudgetaire.Brouillon),
                list.Count(l => l.Statut == StatutVersionBudgetaire.Soumise),
                list.Count(l => l.Statut == StatutVersionBudgetaire.Controlee),
                list.Count(l => l.Statut == StatutVersionBudgetaire.Validee),
                list.Count(l => l.Statut == StatutVersionBudgetaire.Rejetee));
            return Task.FromResult(new SuiviPrevisionListeDto(c, list));
        }

        public Task<SuiviUbDetailDto?> GetUbDetailAsync(
            long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult(Detail);

        public Task<SuiviUbDetailLignesDto> GetUbLignesAsync(
            long idVersion, long idUB, string codeType, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
