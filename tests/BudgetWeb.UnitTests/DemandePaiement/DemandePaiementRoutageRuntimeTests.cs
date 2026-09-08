using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementRoutageRuntimeTests
{
    [Fact]
    public async Task Receptionner_Cree_Routage_Actif_Et_Assignation()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);

        var routage = DemandePaiementRoutageTestHelpers.RoutageActif(repo, id);
        Assert.NotNull(routage);
        Assert.Equal(DemandePaiementRoutageAction.Receptionner, routage!.Action);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, routage.FK_UtilisateurCible);
        Assert.True(routage.EstActif);

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, tracked.FK_UtilisateurAssigne);
    }

    [Fact]
    public async Task Orienter_Desactive_Ancien_Routage_Et_Cree_Nouveau()
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

        var actifs = repo.Routages.Count(r => r.FK_DemandePaiement == id && r.EstActif);
        Assert.Equal(1, actifs);

        var orienter = repo.Routages.Single(r =>
            r.FK_DemandePaiement == id
            && r.Action == DemandePaiementRoutageAction.Orienter
            && r.EstActif);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, orienter.FK_UtilisateurSource);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, orienter.FK_UtilisateurCible);
    }

    [Fact]
    public async Task Idor_Rejet_Sur_Dpm_Assignee_Autre_Utilisateur()
    {
        var repo = new FakeDemandePaiementRepo();
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var intrus = DemandePaiementRoutageTestHelpers.Junior(repo, 999);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            intrus.RetournerAsync(id, new RetourDemandePaiementRequest("IDOR", null)));
    }
}
