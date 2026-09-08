using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Migration progressive CHARGE_DPM → CHARGE_DP :
/// le profil cible doit pouvoir exécuter le circuit Chargé sans dépendre du code historique.
/// </summary>
public class ChargeDpMigrationTests
{
    [Fact]
    public void PermissionsPourProfil_ChargeDp_Identiques_A_ChargeDpm()
    {
        var historique = AppPermissions.PermissionsPourProfil(AppRoles.ChargeDpm);
        var cible = AppPermissions.PermissionsPourProfil(AppRoles.ChargeDp);

        Assert.Equal(historique.OrderBy(x => x), cible.OrderBy(x => x));
        Assert.Contains(AppPermissions.PaiementsChargeDpm, cible);
        Assert.Contains(AppPermissions.PaiementsReceptionBudget, cible);
        Assert.Contains(AppPermissions.PaiementsEcrire, cible);
        Assert.DoesNotContain(AppPermissions.PaiementsReprendreEntite, cible);
    }

    [Fact]
    public void MapUser_Profil_ChargeDp_Injecte_Permissions_Charge()
    {
        var dto = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 7,
                NomUtilisateur = "charge.dp",
                Nom = "Charge",
                Matricule = "M-DP",
                Actif = true
            },
            [AppRoles.ChargeDp]);

        Assert.Contains(AppRoles.ChargeDp, dto.Roles);
        Assert.DoesNotContain(AppRoles.ChargeDpm, dto.Roles);
        Assert.Contains(AppPermissions.PaiementsChargeDpm, dto.Permissions);
        Assert.Contains(AppPermissions.PaiementsChargeDp, dto.Permissions);
    }

    [Fact]
    public void MapUser_Profil_ChargeDpm_Historique_Toujours_Valide()
    {
        var dto = AuthService.MapUser(
            new Domain.Entities.Utilisateur
            {
                IdUtilisateur = 8,
                NomUtilisateur = "charge.dpm",
                Nom = "Charge",
                Matricule = "M-DPM",
                Actif = true
            },
            [AppRoles.ChargeDpm]);

        Assert.Contains(AppRoles.ChargeDpm, dto.Roles);
        Assert.Contains(AppPermissions.PaiementsChargeDpm, dto.Permissions);
    }

    [Fact]
    public void ProfilUtilisateurCodes_EstChargeDp_Accepte_Les_Deux()
    {
        Assert.True(ProfilUtilisateurCodes.EstChargeDp(AppRoles.ChargeDp));
        Assert.True(ProfilUtilisateurCodes.EstChargeDp(AppRoles.ChargeDpm));
        Assert.True(ProfilUtilisateurCodes.IsValid(AppRoles.ChargeDp));
        Assert.False(ProfilUtilisateurCodes.EstChargeDp(AppRoles.Demandeur));
    }

    [Fact]
    public async Task Circuit_ChargeDp_Receptionne_Et_Traite()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();

        var demandeur = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire, AppPermissions.PaiementsEcrire, AppPermissions.PaiementsSoumettre]);

        // Permissions héritées du profil CHARGE_DP (pas du code historique CHARGE_DPM).
        var charge = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ChargeDp));

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(5_000m));
        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, demandeur);

        var receptionnee = await charge.ReceptionnerAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, receptionnee.Statut);

        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);
        var traite = await charge.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));

        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, traite.Statut);
        Assert.Equal("CDF", traite.DevisePaiement);
        Assert.NotNull(traite.MontantPaiement);
    }

    [Fact]
    public async Task Circuit_ChargeDpm_Historique_Toujours_Fonctionnel()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();

        var demandeur = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire, AppPermissions.PaiementsEcrire, AppPermissions.PaiementsSoumettre]);
        var charge = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ChargeDpm));

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(1_000m));
        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, demandeur);

        var receptionnee = await charge.ReceptionnerAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, receptionnee.Statut);
    }
}
