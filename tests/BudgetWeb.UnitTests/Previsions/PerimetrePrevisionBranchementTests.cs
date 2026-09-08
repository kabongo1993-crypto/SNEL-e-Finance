using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Security;
using BudgetWeb.UnitTests.DemandePaiement;
using Xunit;

namespace BudgetWeb.UnitTests.Previsions;

/// <summary>
/// Branchement périmètre sur Prévisions : saisie / lecture / liste UB.
/// </summary>
public class PerimetrePrevisionBranchementTests
{
    [Fact]
    public void PeutVoirToutesUbPrevisions_Versions_Ou_Admin()
    {
        var ctrl = new PerimetreAccesService(
            new FakePerimetreReader(new FakeDemandePaiementRepo()),
            new FakeUser { Permissions = [AppPermissions.VersionsControler] });
        Assert.True(ctrl.PeutVoirToutesUbPrevisions());

        var saisisseur = new PerimetreAccesService(
            new FakePerimetreReader(new FakeDemandePaiementRepo()),
            new FakeUser { Permissions = [AppPermissions.PrevisionsEcrire, AppPermissions.PrevisionsSoumettre] });
        Assert.False(saisisseur.PeutVoirToutesUbPrevisions());
    }

    [Fact]
    public async Task Saisie_Perimetre_Configure_Refuse_Ub_Hors_Perimetre()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbDepartements[11] = 1;

        var service = new PerimetreAccesService(
            new FakePerimetreReader(repo),
            new FakeUser { UserId = 1, Permissions = [AppPermissions.PrevisionsEcrire] });

        await service.GarantirAccesUbSaisiePrevisionsAsync(10);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GarantirAccesUbSaisiePrevisionsAsync(11));
    }

    [Fact]
    public async Task Saisie_Sans_Perimetre_Autorise_Toute_Ub()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = null;

        var service = new PerimetreAccesService(
            new FakePerimetreReader(repo),
            new FakeUser { UserId = 1, Permissions = [AppPermissions.PrevisionsEcrire] });

        await service.GarantirAccesUbSaisiePrevisionsAsync(999);
    }

    [Fact]
    public async Task GetIdsUbAutorisees_Retourne_Null_Sans_Perimetre()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = null;

        var service = new PerimetreAccesService(
            new FakePerimetreReader(repo),
            new FakeUser { UserId = 1, Permissions = [AppPermissions.PrevisionsEcrire] });

        var ids = await service.GetIdsUbAutoriseesSaisieAsync();
        Assert.Null(ids);
    }

    [Fact]
    public async Task GetIdsUbAutorisees_Filtre_Selon_Perimetre()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbDepartements[11] = 1;

        var service = new PerimetreAccesService(
            new FakePerimetreReader(repo),
            new FakeUser { UserId = 1, Permissions = [AppPermissions.PrevisionsEcrire] });

        var ids = await service.GetIdsUbAutoriseesSaisieAsync();
        Assert.NotNull(ids);
        Assert.Contains(10L, ids!);
        Assert.DoesNotContain(11L, ids!);
    }

    [Fact]
    public async Task Lecture_Sans_Perimetre_Exige_Creation_Prevision()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = null;
        repo.UbProxyPrevision.Clear();
        repo.UbProxyPrevision.Add(5);

        var service = new PerimetreAccesService(
            new FakePerimetreReader(repo),
            new FakeUser { UserId = 1, Permissions = [AppPermissions.PrevisionsEcrire] });

        Assert.True(await service.PeutAccederUbLectureCourantAsync(5));
        Assert.False(await service.PeutAccederUbLectureCourantAsync(6));
    }
}
