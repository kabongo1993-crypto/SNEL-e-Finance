using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementRetourZYTests
{
    [Fact]
    public async Task Z_Rejet_Retourne_Y1_En_Traitement()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);

        var id = repo.Demandes.Single().IdDemandePaiement;
        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.RetournerAsync(id, new RetourDemandePaiementRequest("Correction imputation", null));

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, tracked.FK_UtilisateurAssigne);
        Assert.NotEqual(StatutDemandePaiement.ACorriger, tracked.Statut);

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => y2.GetByIdAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            y2.RetournerAsync(id, new RetourDemandePaiementRequest("Tentative", null)));

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        var detail = await y1.GetByIdAsync(id);
        Assert.NotNull(detail);

        await y1.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1));
        tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(StatutDemandePaiement.EnControleBudgetaire, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, tracked.FK_UtilisateurAssigne);
    }

    [Fact]
    public async Task Y1_Rejet_Retourne_X_ACorriger()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);

        await y1.RetournerAsync(id, new RetourDemandePaiementRequest("Correction demandeur", null));

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(StatutDemandePaiement.ACorriger, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.X1, tracked.FK_UtilisateurAssigne);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        Assert.NotNull(await x1.GetByIdAsync(id));

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => y2.GetByIdAsync(id));
    }

    [Fact]
    public async Task Prise_En_Charge_Directe_Sans_Orientation_Utilise_Amont_Reel()
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

        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.PrendreEnControleAsync(id);

        tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, tracked.FK_UtilisateurAssigne);

        var controler = repo.Routages.Single(r =>
            r.FK_DemandePaiement == id
            && r.Action == DemandePaiementRoutageAction.Controler);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, controler.FK_UtilisateurSource);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, controler.FK_UtilisateurCible);
        Assert.DoesNotContain(
            repo.Routages,
            r => r.Action == DemandePaiementRoutageAction.Orienter);
    }
}
