using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Cohérence routage actif et assignation — Lot 3.4.</summary>
public class DemandePaiementRoutageConsistencyTests
{
    [Fact]
    public async Task Une_Seule_Transmission_Active_A_La_Fois()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);

        await y1.ReceptionnerAsync(id);
        Assert.Single(repo.Routages.Where(r => r.FK_DemandePaiement == id && r.EstActif));

        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, y1, id);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(y1, id);
        await y1.TraiterChargeAsync(
            id,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await y1.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1));

        var actifs = repo.Routages.Where(r => r.FK_DemandePaiement == id && r.EstActif).ToList();
        Assert.Single(actifs);
        Assert.Equal(DemandePaiementRoutageAction.Orienter, actifs[0].Action);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, actifs[0].FK_UtilisateurCible);
    }

    [Fact]
    public async Task Sequence_Retour_Y_Puis_Relance_Z()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.RetournerAsync(id, new RetourDemandePaiementRequest("Correction", null));

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, tracked.FK_UtilisateurAssigne);

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1));

        tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, tracked.FK_UtilisateurAssigne);
        Assert.Equal(1, repo.Routages.Count(r => r.FK_DemandePaiement == id && r.EstActif));
    }

    [Fact]
    public async Task Sequence_Visa_Retour_Z_Puis_Relance()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.AddImputationAsync(id, DemandePaiementTestData.ImputationDc(montantUsd: 100m, idBudgetLigne: 100));

        var v = DemandePaiementRoutageTestHelpers.Viseur(repo);
        await v.RetournerAsync(id, new RetourDemandePaiementRequest("Rejet visa", null));

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(StatutDemandePaiement.EnControleBudgetaire, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, tracked.FK_UtilisateurAssigne);
        Assert.Contains(repo.Routages, r => r.Action == DemandePaiementRoutageAction.RetourVisa && r.EstActif);
    }

    [Fact]
    public async Task Historique_Routage_Conservé_Apres_Nouvelle_Transmission()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);

        await y1.ReceptionnerAsync(id);
        var countApresReception = repo.Routages.Count(r => r.FK_DemandePaiement == id);

        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, y1, id);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(y1, id);
        await y1.TraiterChargeAsync(
            id,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await y1.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1));

        Assert.True(repo.Routages.Count(r => r.FK_DemandePaiement == id) > countApresReception);
        Assert.Equal(1, repo.Routages.Count(r => r.FK_DemandePaiement == id && r.EstActif));
        Assert.Contains(repo.Routages, r => r.Action == DemandePaiementRoutageAction.Receptionner && !r.EstActif);
    }

    [Fact]
    public async Task Assignation_Correspond_Au_Detenteur_Courant()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);

        await y1.ReceptionnerAsync(id);
        var actif = DemandePaiementRoutageTestHelpers.RoutageActif(repo, id);
        Assert.NotNull(actif);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, actif.FK_UtilisateurCible);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, DemandePaiementRoutageTestHelpers.Tracked(repo, id).FK_UtilisateurAssigne);
    }
}
