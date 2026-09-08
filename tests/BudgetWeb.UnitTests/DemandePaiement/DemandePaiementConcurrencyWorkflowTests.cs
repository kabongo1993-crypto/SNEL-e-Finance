using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Concurrence workflow DPM — Lot 3.4.</summary>
public class DemandePaiementConcurrencyWorkflowTests
{
    [Fact]
    public async Task Double_Receptionner_Second_Appel_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);

        await y1.ReceptionnerAsync(id);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => y1.ReceptionnerAsync(id));
        Assert.Contains("accès", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, DemandePaiementRoutageTestHelpers.Tracked(repo, id).Statut);
    }

    [Fact]
    public async Task Double_Orienter_Second_Appel_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, y1, id);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(y1, id);
        await y1.TraiterChargeAsync(
            id,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));

        await y1.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            y1.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1)));
    }

    [Fact]
    public async Task Double_PrendreEnControle_Second_Appel_Refuse()
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

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        tracked.FK_TypeBudget = 1;
        tracked.TypeBudget = repo.TypesBudget[1];

        var z1 = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z1.PrendreEnControleAsync(id);

        await Assert.ThrowsAsync<DemandePaiementConcurrencyException>(() => z1.PrendreEnControleAsync(id));
    }

    [Fact]
    public async Task Double_Retourner_Second_Appel_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);

        await y1.RetournerAsync(id, new RetourDemandePaiementRequest("Premier retour", null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            y1.RetournerAsync(id, new RetourDemandePaiementRequest("Second retour", null)));
    }

    [Fact]
    public async Task Double_RejetValidationEntite_Second_Appel_Conflit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1 = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var n2 = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N2,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await n1.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);

        await n2.RejeterValidationEntiteAsync(
            created.IdDemandePaiement,
            new RetourDemandePaiementRequest("Premier rejet", null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            n2.RejeterValidationEntiteAsync(
                created.IdDemandePaiement,
                new RetourDemandePaiementRequest("Second rejet", null)));
    }

    [Fact]
    public async Task Viser_Apres_Visa_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);
        DemandePaiementConcurrencyTestsHelper.MettreEnControle(repo, created.IdDemandePaiement, idTypeBudget: 1);
        await svc.AddImputationAsync(created.IdDemandePaiement, DemandePaiementTestData.ImputationDc());

        await svc.ViserAsync(created.IdDemandePaiement);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ViserAsync(created.IdDemandePaiement));
    }
}

/// <summary>Helper partagé avec DemandePaiementConcurrencyTests.</summary>
internal static class DemandePaiementConcurrencyTestsHelper
{
    internal static void MettreEnControle(FakeDemandePaiementRepo repo, long id, long idTypeBudget)
    {
        var d = repo.Demandes.Single(x => x.IdDemandePaiement == id);
        d.Statut = StatutDemandePaiement.EnControleBudgetaire;
        d.FK_TypeBudget = idTypeBudget;
        d.TypeBudget = repo.TypesBudget[idTypeBudget];
        d.FK_VersionBudgetaire = 4;
        d.MontantUsd = d.MontantBrut;
        d.TauxConversion = 1m;
    }
}
