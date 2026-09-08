using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Analyse / préparation DIRECTEUR_BUDGETS — autorité métier sans admin technique.
/// </summary>
public class DirecteurBudgetsPreparationTests
{
    [Fact]
    public void Bundle_Contient_Visa_Et_Versions_Sans_Admin()
    {
        var perms = AppPermissions.PermissionsPourProfil(AppRoles.DirecteurBudgets);

        Assert.Contains(AppPermissions.PaiementsLire, perms);
        Assert.Contains(AppPermissions.PaiementsViserBudget, perms);
        Assert.Contains(AppPermissions.VersionsControler, perms);
        Assert.Contains(AppPermissions.VersionsValider, perms);
        Assert.Contains(AppPermissions.VersionsRejeter, perms);
        Assert.Contains(AppPermissions.AjustementsLire, perms);

        Assert.DoesNotContain(AppPermissions.AdminAll, perms);
        Assert.DoesNotContain(AppPermissions.AdminUtilisateurs, perms);
        Assert.DoesNotContain(AppPermissions.ReferentielsEcrire, perms);
    }

    [Fact]
    public void Bundle_Exclut_Operations_Charge_Junior_Senior_Dg()
    {
        var perms = AppPermissions.PermissionsPourProfil(AppRoles.DirecteurBudgets);

        foreach (var exclus in AppPermissions.DirecteurBudgetsExclusions)
            Assert.DoesNotContain(exclus, perms);

        Assert.DoesNotContain(AppPermissions.PaiementsControlerBudget, perms);
        Assert.DoesNotContain(AppPermissions.PaiementsChargeDpm, perms);
        Assert.DoesNotContain(AppPermissions.PaiementsImputerDc, perms);
        Assert.DoesNotContain(AppPermissions.AjustementsValider, perms);
        Assert.DoesNotContain(AppPermissions.PrevisionsEcrire, perms);
    }

    [Fact]
    public void Distinct_De_ChefDivision_Et_Senior()
    {
        var directeur = AppPermissions.PermissionsPourProfil(AppRoles.DirecteurBudgets);
        var chef = AppPermissions.PermissionsPourProfil(AppRoles.ChefDivision);
        var senior = AppPermissions.PermissionsPourProfil(AppRoles.GestionnaireSenior);

        Assert.Contains(AppPermissions.VersionsValider, directeur);
        Assert.DoesNotContain(AppPermissions.VersionsValider, chef);
        Assert.DoesNotContain(AppPermissions.VersionsValider, senior);

        Assert.Contains(AppPermissions.PaiementsViserBudget, directeur);
        Assert.Contains(AppPermissions.PaiementsViserBudget, chef);
        Assert.DoesNotContain(AppPermissions.PaiementsViserBudget, senior);

        Assert.DoesNotContain(AppPermissions.PaiementsControlerBudget, directeur);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, senior);
    }

    [Fact]
    public void Distinct_De_Admin_Et_Dg()
    {
        var directeur = AppPermissions.PermissionsPourProfil(AppRoles.DirecteurBudgets);
        var admin = AppPermissions.PermissionsPourProfil(AppRoles.AdministrateurSysteme);

        Assert.DoesNotContain(AppPermissions.AdminAll, directeur);
        Assert.Contains(AppPermissions.AdminAll, admin);

        Assert.DoesNotContain(AppPermissions.AjustementsEcrire, directeur);
        Assert.Contains(AppPermissions.AjustementsEcrire, AppPermissions.Dg);
        Assert.Contains(AppPermissions.AjustementsValider, AppPermissions.Dg);
    }

    [Fact]
    public void MapUser_Injecte_Role_Et_Permissions()
    {
        var dto = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 31,
                NomUtilisateur = "dir.budgets",
                Nom = "Directeur",
                Matricule = "DIR-1",
                Actif = true
            },
            [AppRoles.DirecteurBudgets]);

        Assert.Contains(AppRoles.DirecteurBudgets, dto.Roles);
        Assert.Contains(AppPermissions.PaiementsViserBudget, dto.Permissions);
        Assert.Contains(AppPermissions.VersionsValider, dto.Permissions);
        Assert.DoesNotContain(AppPermissions.AdminAll, dto.Permissions);
        Assert.DoesNotContain(AppRoles.UserAdminFull, dto.Roles);
    }

    [Fact]
    public void Helpers_Profil()
    {
        Assert.True(ProfilUtilisateurCodes.EstDirecteurBudgets(AppRoles.DirecteurBudgets));
        Assert.True(ProfilUtilisateurCodes.EstDirecteurBudgets("directeur_budgets"));
        Assert.False(ProfilUtilisateurCodes.EstDirecteurBudgets(AppRoles.ChefDivision));
        Assert.True(ProfilUtilisateurCodes.EstControleOuVisaBudget(AppRoles.DirecteurBudgets));
        Assert.True(ProfilUtilisateurCodes.IsValid(AppRoles.DirecteurBudgets));
    }

    [Fact]
    public async Task Circuit_Directeur_Peut_Viser_Sans_Imputer()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await admin.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(4_000m));
        await DemandePaiementTestData.AddSamplePieceAsync(admin, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, admin);
        await admin.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, admin, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(admin, created.IdDemandePaiement);
        await admin.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await admin.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());
        await admin.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc(montantUsd: 4_000m, idBudgetLigne: 100));

        var directeur = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.DirecteurBudgets));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            directeur.AddImputationAsync(
                created.IdDemandePaiement,
                DemandePaiementTestData.ImputationDc(montantUsd: 1m, idBudgetLigne: 100)));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            directeur.ControlerBudgetaireAsync(created.IdDemandePaiement));

        var visee = await directeur.ViserAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.ViseeBudgetairement, visee.Statut);
    }

    [Fact]
    public async Task Circuit_Directeur_Ne_Peut_Pas_Receptionner_Comme_Charge()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();

        var demandeur = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire, AppPermissions.PaiementsEcrire, AppPermissions.PaiementsSoumettre]);
        var directeur = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.DirecteurBudgets));

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(1_000m));
        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, demandeur);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            directeur.ReceptionnerAsync(created.IdDemandePaiement));
    }
}
