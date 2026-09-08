using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Branchement progressif du périmètre DPM :
/// périmètre configuré → PerimetreAccess ; sinon → fallback historique.
/// </summary>
public class PerimetreDpmBranchementTests
{
    [Fact]
    public void EstConfigure_Refuse_Null_Et_Vide()
    {
        Assert.False(PerimetreAccess.EstConfigure(null));
        Assert.False(PerimetreAccess.EstConfigure(
            new PerimetreUtilisateurSnapshot(false, false, [], [])));
    }

    [Fact]
    public void EstConfigure_Accepte_Flags_Ou_Lignes()
    {
        Assert.True(PerimetreAccess.EstConfigure(
            new PerimetreUtilisateurSnapshot(true, false, [], [])));
        Assert.True(PerimetreAccess.EstConfigure(
            new PerimetreUtilisateurSnapshot(false, true, [], [])));
        Assert.True(PerimetreAccess.EstConfigure(
            new PerimetreUtilisateurSnapshot(false, false, [1], [])));
        Assert.True(PerimetreAccess.EstConfigure(
            new PerimetreUtilisateurSnapshot(false, false, [], [10])));
    }

    [Fact]
    public async Task Ub_Sans_Perimetre_Utilise_Fallback_Proxy()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = null;
        repo.UbProxyPrevision.Clear();
        repo.UbProxyPrevision.Add(1);
        repo.UbAutorisees.Clear();

        Assert.True(await repo.UtilisateurPeutAccederUbAsync(1, 1));
        Assert.False(await repo.UtilisateurPeutAccederUbAsync(1, 2));
    }

    [Fact]
    public async Task Ub_Perimetre_Vide_Utilise_Fallback_Proxy()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [], []);
        repo.UbProxyPrevision.Clear();
        repo.UbProxyPrevision.Add(10);
        repo.UbAutorisees.Clear();

        Assert.True(await repo.UtilisateurPeutAccederUbAsync(1, 10));
        Assert.False(await repo.UtilisateurPeutAccederUbAsync(1, 1));
    }

    [Fact]
    public async Task Ub_Perimetre_Configure_Ignore_Proxy()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [1]);
        repo.UbProxyPrevision.Clear();
        repo.UbProxyPrevision.Add(2); // proxy autoriserait 2, périmètre non

        Assert.True(await repo.UtilisateurPeutAccederUbAsync(1, 1));
        Assert.False(await repo.UtilisateurPeutAccederUbAsync(1, 2));
    }

    [Fact]
    public async Task Circuit_Perimetre_Autorise_Creation_Sur_Ub()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        // Demandeur fake → UB 10 / département 1
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbProxyPrevision.Clear();

        var service = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

        var created = await service.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(1_200m));
        Assert.Equal(StatutDemandePaiement.Brouillon, created.Statut);
    }

    [Fact]
    public async Task Circuit_Perimetre_Refuse_Ub_Hors_Perimetre()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [2], [3]);
        repo.UbProxyPrevision.Clear();
        repo.UbAutorisees.Clear();

        var service = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(500m)));
    }

    [Fact]
    public async Task Circuit_Perimetre_Refuse_Dp_Collegue_Demandeur_Sur_Meme_Ub()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);

        var createur = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await createur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(700m));

        var collegue = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 2,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            collegue.GetByIdAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task Circuit_Sans_Perimetre_Refuse_Dp_D_Un_Autre()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Perimetre = null;
        repo.UbProxyPrevision.Clear();
        repo.UbProxyPrevision.Add(10);
        repo.UbAutorisees.Clear();
        repo.UbAutorisees.Add(10);

        var createur = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await createur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(600m));

        var autre = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 99,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur)
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            autre.GetByIdAsync(created.IdDemandePaiement));
    }
}
