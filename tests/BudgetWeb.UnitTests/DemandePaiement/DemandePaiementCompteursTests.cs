using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementCompteursTests
{
    [Fact]
    public async Task Compteurs_Vides_Retournent_Zero()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));

        Assert.Equal(0, c.Total);
        Assert.Equal(0, c.Brouillon);
        Assert.Equal(0, c.Soumise);
        Assert.Equal(0, c.EnTraitementDpm);
        Assert.Equal(0, c.ViseeBudgetairement);
    }

    [Fact]
    public async Task Compteurs_Chaque_Statut_Canonique_Dans_Bon_Bucket()
    {
        var repo = new FakeDemandePaiementRepo();
        SeedStatuts(repo, StatutDemandePaiement.Brouillon, StatutDemandePaiement.Soumise);
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));

        Assert.Equal(2, c.Total);
        Assert.Equal(1, c.Brouillon);
        Assert.Equal(1, c.Soumise);
    }

    [Fact]
    public async Task Compteurs_ReceptionneeBudgets_Dans_EnTraitementDpm()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.ReceptionneeBudgets));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));

        Assert.Equal(1, c.Total);
        Assert.Equal(1, c.EnTraitementDpm);
        Assert.Equal(0, c.Soumise);
    }

    [Fact]
    public async Task Compteurs_DateDebut_Filtre_DateEmission()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Brouillon, dateEmission: new DateOnly(2026, 1, 1)));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.Soumise, dateEmission: new DateOnly(2026, 3, 1)));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(
            DemandePaiementListScope.MesDemandes,
            DateDebut: new DateOnly(2026, 2, 1)));

        Assert.Equal(1, c.Total);
        Assert.Equal(1, c.Soumise);
    }

    [Fact]
    public async Task Compteurs_DateFin_Filtre_DateEmission()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Brouillon, dateEmission: new DateOnly(2026, 1, 1)));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.Soumise, dateEmission: new DateOnly(2026, 3, 1)));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(
            DemandePaiementListScope.MesDemandes,
            DateFin: new DateOnly(2026, 2, 1)));

        Assert.Equal(1, c.Total);
        Assert.Equal(1, c.Brouillon);
    }

    [Fact]
    public async Task Compteurs_PerimetreUb_Configure()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbDepartements[20] = 2;
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Brouillon, ub: 10));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.Soumise, ub: 20, userId: 99));
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));

        Assert.Equal(1, c.Total);
        Assert.Equal(1, c.Brouillon);
    }

    [Fact]
    public async Task Compteurs_Fallback_Createur()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = null;
        repo.UbProxyPrevision.Clear();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Brouillon, userId: 1));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.Soumise, userId: 99));
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));

        Assert.Equal(1, c.Total);
        Assert.Equal(1, c.Brouillon);
    }

    [Fact]
    public async Task Compteurs_ChargeDp_NonAdmin_MesDemandes_Inclut_Tous_Statuts()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Soumise));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.EnTraitementDpm));
        repo.Demandes.Add(MakeDemande(3, StatutDemandePaiement.Brouillon));
        repo.Demandes.Add(MakeDemande(4, StatutDemandePaiement.EnControleBudgetaire));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));

        Assert.Equal(4, c.Total);
        Assert.Equal(1, c.Soumise);
        Assert.Equal(1, c.EnTraitementDpm);
        Assert.Equal(1, c.Brouillon);
        Assert.Equal(1, c.EnControleBudgetaire);
    }

    [Fact]
    public async Task Compteurs_ChargeDp_NonAdmin_Scope_ChargeDpm_Total_ParcoursDpm()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Soumise));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.EnTraitementDpm));
        repo.Demandes.Add(MakeDemande(3, StatutDemandePaiement.Brouillon, userId: 999));
        repo.Demandes.Add(MakeDemande(4, StatutDemandePaiement.EnControleBudgetaire));
        repo.Demandes.Add(MakeDemande(5, StatutDemandePaiement.ACorriger));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.ChargeDpm));

        Assert.Equal(4, c.Total);
        Assert.Equal(1, c.Soumise);
        Assert.Equal(1, c.EnTraitementDpm);
        Assert.Equal(1, c.EnControleBudgetaire);
        Assert.Equal(1, c.ACorriger);
        Assert.Equal(0, c.Brouillon);
    }

    [Fact]
    public async Task Compteurs_Scope_ChargeDpm_Inclut_ReceptionneeBudgets_Dans_EnTraitementDpm()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Soumise));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.ReceptionneeBudgets));
        repo.Demandes.Add(MakeDemande(3, StatutDemandePaiement.Brouillon));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.ChargeDpm));

        Assert.Equal(3, c.Total);
        Assert.Equal(1, c.Soumise);
        Assert.Equal(1, c.EnTraitementDpm);
        Assert.Equal(1, c.Brouillon);
        Assert.Equal(0, c.ViseeBudgetairement);
    }

    [Fact]
    public async Task Compteurs_JuniorDc_Uniquement_Dc()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.EnControleBudgetaire, typeBudget: 1));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.EnControleBudgetaire, typeBudget: 2));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.PermissionsPourProfil(AppRoles.GestionnaireJuniorDc));

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.JuniorDc));

        Assert.Equal(1, c.Total);
        Assert.Equal(1, c.EnControleBudgetaire);
    }

    [Fact]
    public async Task Compteurs_JuniorAe_Uniquement_Ae()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.EnControleBudgetaire, typeBudget: 1));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.EnControleBudgetaire, typeBudget: 2));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.PermissionsPourProfil(AppRoles.GestionnaireJuniorAe));

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.JuniorAe));

        Assert.Equal(1, c.Total);
        Assert.Equal(1, c.EnControleBudgetaire);
    }

    [Fact]
    public async Task Compteurs_JuniorBi_Uniquement_Bi()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.EnControleBudgetaire, typeBudget: 3));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.EnControleBudgetaire, typeBudget: 1));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.PermissionsPourProfil(AppRoles.GestionnaireJuniorBi));

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.JuniorBi));

        Assert.Equal(1, c.Total);
        Assert.Equal(1, c.EnControleBudgetaire);
    }

    [Fact]
    public async Task Compteurs_AdminAll_MesDemandes_Tous_Statuts()
    {
        var repo = new FakeDemandePaiementRepo();
        SeedAllStatuts(repo);
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));

        Assert.Equal(9, c.Total);
        Assert.Equal(1, c.Brouillon);
        Assert.Equal(1, c.ViseeBudgetairement);
    }

    [Fact]
    public async Task Compteurs_AdminAll_Scope_ChargeDpm_ParcoursComplet()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Soumise));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.EnTraitementDpm));
        repo.Demandes.Add(MakeDemande(3, StatutDemandePaiement.Brouillon));
        repo.Demandes.Add(MakeDemande(4, StatutDemandePaiement.ViseeBudgetairement));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.ChargeDpm));

        Assert.Equal(4, c.Total);
        Assert.Equal(1, c.Brouillon);
        Assert.Equal(1, c.ViseeBudgetairement);
    }

    [Fact]
    public async Task Compteurs_Total_Correspond_Liste_Sans_Statut()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Brouillon));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.Soumise));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var list = await svc.ListAsync(new DemandePaiementQuery());
        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));

        Assert.Equal(list.Count, c.Total);
    }

    [Fact]
    public async Task Compteurs_Aucun_Statut_Hors_Perimetre()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Brouillon, ub: 10));
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.Soumise, ub: 20, userId: 99));
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

        var c = await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));

        Assert.Equal(1, c.Total);
        Assert.Equal(1, c.Brouillon);
        Assert.Equal(0, c.Soumise);
    }

    [Fact]
    public async Task Compteurs_Une_Seule_Operation_Repository()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Brouillon));
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        await svc.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));

        Assert.Equal(1, repo.ListScopeRowsCallCount);
    }

    private static DemandePaiementEntity MakeDemande(
        long id,
        string statut,
        long ub = 10,
        long? userId = 1,
        long? typeBudget = 1,
        DateOnly? dateEmission = null)
        => new()
        {
            IdDemandePaiement = id,
            Reference = $"DP-TEST-{id:D3}",
            Statut = statut,
            FK_UniteBudgetaire = ub,
            FK_UtilisateurCreation = userId ?? 1,
            FK_TypeBudget = typeBudget,
            FK_Demandeur = 1,
            FK_ExerciceBudgetaire = 1,
            FK_CasDossier = 1,
            MontantBrut = 100m,
            Devise = "USD",
            DateEmission = dateEmission ?? new DateOnly(2026, 3, 1),
            DateCreation = DateTime.UtcNow,
        };

    private static void SeedStatuts(FakeDemandePaiementRepo repo, params string[] statuts)
    {
        long id = 1;
        foreach (var st in statuts)
        {
            repo.Demandes.Add(MakeDemande(id++, st));
        }
    }

    private static void SeedAllStatuts(FakeDemandePaiementRepo repo)
    {
        var statuts = new[]
        {
            StatutDemandePaiement.Brouillon,
            StatutDemandePaiement.EnValidationN1,
            StatutDemandePaiement.EnValidationN2,
            StatutDemandePaiement.ValideeEntite,
            StatutDemandePaiement.Soumise,
            StatutDemandePaiement.EnTraitementDpm,
            StatutDemandePaiement.EnControleBudgetaire,
            StatutDemandePaiement.ACorriger,
            StatutDemandePaiement.ViseeBudgetairement,
        };
        SeedStatuts(repo, statuts);
    }
}
