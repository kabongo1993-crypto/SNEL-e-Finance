using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Analyse / préparation Entité Initiatrice :
/// DEMANDEUR → SERVICE_DEMANDEUR ; RESPONSABLE_SERVICE / RESPONSABLE_ENTITE.
/// </summary>
public class EntiteInitiatricePreparationTests
{
    [Fact]
    public void ServiceDemandeur_Equivaut_Demandeur_Historique()
    {
        var historique = AppPermissions.PermissionsPourProfil(AppRoles.Demandeur);
        var cible = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur);

        Assert.Equal(
            historique.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            cible.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        Assert.Contains(AppPermissions.PaiementsLire, cible);
        Assert.Contains(AppPermissions.PaiementsEcrire, cible);
        Assert.Contains(AppPermissions.PaiementsSoumettre, cible);
    }

    [Fact]
    public void Responsables_Ont_Permissions_Validation_Distinctes()
    {
        var service = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur);
        var respService = AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur);
        var respEntite = AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice);

        Assert.Contains(AppPermissions.PaiementsEnvoyerValidation, service);
        Assert.Contains(AppPermissions.PaiementsValiderN1, respService);
        Assert.Contains(AppPermissions.PaiementsValiderN2, respEntite);
        Assert.DoesNotContain(AppPermissions.PaiementsEcrire, respService);
        Assert.DoesNotContain(AppPermissions.PaiementsEcrire, respEntite);
    }

    [Fact]
    public void EntiteInitiatrice_Exclut_Circuit_Direction_Budgets()
    {
        foreach (var profil in new[]
                 {
                     AppRoles.ServiceDemandeur,
                     AppRoles.ResponsableServiceDemandeur,
                     AppRoles.ResponsableEntiteInitiatrice,
                     AppRoles.Demandeur
                 })
        {
            var perms = AppPermissions.PermissionsPourProfil(profil);
            foreach (var exclus in AppPermissions.EntiteInitiatriceExclusions)
                Assert.DoesNotContain(exclus, perms);
        }
    }

    [Fact]
    public void MapUser_ServiceDemandeur_Et_Historique()
    {
        var cible = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 41,
                NomUtilisateur = "svc.dem",
                Nom = "Svc",
                Matricule = "SD-1",
                Actif = true
            },
            [AppRoles.ServiceDemandeur]);

        Assert.Contains(AppRoles.ServiceDemandeur, cible.Roles);
        Assert.Contains(AppPermissions.PaiementsSoumettre, cible.Permissions);
        Assert.DoesNotContain(AppRoles.Demandeur, cible.Roles);

        var hist = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 42,
                NomUtilisateur = "dem.hist",
                Nom = "Dem",
                Matricule = "D-H",
                Actif = true
            },
            [AppRoles.Demandeur]);

        Assert.Contains(AppRoles.Demandeur, hist.Roles);
        Assert.Contains(AppPermissions.PaiementsEcrire, hist.Permissions);
    }

    [Fact]
    public void MapUser_Responsables_Injectent_Roles_Distincts()
    {
        var dto = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 43,
                NomUtilisateur = "resp.ent",
                Nom = "Resp",
                Matricule = "RE-1",
                Actif = true
            },
            [AppRoles.ResponsableServiceDemandeur, AppRoles.ResponsableEntiteInitiatrice]);

        Assert.Contains(AppRoles.ResponsableServiceDemandeur, dto.Roles);
        Assert.Contains(AppRoles.ResponsableEntiteInitiatrice, dto.Roles);
        Assert.Contains(AppPermissions.PaiementsValiderN2, dto.Permissions);
        Assert.DoesNotContain(AppPermissions.PaiementsEcrire, dto.Permissions);
    }

    [Fact]
    public void Helpers_Profil()
    {
        Assert.True(ProfilUtilisateurCodes.EstServiceDemandeur(AppRoles.ServiceDemandeur));
        Assert.True(ProfilUtilisateurCodes.EstServiceDemandeur(AppRoles.Demandeur));
        Assert.False(ProfilUtilisateurCodes.EstServiceDemandeur(AppRoles.ResponsableServiceDemandeur));

        Assert.True(ProfilUtilisateurCodes.EstEntiteInitiatrice(AppRoles.ServiceDemandeur));
        Assert.True(ProfilUtilisateurCodes.EstEntiteInitiatrice(AppRoles.ResponsableEntiteInitiatrice));
        Assert.False(ProfilUtilisateurCodes.EstEntiteInitiatrice(AppRoles.ChargeDp));

        Assert.True(ProfilUtilisateurCodes.EstResponsableEntiteInitiatrice(AppRoles.ResponsableServiceDemandeur));
        Assert.True(ProfilUtilisateurCodes.EstResponsableEntiteInitiatrice(AppRoles.ResponsableEntiteInitiatrice));
        Assert.False(ProfilUtilisateurCodes.EstResponsableEntiteInitiatrice(AppRoles.ServiceDemandeur));

        Assert.True(ProfilUtilisateurCodes.IsValid(AppRoles.ServiceDemandeur));
        Assert.True(ProfilUtilisateurCodes.IsValid(AppRoles.ResponsableEntiteInitiatrice));
    }

    [Fact]
    public async Task Circuit_ServiceDemandeur_Cree_Et_Soumet()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();

        var service = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

        var created = await service.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(2_500m));
        await DemandePaiementTestData.AddSamplePieceAsync(service, created.IdDemandePaiement);
        var soumise = await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, service);

        Assert.Equal(StatutDemandePaiement.Soumise, soumise.Statut);
    }

    [Fact]
    public async Task Circuit_ResponsableEntite_Valide_N2()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();

        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var resp2 = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice));

        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(1_500m));
        await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
        var soumise = await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, agent);

        Assert.Equal(StatutDemandePaiement.Soumise, soumise.Statut);
        Assert.Contains(AppPermissions.PaiementsValiderN2, AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice));
    }

    [Fact]
    public async Task Circuit_ServiceDemandeur_Ne_Peut_Pas_Receptionner_Ni_Viser()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();

        var demandeur = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(900m));
        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, demandeur);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            demandeur.ReceptionnerAsync(created.IdDemandePaiement));

        await charge.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);
        await charge.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await charge.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            demandeur.ViserAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task Circuit_Demandeur_Historique_Toujours_Valide()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();

        var hist = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.Demandeur));

        var created = await hist.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(800m));
        await DemandePaiementTestData.AddSamplePieceAsync(hist, created.IdDemandePaiement);
        var soumise = await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, hist);

        Assert.Equal(StatutDemandePaiement.Soumise, soumise.Statut);
    }
}
