using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Lot 3.6.6 — invariants assignation active = FK_UtilisateurCible du routage actif.</summary>
public class DemandePaiementLot366RoutageInvariantTests
{
    [Fact]
    public async Task Y_Vers_Z_Orienter_Assignation_Egale_Cible_Routage()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, y1, id);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(y1, id);
        await y1.TraiterChargeAsync(
            id,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await y1.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1));

        var actif = DemandePaiementRoutageTestHelpers.RoutageActif(repo, id);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);

        Assert.NotNull(actif);
        Assert.Equal(DemandePaiementRoutageAction.Orienter, actif!.Action);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, actif.FK_UtilisateurSource);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, actif.FK_UtilisateurCible);
        Assert.Equal(actif.FK_UtilisateurCible, tracked.FK_UtilisateurAssigne);
    }

    [Fact]
    public async Task Z_Vers_Y_Retour_Assignation_Egale_Cible_Routage()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.RetournerAsync(id, new RetourDemandePaiementRequest("Correction", null));

        var actif = DemandePaiementRoutageTestHelpers.RoutageActif(repo, id);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);

        Assert.NotNull(actif);
        Assert.Equal(DemandePaiementRoutageAction.RetourInterEtapes, actif!.Action);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, actif.FK_UtilisateurCible);
        Assert.Equal(actif.FK_UtilisateurCible, tracked.FK_UtilisateurAssigne);
    }

    [Fact]
    public async Task V_Vers_Z_RetourVisa_Assignation_Egale_Cible_Routage()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.AddImputationAsync(id, DemandePaiementTestData.ImputationDc(montantUsd: 5_000m, idBudgetLigne: 100));

        var v = DemandePaiementRoutageTestHelpers.Viseur(repo);
        await v.RetournerAsync(id, new RetourDemandePaiementRequest("Rejet visa", null));

        var actif = DemandePaiementRoutageTestHelpers.RoutageActif(repo, id);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);

        Assert.NotNull(actif);
        Assert.Equal(DemandePaiementRoutageAction.RetourVisa, actif!.Action);
        Assert.Equal(StatutDemandePaiement.EnControleBudgetaire, actif.StatutSource);
        Assert.Equal(StatutDemandePaiement.EnControleBudgetaire, actif.StatutCible);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, actif.FK_UtilisateurCible);
        Assert.Equal(actif.FK_UtilisateurCible, tracked.FK_UtilisateurAssigne);
    }

    [Fact]
    public async Task N2_Vers_N1_Rejet_Assignation_Egale_Cible_Routage()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1Svc = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var n2Svc = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N2,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await n1Svc.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);

        var id = created.IdDemandePaiement;
        await n2Svc.RejeterValidationEntiteAsync(id, new RetourDemandePaiementRequest("Correction", null));

        var actif = DemandePaiementRoutageTestHelpers.RoutageActif(repo, id);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);

        Assert.NotNull(actif);
        Assert.Equal(DemandePaiementRoutageAction.RejetValidationEntite, actif!.Action);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, actif.StatutSource);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, actif.StatutCible);
        Assert.Equal(DemandePaiementRoutageTestHelpers.N1, actif.FK_UtilisateurCible);
        Assert.Equal(actif.FK_UtilisateurCible, tracked.FK_UtilisateurAssigne);
    }

    [Fact]
    public async Task N1_Vers_X_Rejet_Assignation_Egale_Cible_Routage()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1Svc = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement).FK_UtilisateurCreation =
            DemandePaiementRoutageTestHelpers.X1;
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var id = created.IdDemandePaiement;
        await n1Svc.RejeterValidationEntiteAsync(id, new RetourDemandePaiementRequest("Correction", null));

        var actif = DemandePaiementRoutageTestHelpers.RoutageActif(repo, id);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);

        Assert.NotNull(actif);
        Assert.Equal(DemandePaiementRoutageAction.RejetValidationEntite, actif!.Action);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, actif.StatutSource);
        Assert.Equal(StatutDemandePaiement.ACorriger, actif.StatutCible);
        Assert.Equal(DemandePaiementRoutageTestHelpers.X1, actif.FK_UtilisateurCible);
        Assert.Equal(actif.FK_UtilisateurCible, tracked.FK_UtilisateurAssigne);
    }

    [Fact]
    public async Task Pool_Soumise_Assignation_Nulle_Sans_Routage_Actif()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Null(tracked.FK_UtilisateurAssigne);
        Assert.DoesNotContain(repo.Routages, r => r.FK_DemandePaiement == id && r.EstActif);

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        var detail = await y1.GetByIdAsync(id);
        Assert.NotNull(detail);
        Assert.Null(detail!.IdUtilisateurAssigne);
        Assert.Null(detail.NomUtilisateurAssigne);
    }
}
