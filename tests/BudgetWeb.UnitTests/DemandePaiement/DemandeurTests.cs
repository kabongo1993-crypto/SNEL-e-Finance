using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandeurTests
{
    [Fact]
    public async Task CreateDemandeur_RattacheUb()
    {
        var repo = new FakeDemandeurRepo();
        var demandeurCountBefore = repo.Items.Count;
        var svc = DemandePaiementTestData.CreateDemandeurService(repo);

        var created = await svc.CreateAsync(new CreateDemandeurRequest("CODE/TEST", "Test libellé", 10));

        Assert.Equal("CODE/TEST", created.Code);
        Assert.Equal(10, created.IdUB);
        Assert.Equal("UB001", created.CodeUB);
        Assert.Equal("Direction Générale", created.LibelleDepartement);
        Assert.NotNull(repo.LastAdded);
        Assert.False(repo.LastAddedHadUbNavigationAtInsert);
        Assert.Equal(10, repo.LastAdded!.FK_UniteBudgetaire);
        Assert.Equal(demandeurCountBefore + 1, repo.Items.Count);
    }

    [Fact]
    public async Task CreerDemandeurAvecUBExistante_CreeUniquementDemandeur()
    {
        var repo = new FakeDemandeurRepo();
        var demandeurCountBefore = repo.Items.Count;
        var ubCountBefore = repo.Ubs.Count;
        var svc = DemandePaiementTestData.CreateDemandeurService(repo);

        await svc.CreateAsync(new CreateDemandeurRequest("NEW/UB", "Nouveau demandeur", 10));

        Assert.Equal(ubCountBefore, repo.Ubs.Count);
        Assert.Equal(demandeurCountBefore + 1, repo.Items.Count);
        Assert.False(repo.LastAddedHadUbNavigationAtInsert);
        Assert.Equal(10, repo.LastAdded!.FK_UniteBudgetaire);
    }

    [Fact]
    public async Task CreateDemandeur_UbInexistante_Refuse()
    {
        var repo = new FakeDemandeurRepo();
        var demandeurCountBefore = repo.Items.Count;
        var svc = DemandePaiementTestData.CreateDemandeurService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateDemandeurRequest("X/Y", "Test", 999)));

        Assert.Contains("introuvable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(demandeurCountBefore, repo.Items.Count);
        Assert.Null(repo.LastAdded);
    }

    [Fact]
    public async Task CreateDemandeur_CodeUnique()
    {
        var repo = new FakeDemandeurRepo();
        var svc = DemandePaiementTestData.CreateDemandeurService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateDemandeurRequest("DDK/DKC/DG", "Dup", 10)));
    }

    [Fact]
    public async Task CreateDemandeur_Refuse_Ub_Hors_Perimetre()
    {
        var perimetreRepo = new FakeDemandePaiementRepo();
        perimetreRepo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        perimetreRepo.UbDepartements[10] = 1;
        perimetreRepo.UbDepartements[20] = 2;

        var demandeurs = new FakeDemandeurRepo();
        demandeurs.Ubs[20] = new UniteBudgetaire
        {
            IdUB = 20,
            CodeUB = "UB020",
            Libelle = "Hors périmètre",
            FK_Departement = 2,
            Actif = true,
            Departement = new Departement { IdDepartement = 2, Code = "D2", Libelle = "Dept 2", Actif = true },
        };

        var svc = DemandePaiementTestData.CreateDemandeurService(
            demandeurs,
            [AppPermissions.DemandeursEcrire],
            perimetreRepo);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(new CreateDemandeurRequest("HORS/UB", "Interdit", 20)));
    }

    [Fact]
    public async Task CreateDemandeur_AvecDemandeursEcrire_SansReferentiels_Reussit()
    {
        var repo = new FakeDemandeurRepo();
        var svc = DemandePaiementTestData.CreateDemandeurService(
            repo,
            [AppPermissions.DemandeursEcrire, AppPermissions.PaiementsLire]);

        var created = await svc.CreateAsync(new CreateDemandeurRequest("PERM/ONLY", "Avec demandeurs.ecrire", 10));

        Assert.Equal("PERM/ONLY", created.Code);
        Assert.Equal(10, created.IdUB);
    }

    [Fact]
    public async Task CreateDemandeur_SansPermission_Refuse()
    {
        var repo = new FakeDemandeurRepo();
        var svc = DemandePaiementTestData.CreateDemandeurService(
            repo,
            [AppPermissions.PaiementsLire]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(new CreateDemandeurRequest("X", "Y", 10)));
    }

    [Fact]
    public void ProfilsEntiteInitiatrice_Ont_DemandeursEcrire_Selon_Profil()
    {
        var service = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur);
        Assert.Contains(AppPermissions.DemandeursEcrire, service);
        Assert.DoesNotContain(AppPermissions.ReferentielsEcrire, service);

        foreach (var profil in new[]
                 {
                     AppRoles.ResponsableServiceDemandeur,
                     AppRoles.ResponsableEntiteInitiatrice
                 })
        {
            var perms = AppPermissions.PermissionsPourProfil(profil);
            Assert.DoesNotContain(AppPermissions.DemandeursEcrire, perms);
            Assert.DoesNotContain(AppPermissions.ReferentielsEcrire, perms);
        }

        var demandeur = AppPermissions.PermissionsPourProfil(AppRoles.Demandeur);
        Assert.Contains(AppPermissions.DemandeursEcrire, demandeur);
        Assert.DoesNotContain(AppPermissions.ReferentielsEcrire, demandeur);
    }

    [Fact]
    public void DemandeurRules_CoherenceUb()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DemandeurRules.ExigerCoherenceUbDemandeur(10, 99));
        Assert.Contains("demandeur", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListDemandeur_DeriveDepartement()
    {
        var repo = new FakeDemandeurRepo();
        var svc = DemandePaiementTestData.CreateDemandeurService(repo);

        var rows = await svc.ListAsync();

        Assert.NotEmpty(rows);
        Assert.Equal("DG", rows[0].CodeDepartement);
    }
}
