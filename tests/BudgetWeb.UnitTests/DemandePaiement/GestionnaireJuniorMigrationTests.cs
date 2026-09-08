using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Migration progressive GESTIONNAIRE_JUNIOR_DC|AE|BI → GESTIONNAIRE_JUNIOR + imputer_*.
/// </summary>
public class GestionnaireJuniorMigrationTests
{
    [Theory]
    [InlineData(AppRoles.GestionnaireJuniorDc, AppPermissions.PaiementsImputerDc)]
    [InlineData(AppRoles.GestionnaireJuniorAe, AppPermissions.PaiementsImputerAe)]
    [InlineData(AppRoles.GestionnaireJuniorBi, AppPermissions.PaiementsImputerBi)]
    public void Permissions_Effectives_Apres_Migration_Equivalentes(
        string profilHistorique,
        string permissionImputer)
    {
        var historiques = AppPermissions.PermissionsPourProfil(profilHistorique);
        var migrees = EffectivePermissions.Compute(
            [AppRoles.GestionnaireJunior],
            [permissionImputer]);

        Assert.Equal(
            historiques.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            migrees.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void GestionnaireJunior_Sans_Imputer_Ne_Donne_Pas_Filiere()
    {
        var perms = AppPermissions.PermissionsPourProfil(AppRoles.GestionnaireJunior);
        Assert.Contains(AppPermissions.PaiementsLire, perms);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, perms);
        Assert.DoesNotContain(AppPermissions.PaiementsImputerDc, perms);
        Assert.DoesNotContain(AppPermissions.PaiementsImputerAe, perms);
        Assert.DoesNotContain(AppPermissions.PaiementsImputerBi, perms);
    }

    [Fact]
    public void MapUser_Junior_Unifie_Plus_ImputerDc()
    {
        var dto = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 11,
                NomUtilisateur = "junior.dc",
                Nom = "Junior",
                Matricule = "J-DC",
                Actif = true
            },
            [AppRoles.GestionnaireJunior],
            [AppPermissions.PaiementsImputerDc]);

        Assert.Contains(AppRoles.GestionnaireJunior, dto.Roles);
        Assert.Contains(AppPermissions.PaiementsImputerDc, dto.Permissions);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, dto.Permissions);
        Assert.DoesNotContain(AppRoles.GestionnaireJuniorDc, dto.Roles);
    }

    [Fact]
    public void MapUser_Profils_Historiques_Toujours_Valides()
    {
        var dc = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 12,
                NomUtilisateur = "j.dc",
                Nom = "J",
                Matricule = "H1",
                Actif = true
            },
            [AppRoles.GestionnaireJuniorDc]);

        Assert.Contains(AppPermissions.PaiementsImputerDc, dc.Permissions);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, dc.Permissions);
    }

    [Fact]
    public void Multi_Filieres_Via_Permissions_Individuelles()
    {
        var dto = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 13,
                NomUtilisateur = "j.multi",
                Nom = "J",
                Matricule = "JM",
                Actif = true
            },
            [AppRoles.GestionnaireJunior],
            [
                AppPermissions.PaiementsImputerDc,
                AppPermissions.PaiementsImputerAe,
                AppPermissions.PaiementsImputerBi
            ]);

        Assert.Contains(AppPermissions.PaiementsImputerDc, dto.Permissions);
        Assert.Contains(AppPermissions.PaiementsImputerAe, dto.Permissions);
        Assert.Contains(AppPermissions.PaiementsImputerBi, dto.Permissions);
    }

    [Fact]
    public void ProfilUtilisateurCodes_Helpers_Junior()
    {
        Assert.True(ProfilUtilisateurCodes.EstGestionnaireJunior(AppRoles.GestionnaireJunior));
        Assert.True(ProfilUtilisateurCodes.EstGestionnaireJunior(AppRoles.GestionnaireJuniorDc));
        Assert.Equal(
            AppPermissions.PaiementsImputerAe,
            ProfilUtilisateurCodes.PermissionImputerPourProfilJuniorHistorique(AppRoles.GestionnaireJuniorAe));
        Assert.Null(ProfilUtilisateurCodes.PermissionImputerPourProfilJuniorHistorique(AppRoles.GestionnaireJunior));
    }

    [Fact]
    public async Task Circuit_Junior_Unifie_Impute_Dc()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);

        var demandeur = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire, AppPermissions.PaiementsEcrire, AppPermissions.PaiementsSoumettre]);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);
        var junior = DemandePaiementTestData.CreateService(
            repo,
            EffectivePermissions.Compute(
                [AppRoles.GestionnaireJunior],
                [AppPermissions.PaiementsImputerDc]));

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(10_000m));
        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, demandeur);
        await charge.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);
        await charge.TraiterChargeAsync(
            created.IdDemandePaiement,
            new Application.DTOs.TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await charge.OrienterAsync(created.IdDemandePaiement, new Application.DTOs.OrienterDemandePaiementRequest());

        var detail = await junior.GetByIdAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.EnControleBudgetaire, detail!.Statut);

        var imp = await junior.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc(montantUsd: 10_000m, idBudgetLigne: 100));
        Assert.Equal(TypeBudgetCode.DepensesCourantes, imp.CodeTypeBudget);
    }

    [Fact]
    public async Task Circuit_Junior_Dc_Historique_Toujours_Fonctionnel()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);

        var demandeur = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire, AppPermissions.PaiementsEcrire, AppPermissions.PaiementsSoumettre]);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);
        var junior = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.GestionnaireJuniorDc));

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(8_000m));
        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, demandeur);
        await charge.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);
        await charge.TraiterChargeAsync(
            created.IdDemandePaiement,
            new Application.DTOs.TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await charge.OrienterAsync(created.IdDemandePaiement, new Application.DTOs.OrienterDemandePaiementRequest());

        var imp = await junior.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc(montantUsd: 8_000m, idBudgetLigne: 100));
        Assert.Equal(TypeBudgetCode.DepensesCourantes, imp.CodeTypeBudget);
    }
}
