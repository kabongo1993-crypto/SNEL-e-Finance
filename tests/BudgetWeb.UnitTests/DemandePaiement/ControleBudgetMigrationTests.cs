using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Analyse / préparation CONTROLE_BUDGET → GESTIONNAIRE_SENIOR + CHEF_DIVISION.
/// </summary>
public class ControleBudgetMigrationTests
{
    [Fact]
    public void ControleBudget_Couvre_Controler_Et_Viser()
    {
        var perms = AppPermissions.PermissionsPourProfil(AppRoles.ControleBudget);
        Assert.Contains(AppPermissions.PaiementsLire, perms);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, perms);
        Assert.Contains(AppPermissions.PaiementsViserBudget, perms);
        Assert.DoesNotContain(AppPermissions.PaiementsImputerDc, perms);
    }

    [Fact]
    public void Senior_A_Controler_Sans_Viser()
    {
        var perms = AppPermissions.PermissionsPourProfil(AppRoles.GestionnaireSenior);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, perms);
        Assert.DoesNotContain(AppPermissions.PaiementsViserBudget, perms);
    }

    [Fact]
    public void ChefDivision_A_Viser_Sans_Controler()
    {
        var perms = AppPermissions.PermissionsPourProfil(AppRoles.ChefDivision);
        Assert.Contains(AppPermissions.PaiementsViserBudget, perms);
        Assert.DoesNotContain(AppPermissions.PaiementsControlerBudget, perms);
    }

    [Fact]
    public void Scission_Senior_Plus_Chef_Equivaut_ControleBudget()
    {
        var historique = AppPermissions.PermissionsPourProfil(AppRoles.ControleBudget);
        var scission = AppPermissions.PermissionsEffectivesApresScissionControleBudget();

        Assert.Equal(
            historique.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            scission.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void MapUser_Scission_Injecte_Les_Deux_Roles()
    {
        var dto = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 21,
                NomUtilisateur = "ctrl.split",
                Nom = "Ctrl",
                Matricule = "CB-1",
                Actif = true
            },
            [AppRoles.GestionnaireSenior, AppRoles.ChefDivision]);

        Assert.Contains(AppRoles.GestionnaireSenior, dto.Roles);
        Assert.Contains(AppRoles.ChefDivision, dto.Roles);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, dto.Permissions);
        Assert.Contains(AppPermissions.PaiementsViserBudget, dto.Permissions);
        Assert.DoesNotContain(AppRoles.ControleBudget, dto.Roles);
    }

    [Fact]
    public void MapUser_ControleBudget_Historique_Toujours_Valide()
    {
        var dto = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 22,
                NomUtilisateur = "ctrl.hist",
                Nom = "Ctrl",
                Matricule = "CB-H",
                Actif = true
            },
            [AppRoles.ControleBudget]);

        Assert.Contains(AppRoles.ControleBudget, dto.Roles);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, dto.Permissions);
        Assert.Contains(AppPermissions.PaiementsViserBudget, dto.Permissions);
    }

    [Fact]
    public void ProfilUtilisateurCodes_Helpers()
    {
        Assert.True(ProfilUtilisateurCodes.EstControleOuVisaBudget(AppRoles.ControleBudget));
        Assert.True(ProfilUtilisateurCodes.EstControleOuVisaBudget(AppRoles.GestionnaireSenior));
        Assert.True(ProfilUtilisateurCodes.EstControleOuVisaBudget(AppRoles.ChefDivision));
        Assert.True(ProfilUtilisateurCodes.EstControleOuVisaBudget(AppRoles.DirecteurBudgets));
        Assert.False(ProfilUtilisateurCodes.EstControleOuVisaBudget(AppRoles.ChargeDp));
        Assert.True(ProfilUtilisateurCodes.IsValid(AppRoles.GestionnaireSenior));
        Assert.True(ProfilUtilisateurCodes.IsValid(AppRoles.ChefDivision));
    }

    [Fact]
    public async Task Circuit_Senior_Ne_Peut_Pas_Viser()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);

        var demandeur = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire, AppPermissions.PaiementsEcrire, AppPermissions.PaiementsSoumettre]);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);
        var junior = DemandePaiementTestData.CreateService(repo, AppPermissions.JuniorDc);
        var senior = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.GestionnaireSenior));

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(5_000m));
        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, demandeur);
        await charge.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);
        await charge.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await charge.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());
        await junior.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc(montantUsd: 5_000m, idBudgetLigne: 100));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            senior.ViserAsync(created.IdDemandePaiement));

        // Le senior peut toujours lancer le contrôle budgétaire.
        var controle = await senior.ControlerBudgetaireAsync(created.IdDemandePaiement);
        Assert.NotNull(controle);
    }

    [Fact]
    public async Task Circuit_ChefDivision_Peut_Viser_Apres_Imputation()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await admin.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(5_000m));
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
            DemandePaiementTestData.ImputationDc(montantUsd: 5_000m, idBudgetLigne: 100));

        var chef = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ChefDivision));

        var visee = await chef.ViserAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.ViseeBudgetairement, visee.Statut);
    }

    [Fact]
    public async Task Circuit_ControleBudget_Historique_Peut_Viser()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await admin.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(3_000m));
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
            DemandePaiementTestData.ImputationDc(montantUsd: 3_000m, idBudgetLigne: 100));

        var controleur = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ControleBudget));

        var visee = await controleur.ViserAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.ViseeBudgetairement, visee.Statut);
    }
}
